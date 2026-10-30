using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Services.Languages;
using CollectionCatalog.Web.Services.Text;
using Microsoft.EntityFrameworkCore;

namespace CollectionCatalog.Web.Services.Items;

/// <summary>
/// Item list of the administration.
/// </summary>
/// <remarks>
/// Structured filters run in SQL, full-text search in memory: SQLite cannot compare without diacritics and case
/// for non-ASCII characters, translatable texts are JSON, and the collection has hundreds of items, so projecting
/// the whole list is cheap. Search matches every language of the translatable texts.
/// </remarks>
public sealed class AdminItemQuery(IDbContextFactory<AppDbContext> dbContextFactory, LanguageService languages)
{
    public async Task<List<AdminItemRow>> GetItemsAsync(AdminItemFilter filter, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Items.AsNoTracking();

        if (filter.TypeId is { } typeId)
        {
            query = query.Where(i => i.TypeId == typeId);
        }
        if (filter.CountryId is { } countryId)
        {
            query = query.Where(i => i.CountryId == countryId);
        }
        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(i => i.Categories.Any(c => c.Id == categoryId));
        }
        if (filter.Visibility is { } visibility)
        {
            query = query.Where(i => i.Visibility == visibility);
        }
        if (filter.Sold is { } sold)
        {
            query = query.Where(i => (i.SaleDate != null || i.SalePrice != null) == sold);
        }
        if (filter.HasImages is { } hasImages)
        {
            query = query.Where(i => i.Images.Any() == hasImages);
        }

        var items = await query
            .Select(i => new
            {
                i.Id,
                i.Name,
                TypeName = i.Type != null ? i.Type.Name : null,
                CountryName = i.Country != null ? i.Country.Name : null,
                i.YearReleased,
                i.YearDiscontinued,
                i.Condition,
                i.Visibility,
                IsSold = i.SaleDate != null || i.SalePrice != null,
                i.PurchasePrice,
                i.EstimatedValue,
                ThumbnailKey = i.Images.OrderBy(im => im.SortOrder).Select(im => im.FileKey).FirstOrDefault(),
                ImageCount = i.Images.Count,
                Categories = i.Categories.Select(c => new { c.Name, c.Color }).ToList(),
                HasOpenListing = i.Listings.Any(l => l.Status == ListingStatus.Draft || l.Status == ListingStatus.Active || l.Status == ListingStatus.Reserved),
                i.CreatedAt,
                i.Seller,
                i.PrivateNotes,
                i.PublicDescription,
            })
            .ToListAsync(cancellationToken);

        var language = languages.Current;
        var comparer = TextComparer.For(language.Code);
        var rows = items.Select(i => new
        {
            Row = new AdminItemRow
            {
                Id = i.Id,
                Name = i.Name.Get(language) ?? "",
                TypeName = i.TypeName?.Get(language),
                CountryName = i.CountryName?.Get(language),
                YearReleased = i.YearReleased,
                YearDiscontinued = i.YearDiscontinued,
                Condition = i.Condition,
                Visibility = i.Visibility,
                IsSold = i.IsSold,
                PurchasePrice = i.PurchasePrice,
                EstimatedValue = i.EstimatedValue,
                ThumbnailKey = i.ThumbnailKey,
                ImageCount = i.ImageCount,
                Categories = i.Categories
                    .Select(c => new CategoryTag(c.Name.Get(language) ?? "", c.Color))
                    .OrderBy(c => c.Name, comparer)
                    .ToList(),
                HasOpenListing = i.HasOpenListing,
                CreatedAt = i.CreatedAt,
            },
            SearchTexts = (IEnumerable<string?>)[
                .. i.Name.Values.Values, .. i.TypeName?.Values.Values ?? [], .. i.CountryName?.Values.Values ?? [],
                .. i.PublicDescription.Values.Values, .. i.Categories.SelectMany(c => c.Name.Values.Values),
                i.Seller, i.PrivateNotes],
        }).ToList();

        var search = SearchText.Normalize(filter.Search?.Trim());
        if (search.Length == 0)
        {
            return rows.Select(r => r.Row).ToList();
        }

        // "#12" searches by Id.
        if (search.StartsWith('#') && int.TryParse(search[1..], out var id))
        {
            return rows.Where(r => r.Row.Id == id).Select(r => r.Row).ToList();
        }

        return rows
            .Where(r => SearchText.Matches(search, r.SearchTexts))
            .Select(r => r.Row)
            .ToList();
    }
}

public sealed record AdminItemFilter
{
    public string? Search { get; init; }
    public int? TypeId { get; init; }
    public int? CountryId { get; init; }
    public int? CategoryId { get; init; }
    public ItemVisibility? Visibility { get; init; }
    public bool? Sold { get; init; }
    public bool? HasImages { get; init; }
}

public sealed class AdminItemRow
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public string? TypeName { get; init; }
    public string? CountryName { get; init; }
    public int? YearReleased { get; init; }
    public int? YearDiscontinued { get; init; }
    public ConditionState Condition { get; init; }
    public ItemVisibility Visibility { get; init; }
    public bool IsSold { get; init; }
    public decimal? PurchasePrice { get; init; }
    public decimal? EstimatedValue { get; init; }
    public string? ThumbnailKey { get; init; }
    public int ImageCount { get; init; }
    public IReadOnlyList<CategoryTag> Categories { get; init; } = [];
    public bool HasOpenListing { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>Period of use for display, e.g. „1918–1945“.</summary>
    public string? Years => (YearReleased, YearDiscontinued) switch
    {
        (null, null) => null,
        ({ } from, null) => from.ToString(System.Globalization.CultureInfo.InvariantCulture),
        (null, { } to) => $"–{to}",
        ({ } from, { } to) => $"{from}–{to}",
    };
}

public sealed record CategoryTag(string Name, string? Color);
