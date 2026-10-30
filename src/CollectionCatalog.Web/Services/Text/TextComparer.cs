using System.Globalization;

namespace CollectionCatalog.Web.Services.Text;

/// <summary>Sorting and comparing names by the rules of a language (Czech: „Ch“ after „H“, case-insensitive).</summary>
public static class TextComparer
{
    public static StringComparer For(string languageCode) =>
        StringComparer.Create(CultureInfo.GetCultureInfo(languageCode), ignoreCase: true);

    /// <summary>For the language of the current request (UI culture).</summary>
    public static StringComparer Current => StringComparer.Create(CultureInfo.CurrentUICulture, ignoreCase: true);
}
