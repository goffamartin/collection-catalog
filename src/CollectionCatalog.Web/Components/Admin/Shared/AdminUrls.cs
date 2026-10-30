namespace CollectionCatalog.Web.Components.Admin.Shared;

/// <summary>Addresses of administration pages for links and navigation (the <c>@page</c> templates must repeat them verbatim).</summary>
public static class AdminUrls
{
    public const string Items = "admin/predmety";

    public const string NewItem = "admin/predmety/novy";

    public const string Listings = "admin/nabidky";

    public const string Inquiries = "admin/poptavky";

    public const string Codebooks = "admin/ciselniky";

    public const string Settings = "admin/nastaveni";

    public const string Account = "admin/ucet";

    public static string Item(int id) => $"admin/predmety/{id}";

    public static string InquiriesForListing(int listingId) => $"admin/poptavky?nabidka={listingId}";
}
