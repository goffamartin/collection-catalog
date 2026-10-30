namespace CollectionCatalog.Web.Domain;

/// <summary>
/// Photo of an item. Files (master + variants) are managed by <c>ImageStore</c> by <see cref="FileKey"/>.
/// </summary>
public class ItemImage : IAuditable
{
    public int Id { get; set; }

    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    /// <summary>Content hash – determines the file names on disk.</summary>
    public required string FileKey { get; set; }

    public int Width { get; set; }
    public int Height { get; set; }

    public ImageType ImageType { get; set; } = ImageType.Front;

    /// <summary>Order in the gallery (ascending).</summary>
    public int SortOrder { get; set; }

    public const int MaxCaptionLength = 300;

    public LocalizedText Caption { get; set; } = LocalizedText.Empty;

    /// <summary>Show on the public website (e.g. a certificate with a name rather not).</summary>
    public bool IsPublic { get; set; } = true;

    public string? OriginalFileName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}
