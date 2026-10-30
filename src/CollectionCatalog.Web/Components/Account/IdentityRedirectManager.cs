using CollectionCatalog.Web.Infrastructure;
using Microsoft.AspNetCore.Components;

namespace CollectionCatalog.Web.Components.Account;

/// <summary>
/// Redirects from the static (SSR) account pages, optionally with a status message in a short-lived cookie.
/// </summary>
internal sealed class IdentityRedirectManager(NavigationManager navigationManager)
{
    public const string StatusCookieName = "Identity.StatusMessage";

    private static readonly CookieBuilder _statusCookieBuilder = new()
    {
        SameSite = SameSiteMode.Strict,
        HttpOnly = true,
        IsEssential = true,
        MaxAge = TimeSpan.FromSeconds(5),
    };

    /// <summary>Redirects within the application; an address of another website is replaced by <paramref name="fallback"/>.</summary>
    public void RedirectTo(string? uri, string fallback = "")
    {
        // An absolute address of this website becomes relative; anything outside the application is rejected (open redirect).
        if (uri is not null && uri.StartsWith(navigationManager.BaseUri, StringComparison.OrdinalIgnoreCase))
        {
            uri = navigationManager.ToBaseRelativePath(uri);
        }

        navigationManager.NavigateTo(uri == "" || LocalUrl.IsLocal(uri) ? uri! : fallback);
    }

    public void RedirectToWithStatus(string uri, string message, HttpContext context)
    {
        context.Response.Cookies.Append(StatusCookieName, message, _statusCookieBuilder.Build(context));
        RedirectTo(uri);
    }

    public void RedirectToCurrentPageWithStatus(string message, HttpContext context) =>
        RedirectToWithStatus(navigationManager.ToAbsoluteUri(navigationManager.Uri).GetLeftPart(UriPartial.Path), message, context);
}
