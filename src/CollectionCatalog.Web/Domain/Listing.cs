namespace CollectionCatalog.Web.Domain;

/// <summary>
/// Offer of an item for sale / trade. An item can have several offers over time,
/// but at most one open (Draft/Active/Reserved).
/// </summary>
public class Listing : IAuditable
{
    public static readonly IReadOnlyList<ListingStatus> OpenStatuses = [ListingStatus.Draft, ListingStatus.Active, ListingStatus.Reserved];

    public int Id { get; set; }

    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;

    public ListingStatus Status { get; set; } = ListingStatus.Draft;

    /// <summary>Asking price. <c>null</c> = price by agreement.</summary>
    public decimal? AskingPrice { get; set; }

    /// <summary>A buyer can propose their own price.</summary>
    public bool AcceptsOffers { get; set; } = true;

    /// <summary>Trade possible.</summary>
    public bool OpenToTrade { get; set; }

    public LocalizedText PublicNote { get; set; } = LocalizedText.Empty;

    public DateTime? PublishedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }

    public ICollection<Inquiry> Inquiries { get; set; } = [];

    public bool IsOpen => OpenStatuses.Contains(Status);
}
