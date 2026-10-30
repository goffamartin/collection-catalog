using System.Globalization;
using CollectionCatalog.Web.Services.Languages;

namespace CollectionCatalog.Web.Infrastructure;

/// <summary>
/// Language from the URL: <c>/en/sbirka</c> → English, <c>/sbirka</c> → the default language.
/// </summary>
/// <remarks>
/// The language prefix moves into <see cref="HttpRequest.PathBase"/>, so routing, pages and endpoints work with the same
/// paths in every language, and every relative link (resolved against <c>&lt;base href&gt;</c>, see <c>App.razor</c>)
/// stays in the language automatically – including the interactive circuit (<c>/en/_blazor</c>).
/// Must run before routing.
/// </remarks>
public sealed class LanguagePathMiddleware(RequestDelegate next, LanguageService languages)
{
    public Task InvokeAsync(HttpContext context)
    {
        var language = languages.Default;
        var path = context.Request.Path.Value ?? "";
        var segmentEnd = path.IndexOf('/', 1);
        var segment = segmentEnd < 0 ? path.TrimStart('/') : path[1..segmentEnd];

        if (languages.Find(segment) is { IsDefault: false } prefixed && segment == prefixed.Code)
        {
            language = prefixed;
            context.Request.PathBase = context.Request.PathBase.Add($"/{prefixed.Code}");
            context.Request.Path = segmentEnd < 0 ? "/" : path[segmentEnd..];
        }

        var culture = CultureInfo.GetCultureInfo(language.Code);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        return next(context);
    }
}

public static class LanguagePathMiddlewareExtensions
{
    public static IApplicationBuilder UseLanguagePath(this IApplicationBuilder app) =>
        app.UseMiddleware<LanguagePathMiddleware>();
}
