using CollectionCatalog.Tests.Infrastructure;
using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Services.Site;
using Microsoft.EntityFrameworkCore;

namespace CollectionCatalog.Tests.Public;

[TestClass]
public sealed class SiteSettingsServiceTests
{
    private TestEnvironment _env = null!;

    [TestInitialize]
    public async Task Initialize() => _env = await TestEnvironment.CreateAsync();

    [TestCleanup]
    public async Task Cleanup() => await _env.DisposeAsync();

    [TestMethod]
    public async Task Get_ReturnsSeededDefaults()
    {
        var site = await new SiteSettingsService(new TestDbContextFactory(_env), _env.Languages).GetAsync();

        Assert.AreEqual("Collection", site.SiteName);
        Assert.AreEqual("A private collection", site.Headline);
        Assert.IsFalse(site.HasContact);
    }

    [TestMethod]
    public async Task Save_TrimsEmptyValues_AndRefreshesCache()
    {
        var service = new SiteSettingsService(new TestDbContextFactory(_env), _env.Languages);
        var model = await service.GetForEditAsync();
        model.SiteName = "  The Smith family collection ";
        model.ContactEmail = "collection@example.com";
        model.About = "   ";

        await service.SaveAsync(model);

        var cached = await service.GetAsync();
        Assert.AreEqual("The Smith family collection", cached.SiteName);
        Assert.AreEqual("collection@example.com", cached.ContactEmail);
        Assert.IsNull(cached.About);
        Assert.IsTrue(cached.HasContact);

        // A new instance reads from the database.
        var fresh = await new SiteSettingsService(new TestDbContextFactory(_env), _env.Languages).GetAsync();
        Assert.AreEqual(cached, fresh);
    }

    [TestMethod]
    public void Model_EmptyEmail_IsValid()
    {
        var model = new SiteSettingsModel { SiteName = "Collection", Headline = "Headline", ContactEmail = "" };

        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(model, new(model), results, validateAllProperties: true);

        Assert.IsTrue(valid, string.Join(", ", results.Select(r => r.ErrorMessage)));
        Assert.IsNull(model.ContactEmail);
    }
}
