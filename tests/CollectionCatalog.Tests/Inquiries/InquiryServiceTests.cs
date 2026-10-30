using static CollectionCatalog.Tests.Infrastructure.TestText;
using CollectionCatalog.Tests.Infrastructure;
using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Services.Inquiries;
using Microsoft.EntityFrameworkCore;

namespace CollectionCatalog.Tests.Inquiries;

[TestClass]
public sealed class InquiryServiceTests
{
    private TestEnvironment _env = null!;
    private FixedTimeProvider _time = null!;
    private InquiryService _service = null!;
    private Item _public = null!;
    private Item _private = null!;
    private Listing _active = null!;
    private Listing _noOffers = null!;
    private Listing _draft = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _time = new FixedTimeProvider(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));
        _env = await TestEnvironment.CreateAsync(_time);
        _service = new InquiryService(new TestDbContextFactory(_env), _time, _env.Languages);

        await using var db = _env.CreateDbContext();
        _public = new Item { Name = En("Public piece"), Visibility = ItemVisibility.Public };
        _private = new Item { Name = En("Private piece") };
        _active = new Listing { Item = new Item { Name = En("Duplicate") }, Status = ListingStatus.Active, AcceptsOffers = true };
        _noOffers = new Listing { Item = new Item { Name = En("Fixed price") }, Status = ListingStatus.Reserved, AcceptsOffers = false, AskingPrice = 900 };
        _draft = new Listing { Item = new Item { Name = En("Draft") }, Status = ListingStatus.Draft };
        db.AddRange(_public, _private, _active, _noOffers, _draft);
        await db.SaveChangesAsync();
    }

    [TestCleanup]
    public async Task Cleanup() => await _env.DisposeAsync();

    [TestMethod]
    public async Task Submit_ToListing_StoresOfferedPriceAndItem()
    {
        Assert.IsTrue(await _service.SubmitAsync(Input(price: 1500), itemId: null, listingId: _active.Id));

        await using var db = _env.CreateDbContext();
        var inquiry = await db.Inquiries.SingleAsync();
        Assert.AreEqual(_active.Id, inquiry.ListingId);
        Assert.AreEqual(_active.ItemId, inquiry.ItemId);
        Assert.AreEqual(1500m, inquiry.OfferedPrice);
        Assert.AreEqual(InquiryStatus.New, inquiry.Status);
        Assert.AreEqual("jane@example.com", inquiry.Email);
        Assert.AreEqual(_time.Now.UtcDateTime, inquiry.ConsentGivenAt);
        Assert.IsNull(inquiry.Phone);
    }

    [TestMethod]
    public async Task Submit_ToListingWithoutOffers_IgnoresPrice()
    {
        Assert.IsTrue(await _service.SubmitAsync(Input(price: 1500), itemId: null, listingId: _noOffers.Id));

        await using var db = _env.CreateDbContext();
        Assert.IsNull((await db.Inquiries.SingleAsync()).OfferedPrice);
    }

    [TestMethod]
    public async Task Submit_ToPublicItemOrGeneral_Works()
    {
        Assert.IsTrue(await _service.SubmitAsync(Input(price: 1000), itemId: _public.Id, listingId: null));
        Assert.IsTrue(await _service.SubmitAsync(Input(), itemId: null, listingId: null));

        await using var db = _env.CreateDbContext();
        var inquiries = await db.Inquiries.OrderBy(i => i.Id).ToListAsync();
        Assert.AreEqual(_public.Id, inquiries[0].ItemId);
        Assert.IsNull(inquiries[0].OfferedPrice, "price only for offers");
        Assert.IsNull(inquiries[1].ItemId);
    }

    [TestMethod]
    public async Task Submit_ToPrivateItemOrDraftListing_IsRejected()
    {
        Assert.IsFalse(await _service.SubmitAsync(Input(), itemId: _private.Id, listingId: null));
        Assert.IsFalse(await _service.SubmitAsync(Input(), itemId: null, listingId: _draft.Id));
        Assert.IsFalse(await _service.SubmitAsync(Input(), itemId: null, listingId: 9999));

        await using var db = _env.CreateDbContext();
        Assert.AreEqual(0, await db.Inquiries.CountAsync());
    }

    [TestMethod]
    public async Task Open_MarksNewAsRead_AndInboxFilters()
    {
        await _service.SubmitAsync(Input(), null, _active.Id);
        await _service.SubmitAsync(Input(), _public.Id, null);
        var listingInquiry = (await _service.GetInboxAsync(new InquiryFilter { ListingId = _active.Id })).Single();

        Assert.AreEqual(2, await _service.CountNewAsync());
        var detail = await _service.OpenAsync(listingInquiry.Id);

        Assert.AreEqual(InquiryStatus.Read, detail!.Status);
        Assert.AreEqual("Reply to an offer", detail.Kind);
        Assert.AreEqual(1, await _service.CountNewAsync());

        await _service.SetStatusAsync(listingInquiry.Id, InquiryStatus.Replied);
        Assert.HasCount(1, await _service.GetInboxAsync(new InquiryFilter { Box = InquiryBox.Open }));
        Assert.HasCount(1, await _service.GetInboxAsync(new InquiryFilter { Box = InquiryBox.Closed }));
        Assert.HasCount(2, await _service.GetInboxAsync(new InquiryFilter { Box = InquiryBox.All }));
        Assert.HasCount(1, await _service.GetInboxAsync(new InquiryFilter { Box = InquiryBox.All, Search = "duplicate" }));
    }

    [TestMethod]
    public async Task DeleteExpired_RemovesOnlyOldClosedInquiries()
    {
        await _service.SubmitAsync(Input(), null, null); // stays open
        await _service.SubmitAsync(Input(), null, null); // closed – gets deleted
        var ids = (await _service.GetInboxAsync(new InquiryFilter { Box = InquiryBox.All })).Select(r => r.Id).Order().ToList();
        await _service.SetStatusAsync(ids[1], InquiryStatus.Declined);

        _time.Now = _time.Now.AddDays(200);
        Assert.AreEqual(0, await _service.DeleteExpiredAsync(), "not a year yet");

        _time.Now = _time.Now.AddDays(200);
        Assert.AreEqual(1, await _service.DeleteExpiredAsync());

        await using var db = _env.CreateDbContext();
        Assert.AreEqual(ids[0], (await db.Inquiries.SingleAsync()).Id);
    }

    [TestMethod]
    public async Task Inquiries_SurviveListingDeletion()
    {
        await _service.SubmitAsync(Input(), null, _active.Id);

        await using (var db = _env.CreateDbContext())
        {
            db.Items.Remove(await db.Items.SingleAsync(i => i.Id == _active.ItemId));
            await db.SaveChangesAsync();
        }

        var row = (await _service.GetInboxAsync(new InquiryFilter { Box = InquiryBox.All })).Single();
        Assert.IsNull(row.ItemId);
        Assert.IsNull(row.ListingId);
    }

    private static InquiryInput Input(decimal? price = null) => new()
    {
        Name = " Jane Doe ",
        Email = " jane@example.com ",
        Phone = " ",
        Message = "I am interested.",
        OfferedPrice = price,
        Consent = true,
    };
}
