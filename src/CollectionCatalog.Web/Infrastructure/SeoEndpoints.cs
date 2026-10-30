using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Services.Languages;
using CollectionCatalog.Web.Services.Public;
using CollectionCatalog.Web.Services.Site;

namespace CollectionCatalog.Web.Infrastructure;

public static class SeoEndpoints
{
    private static readonly XNamespace _sitemap = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private static readonly XNamespace _image = "http://www.google.com/schemas/sitemap-image/1.1";
    private static readonly XNamespace _xhtml = "http://www.w3.org/1999/xhtml";

    private static readonly string[] _privatePaths = ["/admin", "/prihlaseni", "/ucet"];

    /// <summary>
    /// The administration and sign-in must not appear in search engines even when something links to them
    /// (robots.txt only forbids crawling, not indexing of the address).
    /// </summary>
    public static IApplicationBuilder UsePrivatePagesNoIndex(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        if (_privatePaths.Any(path => context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        }
        await next(context);
    });

    public static IEndpointRouteBuilder MapSeoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/robots.txt", (SiteOptions site, LanguageService languages, HttpContext context) =>
        {
            var robots = new StringBuilder().AppendLine("User-agent: *");
            // Private pages exist under every language prefix (/admin, /en/admin…).
            foreach (var language in languages.All)
            {
                foreach (var path in _privatePaths)
                {
                    robots.AppendLine(CultureInfo.InvariantCulture, $"Disallow: /{languages.PathPrefix(language.Code)}{path.TrimStart('/')}");
                }
            }
            robots.AppendLine(CultureInfo.InvariantCulture, $"Sitemap: {site.Absolute(context, "sitemap.xml")}");
            return Results.Text(robots.ToString(), "text/plain", Encoding.UTF8);
        });

        endpoints.MapGet("/sitemap.xml", async (PublicItemQuery query, SiteOptions site, LanguageService languages, HttpContext context) =>
        {
            var entries = await query.GetSitemapEntriesAsync(context.RequestAborted);
            var offers = await query.GetOffersAsync(context.RequestAborted);

            // Every page once per language, each listing all its language versions (hreflang) – Google then
            // shows visitors the version in their language. Photos help image search (collectors search by look).
            IEnumerable<XElement> Urls(Func<LanguageContext, string> path, DateTime? modified = null, IEnumerable<string>? imageKeys = null)
            {
                var versions = languages.All
                    .Select(l => (l.Code, Url: site.PageIn(context, languages.PathPrefix(l.Code), path(new LanguageContext(l.Code, languages.Default.Code)))))
                    .ToList();
                return versions.Select(version => new XElement(_sitemap + "url",
                    new XElement(_sitemap + "loc", version.Url),
                    modified is { } m ? new XElement(_sitemap + "lastmod", m.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) : null,
                    versions.Count > 1
                        ? versions.Select(alternate => new XElement(_xhtml + "link",
                            new XAttribute("rel", "alternate"), new XAttribute("hreflang", alternate.Code), new XAttribute("href", alternate.Url)))
                        : null,
                    imageKeys?.Select(key => new XElement(_image + "image",
                        new XElement(_image + "loc", site.Absolute(context, PublicUrls.Image(key, 1600)))))));
            }

            IEnumerable<XElement> Page(string path) => Urls(_ => path);

            var urlset = new XElement(_sitemap + "urlset",
                new XAttribute(XNamespace.Xmlns + "image", _image),
                new XAttribute(XNamespace.Xmlns + "xhtml", _xhtml),
                Page(""),
                Page(PublicUrls.Collection),
                Page(PublicUrls.Offers),
                Page("o-sbirce"),
                Page("kontakt"),
                Page("ochrana-osobnich-udaju"),
                entries.Select(e => Urls(language => PublicUrls.Item(e.Id, e.Name.Get(language) ?? ""), e.ModifiedAt, e.ImageKeys)),
                offers.Select(o => Urls(language => PublicUrls.Offer(o.ListingId, o.Item.AllNames.Get(language) ?? ""),
                    imageKeys: o.Item.Image is { } image ? [image.FileKey] : null)));

            return Results.Text("<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + urlset.ToString(SaveOptions.DisableFormatting), "application/xml", Encoding.UTF8);
        });

        // Manifest for "Add to home screen" – named by the website settings.
        endpoints.MapGet("/site.webmanifest", async (SiteSettingsService settings, LanguageService languages, CancellationToken cancellationToken) =>
        {
            var site = await settings.GetAsync(cancellationToken);
            var manifest = new
            {
                name = site.SiteName,
                short_name = site.SiteName,
                description = site.Intro,
                lang = languages.Current.Code,
                start_url = "/",
                display = "standalone",
                background_color = "#f7f5f0",
                theme_color = "#1c1917",
                icons = new object[]
                {
                    new { src = "/icon-192.png", sizes = "192x192", type = "image/png" },
                    new { src = "/icon-512.png", sizes = "512x512", type = "image/png" },
                    new { src = "/icon-maskable-512.png", sizes = "512x512", type = "image/png", purpose = "maskable" },
                },
            };
            return Results.Text(JsonSerializer.Serialize(manifest), "application/manifest+json", Encoding.UTF8);
        });

        return endpoints;
    }
}
