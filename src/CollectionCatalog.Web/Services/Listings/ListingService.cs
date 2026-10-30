using System.ComponentModel.DataAnnotations;
using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Infrastructure;
using CollectionCatalog.Web.Resources;
using CollectionCatalog.Web.Services.Languages;
using CollectionCatalog.Web.Services.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace CollectionCatalog.Web.Services.Listings;

/// <summary>
/// Offers for sale / trade (administration). An item has at most one open offer (Draft/Active/Reserved);
/// closed ones (Sold/Withdrawn) stay as history.
/// </summary>
public sealed class ListingService(IDbContextFactory<AppDbContext> dbContextFactory, TimeProvider timeProvider, LanguageService languages, IStringLocalizer<AppText> text)
{
    /// <summary>Open offer of the item, or <c>null</c>.</summary>
    public async Task<ListingEditModel?> GetOpenForItemAsync(int itemId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var model = await db.Listings.AsNoTracking()
            .Where(l => l.ItemId == itemId && Listing.OpenStatuses.Contains(l.Status))
            .Select(l => new ListingEditModel
            {
                Id = l.Id,
                Status = l.Status,
                AskingPrice = l.AskingPrice,
                AcceptsOffers = l.AcceptsOffers,
                OpenToTrade = l.OpenToTrade,
                PublicNoteText = l.PublicNote,
                InquiryCount = l.Inquiries.Count,
                NewInquiryCount = l.Inquiries.Count(i => i.Status == InquiryStatus.New),
            })
            .SingleOrDefaultAsync(cancellationToken);
        model?.SetLanguages(languages.Current);
        return model;
    }

    /// <summary>Number of earlier (closed) offers of the item.</summary>
    public async Task<int> CountClosedForItemAsync(int itemId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Listings.CountAsync(l => l.ItemId == itemId && !Listing.OpenStatuses.Contains(l.Status), cancellationToken);
    }

    /// <summary>
    /// Creates / saves the open offer. On switching to "Sold" it fills in the item's sale price and date
    /// (when missing); closing statuses close the offer.
    /// </summary>
    public async Task<ListingSaveResult> SaveAsync(int itemId, ListingEditModel model, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Items.SingleAsync(i => i.Id == itemId, cancellationToken);

        // Only an open offer can be edited – an older tab must not reopen an offer another one has closed meanwhile.
        Listing? listing = null;
        if (model.Id is { } id)
        {
            listing = await db.Listings.SingleOrDefaultAsync(l => l.Id == id && l.ItemId == itemId && Listing.OpenStatuses.Contains(l.Status), cancellationToken);
            if (listing is null)
            {
                return ListingSaveResult.Failed(text["The offer was changed meanwhile by someone else (or another tab). Reload the page."]);
            }
        }
        else
        {
            listing = new Listing { ItemId = itemId };
            db.Listings.Add(listing);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        listing.Status = model.Status;
        listing.AskingPrice = model.AskingPrice;
        listing.AcceptsOffers = model.AcceptsOffers;
        listing.OpenToTrade = model.OpenToTrade;
        listing.PublicNote = model.PublicNoteText.Select(note => TextLimit.Clean(note, ListingEditModel.MaxNoteLength));

        if (model.Status is ListingStatus.Active or ListingStatus.Reserved)
        {
            listing.PublishedAt ??= now;
        }
        if (!listing.IsOpen)
        {
            listing.ClosedAt = now;
        }

        if (model.Status == ListingStatus.Sold)
        {
            // A sale price entered by hand wins; otherwise keep the earlier one, or take the asking price.
            item.SalePrice = model.SalePrice ?? item.SalePrice ?? model.AskingPrice;
            item.SaleDate ??= AppTime.Today(timeProvider);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 } && model.Id is null)
        {
            // Unique index IX_Listings_ItemId_Open: another tab has created an open offer meanwhile.
            return ListingSaveResult.Failed(text["The item already has an open offer (created in another tab). Reload the page."]);
        }
        return new ListingSaveResult(listing.IsOpen ? listing.Id : null, item.SalePrice, item.SaleDate);
    }

    public async Task<List<ListingRow>> GetListingsAsync(ListingBox box, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Listings.AsNoTracking();
        query = box switch
        {
            ListingBox.Open => query.Where(l => Listing.OpenStatuses.Contains(l.Status)),
            ListingBox.Closed => query.Where(l => !Listing.OpenStatuses.Contains(l.Status)),
            _ => query,
        };

        // Get(language) runs on the client – EF evaluates it in the final projection.
        var language = languages.Current;
        var rows = await query
            .Select(l => new ListingRow
            {
                Id = l.Id,
                ItemId = l.ItemId,
                ItemName = l.Item.Name.Get(language) ?? "",
                ItemIsPublic = l.Item.Visibility == ItemVisibility.Public,
                ThumbnailKey = l.Item.Images.OrderBy(im => im.SortOrder).Select(im => im.FileKey).FirstOrDefault(),
                Status = l.Status,
                AskingPrice = l.AskingPrice,
                SalePrice = l.Item.SalePrice,
                PublishedAt = l.PublishedAt,
                ClosedAt = l.ClosedAt,
                CreatedAt = l.CreatedAt,
                InquiryCount = l.Inquiries.Count,
                NewInquiryCount = l.Inquiries.Count(i => i.Status == InquiryStatus.New),
            })
            .ToListAsync(cancellationToken);

        // Order of statuses: active, reserved, draft, then closed; newest first within a status.
        return rows
            .OrderBy(r => r.Status switch
            {
                ListingStatus.Active => 0,
                ListingStatus.Reserved => 1,
                ListingStatus.Draft => 2,
                _ => 3,
            })
            .ThenByDescending(r => r.ClosedAt ?? r.PublishedAt ?? r.CreatedAt)
            .ToList();
    }
}

/// <summary>Offer of an item; the public note is translatable (edited in the language chosen in the item editor).</summary>
public sealed class ListingEditModel : TranslatableModel
{
    public const int MaxNoteLength = 2000;

    public int? Id { get; set; }
    public ListingStatus Status { get; set; } = ListingStatus.Draft;

    [Range(0, 100_000_000, ErrorMessage = "Enter a valid amount.")]
    public decimal? AskingPrice { get; set; }

    public bool AcceptsOffers { get; set; } = true;
    public bool OpenToTrade { get; set; }

    public LocalizedText PublicNoteText { get; set; } = LocalizedText.Empty;

    [StringLength(MaxNoteLength, ErrorMessage = "The note can have at most {1} characters.")]
    public string? PublicNote { get => Read(PublicNoteText); set => PublicNoteText = Write(PublicNoteText, value); }

    /// <summary>Actual sale price – entered only when marking the offer as sold (default = asking price).</summary>
    [Range(0, 100_000_000, ErrorMessage = "Enter a valid amount.")]
    public decimal? SalePrice { get; set; }

    public int InquiryCount { get; init; }
    public int NewInquiryCount { get; init; }
}

/// <param name="OpenListingId">Id of the offer when it stayed open (otherwise the editor offers a new one).</param>
/// <param name="Error">Why the offer was not saved (concurrent change in another tab); <c>null</c> = saved.</param>
public sealed record ListingSaveResult(int? OpenListingId, decimal? ItemSalePrice, DateOnly? ItemSaleDate, string? Error = null)
{
    public static ListingSaveResult Failed(string error) => new(null, null, null, error);
}

public enum ListingBox
{
    Open,
    Closed,
    All,
}

public sealed class ListingRow
{
    public int Id { get; init; }
    public int ItemId { get; init; }
    public required string ItemName { get; init; }
    public bool ItemIsPublic { get; init; }
    public string? ThumbnailKey { get; init; }
    public ListingStatus Status { get; init; }
    public decimal? AskingPrice { get; init; }
    public decimal? SalePrice { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime? ClosedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public int InquiryCount { get; init; }
    public int NewInquiryCount { get; init; }
}
