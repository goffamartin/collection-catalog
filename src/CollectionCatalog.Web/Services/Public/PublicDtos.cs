using System.Globalization;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Infrastructure;
using CollectionCatalog.Web.Resources;
using CollectionCatalog.Web.Services.Text;

namespace CollectionCatalog.Web.Services.Public;

// Public DTOs. They may contain only data meant for visitors of the website – the PublicDtoTests test
// guards that no sensitive field gets here (prices, seller, provenance, private notes…).

public sealed record PublicItemFilter
{
    public string? Search { get; init; }
    public int? TypeId { get; init; }
    public int? CountryId { get; init; }
    public int? CategoryId { get; init; }
    public int? YearFrom { get; init; }
    public int? YearTo { get; init; }
    public PublicSort Sort { get; init; }
    public int Page { get; init; } = 1;
}

public enum PublicSort
{
    Newest,     // Recently added
    Name,       // Name A–Z
    Oldest,     // Oldest (year introduced)
    Youngest,   // Newest
}

public sealed record PublicImage(string FileKey, int Width, int Height, string? Caption, ImageType ImageType);

public sealed record PublicCategory(int Id, string Name, string? Color);

public sealed record FacetOption(int Id, string Name, int Count);

public sealed class PublicItemCard
{
    public int Id { get; init; }
    public required string Name { get; init; }
    /// <summary>Name in every language – for links to the item in other languages (each has its own slug).</summary>
    public LocalizedText AllNames { get; init; } = LocalizedText.Empty;
    public string? TypeName { get; init; }
    public string? CountryName { get; init; }
    public int? YearReleased { get; init; }
    public int? YearDiscontinued { get; init; }
    public PublicImage? Image { get; init; }
    public DateTime CreatedAt { get; init; }

    public string Url => PublicUrls.Item(Id, Name);
    public string? Years => PublicUrls.FormatYears(YearReleased, YearDiscontinued);
}

public sealed class PublicItemDetail
{
    public int Id { get; init; }
    public required string Name { get; init; }
    /// <summary>Name in every language – for links to the item in other languages (each has its own slug).</summary>
    public LocalizedText AllNames { get; init; } = LocalizedText.Empty;
    public int? TypeId { get; init; }
    public string? TypeName { get; init; }
    public int? CountryId { get; init; }
    public string? CountryName { get; init; }
    public int? YearReleased { get; init; }
    public int? YearDiscontinued { get; init; }
    public ConditionState Condition { get; init; }
    public Originality Originality { get; init; }
    public bool HasBox { get; init; }
    public bool HasCertificate { get; init; }
    public bool HasDocumentation { get; init; }
    public string? PublicDescription { get; init; }
    public List<PublicCategory> Categories { get; set; } = [];
    public List<PublicImage> Images { get; init; } = [];
    public List<PublicItemCard> Related { get; set; } = [];

    /// <summary>Active / reserved offer of the item.</summary>
    public PublicOffer? Offer { get; init; }

    /// <summary>More offers (on the offer page).</summary>
    public List<PublicOfferCard> RelatedOffers { get; set; } = [];

    public DateTime ModifiedAt { get; init; }

    public string Slug => Text.Slug.From(Name);
    public string Url => PublicUrls.Item(Id, Name);
    public string? OfferUrl => Offer is null ? null : PublicUrls.Offer(Offer.ListingId, Name);
    public string? Years => PublicUrls.FormatYears(YearReleased, YearDiscontinued);
}

public sealed class PublicItemPage
{
    public required List<PublicItemCard> Items { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageCount { get; init; }
    public required List<FacetOption> Types { get; init; }
    public required List<FacetOption> Countries { get; init; }
    public required List<FacetOption> Categories { get; init; }
}

public sealed class PublicHome
{
    public int TotalCount { get; init; }
    public int CountryCount { get; init; }
    public int? YearFrom { get; init; }
    public int? YearTo { get; init; }
    public PublicItemCard? Hero { get; init; }
    public required List<PublicOfferCard> Offers { get; init; }
    public int OfferCount { get; init; }
    public required List<PublicItemCard> Featured { get; init; }
    public required List<FacetOption> Types { get; init; }
    public required List<FacetOption> Countries { get; init; }
}

/// <summary>Offer for sale. The asking price is public on purpose (<c>null</c> = price by agreement).</summary>
public sealed record PublicOffer(int ListingId, ListingStatus Status, decimal? AskingPrice, bool AcceptsOffers, bool OpenToTrade, string? PublicNote);

public sealed class PublicOfferCard
{
    public int ListingId { get; init; }
    public ListingStatus Status { get; init; }
    public decimal? AskingPrice { get; init; }
    public bool AcceptsOffers { get; init; }
    public bool OpenToTrade { get; init; }
    public required PublicItemCard Item { get; init; }

    public string Url => PublicUrls.Offer(ListingId, Item.Name);
}

/// <param name="Name">Name in every language – each language has its own URL (slug).</param>
public sealed record PublicSitemapEntry(int Id, LocalizedText Name, DateTime ModifiedAt, IReadOnlyList<string> ImageKeys);

/// <summary>URLs of public pages and photos – in one place, so links, canonical URLs and the sitemap match.</summary>
public static class PublicUrls
{
    public const string Collection = "sbirka";

    public const string Offers = "nabidka";

    public static string Item(int id, string name) => $"sbirka/{id}/{Slug.From(name)}";

    public static string Offer(int listingId, string name) => $"nabidka/{listingId}/{Slug.From(name)}";

    /// <summary>Offer price for display („€12,500“ / „Price by agreement“) in the currency of the collection.</summary>
    public static string Price(decimal? price) =>
        price is { } p ? Money.Format(p) : AppText.Get("Price by agreement");

    public static string Image(string fileKey, int size) => $"media/{fileKey}/{size}";

    /// <summary>Full JPEG (previews for social networks, which may not support WebP).</summary>
    public static string ImageJpeg(string fileKey) => $"media/{fileKey}/jpg";

    /// <summary>Period of use for display, e.g. "1918–1945".</summary>
    public static string? FormatYears(int? from, int? to) => (from, to) switch
    {
        (null, null) => null,
        ({ } f, null) => f.ToString(CultureInfo.InvariantCulture),
        (null, { } t) => $"do {t}",
        ({ } f, { } t) when f == t => f.ToString(CultureInfo.InvariantCulture),
        ({ } f, { } t) => $"{f}–{t}",
    };
}
