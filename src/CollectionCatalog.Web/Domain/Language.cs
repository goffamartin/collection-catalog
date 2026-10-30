namespace CollectionCatalog.Web.Domain;

/// <summary>
/// Language of the website and of translatable texts, managed as a codebook in the administration.
/// </summary>
/// <remarks>
/// The default language lives at the root of the site (<c>/sbirka</c>), every other one under its code (<c>/en/sbirka</c>).
/// A text missing in the requested language falls back to the default language.
/// </remarks>
public class Language
{
    public const int MaxCodeLength = 10;
    public const int MaxNameLength = 50;

    public int Id { get; set; }

    /// <summary>Two-letter ISO 639-1 code (<c>cs</c>, <c>en</c>) – URL prefix and culture of the UI.</summary>
    public required string Code { get; set; }

    /// <summary>Name of the language in the language itself (<c>Čeština</c>, <c>English</c>) – shown in the switcher.</summary>
    public required string Name { get; set; }

    /// <summary>Exactly one language is the default one.</summary>
    public bool IsDefault { get; set; }

    public int SortOrder { get; set; }
}
