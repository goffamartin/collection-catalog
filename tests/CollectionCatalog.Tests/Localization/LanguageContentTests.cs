using System.Globalization;
using CollectionCatalog.Tests.Infrastructure;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Services.Codebooks;
using CollectionCatalog.Web.Services.Items;
using CollectionCatalog.Web.Services.Public;
using Microsoft.EntityFrameworkCore;
using static CollectionCatalog.Tests.Infrastructure.TestText;

namespace CollectionCatalog.Tests.Localization;

/// <summary>Languages codebook and translated content in services.</summary>
[TestClass]
public sealed class LanguageContentTests
{
    private TestEnvironment _env = null!;

    [TestInitialize]
    public async Task Initialize() => _env = await TestEnvironment.CreateAsync();

    [TestCleanup]
    public async Task Cleanup() => await _env.DisposeAsync();

    [TestMethod]
    public void Languages_AreSeeded_EnglishDefault()
    {
        Assert.AreEqual("en", _env.Languages.Default.Code);
        CollectionAssert.AreEqual(new[] { "en", "cs" }, _env.Languages.All.Select(l => l.Code).ToArray());
        Assert.AreEqual("", _env.Languages.PathPrefix("en"));
        Assert.AreEqual("cs/", _env.Languages.PathPrefix("cs"));
    }

    [TestMethod]
    public async Task Languages_AddValidates_DefaultCannotBeDeleted()
    {
        Assert.IsNotNull(await _env.Languages.AddAsync("xx1", "Nope"), "code must be two letters");
        Assert.IsNotNull(await _env.Languages.AddAsync("cs", "Czech again"), "duplicate");
        Assert.IsNull(await _env.Languages.AddAsync("DE", "Deutsch"));
        Assert.AreEqual("de", _env.Languages.All.Last().Code, "code is normalized, new language goes last");

        Assert.IsNotNull(await _env.Languages.DeleteAsync(_env.Languages.Default.Id));
        Assert.IsNull(await _env.Languages.DeleteAsync(_env.Languages.Find("de")!.Id));
        Assert.IsNull(_env.Languages.Find("de"));
    }

    [TestMethod]
    public async Task Languages_SetDefault_MovesFallback()
    {
        await _env.Languages.SetDefaultAsync(_env.Languages.Find("cs")!.Id);

        Assert.AreEqual("cs", _env.Languages.Default.Code);
        Assert.AreEqual("en/", _env.Languages.PathPrefix("en"));
        Assert.AreEqual(1, _env.Languages.All.Count(l => l.IsDefault));
    }

    [TestMethod]
    public async Task Codebook_Translation_IsOptional_AndFallsBack()
    {
        var codebooks = new CodebookService(new TestDbContextFactory(_env), _env.Languages, TestEnvironment.Text);
        Assert.IsNull(await codebooks.AddAsync(CodebookKind.Countries, "Austria"));
        var id = (await codebooks.GetAsync(CodebookKind.Countries, new LanguageContext("en", "en"))).Single().Id;

        var untranslated = (await codebooks.GetAsync(CodebookKind.Countries, new LanguageContext("cs", "en"))).Single();
        Assert.AreEqual("Austria", untranslated.Name, "falls back to English");
        Assert.IsNull(untranslated.Translation);

        Assert.IsNull(await codebooks.UpdateAsync(CodebookKind.Countries, id, "cs", "Rakousko"));
        Assert.AreEqual("Rakousko", (await codebooks.GetAsync(CodebookKind.Countries, new LanguageContext("cs", "en"))).Single().Name);

        Assert.IsNull(await codebooks.UpdateAsync(CodebookKind.Countries, id, "cs", ""), "an empty translation just removes it");
        Assert.IsNotNull(await codebooks.UpdateAsync(CodebookKind.Countries, id, "en", ""), "the default language is required");
    }

    [TestMethod]
    public void ItemEditModel_RequiresNameOnlyInDefaultLanguage()
    {
        var model = new ItemEditModel();
        model.SetLanguages(new LanguageContext("en", "en"));
        model.EditLanguage = "cs";
        model.Name = "Ametystová geoda";

        Assert.IsTrue(model.Validate(new(model)).Any(r => r.MemberNames.Contains(nameof(ItemEditModel.Name))));

        model.EditLanguage = "en";
        model.Name = "Amethyst geode";
        Assert.IsFalse(model.Validate(new(model)).Any());
        Assert.AreEqual("Ametystová geoda", model.NameText["cs"], "switching the language keeps the other translation");
    }

    [TestMethod]
    public async Task PublicQuery_ShowsTranslation_OrEnglishFallback()
    {
        await using (var db = _env.CreateDbContext())
        {
            db.Items.Add(new Item { Name = En("Amethyst geode").With("cs", "Ametystová geoda"), Visibility = ItemVisibility.Public, PublicDescription = En("Description") });
            db.Items.Add(new Item { Name = En("Ammonite"), Visibility = ItemVisibility.Public });
            await db.SaveChangesAsync();
        }
        var query = new PublicItemQuery(new TestDbContextFactory(_env), _env.Languages);

        var original = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("cs");
        try
        {
            var page = await query.SearchAsync(new PublicItemFilter());
            CollectionAssert.AreEquivalent(new[] { "Ametystová geoda", "Ammonite" }, page.Items.Select(i => i.Name).ToArray());

            var detail = await query.GetDetailAsync(page.Items.Single(i => i.Name == "Ametystová geoda").Id);
            Assert.AreEqual("Description", detail!.PublicDescription, "untranslated description falls back to English");
            StringAssert.EndsWith(detail.Url, "/ametystova-geoda", "the slug comes from the name in the language");
        }
        finally
        {
            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public async Task Database_StoresTextsAsJson_EmptyAsBraces()
    {
        await using (var db = _env.CreateDbContext())
        {
            db.Items.Add(new Item { Name = Cs("Ametystová geoda") });
            await db.SaveChangesAsync();
        }

        await using var check = _env.CreateDbContext();
        var raw = await check.Database.SqlQueryRaw<string>("SELECT \"Name\" || ' ' || \"PublicDescription\" AS \"Value\" FROM \"Items\"").SingleAsync();
        Assert.AreEqual("{\"cs\":\"Ametystová geoda\"} {}", raw, "Czech letters stay readable in the JSON");
    }
}
