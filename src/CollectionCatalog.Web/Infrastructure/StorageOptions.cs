namespace CollectionCatalog.Web.Infrastructure;

/// <summary>
/// Location of the persistent data of the application (SQLite database, images, Data Protection keys).
/// In the container it is the mounted volume <c>/data</c>.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Root folder of the data. A relative path is resolved against the content root.
    /// </summary>
    public string DataPath { get; set; } = "App_Data";

    public string DatabasePath => Path.Combine(DataPath, "catalog.db");
    public string ImagesPath => Path.Combine(DataPath, "images");
    public string KeysPath => Path.Combine(DataPath, "keys");
}
