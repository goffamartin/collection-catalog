using System.Globalization;

namespace CollectionCatalog.Web.Infrastructure;

/// <summary>
/// Reading values from the query string. Page parameters are taken as <c>string?</c> and converted here:
/// <c>[SupplyParameterFromQuery] int?</c> would crash the page with an exception on a hand-edited address (<c>?strana=x</c>).
/// </summary>
public static class QueryValue
{
    /// <summary>A non-negative integer, otherwise <c>null</c>.</summary>
    public static int? Int(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) ? result : null;
}
