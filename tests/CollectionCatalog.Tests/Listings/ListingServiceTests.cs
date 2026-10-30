using static CollectionCatalog.Tests.Infrastructure.TestText;
using CollectionCatalog.Tests.Infrastructure;
using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Services.Listings;
using Microsoft.EntityFrameworkCore;

namespace CollectionCatalog.Tests.Listings;

[TestClass]
public sealed class ListingServiceTests
{
    private TestEnvironment _env = null!;
    private FixedTimeProvider _time = null!;
    private ListingService _service = null!;
    private int _itemId;

    [TestInitialize]
    public async Task Initialize()
    {
        _time = new FixedTimeProvider(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));
        _env = await TestEnvironment.CreateAsync(_time);
        _service = new ListingService(new TestDbContextFactory(_env), _time, _env.Languages, TestEnvironment.Text);

        await using var db = _env.CreateDbContext();
        var item = new Item { Name = En("Specimen") };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        _itemId = item.Id;
    }

    [TestCleanup]
    public async Task Cleanup() => await _env.DisposeAsync();

    [TestMethod]
    public async Task Save_NewActiveListing_IsPublished()
    {
        var result = await _service.SaveAsync(_itemId, new ListingEditModel { Status = ListingStatus.Active, AskingPrice = 1200, PublicNote = "  " });

        var open = await _service.GetOpenForItemAsync(_itemId);
        Assert.IsNotNull(open);
        Assert.AreEqual(result.OpenListingId, open.Id);
        Assert.AreEqual(1200m, open.AskingPrice);
        Assert.IsNull(open.PublicNote);

        await using var db = _env.CreateDbContext();
        var listing = await db.Listings.SingleAsync();
        Assert.AreEqual(_time.Now.UtcDateTime, listing.PublishedAt);
        Assert.IsNull(listing.ClosedAt);
    }

    [TestMethod]
    public async Task Save_DraftIsNotPublished_UntilActivated()
    {
        await _service.SaveAsync(_itemId, new ListingEditModel { Status = ListingStatus.Draft });
        var open = await _service.GetOpenForItemAsync(_itemId);

        await using (var db = _env.CreateDbContext())
        {
            Assert.IsNull((await db.Listings.SingleAsync()).PublishedAt);
        }

        open!.Status = ListingStatus.Active;
        await _service.SaveAsync(_itemId, open);

        await using (var db = _env.CreateDbContext())
        {
            Assert.AreEqual(1, await db.Listings.CountAsync(), "the existing offer is updated, no new one");
            Assert.IsNotNull((await db.Listings.SingleAsync()).PublishedAt);
        }
    }

    [TestMethod]
    public async Task Save_Sold_ClosesListingAndRecordsSale()
    {
        await _service.SaveAsync(_itemId, new ListingEditModel { Status = ListingStatus.Active, AskingPrice = 1200 });
        var open = await _service.GetOpenForItemAsync(_itemId);
        open!.Status = ListingStatus.Sold;
        open.SalePrice = 1100;

        var result = await _service.SaveAsync(_itemId, open);

        Assert.IsNull(result.OpenListingId);
        Assert.AreEqual(1100m, result.ItemSalePrice);
        Assert.AreEqual(new DateOnly(2026, 10, 25), result.ItemSaleDate);
        Assert.IsNull(await _service.GetOpenForItemAsync(_itemId));
        Assert.AreEqual(1, await _service.CountClosedForItemAsync(_itemId));

        await using var db = _env.CreateDbContext();
        var item = await db.Items.SingleAsync();
        Assert.AreEqual(1100m, item.SalePrice);
        Assert.IsNotNull((await db.Listings.SingleAsync()).ClosedAt);
    }

    [TestMethod]
    public async Task Save_SoldWithoutPrice_UsesAskingPrice_AndKeepsExistingSale()
    {
        await _service.SaveAsync(_itemId, new ListingEditModel { Status = ListingStatus.Sold, AskingPrice = 800 });
        await using (var db = _env.CreateDbContext())
        {
            Assert.AreEqual(800m, (await db.Items.SingleAsync()).SalePrice);
        }

        // A second sale (new offer) does not overwrite the price entered earlier.
        await _service.SaveAsync(_itemId, new ListingEditModel { Status = ListingStatus.Sold, AskingPrice = 999 });
        await using (var db = _env.CreateDbContext())
        {
            Assert.AreEqual(800m, (await db.Items.SingleAsync()).SalePrice);
        }
    }

    [TestMethod]
    public async Task Save_Sold_ManualSalePriceOverridesEarlierOne()
    {
        await _service.SaveAsync(_itemId, new ListingEditModel { Status = ListingStatus.Sold, AskingPrice = 800 });

        await _service.SaveAsync(_itemId, new ListingEditModel { Status = ListingStatus.Sold, AskingPrice = 999, SalePrice = 950 });

        await using var db = _env.CreateDbContext();
        Assert.AreEqual(950m, (await db.Items.SingleAsync()).SalePrice);
    }

    [TestMethod]
    public async Task Save_ListingClosedInAnotherTab_IsNotReopened()
    {
        await _service.SaveAsync(_itemId, new ListingEditModel { Status = ListingStatus.Active, AskingPrice = 1200 });
        var staleTab = await _service.GetOpenForItemAsync(_itemId);
        var otherTab = await _service.GetOpenForItemAsync(_itemId);
        otherTab!.Status = ListingStatus.Withdrawn;
        await _service.SaveAsync(_itemId, otherTab);

        staleTab!.AskingPrice = 1000;
        var result = await _service.SaveAsync(_itemId, staleTab);

        Assert.IsNotNull(result.Error);
        await using var db = _env.CreateDbContext();
        var listing = await db.Listings.SingleAsync();
        Assert.AreEqual(ListingStatus.Withdrawn, listing.Status);
        Assert.AreEqual(1200m, listing.AskingPrice);
    }

    [TestMethod]
    public async Task Save_SecondNewOpenListing_ReturnsErrorInsteadOfThrowing()
    {
        // Two tabs create an offer at the same time – the unique index of the open offer stops the second one.
        await _service.SaveAsync(_itemId, new ListingEditModel { Status = ListingStatus.Active, AskingPrice = 1200 });

        var result = await _service.SaveAsync(_itemId, new ListingEditModel { Status = ListingStatus.Draft, AskingPrice = 900 });

        Assert.IsNotNull(result.Error);
        Assert.IsNull(result.OpenListingId);
        await using var db = _env.CreateDbContext();
        Assert.AreEqual(1, await db.Listings.CountAsync());
    }

    [TestMethod]
    public async Task GetListings_FiltersOpenAndClosed()
    {
        await _service.SaveAsync(_itemId, new ListingEditModel { Status = ListingStatus.Withdrawn });
        await _service.SaveAsync(_itemId, new ListingEditModel { Status = ListingStatus.Active, AskingPrice = 500 });

        Assert.AreEqual(ListingStatus.Active, (await _service.GetListingsAsync(ListingBox.Open)).Single().Status);
        Assert.AreEqual(ListingStatus.Withdrawn, (await _service.GetListingsAsync(ListingBox.Closed)).Single().Status);
        Assert.HasCount(2, await _service.GetListingsAsync(ListingBox.All));
    }
}
