namespace CollectionCatalog.Web.Domain;

/// <summary>
/// Inquiry / question of a visitor. Contains personal data – subject to retention (GDPR).
/// </summary>
public class Inquiry : IAuditable
{
    public int Id { get; set; }

    /// <summary>Item the inquiry is about (from the collection or from an offer).</summary>
    public int? ItemId { get; set; }
    public Item? Item { get; set; }

    /// <summary>Offer the inquiry came from. <c>null</c> = an inquiry outside offers.</summary>
    public int? ListingId { get; set; }
    public Listing? Listing { get; set; }

    public required string Name { get; set; }
    public required string Email { get; set; }
    public string? Phone { get; set; }
    public required string Message { get; set; }

    /// <summary>Offered price.</summary>
    public decimal? OfferedPrice { get; set; }

    public InquiryStatus Status { get; set; } = InquiryStatus.New;

    public const int MaxNoteLength = 2000;

    /// <summary>Internal note of the collector.</summary>
    public string? InternalNote { get; set; }

    public DateTime ConsentGivenAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}
