using System.Globalization;

namespace CollectionCatalog.Web.Infrastructure;

/// <summary>
/// Currency of all prices in the collection (configuration <c>Site:Currency</c>, ISO 4217 code). Amounts are formatted
/// by the rules of the current language – <c>€12,500</c> in English, <c>12 500 €</c> in Czech.
/// </summary>
public static class Money
{
    /// <summary>ISO 4217 code, e.g. <c>EUR</c>, <c>CZK</c>, <c>USD</c>.</summary>
    public static string CurrencyCode { get; private set; } = "EUR";

    /// <summary>Symbol of the currency (€, Kč, $) – the code itself when no culture knows it.</summary>
    public static string Symbol { get; private set; } = "€";

    /// <summary>Set once at startup from the configuration.</summary>
    public static void Configure(string? currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return;
        }
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        Symbol = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Select(culture => new RegionInfo(culture.Name))
            .FirstOrDefault(region => region.ISOCurrencySymbol == CurrencyCode)?.CurrencySymbol ?? CurrencyCode;
    }

    /// <summary>Whole amount with the currency, e.g. „€12,500“.</summary>
    public static string Format(decimal amount)
    {
        var format = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
        format.CurrencySymbol = Symbol;
        return amount.ToString("C0", format);
    }
}
