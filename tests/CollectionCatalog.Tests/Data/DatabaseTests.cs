using CollectionCatalog.Tests.Infrastructure;
using CollectionCatalog.Web.Domain;
using Microsoft.EntityFrameworkCore;
using static CollectionCatalog.Tests.Infrastructure.TestText;

namespace CollectionCatalog.Tests.Data;

[TestClass]
public sealed class DatabaseTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private FixedTimeProvider _time = null!;
    private TestEnvironment _env = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _time = new FixedTimeProvider(_now);
        _env = await TestEnvironment.CreateAsync(_time);
    }

    [TestCleanup]
    public async Task Cleanup() => await _env.DisposeAsync();

    [TestMethod]
    public async Task Migrations_MatchModel()
    {
        await using var db = _env.CreateDbContext();

        Assert.IsFalse(db.Database.HasPendingModelChanges(), "The model differs from the migrations – `dotnet ef migrations add` is missing.");
        Assert.IsEmpty(await db.Database.GetPendingMigrationsAsync());
    }

    [TestMethod]
    public async Task Enums_AreStoredAsText()
    {
        await using (var db = _env.CreateDbContext())
        {
            db.Items.Add(new Item { Name = En("Amethyst geode"), Originality = Originality.Replica });
            await db.SaveChangesAsync();
        }

        await using (var db = _env.CreateDbContext())
        {
            var row = await db.Database
                .SqlQuery<EnumRow>($"SELECT Condition, Originality, Visibility FROM Items")
                .SingleAsync();

            Assert.AreEqual("Good", row.Condition);
            Assert.AreEqual("Replica", row.Originality);
            Assert.AreEqual("Private", row.Visibility);
        }
    }

    [TestMethod]
    public async Task Listing_OnlyOneOpenPerItem()
    {
        await using var db = _env.CreateDbContext();
        var item = new Item { Name = En("Specimen") };
        item.Listings.Add(new Listing { Status = ListingStatus.Sold });
        item.Listings.Add(new Listing { Status = ListingStatus.Withdrawn });
        item.Listings.Add(new Listing { Status = ListingStatus.Active });
        db.Items.Add(item);
        await db.SaveChangesAsync();

        db.Listings.Add(new Listing { ItemId = item.Id, Status = ListingStatus.Draft });

        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [TestMethod]
    public async Task AuditInterceptor_SetsTimestamps()
    {
        int itemId;
        await using (var db = _env.CreateDbContext())
        {
            var item = new Item { Name = En("Specimen") };
            db.Items.Add(item);
            await db.SaveChangesAsync();
            itemId = item.Id;

            Assert.AreEqual(_now.UtcDateTime, item.CreatedAt);
            Assert.IsNull(item.ModifiedAt);
        }

        _time.Now = _now.AddHours(1);
        await using (var db = _env.CreateDbContext())
        {
            var item = await db.Items.SingleAsync(i => i.Id == itemId);
            item.Name = En("Amethyst geode, polished");
            await db.SaveChangesAsync();

            Assert.AreEqual(_now.UtcDateTime, item.CreatedAt);
            Assert.AreEqual(_now.AddHours(1).UtcDateTime, item.ModifiedAt);
        }
    }

    [TestMethod]
    public async Task AuditInterceptor_KeepsExplicitCreatedAt()
    {
        var created = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await using var db = _env.CreateDbContext();
        var item = new Item { Name = En("Specimen"), CreatedAt = created };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        Assert.AreEqual(created, item.CreatedAt);
    }

    [TestMethod]
    public async Task DeletingItem_KeepsInquiries()
    {
        await using var db = _env.CreateDbContext();
        var item = new Item { Name = En("Specimen") };
        var listing = new Listing { Item = item, Status = ListingStatus.Active };
        db.Inquiries.Add(new Inquiry { Item = item, Listing = listing, Name = "Jane", Email = "jane@example.com", Message = "I offer 1000", OfferedPrice = 1000m });
        await db.SaveChangesAsync();

        db.Items.Remove(item);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var inquiry = await db.Inquiries.SingleAsync();
        Assert.IsNull(inquiry.ItemId);
        Assert.IsNull(inquiry.ListingId);
        Assert.IsEmpty(await db.Listings.ToListAsync());
    }

    [TestMethod]
    public async Task Migrations_SeedLanguagesAndSiteTexts_ButNoCodebookEntries()
    {
        await using var db = _env.CreateDbContext();

        CollectionAssert.AreEqual(new[] { "en", "cs" }, await db.Languages.OrderBy(l => l.SortOrder).Select(l => l.Code).ToArrayAsync());
        Assert.AreEqual("en", (await db.Languages.SingleAsync(l => l.IsDefault)).Code);
        Assert.AreEqual("Collection", (await db.SiteSettings.SingleAsync()).SiteName["en"]);
        Assert.IsEmpty(await db.ItemTypes.ToListAsync());
    }

    [TestMethod]
    public async Task DeletingItemType_KeepsItemsWithoutType()
    {
        await using var db = _env.CreateDbContext();
        var type = new ItemType { Name = En("Meteorite (test)") };
        db.Items.Add(new Item { Name = En("Meteorite"), Type = type });
        await db.SaveChangesAsync();

        db.ItemTypes.Remove(type);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        Assert.IsNull((await db.Items.SingleAsync()).TypeId);
    }


    private sealed record EnumRow(string Condition, string Originality, string Visibility);
}
