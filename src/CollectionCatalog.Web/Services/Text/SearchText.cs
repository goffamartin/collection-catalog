using System.Globalization;
using System.Text;

namespace CollectionCatalog.Web.Services.Text;

/// <summary>
/// Normalizes text for searching regardless of case and diacritics („kremen“ finds „Křemen“).
/// </summary>
public static class SearchText
{
    private static readonly char[] _separators = [' ', ',', '.', ';', ':', '-', '–', '/', '(', ')', '"', '„', '“', '\n', '\r', '\t'];

    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }
        return builder.ToString();
    }

    /// <summary>
    /// Every word of the query must match the beginning of some word in the texts (in any order) –
    /// „kre“ finds „Křemen“, but not „Akrecie“.
    /// </summary>
    public static bool Matches(string normalizedQuery, IEnumerable<string?> texts)
    {
        var words = texts
            .SelectMany(text => Normalize(text).Split(_separators, StringSplitOptions.RemoveEmptyEntries))
            .ToList();

        return normalizedQuery
            .Split(_separators, StringSplitOptions.RemoveEmptyEntries)
            .All(queryWord => words.Any(word => word.StartsWith(queryWord, StringComparison.Ordinal)));
    }
}
