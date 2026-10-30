namespace CollectionCatalog.Web.Infrastructure;

/// <summary>
/// Protection against open redirects (e.g. <c>?ReturnUrl=//evil.com</c> after sign-in).
/// Beware: <see cref="Uri.IsWellFormedUriString(string, UriKind)"/> with <see cref="UriKind.Relative"/> considers
/// "//evil.com" a relative address – browsers treat it as an address of another website.
/// </summary>
public static class LocalUrl
{
    /// <summary>
    /// Address within the application: a path from the root ("/admin") or a relative path without a scheme ("admin").
    /// Rejects addresses of other websites (<c>//host</c>, <c>/\host</c>, <c>https:</c>, <c>javascript:</c>) and control characters.
    /// </summary>
    public static bool IsLocal(string? url)
    {
        if (string.IsNullOrEmpty(url) || url.Any(c => char.IsControl(c) || c == '\\'))
        {
            return false;
        }

        if (url[0] == '/')
        {
            return url.Length == 1 || url[1] != '/';
        }

        // A relative path must not start with a scheme ("https:", "javascript:"…) – a colon may only follow the first "/", "?" or "#".
        var colon = url.IndexOf(':');
        var firstDelimiter = url.IndexOfAny(['/', '?', '#']);
        return colon < 0 || (firstDelimiter >= 0 && firstDelimiter < colon);
    }
}
