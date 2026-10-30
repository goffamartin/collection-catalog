using System.Globalization;

namespace CollectionCatalog.Web.Components.Admin.Shared;

public static class Format
{

    /// <summary>Whole amount in the currency of the collection, e.g. „€125,531“ (format of the current language).</summary>
    public static string Money(decimal? value) => value is { } v ? Infrastructure.Money.Format(v) : "";

    public static string Number(int value) => value.ToString("N0", CultureInfo.CurrentCulture);
}

/// <summary>Columns of the item list that can be sorted by.</summary>
public enum SortColumn
{
    Name,
    Type,
    Country,
    Year,
    Condition,
    PurchasePrice,
    EstimatedValue,
}
