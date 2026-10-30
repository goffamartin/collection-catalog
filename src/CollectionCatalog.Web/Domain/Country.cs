namespace CollectionCatalog.Web.Domain;

/// <summary>
/// Country / state of origin (including historical ones – Austria-Hungary, the Protectorate…).
/// </summary>
public class Country
{
    public int Id { get; set; }
    public required LocalizedText Name { get; set; }

    public ICollection<Item> Items { get; set; } = [];
}
