using System.ComponentModel.DataAnnotations;
using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Resources;
using CollectionCatalog.Web.Services.Languages;
using CollectionCatalog.Web.Services.Public;
using CollectionCatalog.Web.Services.Text;
using Microsoft.EntityFrameworkCore;

namespace CollectionCatalog.Web.Services.Inquiries;

/// <summary>
/// Inquiries and questions of visitors: receiving them from the public website and managing them in the administration.
/// </summary>
public sealed class InquiryService(IDbContextFactory<AppDbContext> dbContextFactory, TimeProvider timeProvider, LanguageService languages)
{
    /// <summary>Statuses meaning "handled" – after <see cref="RetentionPeriod"/> the inquiry is deleted (GDPR).</summary>
    public static readonly IReadOnlyList<InquiryStatus> ClosedStatuses = [InquiryStatus.Replied, InquiryStatus.Accepted, InquiryStatus.Declined, InquiryStatus.Archived];

    public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(365);

    // === Public website ===

    /// <summary>
    /// Stores a message from the website. Linked to an item only if it is public; to an offer only if it is active / reserved.
    /// </summary>
    /// <returns><c>false</c> when the item / offer is not (or no longer) publicly available.</returns>
    public async Task<bool> SubmitAsync(InquiryInput input, int? itemId, int? listingId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var acceptsOffers = false;
        if (listingId is { } lid)
        {
            var listing = await db.Listings.AsNoTracking()
                .Where(l => l.Id == lid && PublicItemQuery.PublicOfferStatuses.Contains(l.Status))
                .Select(l => new { l.ItemId, l.AcceptsOffers })
                .SingleOrDefaultAsync(cancellationToken);
            if (listing is null)
            {
                return false;
            }
            itemId = listing.ItemId;
            acceptsOffers = listing.AcceptsOffers;
        }
        else if (itemId is { } iid && !await db.Items.AnyAsync(i => i.Id == iid && i.Visibility == ItemVisibility.Public, cancellationToken))
        {
            return false;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        db.Inquiries.Add(new Inquiry
        {
            ItemId = itemId,
            ListingId = listingId,
            Name = input.Name!.Trim(),
            Email = input.Email!.Trim(),
            Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim(),
            Message = input.Message!.Trim(),
            OfferedPrice = acceptsOffers ? input.OfferedPrice : null,
            ConsentGivenAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // === Administrace ===

    public async Task<int> CountNewAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Inquiries.CountAsync(i => i.Status == InquiryStatus.New, cancellationToken);
    }

    public async Task<List<InquiryRow>> GetInboxAsync(InquiryFilter filter, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Inquiries.AsNoTracking();
        query = filter.Box switch
        {
            InquiryBox.Open => query.Where(i => !ClosedStatuses.Contains(i.Status)),
            InquiryBox.Closed => query.Where(i => ClosedStatuses.Contains(i.Status)),
            _ => query,
        };
        if (filter.ItemId is { } itemId)
        {
            query = query.Where(i => i.ItemId == itemId);
        }
        if (filter.ListingId is { } listingId)
        {
            query = query.Where(i => i.ListingId == listingId);
        }

        var language = languages.Current;
        var rows = await query
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InquiryRow
            {
                Id = i.Id,
                Name = i.Name,
                Email = i.Email,
                Message = i.Message,
                OfferedPrice = i.OfferedPrice,
                Status = i.Status,
                ItemId = i.ItemId,
                // Get(language) runs on the client – EF evaluates it in the final projection.
                ItemName = i.Item != null ? i.Item.Name.Get(language) : null,
                ListingId = i.ListingId,
                ThumbnailKey = i.Item != null ? i.Item.Images.OrderBy(im => im.SortOrder).Select(im => im.FileKey).FirstOrDefault() : null,
                CreatedAt = i.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var search = SearchText.Normalize(filter.Search?.Trim());
        return search.Length == 0
            ? rows
            : rows.Where(r => SearchText.Matches(search, [r.Name, r.Email, r.Message, r.ItemName])).ToList();
    }

    /// <summary>Detail of an inquiry; a new one gets marked as read.</summary>
    public async Task<InquiryDetail?> OpenAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var inquiry = await db.Inquiries
            .Include(i => i.Item)
            .Include(i => i.Listing)
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (inquiry is null)
        {
            return null;
        }

        if (inquiry.Status == InquiryStatus.New)
        {
            inquiry.Status = InquiryStatus.Read;
            await db.SaveChangesAsync(cancellationToken);
        }

        return new InquiryDetail
        {
            Id = inquiry.Id,
            Name = inquiry.Name,
            Email = inquiry.Email,
            Phone = inquiry.Phone,
            Message = inquiry.Message,
            OfferedPrice = inquiry.OfferedPrice,
            Status = inquiry.Status,
            InternalNote = inquiry.InternalNote,
            ItemId = inquiry.ItemId,
            ItemName = inquiry.Item?.Name.Get(languages.Current),
            ListingId = inquiry.ListingId,
            ListingStatus = inquiry.Listing?.Status,
            AskingPrice = inquiry.Listing?.AskingPrice,
            CreatedAt = inquiry.CreatedAt,
        };
    }

    public async Task SetStatusAsync(int id, InquiryStatus status, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var inquiry = await db.Inquiries.SingleAsync(i => i.Id == id, cancellationToken);
        inquiry.Status = status;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveNoteAsync(int id, string? note, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var inquiry = await db.Inquiries.SingleAsync(i => i.Id == id, cancellationToken);
        inquiry.InternalNote = TextLimit.Clean(note, Inquiry.MaxNoteLength);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.Inquiries.Where(i => i.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>Deletes handled inquiries older than <see cref="RetentionPeriod"/> (personal data is not kept longer than needed).</summary>
    /// <returns>Number of deleted inquiries.</returns>
    public async Task<int> DeleteExpiredAsync(CancellationToken cancellationToken = default)
    {
        var limit = timeProvider.GetUtcNow().UtcDateTime - RetentionPeriod;
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Inquiries
            .Where(i => ClosedStatuses.Contains(i.Status) && (i.ModifiedAt ?? i.CreatedAt) < limit)
            .ExecuteDeleteAsync(cancellationToken);
    }
}

/// <summary>Inquiry form on the website.</summary>
public sealed class InquiryInput
{
    [Required(ErrorMessage = "Fill in your name.")]
    [StringLength(200, ErrorMessage = "The name can have at most 200 characters.")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "Fill in your e-mail so we can reply.")]
    [StrictEmail(ErrorMessage = "Enter a valid e-mail.")]
    [StringLength(320, ErrorMessage = "The e-mail is too long.")]
    public string? Email { get; set; }

    [StringLength(50, ErrorMessage = "The phone can have at most 50 characters.")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Write what you are contacting us about.")]
    [StringLength(4000, ErrorMessage = "The message can have at most 4,000 characters.")]
    public string? Message { get; set; }

    [Range(1, 100_000_000, ErrorMessage = "Enter a valid amount.")]
    public decimal? OfferedPrice { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "Consent with the processing of the data is required to send the form.")]
    public bool Consent { get; set; }

    /// <summary>Bot trap – a hidden field a person does not fill in.</summary>
    public string? Website { get; set; }
}

public enum InquiryBox
{
    Open,   // New and in progress
    Closed, // Handled
    All,
}

public sealed record InquiryFilter
{
    public InquiryBox Box { get; init; }
    public string? Search { get; init; }
    public int? ItemId { get; init; }
    public int? ListingId { get; init; }
}

public sealed class InquiryRow
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required string Message { get; init; }
    public decimal? OfferedPrice { get; init; }
    public InquiryStatus Status { get; init; }
    public int? ItemId { get; init; }
    public string? ItemName { get; init; }
    public int? ListingId { get; init; }
    public string? ThumbnailKey { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class InquiryDetail
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public string? Phone { get; init; }
    public required string Message { get; init; }
    public decimal? OfferedPrice { get; init; }
    public InquiryStatus Status { get; set; }
    public string? InternalNote { get; set; }
    public int? ItemId { get; init; }
    public string? ItemName { get; init; }
    public int? ListingId { get; init; }
    public ListingStatus? ListingStatus { get; init; }
    public decimal? AskingPrice { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>Kind of the inquiry for display.</summary>
    public string Kind => AppText.Get(ListingId is not null ? "Reply to an offer" : ItemId is not null ? "Question about a piece of the collection" : "General question");
}
