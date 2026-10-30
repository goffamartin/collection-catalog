using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Services.Images;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CollectionCatalog.Web.Infrastructure;

public static class StorageSetup
{
    /// <summary>
    /// Registers the storage: data folder, SQLite database, image store and persistent Data Protection keys
    /// (without them every container restart would invalidate the sign-in cookies).
    /// </summary>
    public static WebApplicationBuilder AddStorage(this WebApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
        options.DataPath = Path.GetFullPath(options.DataPath, builder.Environment.ContentRootPath);

        Directory.CreateDirectory(options.DataPath);
        Directory.CreateDirectory(options.ImagesPath);
        Directory.CreateDirectory(options.KeysPath);

        builder.Services.AddSingleton(options);

        builder.Services.AddDataProtection()
            .SetApplicationName("CollectionCatalog")
            .PersistKeysToFileSystem(new DirectoryInfo(options.KeysPath));

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = options.DatabasePath,
            ForeignKeys = true,
        }.ToString();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<AuditInterceptor>();
        // Factory for interactive (Blazor Server) components – a DbContext must not live for the whole circuit.
        // The scoped DbContext for everything else (Identity, SSR pages, CLI) is created from the same factory.
        builder.Services.AddDbContextFactory<AppDbContext>((services, db) => db
            .UseSqlite(connectionString)
            .AddInterceptors(services.GetRequiredService<AuditInterceptor>()));
        builder.Services.AddScoped(services => services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

        builder.Services.AddSingleton<ImageStore>();

        return builder;
    }
}
