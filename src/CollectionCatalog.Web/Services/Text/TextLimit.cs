namespace CollectionCatalog.Web.Services.Text;

/// <summary>
/// Cleans free text of immediately saved fields (photo caption, inquiry note), which do not go through form
/// validation. SQLite does not enforce column lengths, so this function keeps the limit of the entity configuration.
/// </summary>
public static class TextLimit
{
    /// <summary>Trims whitespace, returns empty text as <c>null</c> and shortens longer text to <paramref name="maxLength"/> characters.</summary>
    public static string? Clean(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength].TrimEnd();
    }
}
