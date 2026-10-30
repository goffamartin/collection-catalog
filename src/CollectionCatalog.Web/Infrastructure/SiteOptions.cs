namespace CollectionCatalog.Web.Infrastructure;

/// <summary>
/// Configuration of the public website (section <c>Site</c>).
/// </summary>
public sealed class SiteOptions
{
    public const string SectionName = "Site";

    /// <summary>
    /// Public address of the website, e.g. <c>https://collection.example.com</c> – for canonical URLs, the sitemap and
    /// social previews. When missing, the address of the request is used.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>Time zone for displayed times and dates (IANA id, e.g. <c>Europe/Prague</c>); UTC when empty.</summary>
    public string? TimeZone { get; set; }

    /// <summary>Currency of prices (ISO 4217 code, e.g. <c>EUR</c>, <c>CZK</c>); EUR when empty.</summary>
    public string? Currency { get; set; }

    /// <summary>Author credit in the footer („Website by …“); hidden when empty.</summary>
    public string? AuthorName { get; set; }

    /// <summary>Link of the author credit.</summary>
    public string? AuthorUrl { get; set; }

    /// <summary>Absolute URL of a language-neutral resource (photo, sitemap) – path without the leading slash.</summary>
    public string Absolute(HttpContext? context, string relativePath) => Combine(Root(context), "", relativePath);

    /// <summary>Absolute URL of a page in the language of the request (keeps the language prefix, e.g. <c>/en</c>).</summary>
    public string Page(HttpContext? context, string relativePath) =>
        Combine(Root(context), context?.Request.PathBase.Value ?? "", relativePath);

    /// <summary>Absolute URL of a page in a given language – <paramref name="languagePrefix"/> is empty or <c>en/</c>.</summary>
    public string PageIn(HttpContext? context, string languagePrefix, string relativePath) =>
        Combine(Root(context), languagePrefix, relativePath);

    private string Root(HttpContext? context) =>
        string.IsNullOrWhiteSpace(BaseUrl)
            ? context is null ? "" : $"{context.Request.Scheme}://{context.Request.Host}"
            : BaseUrl;

    private static string Combine(string root, string prefix, string relativePath)
    {
        var path = string.Join('/', new[] { prefix.Trim('/'), relativePath.TrimStart('/') }.Where(p => p.Length > 0));
        return $"{root.TrimEnd('/')}/{path}";
    }
}
