namespace CollectionCatalog.Web.Domain;

/// <summary>
/// User-managed category (Army, Police, Firefighters…).
/// </summary>
public class Category
{
    public int Id { get; set; }
    public required LocalizedText Name { get; set; }

    /// <summary>Color to tell categories apart in the UI, hex (#RRGGBB).</summary>
    public string? Color { get; set; }

    public ICollection<Item> Items { get; set; } = [];
}
