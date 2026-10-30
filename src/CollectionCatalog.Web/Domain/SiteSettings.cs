namespace CollectionCatalog.Web.Domain;

/// <summary>
/// Texts and contacts of the public website, editable in the administration. The table always has exactly one row (<see cref="SingletonId"/>).
/// </summary>
public class SiteSettings : IAuditable
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>Name of the collection in the header and titles.</summary>
    public required LocalizedText SiteName { get; set; }

    /// <summary>Main headline of the home page.</summary>
    public required LocalizedText Headline { get; set; }

    /// <summary>Intro paragraph on the home page (and the description of the website for search engines).</summary>
    public LocalizedText Intro { get; set; } = LocalizedText.Empty;

    /// <summary>Text of the „About“ page – paragraphs separated by an empty line.</summary>
    public LocalizedText About { get; set; } = LocalizedText.Empty;

    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }

    /// <summary>Additional text on the contact page.</summary>
    public LocalizedText ContactNote { get; set; } = LocalizedText.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}
