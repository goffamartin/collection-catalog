namespace CollectionCatalog.Web.Domain;

/// <summary>
/// User-managed item type (e.g. Mineral, Fossil, Meteorite).
/// </summary>
public class ItemType
{
    public int Id { get; set; }
    public required LocalizedText Name { get; set; }

    public ICollection<Item> Items { get; set; } = [];
}
