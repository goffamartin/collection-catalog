namespace CollectionCatalog.Web.Infrastructure;

public static class SecurityHeaders
{
    /// <summary>
    /// Basic security headers for all responses. The CSP restricts only framing (clickjacking of the
    /// administration) – a full script CSP would be needlessly complicated with Blazor (inline import map, own JS).
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Content-Security-Policy"] = "frame-ancestors 'none'";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        await next(context);
    });
}
