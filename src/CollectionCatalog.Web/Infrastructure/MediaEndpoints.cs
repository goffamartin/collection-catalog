using CollectionCatalog.Web.Services.Images;
using CollectionCatalog.Web.Services.Public;
using Microsoft.Net.Http.Headers;

namespace CollectionCatalog.Web.Infrastructure;

public static class MediaEndpoints
{
    private const string FileKeyPattern = "regex(^[0-9a-f]{{32}}$)";

    // Browsers may keep a photo for a year (the URL is a hash of the content), shared caches (Cloudflare) only a day –
    // a photo hidden from the website disappears from the cache by the next day.
    internal const string PublicCacheControl = "public, max-age=31536000, s-maxage=86400, immutable";

    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Photos for the administration (all of them, including non-public ones) – signed-in users only.
        endpoints.MapGet($"/admin/media/{{fileKey:{FileKeyPattern}}}/{{size:int}}", (string fileKey, int size, ImageStore imageStore, HttpContext context) =>
        {
            // Files are immutable (named by the hash of the content); "private" – shared caches (Cloudflare) must not store non-public photos.
            return Variant(imageStore, fileKey, size, context, "private, max-age=31536000, immutable");
        })
        .RequireAuthorization();

        // Public photos: only public photos of public items. Shared caching is fine – the URL is a hash of the content,
        // known only to someone who saw the photo on the website.
        endpoints.MapGet($"/media/{{fileKey:{FileKeyPattern}}}/{{size:int}}", async (string fileKey, int size, ImageStore imageStore, PublicItemQuery query, HttpContext context) =>
        {
            if (!await query.IsImagePublicAsync(fileKey, context.RequestAborted))
            {
                return Results.NotFound();
            }
            return Variant(imageStore, fileKey, size, context, PublicCacheControl);
        });

        // Full JPEG for social media previews.
        endpoints.MapGet($"/media/{{fileKey:{FileKeyPattern}}}/jpg", async (string fileKey, ImageStore imageStore, PublicItemQuery query, HttpContext context) =>
        {
            var path = imageStore.GetMasterPath(fileKey);
            if (!File.Exists(path) || !await query.IsImagePublicAsync(fileKey, context.RequestAborted))
            {
                return Results.NotFound();
            }
            context.Response.Headers[HeaderNames.CacheControl] = PublicCacheControl;
            return Results.File(path, "image/jpeg");
        });

        return endpoints;
    }

    private static IResult Variant(ImageStore imageStore, string fileKey, int size, HttpContext context, string cacheControl)
    {
        if (!ImageStore.VariantSizes.Contains(size))
        {
            return Results.NotFound();
        }

        var path = imageStore.GetVariantPath(fileKey, size);
        if (!File.Exists(path))
        {
            return Results.NotFound();
        }

        context.Response.Headers[HeaderNames.CacheControl] = cacheControl;
        return Results.File(path, "image/webp");
    }
}
