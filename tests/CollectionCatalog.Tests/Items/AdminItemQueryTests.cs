using CollectionCatalog.Tests.Infrastructure;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Services.Items;
using static CollectionCatalog.Tests.Infrastructure.TestText;

namespace CollectionCatalog.Tests.Items;

[TestClass]
public sealed class AdminItemQueryTests
{
    private TestEnvironment _env = null!;
    private AdminItemQuery _query = null!;
    private Country _morocco = null!;
    private Category _fossils = null!;
    private ItemType _mineral = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _env = await TestEnvironment.CreateAsync();
        _query = new AdminItemQuery(new TestDbContextFactory(_env), _env.Languages);

        await using var db = _env.CreateDbContext();
        _morocco = new Country { Name = En("Morocco") };
        _fossils = new Category { Name = En("Fossils"), Color = "#4CAF50" };
        _mineral = new ItemType { Name = En("Mineral") };
        var fossil = new ItemType { Name = En("Fossil") };
        var curiosity = new ItemType { Name = En("Curiosity") };
        db.Items.AddRange(
            // The admin search matches every language – the Czech translation is searchable without diacritics.
            new Item { Name = En("Amethyst geode").With("cs", "Ametystová geoda"), Type = _mineral, Visibility = ItemVisibility.Public, PurchasePrice = 15000 },
            new Item { Name = En("Polished ammonite"), Type = fossil, Country = _morocco, Categories = { _fossils }, PrivateNotes = "bought at the Munich show" },
            new Item { Name = En("Antique compass"), Type = curiosity, SalePrice = 500, Seller = "Auction house Vienna" });
        await db.SaveChangesAsync();
    }

    [TestCleanup]
    public async Task Cleanup() => await _env.DisposeAsync();

    [TestMethod]
    [DataRow("ametystova geo", "Amethyst geode", DisplayName = "translation without diacritics and case")]
    [DataRow("GEODE amethyst", "Amethyst geode", DisplayName = "words in any order")]
    [DataRow("morocco", "Polished ammonite", DisplayName = "by country")]
    [DataRow("fossils", "Polished ammonite", DisplayName = "by category")]
    [DataRow("munich", "Polished ammonite", DisplayName = "in private notes")]
    [DataRow("auction", "Antique compass", DisplayName = "by seller")]
    [DataRow("mineral", "Amethyst geode", DisplayName = "by type")]
    [DataRow("ammo show", "Polished ammonite", DisplayName = "word starts")]
    public async Task Search_FindsItem(string search, string expectedName)
    {
        var rows = await _query.GetItemsAsync(new AdminItemFilter { Search = search });

        CollectionAssert.AreEqual(new[] { expectedName }, rows.Select(r => r.Name).ToArray());
    }

    [TestMethod]
    public async Task Search_MatchesOnlyWordStarts()
    {
        // "enna" is only inside the word "Vienna" (the seller) – must not match.
        var rows = await _query.GetItemsAsync(new AdminItemFilter { Search = "enna" });

        Assert.IsEmpty(rows);
    }

    [TestMethod]
    public async Task Search_ById()
    {
        var all = await _query.GetItemsAsync(new AdminItemFilter());
        var target = all.Single(r => r.Name == "Antique compass");

        var rows = await _query.GetItemsAsync(new AdminItemFilter { Search = $"#{target.Id}" });

        Assert.AreEqual(target.Id, rows.Single().Id);
    }

    [TestMethod]
    public async Task Filters_AreCombined()
    {
        Assert.HasCount(1, await _query.GetItemsAsync(new AdminItemFilter { TypeId = _mineral.Id }));
        Assert.HasCount(1, await _query.GetItemsAsync(new AdminItemFilter { CountryId = _morocco.Id }));
        Assert.HasCount(1, await _query.GetItemsAsync(new AdminItemFilter { CategoryId = _fossils.Id }));
        Assert.HasCount(1, await _query.GetItemsAsync(new AdminItemFilter { Visibility = ItemVisibility.Public }));
        Assert.HasCount(1, await _query.GetItemsAsync(new AdminItemFilter { Sold = true }));
        Assert.HasCount(2, await _query.GetItemsAsync(new AdminItemFilter { Sold = false }));
        Assert.IsEmpty(await _query.GetItemsAsync(new AdminItemFilter { Sold = true, TypeId = _mineral.Id }));
        Assert.HasCount(3, await _query.GetItemsAsync(new AdminItemFilter { HasImages = false }));
        Assert.IsEmpty(await _query.GetItemsAsync(new AdminItemFilter { HasImages = true }));
    }

    [TestMethod]
    public async Task Rows_ContainDisplayData()
    {
        var row = (await _query.GetItemsAsync(new AdminItemFilter { CountryId = _morocco.Id })).Single();

        Assert.AreEqual("Morocco", row.CountryName);
        Assert.AreEqual("Fossil", row.TypeName);
        Assert.AreEqual("Fossils", row.Categories.Single().Name);
        Assert.AreEqual("#4CAF50", row.Categories.Single().Color);
        Assert.AreEqual(0, row.ImageCount);
        Assert.IsNull(row.ThumbnailKey);
        Assert.IsFalse(row.IsSold);
    }
}
