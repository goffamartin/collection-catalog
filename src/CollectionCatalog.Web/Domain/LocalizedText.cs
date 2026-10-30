using System.Collections.Immutable;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CollectionCatalog.Web.Domain;

/// <summary>
/// User-entered text in several languages (item name, description, codebook names…), stored as JSON
/// <c>{"cs": "…", "en": "…"}</c> in a single column. Immutable – changes create a new instance.
/// </summary>
public sealed class LocalizedText : IEquatable<LocalizedText>
{
    public static readonly LocalizedText Empty = new(ImmutableSortedDictionary<string, string>.Empty);

    // Readable JSON in the database (Czech letters as they are, no \uXXXX escapes); it is never embedded in HTML.
    private static readonly JsonSerializerOptions _json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly ImmutableSortedDictionary<string, string> _values;

    private LocalizedText(ImmutableSortedDictionary<string, string> values) => _values = values;

    /// <summary>Non-empty values by language code.</summary>
    public IReadOnlyDictionary<string, string> Values => _values;

    public bool IsEmpty => _values.IsEmpty;

    /// <summary>Text in one language (e.g. imported data or a new codebook entry).</summary>
    public static LocalizedText Of(string languageCode, string? value) => Empty.With(languageCode, value);

    /// <summary>Exact value in the language, without fallback.</summary>
    public string? this[string languageCode] => _values.GetValueOrDefault(languageCode);

    /// <summary>Value for display: the requested language, else the default one, else any available.</summary>
    public string? Get(LanguageContext language) =>
        this[language.Code] ?? this[language.DefaultCode] ?? _values.Values.FirstOrDefault();

    /// <summary>Copy with the value in one language set (trimmed); an empty value removes the language.</summary>
    public LocalizedText With(string languageCode, string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? new(_values.Remove(languageCode))
            : new(_values.SetItem(languageCode, value.Trim()));

    /// <summary>Copy with every value transformed (e.g. shortened to the column limit).</summary>
    public LocalizedText Select(Func<string, string?> selector) =>
        _values.Aggregate(Empty, (text, pair) => text.With(pair.Key, selector(pair.Value)));

    public string ToJson() => JsonSerializer.Serialize(_values, _json);

    public static LocalizedText FromJson(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return Empty;
        }
        var values = JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? [];
        return values.Aggregate(Empty, (text, pair) => text.With(pair.Key, pair.Value));
    }

    public bool Equals(LocalizedText? other) =>
        other is not null && _values.Count == other._values.Count && _values.All(pair => other[pair.Key] == pair.Value);

    public override bool Equals(object? obj) => Equals(obj as LocalizedText);

    public override int GetHashCode() =>
        _values.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value));

    /// <summary>For debugging only – UI always picks a language via <see cref="Get"/>.</summary>
    public override string ToString() => ToJson();
}

/// <summary>Language in which texts are shown, and the default one used as fallback.</summary>
public readonly record struct LanguageContext(string Code, string DefaultCode)
{
    public bool IsDefault => Code == DefaultCode;
}
