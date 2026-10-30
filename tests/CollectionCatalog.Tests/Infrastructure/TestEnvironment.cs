using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Infrastructure;
using CollectionCatalog.Web.Domain;
using CollectionCatalog.Web.Resources;
using CollectionCatalog.Web.Services.Images;
using CollectionCatalog.Web.Services.Languages;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CollectionCatalog.Tests.Infrastructure;

/// <summary>
/// Isolated test environment: a temporary folder with the data storage and a migrated SQLite database.
/// </summary>
public sealed class TestEnvironment : IAsyncDisposable
{
    // Identity reads the schema version (v3 = passkeys) from IdentityOptions in the application service provider of the context.
    private static readonly IServiceProvider _identityServices = new ServiceCollection()
        .Configure<IdentityOptions>(options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
        .BuildServiceProvider();

    private readonly DbContextOptions<AppDbContext> _dbOptions;

    private TestEnvironment(string rootPath, TimeProvider timeProvider)
    {
        RootPath = rootPath;
        Storage = new StorageOptions { DataPath = Path.Combine(rootPath, "data") };
        Directory.CreateDirectory(Storage.ImagesPath);
        ImageStore = new ImageStore(Storage);
        TimeProvider = timeProvider;

        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Storage.DatabasePath};Foreign Keys=True")
            .UseApplicationServiceProvider(_identityServices)
            .AddInterceptors(new AuditInterceptor(timeProvider))
            .Options;
    }

    /// <summary>UI texts – the real resources, in the culture of the test thread (English, see MSTestSettings).</summary>
    public static IStringLocalizer<AppText> Text { get; } = new StringLocalizer<AppText>(
        new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance));

    public string RootPath { get; }
    public StorageOptions Storage { get; }
    public ImageStore ImageStore { get; }
    public TimeProvider TimeProvider { get; }

    /// <summary>Languages from the migrated database (English default, Czech).</summary>
    public LanguageService Languages { get; private set; } = null!;

    public static async Task<TestEnvironment> CreateAsync(TimeProvider? timeProvider = null)
    {
        var environment = new TestEnvironment(
            Path.Combine(Path.GetTempPath(), "catalog-tests", Guid.NewGuid().ToString("N")),
            timeProvider ?? TimeProvider.System);

        await using (var db = environment.CreateDbContext())
        {
            await db.Database.MigrateAsync();
        }
        environment.Languages = new LanguageService(new TestDbContextFactory(environment), Text);
        await environment.Languages.LoadAsync();
        return environment;
    }

    public AppDbContext CreateDbContext() => new(_dbOptions);

    public IEnumerable<string> StoredImageFiles() =>
        Directory.EnumerateFiles(Storage.ImagesPath, "*", SearchOption.AllDirectories);

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(RootPath, recursive: true);
        }
        catch (IOException)
        {
            // Cleaning up the temp folder does not matter for the result of the test.
        }
        return ValueTask.CompletedTask;
    }
}

public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Translatable texts in tests – English is the default language of the test database, Czech the second one.</summary>
public static class TestText
{
    public static LocalizedText En(string? text) => LocalizedText.Of("en", text);

    public static LocalizedText Cs(string? text) => LocalizedText.Of("cs", text);
}
