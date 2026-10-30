using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Services.Languages;
using CollectionCatalog.Web.Services.Text;
using Microsoft.EntityFrameworkCore;

namespace CollectionCatalog.Web.Services.Public;

/// <summary>
/// Data for the public website. Returns only public items (and items in an active offer) and public photos, exclusively as DTOs
/// from <c>PublicDtos.cs</c> – sensitive fields (prices, seller, provenance, notes) never get into the projections.
/// </summary>
/// <remarks>
/// There are at most hundreds of public pieces, so filtering, sorting and paging run in memory over one projection
/// (like the full-text search of the administration – SQLite cannot compare without diacritics).
/// </remarks>
public sealed class PublicItemQuery(IDbContextFactory<AppDbContext> dbContextFactory, LanguageService languages)
{
    public const int PageSize = 24;
    private const int FeaturedCount = 8;
    private const int RelatedCount = 4;
    private const int HomeOfferCount = 4;

    /// <summary>Offers visible on the website – reserved ones stay visible with a label.</summary>
    public static readonly IReadOnlyList<ListingStatus> PublicOfferStatuses = [ListingStatus.Active, ListingStatus.Reserved];

    /// <summary>Names sort by the rules of the current language.</summary>
    private static StringComparer NameComparer => TextComparer.Current;

    public async Task<PublicHome> GetHomeAsync(CancellationToken cancellationToken = default)
    {
        var rows = await LoadRowsAsync(cancellationToken);

        // Featured pieces, topped up with recently added ones with a photo.
        var featured = rows
            .OrderByDescending(r => r.IsFeatured)
            .ThenByDescending(r => r.Card.Image is not null)
            .ThenByDescending(r => r.Card.CreatedAt)
            .Take(FeaturedCount)
            .Select(r => r.Card)
            .ToList();

        var years = rows.SelectMany(r => new[] { r.Card.YearReleased, r.Card.YearDiscontinued }).OfType<int>().ToList();
        var offers = await GetOffersAsync(cancellationToken);

        return new PublicHome
        {
            Offers = offers.Take(HomeOfferCount).ToList(),
            OfferCount = offers.Count,
            TotalCount = rows.Count,
            CountryCount = rows.Select(r => r.CountryId).OfType<int>().Distinct().Count(),
            YearFrom = years.Count > 0 ? years.Min() : null,
            YearTo = years.Count > 0 ? years.Max() : null,
            Hero = featured.FirstOrDefault(c => c.Image is not null),
            Featured = featured,
            Types = Facet(rows, r => r.TypeId is { } id ? [(id, r.Card.TypeName!)] : []),
            Countries = Facet(rows, r => r.CountryId is { } id ? [(id, r.Card.CountryName!)] : []),
        };
    }

    public async Task<PublicItemPage> SearchAsync(PublicItemFilter filter, CancellationToken cancellationToken = default)
    {
        var rows = await LoadRowsAsync(cancellationToken);
        IEnumerable<PublicItemRow> query = rows;

        if (filter.TypeId is { } typeId)
        {
            query = query.Where(r => r.TypeId == typeId);
        }
        if (filter.CountryId is { } countryId)
        {
            query = query.Where(r => r.CountryId == countryId);
        }
        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(r => r.Categories.Any(c => c.Id == categoryId));
        }
        if (filter.YearFrom is not null || filter.YearTo is not null)
        {
            // The period of use of the item must overlap the searched period; pieces without a year drop out.
            var from = filter.YearFrom ?? int.MinValue;
            var to = filter.YearTo ?? int.MaxValue;
            query = query.Where(r =>
                (r.Card.YearReleased ?? r.Card.YearDiscontinued) is { } start
                && (r.Card.YearDiscontinued ?? r.Card.YearReleased) is { } end
                && end >= from && start <= to);
        }

        var search = SearchText.Normalize(filter.Search?.Trim());
        if (search.Length > 0)
        {
            query = query.Where(r => SearchText.Matches(search,
                [r.Card.Name, r.Card.TypeName, r.Card.CountryName, r.PublicDescription, .. r.Categories.Select(c => c.Name)]));
        }

        query = filter.Sort switch
        {
            PublicSort.Name => query.OrderBy(r => r.Card.Name, NameComparer),
            PublicSort.Oldest => query.OrderBy(r => r.Card.YearReleased ?? r.Card.YearDiscontinued ?? int.MaxValue).ThenBy(r => r.Card.Name, NameComparer),
            PublicSort.Youngest => query.OrderByDescending(r => r.Card.YearReleased ?? r.Card.YearDiscontinued ?? int.MinValue).ThenBy(r => r.Card.Name, NameComparer),
            _ => query.OrderByDescending(r => r.Card.CreatedAt).ThenByDescending(r => r.Card.Id),
        };

        var matched = query.ToList();
        var pageCount = Math.Max(1, (int)Math.Ceiling(matched.Count / (double)PageSize));
        var page = Math.Clamp(filter.Page, 1, pageCount);

        return new PublicItemPage
        {
            Items = matched.Skip((page - 1) * PageSize).Take(PageSize).Select(r => r.Card).ToList(),
            TotalCount = matched.Count,
            Page = page,
            PageCount = pageCount,
            Types = Facet(rows, r => r.TypeId is { } id ? [(id, r.Card.TypeName!)] : []),
            Countries = Facet(rows, r => r.CountryId is { } id ? [(id, r.Card.CountryName!)] : []),
            Categories = Facet(rows, r => r.Categories.Select(c => (c.Id, c.Name))),
        };
    }

    public async Task<PublicItemDetail?> GetDetailAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var detail = await ProjectDetail(db.Items.Where(i => i.Id == id && i.Visibility == ItemVisibility.Public), languages.Current)
            .SingleOrDefaultAsync(cancellationToken);

        if (detail is null)
        {
            return null;
        }

        detail.Categories = detail.Categories.OrderBy(c => c.Name, NameComparer).ToList();

        // Similar pieces: most shared categories, then the same country, preferably with a photo.
        var categoryIds = detail.Categories.Select(c => c.Id).ToHashSet();
        var rows = await LoadRowsAsync(cancellationToken);
        detail.Related = rows
            .Where(r => r.Card.Id != id)
            .Select(r => (Row: r, Score: r.Categories.Count(c => categoryIds.Contains(c.Id)) * 2 + (r.CountryId is not null && r.CountryId == detail.CountryId ? 1 : 0)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Row.Card.Image is not null)
            .ThenByDescending(x => x.Row.Card.CreatedAt)
            .Take(RelatedCount)
            .Select(x => x.Row.Card)
            .ToList();

        return detail;
    }

    /// <summary>Active and reserved offers – active first, newest first.</summary>
    public async Task<List<PublicOfferCard>> GetOffersAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        // Get(language) runs on the client – EF evaluates it in the final projection.
        var language = languages.Current;
        var offers = await db.Listings.AsNoTracking()
            .Where(l => PublicOfferStatuses.Contains(l.Status))
            .Select(l => new
            {
                l.Id,
                l.Status,
                l.AskingPrice,
                l.AcceptsOffers,
                l.OpenToTrade,
                Published = l.PublishedAt ?? l.CreatedAt,
                Item = new PublicItemCard
                {
                    Id = l.Item.Id,
                    Name = l.Item.Name.Get(language) ?? "",
                    AllNames = l.Item.Name,
                    TypeName = l.Item.Type != null ? l.Item.Type.Name.Get(language) : null,
                    CountryName = l.Item.Country != null ? l.Item.Country.Name.Get(language) : null,
                    YearReleased = l.Item.YearReleased,
                    YearDiscontinued = l.Item.YearDiscontinued,
                    Image = l.Item.Images
                        .Where(im => im.IsPublic)
                        .OrderBy(im => im.SortOrder).ThenBy(im => im.Id)
                        .Select(im => new PublicImage(im.FileKey, im.Width, im.Height, im.Caption.Get(language), im.ImageType))
                        .FirstOrDefault(),
                    CreatedAt = l.Item.CreatedAt,
                },
            })
            .ToListAsync(cancellationToken);

        return offers
            .OrderBy(o => o.Status == ListingStatus.Reserved)
            .ThenByDescending(o => o.Published)
            .Select(o => new PublicOfferCard
            {
                ListingId = o.Id,
                Status = o.Status,
                AskingPrice = o.AskingPrice,
                AcceptsOffers = o.AcceptsOffers,
                OpenToTrade = o.OpenToTrade,
                Item = o.Item,
            })
            .ToList();
    }

    /// <summary>Offer detail – the item need not be in the public collection (e.g. a duplicate for sale).</summary>
    public async Task<PublicItemDetail?> GetOfferAsync(int listingId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var detail = await ProjectDetail(db.Items.Where(i => i.Listings.Any(l => l.Id == listingId && PublicOfferStatuses.Contains(l.Status))), languages.Current)
            .SingleOrDefaultAsync(cancellationToken);
        if (detail?.Offer is null)
        {
            return null;
        }

        detail.Categories = detail.Categories.OrderBy(c => c.Name, NameComparer).ToList();
        detail.RelatedOffers = (await GetOffersAsync(cancellationToken))
            .Where(o => o.ListingId != listingId)
            .Take(RelatedCount)
            .ToList();
        return detail;
    }

    /// <summary>May the photo be shown on the public website? (a public photo of a public item or of an item on offer)</summary>
    public async Task<bool> IsImagePublicAsync(string fileKey, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.ItemImages.AnyAsync(im => im.FileKey == fileKey && im.IsPublic
            && (im.Item.Visibility == ItemVisibility.Public || im.Item.Listings.Any(l => PublicOfferStatuses.Contains(l.Status))), cancellationToken);
    }

    public async Task<List<PublicSitemapEntry>> GetSitemapEntriesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.Items.AsNoTracking()
            .Where(i => i.Visibility == ItemVisibility.Public)
            .OrderBy(i => i.Id)
            .Select(i => new
            {
                i.Id,
                i.Name,
                ModifiedAt = i.ModifiedAt ?? i.CreatedAt,
                ImageKeys = i.Images.Where(im => im.IsPublic).OrderBy(im => im.SortOrder).ThenBy(im => im.Id).Select(im => im.FileKey).ToList(),
            })
            .ToListAsync(cancellationToken);
        return items.Select(i => new PublicSitemapEntry(i.Id, i.Name, i.ModifiedAt, i.ImageKeys)).ToList();
    }

    /// <summary>Projection of the detail – the only place selecting item data for the detail page.</summary>
    /// <remarks>Translatable texts are resolved on the client – EF evaluates <c>Get(language)</c> in the final projection.</remarks>
    private static IQueryable<PublicItemDetail> ProjectDetail(IQueryable<Item> items, LanguageContext language) => items
        .AsNoTracking()
            .Select(i => new PublicItemDetail
            {
                Id = i.Id,
                Name = i.Name.Get(language) ?? "",
                AllNames = i.Name,
                TypeId = i.TypeId,
                TypeName = i.Type != null ? i.Type.Name.Get(language) : null,
                CountryId = i.CountryId,
                CountryName = i.Country != null ? i.Country.Name.Get(language) : null,
                YearReleased = i.YearReleased,
                YearDiscontinued = i.YearDiscontinued,
                Condition = i.Condition,
                Originality = i.Originality,
                HasBox = i.HasBox,
                HasCertificate = i.HasCertificate,
                HasDocumentation = i.HasDocumentation,
                PublicDescription = i.PublicDescription.Get(language),
                Categories = i.Categories.Select(c => new PublicCategory(c.Id, c.Name.Get(language) ?? "", c.Color)).ToList(),
                Images = i.Images
                    .Where(im => im.IsPublic)
                    .OrderBy(im => im.SortOrder).ThenBy(im => im.Id)
                    .Select(im => new PublicImage(im.FileKey, im.Width, im.Height, im.Caption.Get(language), im.ImageType))
                    .ToList(),
                Offer = i.Listings
                    .Where(l => PublicOfferStatuses.Contains(l.Status))
                    .Select(l => new PublicOffer(l.Id, l.Status, l.AskingPrice, l.AcceptsOffers, l.OpenToTrade, l.PublicNote.Get(language)))
                    .FirstOrDefault(),
                ModifiedAt = i.ModifiedAt ?? i.CreatedAt,
            });

    private async Task<List<PublicItemRow>> LoadRowsAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Get(language) runs on the client – EF evaluates it in the final projection.
        var language = languages.Current;
        var rows = await db.Items.AsNoTracking()
            .Where(i => i.Visibility == ItemVisibility.Public)
            .Select(i => new
            {
                i.Id,
                Name = i.Name.Get(language) ?? "",
                AllNames = i.Name,
                i.TypeId,
                TypeName = i.Type != null ? i.Type.Name.Get(language) : null,
                i.CountryId,
                CountryName = i.Country != null ? i.Country.Name.Get(language) : null,
                i.YearReleased,
                i.YearDiscontinued,
                i.IsFeatured,
                PublicDescription = i.PublicDescription.Get(language),
                i.CreatedAt,
                Image = i.Images
                    .Where(im => im.IsPublic)
                    .OrderBy(im => im.SortOrder).ThenBy(im => im.Id)
                    .Select(im => new PublicImage(im.FileKey, im.Width, im.Height, im.Caption.Get(language), im.ImageType))
                    .FirstOrDefault(),
                Categories = i.Categories.Select(c => new PublicCategory(c.Id, c.Name.Get(language) ?? "", c.Color)).ToList(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new PublicItemRow
        {
            Card = new PublicItemCard
            {
                Id = r.Id,
                Name = r.Name,
                AllNames = r.AllNames,
                TypeName = r.TypeName,
                CountryName = r.CountryName,
                YearReleased = r.YearReleased,
                YearDiscontinued = r.YearDiscontinued,
                Image = r.Image,
                CreatedAt = r.CreatedAt,
            },
            TypeId = r.TypeId,
            CountryId = r.CountryId,
            IsFeatured = r.IsFeatured,
            PublicDescription = r.PublicDescription,
            Categories = r.Categories,
        }).ToList();
    }

    private static List<FacetOption> Facet(IEnumerable<PublicItemRow> rows, Func<PublicItemRow, IEnumerable<(int Id, string Name)>> selector) =>
        rows.SelectMany(selector)
            .GroupBy(x => x.Id)
            .Select(g => new FacetOption(g.Key, g.First().Name, g.Count()))
            .OrderBy(f => f.Name, NameComparer)
            .ToList();

    /// <summary>Internal row for filtering – only <see cref="Card"/> goes out.</summary>
    private sealed class PublicItemRow
    {
        public required PublicItemCard Card { get; init; }
        public int? TypeId { get; init; }
        public int? CountryId { get; init; }
        public bool IsFeatured { get; init; }
        public string? PublicDescription { get; init; }
        public required List<PublicCategory> Categories { get; init; }
    }
}
