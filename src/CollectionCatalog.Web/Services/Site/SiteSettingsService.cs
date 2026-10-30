using System.ComponentModel.DataAnnotations;
using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Services.Languages;
using Microsoft.EntityFrameworkCore;

namespace CollectionCatalog.Web.Services.Site;

/// <summary>
/// Texts and contacts of the public website. Every public page reads them (layout), so they are kept in memory
/// and refreshed when saved from the administration (the app always runs as a single instance).
/// </summary>
public sealed class SiteSettingsService(IDbContextFactory<AppDbContext> dbContextFactory, LanguageService languages)
{
    private volatile SiteSettings? _cached;

    /// <summary>Texts in the language of the current request.</summary>
    public async Task<SiteInfo> GetAsync(CancellationToken cancellationToken = default) =>
        SiteInfo.From(await GetSettingsAsync(cancellationToken), languages.Current);

    public async Task<SiteSettingsModel> GetForEditAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var model = new SiteSettingsModel
        {
            SiteNameText = settings.SiteName,
            HeadlineText = settings.Headline,
            IntroText = settings.Intro,
            AboutText = settings.About,
            ContactNoteText = settings.ContactNote,
            ContactEmail = settings.ContactEmail,
            ContactPhone = settings.ContactPhone,
        };
        model.SetLanguages(languages.Current);
        return model;
    }

    public async Task SaveAsync(SiteSettingsModel model, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var settings = await db.SiteSettings.SingleAsync(s => s.Id == SiteSettings.SingletonId, cancellationToken);

        settings.SiteName = model.SiteNameText;
        settings.Headline = model.HeadlineText;
        settings.Intro = model.IntroText;
        settings.About = model.AboutText;
        settings.ContactNote = model.ContactNoteText;
        settings.ContactEmail = Clean(model.ContactEmail);
        settings.ContactPhone = Clean(model.ContactPhone);

        await db.SaveChangesAsync(cancellationToken);
        _cached = settings;
    }

    private async Task<SiteSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        if (_cached is { } cached)
        {
            return cached;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return _cached = await db.SiteSettings.AsNoTracking().SingleAsync(s => s.Id == SiteSettings.SingletonId, cancellationToken);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Website settings in one language, for display (immutable, shared between requests).</summary>
public sealed record SiteInfo(
    string SiteName,
    string Headline,
    string? Intro,
    string? About,
    string? ContactEmail,
    string? ContactPhone,
    string? ContactNote)
{
    public bool HasContact => ContactEmail is not null || ContactPhone is not null;

    public static SiteInfo From(SiteSettings s, LanguageContext language) =>
        new(s.SiteName.Get(language) ?? "", s.Headline.Get(language) ?? "", s.Intro.Get(language), s.About.Get(language),
            s.ContactEmail, s.ContactPhone, s.ContactNote.Get(language));
}

public sealed class SiteSettingsModel : TranslatableModel, IValidatableObject
{
    public const int MaxSiteNameLength = 100;
    public const int MaxHeadlineLength = 200;
    public const int MaxIntroLength = 1000;
    public const int MaxAboutLength = 20000;
    public const int MaxContactNoteLength = 2000;

    public LocalizedText SiteNameText { get; set; } = LocalizedText.Empty;
    public LocalizedText HeadlineText { get; set; } = LocalizedText.Empty;
    public LocalizedText IntroText { get; set; } = LocalizedText.Empty;
    public LocalizedText AboutText { get; set; } = LocalizedText.Empty;
    public LocalizedText ContactNoteText { get; set; } = LocalizedText.Empty;

    [StringLength(MaxSiteNameLength, ErrorMessage = "The name can have at most {1} characters.")]
    public string? SiteName { get => Read(SiteNameText); set => SiteNameText = Write(SiteNameText, value); }

    [StringLength(MaxHeadlineLength, ErrorMessage = "The headline can have at most {1} characters.")]
    public string? Headline { get => Read(HeadlineText); set => HeadlineText = Write(HeadlineText, value); }

    [StringLength(MaxIntroLength, ErrorMessage = "The intro can have at most {1} characters.")]
    public string? Intro { get => Read(IntroText); set => IntroText = Write(IntroText, value); }

    [StringLength(MaxAboutLength, ErrorMessage = "The text can have at most {1} characters.")]
    public string? About { get => Read(AboutText); set => AboutText = Write(AboutText, value); }

    [StringLength(MaxContactNoteLength, ErrorMessage = "The text can have at most {1} characters.")]
    public string? ContactNote { get => Read(ContactNoteText); set => ContactNoteText = Write(ContactNoteText, value); }

    [EmailAddress(ErrorMessage = "Invalid e-mail.")]
    [StringLength(200, ErrorMessage = "The e-mail can have at most {1} characters.")]
    public string? ContactEmail
    {
        get;
        // A cleared field posts "" and [EmailAddress] considers that an invalid e-mail.
        set => field = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    [StringLength(50, ErrorMessage = "The phone can have at most {1} characters.")]
    public string? ContactPhone { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Required only in the default language – other languages fall back to it.
        if (SiteNameText[DefaultLanguage] is null)
        {
            yield return new ValidationResult("Fill in the name of the website in the default language.", [nameof(SiteName)]);
        }
        if (HeadlineText[DefaultLanguage] is null)
        {
            yield return new ValidationResult("Fill in the main headline in the default language.", [nameof(Headline)]);
        }
    }
}
