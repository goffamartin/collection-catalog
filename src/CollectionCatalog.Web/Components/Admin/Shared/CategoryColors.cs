namespace CollectionCatalog.Web.Components.Admin.Shared;

/// <summary>
/// Colored category badges – the user picks the color, background and text shades are derived from it (CSS color-mix).
/// </summary>
public static class CategoryColors
{
    private const string Fallback = "#78716c";

    public static string BadgeStyle(string? color)
    {
        var c = string.IsNullOrEmpty(color) ? Fallback : color;
        return $"background-color: color-mix(in srgb, {c} 12%, white); --tw-ring-color: color-mix(in srgb, {c} 30%, white); color: color-mix(in srgb, {c} 70%, black)";
    }
}
