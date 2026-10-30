using CollectionCatalog.Web.Domain;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CollectionCatalog.Web.Data;

/// <summary>
/// <see cref="LocalizedText"/> ↔ JSON text column. An empty text is stored as <c>{}</c>, so the columns are NOT NULL
/// and translatable properties are never null in code.
/// </summary>
public sealed class LocalizedTextConverter() : ValueConverter<LocalizedText, string>(
    text => text.ToJson(),
    json => LocalizedText.FromJson(json));

public sealed class LocalizedTextComparer() : ValueComparer<LocalizedText>(
    (a, b) => object.Equals(a, b),
    text => text.GetHashCode(),
    // Immutable – a snapshot can share the instance.
    text => text);
