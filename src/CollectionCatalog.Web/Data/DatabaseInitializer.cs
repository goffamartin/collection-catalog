using System.Globalization;
using CollectionCatalog.Web.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CollectionCatalog.Web.Data;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies EF Core migrations and turns on the WAL mode of SQLite.
    /// The app always runs as a single instance, so migrating at startup is safe.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<StorageOptions>();

        await BackupBeforeMigrationAsync(db, storage, app.Logger);
        await db.Database.MigrateAsync();

        // WAL allows concurrent reads during a write; the setting persists in the database file.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    }

    /// <summary>
    /// Copy of the database before pending migrations run (<c>backups/catalog-{time}-before-{migration}.db</c>). SQLite
    /// table rebuilds cannot run in one transaction, so a migration failing half-way would otherwise leave the data
    /// inconsistent. <c>VACUUM INTO</c> makes a consistent copy even in WAL mode.
    /// </summary>
    private static async Task BackupBeforeMigrationAsync(AppDbContext db, StorageOptions storage, ILogger logger)
    {
        if (!File.Exists(storage.DatabasePath))
        {
            return;
        }
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0)
        {
            return;
        }

        var directory = Path.Combine(storage.DataPath, "backups");
        Directory.CreateDirectory(directory);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(directory, $"catalog-{stamp}-before-{pending[^1]}.db");

        await db.Database.ExecuteSqlRawAsync("VACUUM INTO {0}", path);
        logger.LogInformation("Database backed up to {Path} before migrations {Migrations}", path, string.Join(", ", pending));
    }
}
