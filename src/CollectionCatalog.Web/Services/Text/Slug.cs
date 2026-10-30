using System.Text;

namespace CollectionCatalog.Web.Services.Text;

/// <summary>
/// URL part from a name („Ametystová geoda z Brazílie“ → „ametystova-geoda-z-brazilie“). Not stored – always computed from the current name.
/// </summary>
public static class Slug
{
    private const int MaxLength = 80;

    public static string From(string? text)
    {
        var normalized = SearchText.Normalize(text);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        if (slug.Length > MaxLength)
        {
            slug = slug[..MaxLength].TrimEnd('-');
        }
        return slug.Length > 0 ? slug : "predmet";
    }
}
