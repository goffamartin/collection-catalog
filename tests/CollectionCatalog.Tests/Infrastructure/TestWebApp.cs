using CollectionCatalog.Web.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CollectionCatalog.Tests.Infrastructure;

/// <summary>
/// The whole app in memory (end-to-end over HTTP) over its own temporary data folder.
/// </summary>
public sealed class TestWebApp : IDisposable
{
    private static int _clientCounter;

    private readonly WebApplicationFactory<Program> _factory;

    public TestWebApp(IReadOnlyDictionary<string, string>? settings = null)
    {
        DataPath = Path.Combine(Path.GetTempPath(), "catalog-tests", Guid.NewGuid().ToString("N"));
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Storage:DataPath", DataPath);
            foreach (var (key, value) in settings ?? new Dictionary<string, string>())
            {
                builder.UseSetting(key, value);
            }
        });
    }

    public string DataPath { get; }

    /// <summary>Services of the app (the first access starts the host and migrates the database).</summary>
    public IServiceProvider Services => _factory.Services;

    /// <summary>
    /// Client without automatic redirects and with its own IP address (as if coming through Cloudflare) –
    /// so limits per IP (sign-in, inquiries) do not affect other tests.
    /// </summary>
    public HttpClient CreateClient(bool cookies = false)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = cookies });
        var n = Interlocked.Increment(ref _clientCounter);
        client.DefaultRequestHeaders.Add("CF-Connecting-IP", $"10.0.{n / 256}.{n % 256}");
        client.Timeout = TimeSpan.FromSeconds(20);
        return client;
    }

    public void Dispose()
    {
        _factory.Dispose();
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(DataPath, recursive: true);
        }
        catch (IOException)
        {
            // Cleaning up the temp folder does not matter for the result of the test.
        }
    }
}

/// <summary>Context factory for services in unit tests – every call gives a new context over the test database.</summary>
public sealed class TestDbContextFactory(TestEnvironment env) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => env.CreateDbContext();
}
