using System.Globalization;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Services.Public;
using CollectionCatalog.Web.Services.Site;

namespace CollectionCatalog.Web.Components.Public.Shared;

/// <summary>
/// Structured data of schema.org for <see cref="JsonLd"/>. Keys with "@" only work through dictionaries;
/// empty values are left out (<see cref="Node"/>) – Google reports null values as errors.
/// </summary>
public static class StructuredData
{
    private const string Context = "https://schema.org";

    /// <summary>The website as a whole – on the home page (name of the website in search results).</summary>
    public static Func<Func<string, string>, object?> WebSite(SiteInfo site) => url => Node(
        ("@context", Context),
        ("@type", "WebSite"),
        ("name", site.SiteName),
        ("url", url("")),
        ("inLanguage", CultureInfo.CurrentUICulture.Name),
        ("description", site.Intro));

    /// <summary>Breadcrumbs; the item without a path is the current page.</summary>
    public static Func<Func<string, string>, object?> Breadcrumbs(params (string Name, string? Path)[] items) => url => Node(
        ("@context", Context),
        ("@type", "BreadcrumbList"),
        ("itemListElement", items.Select((item, index) => Node(
            ("@type", "ListItem"),
            ("position", index + 1),
            ("name", item.Name),
            ("item", item.Path is null ? null : url(item.Path)))).ToList()));

    /// <summary>
    /// Offer for sale as Product + Offer (price in search results). Google requires a price,
    /// so nothing is generated for "price by agreement".
    /// </summary>
    public static Func<Func<string, string>, object?> Offer(PublicItemDetail item) => url =>
        item.Offer is not { AskingPrice: { } price } offer
            ? null
            : Node(
                ("@context", Context),
                ("@type", "Product"),
                ("name", item.Name),
                ("description", offer.PublicNote ?? item.PublicDescription ?? item.Name),
                ("image", item.Images.Select(image => url(PublicUrls.Image(image.FileKey, 1600))).ToList()),
                ("category", item.TypeName),
                ("countryOfOrigin", item.CountryName),
                ("offers", Node(
                    ("@type", "Offer"),
                    ("url", url(item.OfferUrl!)),
                    ("price", price.ToString("0.##", CultureInfo.InvariantCulture)),
                    ("priceCurrency", Infrastructure.Money.CurrencyCode),
                    ("availability", offer.Status == ListingStatus.Reserved ? "https://schema.org/LimitedAvailability" : "https://schema.org/InStock"),
                    ("itemCondition", "https://schema.org/UsedCondition"))));

    private static Dictionary<string, object> Node(params (string Key, object? Value)[] properties) =>
        properties.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value!);
}
