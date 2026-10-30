using System.Text.RegularExpressions;
using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Resources;
using CollectionCatalog.Web.Services.Languages;
using CollectionCatalog.Web.Services.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace CollectionCatalog.Web.Services.Codebooks;

/// <summary>
/// Codebooks managed in the administration: item types, categories and countries.
/// </summary>
/// <remarks>
/// Names are translatable: a new entry is created in the default language, translations are edited one language
/// at a time. Deleting an entry keeps the items – their type and country are cleared, categories removed.
/// </remarks>
public sealed partial class CodebookService(IDbContextFactory<AppDbContext> dbContextFactory, LanguageService languages, IStringLocalizer<AppText> text)
{
    public const int MaxNameLength = 100;

    /// <summary>Entries with names in <paramref name="language"/> (falling back to the default language), sorted by name.</summary>
    public async Task<List<CodebookEntry>> GetAsync(CodebookKind kind, LanguageContext language, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = kind switch
        {
            CodebookKind.ItemTypes => await db.ItemTypes
                .Select(t => new CodebookRow(t.Id, t.Name, null, t.Items.Count))
                .ToListAsync(cancellationToken),
            CodebookKind.Categories => await db.Categories
                .Select(c => new CodebookRow(c.Id, c.Name, c.Color, c.Items.Count))
                .ToListAsync(cancellationToken),
            CodebookKind.Countries => await db.Countries
                .Select(c => new CodebookRow(c.Id, c.Name, null, c.Items.Count))
                .ToListAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        return rows
            .Select(r => new CodebookEntry(r.Id, r.Name.Get(language) ?? "", r.Name[language.Code], r.Color, r.ItemCount))
            .OrderBy(e => e.Name, TextComparer.For(language.Code))
            .ToList();
    }

    /// <summary>Creates an entry named in the default language.</summary>
    /// <returns>Error message, or <c>null</c> on success.</returns>
    public async Task<string?> AddAsync(CodebookKind kind, string? name, string? color = null, CancellationToken cancellationToken = default)
    {
        var language = languages.Default.Code;
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var error = await ValidateAsync(db, kind, id: null, language, name, color, required: true, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var localized = LocalizedText.Of(language, name);
        switch (kind)
        {
            case CodebookKind.ItemTypes:
                db.ItemTypes.Add(new ItemType { Name = localized });
                break;
            case CodebookKind.Categories:
                db.Categories.Add(new Category { Name = localized, Color = NormalizeColor(color) });
                break;
            case CodebookKind.Countries:
                db.Countries.Add(new Country { Name = localized });
                break;
        }

        await db.SaveChangesAsync(cancellationToken);
        return null;
    }

    /// <summary>
    /// Sets the name in one language (and the color of a category). The name is required in the default language;
    /// an empty name in another language removes the translation.
    /// </summary>
    /// <returns>Error message, or <c>null</c> on success.</returns>
    public async Task<string?> UpdateAsync(CodebookKind kind, int id, string languageCode, string? name, string? color = null, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var required = languageCode == languages.Default.Code;
        var error = await ValidateAsync(db, kind, id, languageCode, name, color, required, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        switch (kind)
        {
            case CodebookKind.ItemTypes when await db.ItemTypes.FindAsync([id], cancellationToken) is { } type:
                type.Name = type.Name.With(languageCode, name);
                break;
            case CodebookKind.Categories when await db.Categories.FindAsync([id], cancellationToken) is { } category:
                category.Name = category.Name.With(languageCode, name);
                category.Color = NormalizeColor(color);
                break;
            case CodebookKind.Countries when await db.Countries.FindAsync([id], cancellationToken) is { } country:
                country.Name = country.Name.With(languageCode, name);
                break;
            default:
                return text["The entry no longer exists."];
        }

        await db.SaveChangesAsync(cancellationToken);
        return null;
    }

    public async Task DeleteAsync(CodebookKind kind, int id, CancellationToken cancellationToken = default)
    {
        // Links to items are handled by foreign keys in the database (ON DELETE SET NULL / CASCADE for categories).
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await (kind switch
        {
            CodebookKind.ItemTypes => db.ItemTypes.Where(t => t.Id == id).ExecuteDeleteAsync(cancellationToken),
            CodebookKind.Categories => db.Categories.Where(c => c.Id == id).ExecuteDeleteAsync(cancellationToken),
            CodebookKind.Countries => db.Countries.Where(c => c.Id == id).ExecuteDeleteAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        });
    }

    private async Task<string?> ValidateAsync(AppDbContext db, CodebookKind kind, int? id, string languageCode, string? name, string? color, bool required, CancellationToken cancellationToken)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return required ? text["Enter a name."].Value : null;
        }
        if (trimmed.Length > MaxNameLength)
        {
            return text["The name can have at most {0} characters.", MaxNameLength];
        }
        if (kind == CodebookKind.Categories && !string.IsNullOrEmpty(color) && !ColorRegex().IsMatch(color))
        {
            return text["The color must be in the format #RRGGBB."];
        }

        // Duplicates within the same language, compared in memory (names are JSON in the database).
        var names = kind switch
        {
            CodebookKind.ItemTypes => await db.ItemTypes.Where(t => t.Id != id).Select(t => t.Name).ToListAsync(cancellationToken),
            CodebookKind.Categories => await db.Categories.Where(c => c.Id != id).Select(c => c.Name).ToListAsync(cancellationToken),
            CodebookKind.Countries => await db.Countries.Where(c => c.Id != id).Select(c => c.Name).ToListAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        var comparer = TextComparer.For(languageCode);
        return names.Any(n => n[languageCode] is { } existing && comparer.Equals(existing, trimmed))
            ? text["„{0}“ already exists.", trimmed].Value
            : null;
    }

    private static string? NormalizeColor(string? color) => string.IsNullOrEmpty(color) ? null : color.ToUpperInvariant();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex ColorRegex();

    private sealed record CodebookRow(int Id, LocalizedText Name, string? Color, int ItemCount);
}

public enum CodebookKind
{
    ItemTypes,
    Categories,
    Countries,
}

/// <param name="Name">Name for display (falling back to the default language).</param>
/// <param name="Translation">Name exactly in the requested language – <c>null</c> when not translated yet.</param>
/// <param name="ItemCount">Number of items using the entry.</param>
public sealed record CodebookEntry(int Id, string Name, string? Translation, string? Color, int ItemCount);
