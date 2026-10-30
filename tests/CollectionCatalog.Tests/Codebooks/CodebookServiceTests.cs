using static CollectionCatalog.Tests.Infrastructure.TestText;
using CollectionCatalog.Tests.Infrastructure;
using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Services.Codebooks;
using Microsoft.EntityFrameworkCore;

namespace CollectionCatalog.Tests.Codebooks;

[TestClass]
public sealed class CodebookServiceTests
{
    private static readonly LanguageContext English = new("en", "en");

    private TestEnvironment _env = null!;
    private CodebookService _service = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _env = await TestEnvironment.CreateAsync();
        _service = new CodebookService(new TestDbContextFactory(_env), _env.Languages, TestEnvironment.Text);
    }

    [TestCleanup]
    public async Task Cleanup() => await _env.DisposeAsync();

    [TestMethod]
    [DataRow(CodebookKind.ItemTypes)]
    [DataRow(CodebookKind.Categories)]
    [DataRow(CodebookKind.Countries)]
    public async Task AddUpdateDelete_Works(CodebookKind kind)
    {
        Assert.IsNull(await _service.AddAsync(kind, "  Test entry  ", kind == CodebookKind.Categories ? "#ff0000" : null));
        var added = (await _service.GetAsync(kind, English)).Single(e => e.Name == "Test entry");
        if (kind == CodebookKind.Categories)
        {
            Assert.AreEqual("#FF0000", added.Color);
        }

        Assert.IsNull(await _service.UpdateAsync(kind, added.Id, "en", "Renamed", "#00aa00"));
        Assert.IsTrue((await _service.GetAsync(kind, English)).Any(e => e.Id == added.Id && e.Name == "Renamed"));

        await _service.DeleteAsync(kind, added.Id);
        Assert.IsFalse((await _service.GetAsync(kind, English)).Any(e => e.Id == added.Id));
    }

    [TestMethod]
    public async Task Validation_RejectsEmptyDuplicateAndBadColor()
    {
        Assert.AreEqual("Enter a name.", await _service.AddAsync(CodebookKind.Countries, "   "));

        await _service.AddAsync(CodebookKind.Countries, "Brazil");
        StringAssert.Contains(await _service.AddAsync(CodebookKind.Countries, "BRAZIL"), "already exists");

        StringAssert.Contains(await _service.AddAsync(CodebookKind.Categories, "Crystals", "red"), "#RRGGBB");
        StringAssert.Contains(await _service.AddAsync(CodebookKind.Categories, new string('x', 101)), "at most");
    }

    [TestMethod]
    public async Task Update_AllowsKeepingSameName()
    {
        await _service.AddAsync(CodebookKind.Countries, "Poland");
        var poland = (await _service.GetAsync(CodebookKind.Countries, English)).Single();

        Assert.IsNull(await _service.UpdateAsync(CodebookKind.Countries, poland.Id, "en", "poland"));
    }

    [TestMethod]
    public async Task Get_CountsItems_AndSortsByTheRulesOfTheLanguage()
    {
        await using (var db = _env.CreateDbContext())
        {
            // Czech names: "Č" sorts after "C", "Ch" after "H" – in the Czech list.
            var crystals = new Category { Name = En("Crystals").With("cs", "Krystaly") };
            db.Categories.AddRange(crystals,
                new Category { Name = En("Czech minerals").With("cs", "České minerály") },
                new Category { Name = En("Customs seizures").With("cs", "Celní nálezy") },
                new Category { Name = En("Chalk fossils").With("cs", "Chalcedony") });
            db.Items.Add(new Item { Name = En("Specimen 1"), Categories = { crystals } });
            db.Items.Add(new Item { Name = En("Specimen 2"), Categories = { crystals } });
            await db.SaveChangesAsync();
        }

        var english = await _service.GetAsync(CodebookKind.Categories, English);
        CollectionAssert.AreEqual(new[] { "Chalk fossils", "Crystals", "Customs seizures", "Czech minerals" }, english.Select(e => e.Name).ToArray());
        Assert.AreEqual(2, english.Single(e => e.Name == "Crystals").ItemCount);

        var czech = await _service.GetAsync(CodebookKind.Categories, new LanguageContext("cs", "en"));
        CollectionAssert.AreEqual(new[] { "Celní nálezy", "České minerály", "Chalcedony", "Krystaly" }, czech.Select(e => e.Name).ToArray());
    }

    [TestMethod]
    public async Task Delete_KeepsItems()
    {
        int countryId, typeId, categoryId;
        await using (var db = _env.CreateDbContext())
        {
            var item = new Item { Name = En("Specimen"), Country = new Country { Name = En("Austria") }, Type = new ItemType { Name = En("Meteorite (test)") } };
            var category = new Category { Name = En("Meteorites") };
            item.Categories.Add(category);
            db.Items.Add(item);
            await db.SaveChangesAsync();
            (countryId, typeId, categoryId) = (item.CountryId!.Value, item.TypeId!.Value, category.Id);
        }

        await _service.DeleteAsync(CodebookKind.Countries, countryId);
        await _service.DeleteAsync(CodebookKind.ItemTypes, typeId);
        await _service.DeleteAsync(CodebookKind.Categories, categoryId);

        await using (var db = _env.CreateDbContext())
        {
            var item = await db.Items.Include(i => i.Categories).SingleAsync();
            Assert.IsNull(item.CountryId);
            Assert.IsNull(item.TypeId);
            Assert.IsEmpty(item.Categories);
        }
    }
}
