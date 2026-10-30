using System.Globalization;
using CollectionCatalog.Tests.Infrastructure;
using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Services.Public;
using Microsoft.EntityFrameworkCore;
using static CollectionCatalog.Tests.Infrastructure.TestText;

namespace CollectionCatalog.Tests.Public;

[TestClass]
public sealed class PublicItemQueryTests
{
    private TestEnvironment _env = null!;
    private PublicItemQuery _query = null!;
    private Item _geode = null!;
    private Item _ammonite = null!;
    private Item _secret = null!;
    private Category _crystals = null!;
    private Country _brazil = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _env = await TestEnvironment.CreateAsync();
        _query = new PublicItemQuery(new TestDbContextFactory(_env), _env.Languages);

        await using var db = _env.CreateDbContext();
        _crystals = new Category { Name = En("Crystals").With("cs", "Krystaly") };
        _brazil = new Country { Name = En("Brazil").With("cs", "Brazílie") };
        var mineral = new ItemType { Name = En("Mineral").With("cs", "Minerál") };
        var fossil = new ItemType { Name = En("Fossil").With("cs", "Fosilie") };

        _geode = new Item
        {
            Name = En("Amethyst geode").With("cs", "Ametystová geoda"),
            Type = mineral,
            Country = _brazil,
            YearReleased = 1922,
            YearDiscontinued = 1990,
            Visibility = ItemVisibility.Public,
            IsFeatured = true,
            Categories = { _crystals },
            PublicDescription = En("The largest specimen of the collection."),
            PurchasePrice = 15000,
            Images =
            {
                new ItemImage { FileKey = Key('a'), SortOrder = 1, ImageType = ImageType.Back },
                new ItemImage { FileKey = Key('b'), SortOrder = 0 },
                new ItemImage { FileKey = Key('c'), SortOrder = 2, IsPublic = false, ImageType = ImageType.Certificate },
            },
        };
        _ammonite = new Item
        {
            Name = En("Polished ammonite"),
            Type = fossil,
            Country = _brazil,
            YearReleased = 1920,
            Visibility = ItemVisibility.Public,
            Categories = { _crystals },
            CreatedAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        _secret = new Item
        {
            Name = En("Secret meteorite"),
            Type = mineral,
            Country = _brazil,
            Categories = { _crystals },
            Images = { new ItemImage { FileKey = Key('d') } },
        };
        db.Items.AddRange(_geode, _ammonite, _secret);
        await db.SaveChangesAsync();
    }

    [TestCleanup]
    public async Task Cleanup() => await _env.DisposeAsync();

    [TestMethod]
    public async Task Search_ReturnsOnlyPublicItems()
    {
        var page = await _query.SearchAsync(new PublicItemFilter());

        CollectionAssert.AreEquivalent(new[] { _geode.Id, _ammonite.Id }, page.Items.Select(i => i.Id).ToArray());
        Assert.AreEqual(2, page.TotalCount);
        // Facets count public pieces only.
        Assert.AreEqual(2, page.Categories.Single().Count);
    }

    [TestMethod]
    public async Task Search_CardUsesFirstPublicImage()
    {
        var page = await _query.SearchAsync(new PublicItemFilter());

        Assert.AreEqual(Key('b'), page.Items.Single(i => i.Id == _geode.Id).Image?.FileKey);
        Assert.IsNull(page.Items.Single(i => i.Id == _ammonite.Id).Image);
    }

    [TestMethod]
    [DataRow("geode amethyst", DisplayName = "name, words in any order")]
    [DataRow("largest", DisplayName = "public description")]
    [DataRow("crystals mineral", DisplayName = "category + type")]
    public async Task Search_FindsByText(string search)
    {
        var page = await _query.SearchAsync(new PublicItemFilter { Search = search });

        Assert.AreEqual(_geode.Id, page.Items.Single().Id);
    }

    [TestMethod]
    public async Task Search_IgnoresDiacritics_InTheLanguageOfThePage()
    {
        var original = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("cs");
        try
        {
            var page = await _query.SearchAsync(new PublicItemFilter { Search = "ametystova brazilie" });

            Assert.AreEqual(_geode.Id, page.Items.Single().Id);
        }
        finally
        {
            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public async Task Search_DoesNotSearchPrivateFields()
    {
        await using (var db = _env.CreateDbContext())
        {
            var geode = await db.Items.SingleAsync(i => i.Id == _geode.Id);
            geode.PrivateNotes = "bought in Munich";
            geode.Seller = "Auction house";
            await db.SaveChangesAsync();
        }

        Assert.IsEmpty((await _query.SearchAsync(new PublicItemFilter { Search = "munich" })).Items);
        Assert.IsEmpty((await _query.SearchAsync(new PublicItemFilter { Search = "auction" })).Items);
    }

    [TestMethod]
    public async Task Search_YearRangeMatchesOverlappingPeriod()
    {
        // The geode (1922–1990) overlaps 1950, the ammonite (1920 only) does not.
        var page = await _query.SearchAsync(new PublicItemFilter { YearFrom = 1950, YearTo = 1960 });

        Assert.AreEqual(_geode.Id, page.Items.Single().Id);
    }

    [TestMethod]
    public async Task Search_SortsByNewestByDefault_AndByYear()
    {
        var newest = await _query.SearchAsync(new PublicItemFilter());
        var oldest = await _query.SearchAsync(new PublicItemFilter { Sort = PublicSort.Oldest });

        Assert.AreEqual(_ammonite.Id, newest.Items[0].Id);
        Assert.AreEqual(_ammonite.Id, oldest.Items[0].Id);
        Assert.AreEqual(_geode.Id, (await _query.SearchAsync(new PublicItemFilter { Sort = PublicSort.Youngest })).Items[0].Id);
    }

    [TestMethod]
    public async Task Search_ClampsPage()
    {
        var page = await _query.SearchAsync(new PublicItemFilter { Page = 99 });

        Assert.AreEqual(1, page.Page);
        Assert.AreEqual(1, page.PageCount);
    }

    [TestMethod]
    public async Task Detail_PrivateItem_IsNull()
    {
        Assert.IsNull(await _query.GetDetailAsync(_secret.Id));
        Assert.IsNull(await _query.GetDetailAsync(99999));
    }

    [TestMethod]
    public async Task Detail_ContainsOnlyPublicImagesInOrder_AndRelatedPublicItems()
    {
        var detail = await _query.GetDetailAsync(_geode.Id);

        Assert.IsNotNull(detail);
        CollectionAssert.AreEqual(new[] { Key('b'), Key('a') }, detail.Images.Select(i => i.FileKey).ToArray());
        CollectionAssert.AreEqual(new[] { _ammonite.Id }, detail.Related.Select(r => r.Id).ToArray());
        Assert.AreEqual("amethyst-geode", detail.Slug);
        Assert.AreEqual($"sbirka/{_geode.Id}/amethyst-geode", detail.Url);
        Assert.AreEqual("1922–1990", detail.Years);
    }

    [TestMethod]
    public async Task Home_FeaturedFirst_WithStatistics()
    {
        var home = await _query.GetHomeAsync();

        Assert.AreEqual(2, home.TotalCount);
        Assert.AreEqual(1, home.CountryCount);
        Assert.AreEqual(1920, home.YearFrom);
        Assert.AreEqual(1990, home.YearTo);
        Assert.AreEqual(_geode.Id, home.Featured[0].Id);
        Assert.AreEqual(_geode.Id, home.Hero?.Id);
    }

    [TestMethod]
    [DataRow('b', true, DisplayName = "public photo of a public item")]
    [DataRow('c', false, DisplayName = "private photo (certificate)")]
    [DataRow('d', false, DisplayName = "photo of a private item")]
    [DataRow('e', false, DisplayName = "nonexistent")]
    public async Task IsImagePublic(char key, bool expected)
    {
        Assert.AreEqual(expected, await _query.IsImagePublicAsync(Key(key)));
    }

    [TestMethod]
    public async Task Sitemap_ListsOnlyPublicItems()
    {
        var entries = await _query.GetSitemapEntriesAsync();

        CollectionAssert.AreEquivalent(new[] { _geode.Id, _ammonite.Id }, entries.Select(e => e.Id).ToArray());
    }

    [TestMethod]
    public async Task Offers_IncludePrivateItemWithActiveListing_ButNotDrafts()
    {
        int activeId, draftId;
        await using (var db = _env.CreateDbContext())
        {
            var secret = await db.Items.SingleAsync(i => i.Id == _secret.Id);
            var active = new Listing { Item = secret, Status = ListingStatus.Active, AskingPrice = 2500, PublicNote = En("Duplicate") };
            var draft = new Listing { Item = await db.Items.SingleAsync(i => i.Id == _ammonite.Id), Status = ListingStatus.Draft };
            db.Listings.AddRange(active, draft);
            await db.SaveChangesAsync();
            (activeId, draftId) = (active.Id, draft.Id);
        }

        var offers = await _query.GetOffersAsync();
        Assert.AreEqual(activeId, offers.Single().ListingId);
        Assert.AreEqual(2500m, offers.Single().AskingPrice);
        Assert.AreEqual($"nabidka/{activeId}/secret-meteorite", offers.Single().Url);

        var offer = await _query.GetOfferAsync(activeId);
        Assert.IsNotNull(offer);
        Assert.AreEqual("Duplicate", offer.Offer?.PublicNote);
        Assert.IsNull(await _query.GetOfferAsync(draftId));

        // The private item stays out of the collection, but its public photos are available because of the offer.
        Assert.IsNull(await _query.GetDetailAsync(_secret.Id));
        Assert.DoesNotContain(_secret.Id, (await _query.SearchAsync(new PublicItemFilter())).Items.Select(i => i.Id).ToList());
        Assert.IsTrue(await _query.IsImagePublicAsync(Key('d')));
    }

    [TestMethod]
    public async Task Detail_ShowsActiveOffer()
    {
        await using (var db = _env.CreateDbContext())
        {
            db.Listings.Add(new Listing { ItemId = _geode.Id, Status = ListingStatus.Reserved });
            await db.SaveChangesAsync();
        }

        var detail = await _query.GetDetailAsync(_geode.Id);

        Assert.AreEqual(ListingStatus.Reserved, detail?.Offer?.Status);
        StringAssert.StartsWith(detail!.OfferUrl, "nabidka/");
        Assert.AreEqual(1, (await _query.GetHomeAsync()).OfferCount);
    }

    private static string Key(char c) => new(c, 32);
}
