using System.Net;
using CollectionCatalog.Tests.Infrastructure;

namespace CollectionCatalog.Tests;

/// <summary>
/// Checks that the app starts (including SQLite migrations) and answers the basic endpoints.
/// </summary>
[TestClass]
public sealed class SmokeTests
{
    private static TestWebApp _app = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
    {
        _app = new TestWebApp();
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _app.Dispose();
    }

    [TestMethod]
    public async Task Health_ReturnsHealthy()
    {
        using var client = _app.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("Healthy", await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task Home_ReturnsEnglishPage_AndCzechUnderPrefix()
    {
        using var client = _app.CreateClient();

        StringAssert.Contains(await client.GetStringAsync("/"), "<html lang=\"en\">");
        StringAssert.Contains(await client.GetStringAsync("/cs/"), "<html lang=\"cs\">");
    }

    [TestMethod]
    public void Startup_CreatesDatabaseFile()
    {
        _ = _app.Services; // forces the host to start

        Assert.IsTrue(File.Exists(Path.Combine(_app.DataPath, "catalog.db")));
    }
}
