using System.Globalization;
using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace CollectionCatalog.Web.Services.Languages;

/// <summary>
/// Languages of the website (codebook) and the language of the current request.
/// </summary>
/// <remarks>
/// The list is read on every request (URL prefix, switcher), so it is cached in memory and reloaded after
/// every change from the administration (the app always runs as a single instance).
/// The current language is the UI culture set by <see cref="Infrastructure.LanguagePathMiddleware"/> –
/// in an interactive circuit it is the culture of the request that started the circuit.
/// </remarks>
public sealed class LanguageService(IDbContextFactory<AppDbContext> dbContextFactory, IStringLocalizer<AppText> text)
{
    private volatile IReadOnlyList<LanguageInfo>? _languages;

    /// <summary>All languages, default first, then by sort order. Requires <see cref="LoadAsync"/> at startup.</summary>
    public IReadOnlyList<LanguageInfo> All => _languages ?? throw new InvalidOperationException("Languages are not loaded yet.");

    public LanguageInfo Default => All.First(l => l.IsDefault);

    /// <summary>Language of the current request (or circuit) and the default one for fallback.</summary>
    public LanguageContext Current
    {
        get
        {
            var code = CultureInfo.CurrentUICulture.Name;
            var current = All.FirstOrDefault(l => l.Code == code) ?? Default;
            return new LanguageContext(current.Code, Default.Code);
        }
    }

    public LanguageInfo? Find(string? code) =>
        code is null ? null : All.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>URL prefix of a language: empty for the default one, otherwise <c>en/</c>.</summary>
    public string PathPrefix(string code) => code == Default.Code ? "" : $"{code}/";

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var languages = await db.Languages.AsNoTracking().ToListAsync(cancellationToken);
        _languages = languages
            .OrderByDescending(l => l.IsDefault).ThenBy(l => l.SortOrder).ThenBy(l => l.Code, StringComparer.Ordinal)
            .Select(l => new LanguageInfo(l.Id, l.Code, l.Name, l.IsDefault))
            .ToList();
    }

    /// <returns>Error message, or <c>null</c> on success.</returns>
    public async Task<string?> AddAsync(string? code, string? name, CancellationToken cancellationToken = default)
    {
        code = code?.Trim().ToLowerInvariant();
        name = name?.Trim();
        if (string.IsNullOrEmpty(code) || code.Length != 2 || !code.All(char.IsAsciiLetterLower))
        {
            return text["Enter a two-letter language code (ISO 639-1), e.g. de."];
        }
        if (!IsKnownCulture(code))
        {
            return text["„{0}“ is not a known language code.", code];
        }
        if (string.IsNullOrEmpty(name) || name.Length > Language.MaxNameLength)
        {
            return text["Enter the name of the language (at most {0} characters).", Language.MaxNameLength];
        }
        if (Find(code) is not null)
        {
            return text["Language „{0}“ already exists.", code];
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var sortOrder = await db.Languages.MaxAsync(l => (int?)l.SortOrder, cancellationToken) ?? 0;
        db.Languages.Add(new Language { Code = code, Name = name, SortOrder = sortOrder + 1 });
        await db.SaveChangesAsync(cancellationToken);
        await LoadAsync(cancellationToken);
        return null;
    }

    /// <returns>Error message, or <c>null</c> on success.</returns>
    public async Task<string?> RenameAsync(int id, string? name, CancellationToken cancellationToken = default)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > Language.MaxNameLength)
        {
            return text["Enter the name of the language (at most {0} characters).", Language.MaxNameLength];
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Languages.FindAsync([id], cancellationToken) is not { } language)
        {
            return text["The language no longer exists."];
        }
        language.Name = name;
        await db.SaveChangesAsync(cancellationToken);
        await LoadAsync(cancellationToken);
        return null;
    }

    /// <summary>
    /// Makes the language the default one – it moves to the root of the website and becomes the fallback for missing texts.
    /// </summary>
    public async Task SetDefaultAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        foreach (var language in await db.Languages.ToListAsync(cancellationToken))
        {
            language.IsDefault = language.Id == id;
        }
        await db.SaveChangesAsync(cancellationToken);
        await LoadAsync(cancellationToken);
    }

    /// <summary>
    /// Removes a language from the website. Its translations stay stored in the texts (they are just never shown),
    /// so adding the language back restores them.
    /// </summary>
    /// <returns>Error message, or <c>null</c> on success.</returns>
    public async Task<string?> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Languages.FindAsync([id], cancellationToken) is not { } language)
        {
            return null;
        }
        if (language.IsDefault)
        {
            return text["The default language cannot be deleted – make another language the default one first."];
        }
        db.Languages.Remove(language);
        await db.SaveChangesAsync(cancellationToken);
        await LoadAsync(cancellationToken);
        return null;
    }

    private static bool IsKnownCulture(string code)
    {
        try
        {
            return !CultureInfo.GetCultureInfo(code).EnglishName.StartsWith("Unknown", StringComparison.Ordinal);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}

/// <summary>Language for display (immutable, shared between requests).</summary>
public sealed record LanguageInfo(int Id, string Code, string Name, bool IsDefault);
