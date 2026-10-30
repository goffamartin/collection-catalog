using System.Net;
using CollectionCatalog.Tests.Infrastructure;
using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Domain;
using Microsoft.Extensions.DependencyInjection;
using static CollectionCatalog.Tests.Infrastructure.TestText;

namespace CollectionCatalog.Tests.Localization;

/// <summary>Language prefix in URLs end-to-end: the root is the default language (English), /cs/… is Czech.</summary>
[TestClass]
public sealed class LanguageRoutingTests
{
    private static TestWebApp _app = null!;
    private static int _itemId;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        _app = new TestWebApp(new Dictionary<string, string> { ["Site:BaseUrl"] = "https://collection.example.com" });
        await using var scope = _app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = new Item { Name = En("Amethyst geode").With("cs", "Ametystová geoda"), Visibility = ItemVisibility.Public };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        _itemId = item.Id;
    }

    [ClassCleanup]
    public static void ClassCleanup() => _app.Dispose();

    [TestMethod]
    public async Task CzechPrefix_SetsLanguageBaseAndAlternates()
    {
        using var client = _app.CreateClient();

        // Blazor encodes non-ASCII characters in rendered text.
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/cs/sbirka"));

        StringAssert.Contains(html, "<html lang=\"cs\"");
        StringAssert.Contains(html, "<base href=\"/cs/\"");
        StringAssert.Contains(html, "rel=\"canonical\" href=\"https://collection.example.com/cs/sbirka\"");
        StringAssert.Contains(html, "hreflang=\"en\" href=\"https://collection.example.com/sbirka\"");
        StringAssert.Contains(html, "hreflang=\"x-default\" href=\"https://collection.example.com/sbirka\"");
        StringAssert.Contains(html, "Ametystová geoda");
    }

    [TestMethod]
    public async Task DefaultLanguage_HasNoPrefix()
    {
        using var client = _app.CreateClient();

        var html = await client.GetStringAsync("/sbirka");

        StringAssert.Contains(html, "<html lang=\"en\"");
        StringAssert.Contains(html, "<base href=\"/\"");
        StringAssert.Contains(html, "Amethyst geode");
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync("/en/sbirka")).StatusCode, "the default language has no prefix");
    }

    [TestMethod]
    public async Task Detail_WrongSlugInLanguage_RedirectsToTranslatedSlug()
    {
        using var client = _app.CreateClient();

        var response = await client.GetAsync($"/cs/sbirka/{_itemId}/amethyst-geode");

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        StringAssert.EndsWith(response.Headers.Location?.AbsolutePath, $"/cs/sbirka/{_itemId}/ametystova-geoda");
    }

    [TestMethod]
    public async Task Admin_UnderPrefix_RedirectsToLoginInTheSameLanguage()
    {
        using var client = _app.CreateClient();

        var response = await client.GetAsync("/cs/admin");

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        StringAssert.StartsWith(response.Headers.Location?.PathAndQuery, "/cs/prihlaseni");
        Assert.AreEqual("noindex, nofollow", response.Headers.GetValues("X-Robots-Tag").Single(), "private pages stay noindex under the prefix");
    }

    [TestMethod]
    public async Task Sitemap_ListsEveryLanguage()
    {
        using var client = _app.CreateClient();

        var xml = await client.GetStringAsync("/sitemap.xml");

        StringAssert.Contains(xml, $"<loc>https://collection.example.com/sbirka/{_itemId}/amethyst-geode</loc>");
        StringAssert.Contains(xml, $"<loc>https://collection.example.com/cs/sbirka/{_itemId}/ametystova-geoda</loc>");
        StringAssert.Contains(xml, "hreflang=\"cs\"");
    }

    [TestMethod]
    public async Task Switcher_LinksToTheSamePageInOtherLanguage()
    {
        using var client = _app.CreateClient();

        var html = await client.GetStringAsync("/cs/kontakt");

        StringAssert.Contains(html, "href=\"/kontakt\" hreflang=\"en\"");
        StringAssert.Contains(html, "data-enhance-nav=\"false\"");
    }
}
