namespace CollectionCatalog.Web.Domain;

/// <summary>
/// Collectible item – a mineral, coin, book, artwork… whatever the collection holds.
/// </summary>
public class Item : IAuditable
{
    public int Id { get; set; }

    // === BASIC DATA ===

    public required LocalizedText Name { get; set; }

    public int? TypeId { get; set; }
    public ItemType? Type { get; set; }

    /// <summary>Year of introduction / issue.</summary>
    public int? YearReleased { get; set; }

    /// <summary>Year of discontinuation / withdrawal from use.</summary>
    public int? YearDiscontinued { get; set; }

    public int? CountryId { get; set; }
    public Country? Country { get; set; }

    public ICollection<Category> Categories { get; set; } = [];

    // === CONDITION AND ACCESSORIES ===

    public ConditionState Condition { get; set; } = ConditionState.Good;
    public Originality Originality { get; set; } = Originality.Original;
    /// <summary>Original box, case or packaging.</summary>
    public bool HasBox { get; set; }

    /// <summary>Certificate of authenticity, label.</summary>
    public bool HasCertificate { get; set; }

    /// <summary>Documentation – provenance papers, receipts, literature.</summary>
    public bool HasDocumentation { get; set; }

    // === PRESENTATION ===

    public ItemVisibility Visibility { get; set; } = ItemVisibility.Private;

    /// <summary>Feature on the home page.</summary>
    public bool IsFeatured { get; set; }

    /// <summary>Description for the public.</summary>
    public LocalizedText PublicDescription { get; set; } = LocalizedText.Empty;

    // === PRIVATE DATA (never published) ===

    public string? PrivateNotes { get; set; }

    /// <summary>Provenance – where the item comes from, previous owners.</summary>
    public string? Provenance { get; set; }

    public DateOnly? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }

    /// <summary>Seller / source of the purchase.</summary>
    public string? Seller { get; set; }

    /// <summary>Estimated market value.</summary>
    public decimal? EstimatedValue { get; set; }

    public DateOnly? SaleDate { get; set; }
    public decimal? SalePrice { get; set; }

    // === SYSTEM ===

    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }

    public ICollection<ItemImage> Images { get; set; } = [];
    public ICollection<Listing> Listings { get; set; } = [];

    public bool IsSold => SaleDate is not null || SalePrice is not null;
}
