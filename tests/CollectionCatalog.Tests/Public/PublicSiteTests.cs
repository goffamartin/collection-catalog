using System.Net;
using System.Text.RegularExpressions;
using CollectionCatalog.Tests.Infrastructure;
using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Services.Images;
using Microsoft.Extensions.DependencyInjection;
using static CollectionCatalog.Tests.Infrastructure.TestText;

namespace CollectionCatalog.Tests.Public;

/// <summary>
/// Public pages and endpoints end-to-end over HTTP – above all, nothing private may leak out.
/// </summary>
[TestClass]
public sealed partial class PublicSiteTests
{
    private static TestWebApp _app = null!;
    private static Item _public = null!;
    private static Item _private = null!;
    private static Listing _offer = null!;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        _app = new TestWebApp(new Dictionary<string, string> { ["Site:BaseUrl"] = "https://collection.example.com" });

        await using var scope = _app.Services.CreateAsyncScope();
        var imageStore = scope.ServiceProvider.GetRequiredService<ImageStore>();
        var publicImage = await imageStore.ProcessAsync(new MemoryStream(TestImages.SplitJpeg(400, 300)));
        var privateImage = await imageStore.ProcessAsync(new MemoryStream(TestImages.SplitJpeg(300, 400)));
        await imageStore.WriteAsync(publicImage);
        await imageStore.WriteAsync(privateImage);

        _public = new Item
        {
            Name = En("Amethyst geode"),
            Visibility = ItemVisibility.Public,
            PublicDescription = En("The largest specimen of the collection."),
            PurchasePrice = 12345,
            Seller = "Secret seller",
            PrivateNotes = "Private note",
            Images =
            {
                new ItemImage { FileKey = publicImage.FileKey, Width = 400, Height = 300 },
                new ItemImage { FileKey = privateImage.FileKey, Width = 300, Height = 400, IsPublic = false, SortOrder = 1 },
            },
        };
        _private = new Item { Name = En("Private piece"), Visibility = ItemVisibility.Private };
        // A private duplicate on offer – on the website only in the Offer section.
        _offer = new Listing
        {
            Item = new Item { Name = En("Offered duplicate"), PrivateNotes = "Private note", PurchasePrice = 12345 },
            Status = ListingStatus.Active,
            AskingPrice = 4500,
            AcceptsOffers = true,
        };

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Items.AddRange(_public, _private);
        db.Listings.Add(_offer);
        await db.SaveChangesAsync();
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _app.Dispose();
    }

    [TestMethod]
    [DataRow("/")]
    [DataRow("/sbirka")]
    [DataRow("/sbirka?typ=abc&strana=-1&hledat=geode")]
    [DataRow("/cs/sbirka")]
    [DataRow("/nabidka")]
    [DataRow("/ochrana-osobnich-udaju")]
    [DataRow("/o-sbirce")]
    [DataRow("/kontakt")]
    public async Task Pages_RenderWithoutSensitiveData(string path)
    {
        using var client = _app.CreateClient();

        var response = await client.GetAsync(path);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, path);
        AssertNoSensitiveData(await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task Detail_ShowsPublicDataOnly()
    {
        using var client = _app.CreateClient();

        var response = await client.GetAsync($"/sbirka/{_public.Id}/amethyst-geode");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        StringAssert.Contains(html, "The largest specimen of the collection.");
        StringAssert.Contains(html, $"<link rel=\"canonical\" href=\"https://collection.example.com/sbirka/{_public.Id}/amethyst-geode\"");
        StringAssert.Contains(html, $"https://collection.example.com/media/{_public.Images.First().FileKey}/jpg");
        Assert.DoesNotContain(_public.Images.Last().FileKey, html, "private photo");
        AssertNoSensitiveData(html);
    }

    [TestMethod]
    public async Task Detail_WrongSlug_RedirectsToCanonicalUrl()
    {
        using var client = _app.CreateClient();

        var response = await client.GetAsync($"/sbirka/{_public.Id}/old-name");

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.AreEqual($"/sbirka/{_public.Id}/amethyst-geode", response.Headers.Location?.AbsolutePath);
    }

    [TestMethod]
    public async Task Detail_PrivateItem_IsNotFound()
    {
        using var client = _app.CreateClient();

        var response = await client.GetAsync($"/sbirka/{_private.Id}/private-piece");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("Private piece", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [TestMethod]
    public async Task Media_ServesOnlyPublicImages()
    {
        using var client = _app.CreateClient();
        var publicKey = _public.Images.First().FileKey;
        var privateKey = _public.Images.Last().FileKey;

        var image = await client.GetAsync($"/media/{publicKey}/400");
        Assert.AreEqual(HttpStatusCode.OK, image.StatusCode);
        Assert.AreEqual("image/webp", image.Content.Headers.ContentType?.MediaType);
        Assert.IsTrue(image.Headers.CacheControl?.Public);
        Assert.AreEqual(TimeSpan.FromDays(1), image.Headers.CacheControl?.SharedMaxAge, "shared caches keep a photo for a day only – a hidden photo disappears soon");

        Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync($"/media/{publicKey}/jpg")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/media/{privateKey}/400")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/media/{privateKey}/jpg")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/media/{publicKey}/123")).StatusCode);
    }

    [TestMethod]
    public async Task Sitemap_And_Robots()
    {
        using var client = _app.CreateClient();

        var sitemap = await client.GetStringAsync("/sitemap.xml");
        StringAssert.Contains(sitemap, $"<loc>https://collection.example.com/sbirka/{_public.Id}/amethyst-geode</loc>");
        Assert.DoesNotContain("private-piece", sitemap);

        // Public photos as the image extension, private ones (certificate) not.
        StringAssert.Contains(sitemap, $"<image:loc>https://collection.example.com/media/{_public.Images.First().FileKey}/1600</image:loc>");
        Assert.DoesNotContain(_public.Images.Last().FileKey, sitemap);
        _ = System.Xml.Linq.XDocument.Parse(sitemap); // valid XML

        var robots = await client.GetStringAsync("/robots.txt");
        StringAssert.Contains(robots, "Disallow: /admin");
        StringAssert.Contains(robots, "Disallow: /prihlaseni");
        StringAssert.Contains(robots, "Sitemap: https://collection.example.com/sitemap.xml");
    }

    [TestMethod]
    public async Task StructuredData_IsValidJsonLd()
    {
        using var client = _app.CreateClient();

        var home = JsonLd(await client.GetStringAsync("/"));
        Assert.AreEqual("WebSite", home.Single().GetProperty("@type").GetString());
        Assert.AreEqual("https://collection.example.com/", home.Single().GetProperty("url").GetString());

        var detail = JsonLd(await client.GetStringAsync($"/sbirka/{_public.Id}/amethyst-geode"));
        var crumbs = detail.Single(d => d.GetProperty("@type").GetString() == "BreadcrumbList").GetProperty("itemListElement");
        Assert.AreEqual("https://collection.example.com/sbirka", crumbs[0].GetProperty("item").GetString());
        var current = crumbs[crumbs.GetArrayLength() - 1];
        Assert.AreEqual("Amethyst geode", current.GetProperty("name").GetString());
        Assert.IsFalse(current.TryGetProperty("item", out _), "the current page has no URL, not null");

        var offer = JsonLd(await client.GetStringAsync($"/nabidka/{_offer.Id}/offered-duplicate"));
        var product = offer.Single(d => d.GetProperty("@type").GetString() == "Product");
        Assert.AreEqual("4500", product.GetProperty("offers").GetProperty("price").GetString());
        Assert.AreEqual("EUR", product.GetProperty("offers").GetProperty("priceCurrency").GetString());
        AssertNoSensitiveData(string.Join("\n", offer.Select(o => o.GetRawText())));
    }

    [TestMethod]
    [DataRow("/")]
    [DataRow("/prihlaseni")]
    [DataRow("/sitemap.xml")]
    public async Task Responses_HaveSecurityHeaders(string path)
    {
        using var client = _app.CreateClient();

        var response = await client.GetAsync(path);

        Assert.AreEqual("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single(), path);
        Assert.AreEqual("DENY", response.Headers.GetValues("X-Frame-Options").Single(), path);
        Assert.AreEqual("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single(), path);
        Assert.AreEqual("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single(), path);
    }

    [TestMethod]
    public async Task Icons_Manifest_And_NoIndexForPrivatePages()
    {
        using var client = _app.CreateClient();

        foreach (var path in new[] { "/favicon.ico", "/favicon.svg", "/apple-touch-icon.png", "/icon-192.png", "/icon-512.png", "/icon-maskable-512.png" })
        {
            Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode, path);
        }

        var manifest = await client.GetAsync("/site.webmanifest");
        Assert.AreEqual("application/manifest+json", manifest.Content.Headers.ContentType?.MediaType);
        using (var json = System.Text.Json.JsonDocument.Parse(await manifest.Content.ReadAsStringAsync()))
        {
            Assert.AreEqual("Collection", json.RootElement.GetProperty("name").GetString());
            Assert.AreEqual("en", json.RootElement.GetProperty("lang").GetString());
        }

        Assert.AreEqual("noindex, nofollow", (await client.GetAsync("/prihlaseni")).Headers.GetValues("X-Robots-Tag").Single());
        Assert.AreEqual("noindex, nofollow", (await client.GetAsync("/admin")).Headers.GetValues("X-Robots-Tag").Single());
        Assert.IsFalse((await client.GetAsync("/sbirka")).Headers.Contains("X-Robots-Tag"));
    }

    [TestMethod]
    public async Task OnlyCookie_IsSecureAntiforgery_AndNoAuthorCreditByDefault()
    {
        using var client = _app.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/kontakt");
        request.Headers.Add("X-Forwarded-Proto", "https"); // as behind Cloudflare Tunnel

        var response = await client.SendAsync(request);

        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        Assert.HasCount(1, cookies);
        StringAssert.StartsWith(cookies[0], ".AspNetCore.Antiforgery.");
        StringAssert.Contains(cookies[0].ToLowerInvariant(), "secure");
        Assert.DoesNotContain("Website by", await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task Footer_ShowsConfiguredAuthorCredit()
    {
        using var app = new TestWebApp(new Dictionary<string, string>
        {
            ["Site:AuthorName"] = "Jane Developer",
            ["Site:AuthorUrl"] = "https://jane.example.com",
        });
        using var client = app.CreateClient();

        var html = await client.GetStringAsync("/kontakt");

        StringAssert.Contains(html, "Website by");
        StringAssert.Contains(html, "<a href=\"https://jane.example.com\" target=\"_blank\" rel=\"author noopener\"");
        StringAssert.Contains(html, "Jane Developer");
    }

    private static List<System.Text.Json.JsonElement> JsonLd(string html) =>
        JsonLdRegex().Matches(html)
            .Select(m => System.Text.Json.JsonDocument.Parse(m.Groups["json"].Value).RootElement.Clone())
            .ToList();

    [GeneratedRegex("""<script type="application/ld\+json">(?<json>.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex JsonLdRegex();

    [TestMethod]
    public async Task Offer_ListAndDetail_ShowPrice()
    {
        using var client = _app.CreateClient();

        var list = WebUtility.HtmlDecode(await client.GetStringAsync("/nabidka"));
        StringAssert.Contains(list, "Offered duplicate");
        StringAssert.Contains(list, "€4,500");

        // Czech formatting separates thousands with a no-break space.
        var czech = WebUtility.HtmlDecode(await client.GetStringAsync("/cs/nabidka"));
        StringAssert.Contains(czech.Replace('\u00a0', ' '), "4 500 €");

        var response = await client.GetAsync($"/nabidka/{_offer.Id}/offered-duplicate");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        StringAssert.Contains(html, "Your price offer");
        AssertNoSensitiveData(html);

        // A private item on offer has no detail in the collection.
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/sbirka/{_offer.ItemId}/offered-duplicate")).StatusCode);
    }

    [TestMethod]
    public async Task InquiryForm_PostsAndRedirectsToThanks()
    {
        using var client = _app.CreateClient(cookies: true);
        var path = $"/nabidka/{_offer.Id}/offered-duplicate";

        var response = await PostInquiryAsync(client, path, new() { ["Form.Message"] = "I am interested in the duplicate.", ["Form.OfferedPrice"] = "4000" });

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode, await response.Content.ReadAsStringAsync());
        Assert.AreEqual("?odeslano=1", response.Headers.Location?.Query);
        var thanks = WebUtility.HtmlDecode(await client.GetStringAsync(response.Headers.Location));
        StringAssert.Contains(thanks, "Thank you, the message has been sent");

        await using var scope = _app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inquiry = db.Inquiries.Single(i => i.Message == "I am interested in the duplicate.");
        Assert.AreEqual(_offer.Id, inquiry.ListingId);
        Assert.AreEqual(4000m, inquiry.OfferedPrice);
    }

    [TestMethod]
    public async Task InquiryForm_WithoutConsent_ShowsValidation()
    {
        using var client = _app.CreateClient(cookies: true);

        var response = await PostInquiryAsync(client, "/kontakt", new() { ["Form.Message"] = "Without consent", ["Form.Consent"] = "false" });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()), "Consent with the processing of the data is required");
        await using var scope = _app.Services.CreateAsyncScope();
        Assert.IsFalse(scope.ServiceProvider.GetRequiredService<AppDbContext>().Inquiries.Any(i => i.Message == "Without consent"));
    }

    [TestMethod]
    public async Task InquiryForm_Honeypot_PretendsSuccessButStoresNothing()
    {
        using var client = _app.CreateClient(cookies: true);

        var response = await PostInquiryAsync(client, "/kontakt", new() { ["Form.Message"] = "Spam", ["Form.Website"] = "http://spam.example" });

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        await using var scope = _app.Services.CreateAsyncScope();
        Assert.IsFalse(scope.ServiceProvider.GetRequiredService<AppDbContext>().Inquiries.Any(i => i.Message == "Spam"));
    }

    private static async Task<HttpResponseMessage> PostInquiryAsync(HttpClient client, string path, Dictionary<string, string> fields)
    {
        var page = await client.GetStringAsync(path);
        var form = HiddenInputRegex().Matches(page).ToDictionary(m => m.Groups["name"].Value, m => WebUtility.HtmlDecode(m.Groups["value"].Value));
        form["Form.Name"] = "Jane Doe";
        form["Form.Email"] = "jane@example.com";
        form["Form.Consent"] = "true";
        foreach (var (key, value) in fields)
        {
            form[key] = value;
        }
        return await client.PostAsync(path, new FormUrlEncodedContent(form));
    }

    [GeneratedRegex("""<input[^>]*type="hidden"[^>]*name="(?<name>[^"]+)"[^>]*value="(?<value>[^"]*)""")]
    private static partial Regex HiddenInputRegex();

    private static void AssertNoSensitiveData(string html)
    {
        html = WebUtility.HtmlDecode(html);
        foreach (var secret in new[] { "Secret seller", "Private note", "12345", "12,345", "12 345", "Private piece" })
        {
            Assert.DoesNotContain(secret, html, secret);
        }
    }

}
