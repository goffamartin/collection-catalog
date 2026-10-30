using System.Net;
using System.Text.RegularExpressions;
using CollectionCatalog.Tests.Infrastructure;
using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Services.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CollectionCatalog.Tests.Admin;

/// <summary>
/// Sign-in through the real form and rendering of the administration pages (end-to-end over HTTP).
/// </summary>
[TestClass]
public sealed partial class AdminAuthenticationTests
{
    private const string UserName = "curator";
    private const string Password = "test-password-123";

    // A failed sign-in raises the failed-attempt counter of the account; parallel successful sign-ins of the same
    // account would then both reset it and one of them fails on the concurrency stamp – so wrong passwords use their own.
    private const string WrongPasswordUserName = "visitor";

    private static TestWebApp _app = null!;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        _app = new TestWebApp();

        await using var scope = _app.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        foreach (var (userName, displayName) in new[] { (UserName, "Curator"), (WrongPasswordUserName, "Visitor") })
        {
            var result = await userManager.CreateAsync(new AppUser { UserName = userName, DisplayName = displayName }, Password);
            Assert.IsTrue(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        _app.Dispose();
    }

    [TestMethod]
    [DataRow("/admin")]
    [DataRow("/admin/predmety")]
    [DataRow("/admin/ucet")]
    [DataRow("/admin/media/0123456789abcdef0123456789abcdef/400")]
    public async Task Admin_WithoutLogin_RedirectsToLogin(string path)
    {
        using var client = _app.CreateClient(cookies: true);

        var response = await client.GetAsync(path);

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        StringAssert.StartsWith(response.Headers.Location?.ToString(), "http://localhost/prihlaseni");
    }

    [TestMethod]
    public async Task Admin_BehindTunnel_RedirectsToHttpsLogin()
    {
        // Cloudflare Tunnel passes HTTP to the app and sends the real scheme in X-Forwarded-Proto.
        using var client = _app.CreateClient(cookies: true);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/admin");
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        StringAssert.StartsWith(response.Headers.Location?.ToString(), "https://localhost/prihlaseni");
    }

    [TestMethod]
    public async Task Login_WithValidPassword_OpensAdministration()
    {
        using var client = _app.CreateClient(cookies: true);

        var response = await LoginAsync(client, Password);

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.AreEqual("/admin", response.Headers.Location?.AbsolutePath);

        // Every page must render in reasonable time (also guards against a recursive layout).
        foreach (var path in new[] { "/admin/predmety", "/admin/ciselniky", "/admin/nastaveni", "/admin/ucet" })
        {
            var page = await client.GetAsync(path);
            Assert.AreEqual(HttpStatusCode.OK, page.StatusCode, path);
            StringAssert.Contains(WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync()), "Log out", path);
        }
    }

    [TestMethod]
    public async Task Login_WithWrongPassword_ShowsError()
    {
        using var client = _app.CreateClient(cookies: true);

        var response = await LoginAsync(client, "wrong-password", userName: WrongPasswordUserName);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()), "Incorrect user name or password");
    }

    [TestMethod]
    public async Task Login_AfterRepeatedFailuresFromSameIp_IsBlocked()
    {
        // A nonexistent name: the IP limit counts, but no account gets locked (the other tests sign in with the real one).
        using var attacker = _app.CreateClient(cookies: true);
        for (var i = 0; i < LoginAttemptLimiter.MaxFailures; i++)
        {
            await LoginAsync(attacker, "wrong-password", userName: "nobody");
        }

        var blocked = await LoginAsync(attacker, Password);
        Assert.AreEqual(HttpStatusCode.OK, blocked.StatusCode, "not even the right password signs in from a blocked IP");
        StringAssert.Contains(WebUtility.HtmlDecode(await blocked.Content.ReadAsStringAsync()), "Too many failed sign-in attempts");

        using var otherIp = _app.CreateClient(cookies: true);
        Assert.AreEqual(HttpStatusCode.Redirect, (await LoginAsync(otherIp, Password)).StatusCode, "another IP is not blocked");
    }

    [TestMethod]
    [DataRow("//evil.example/admin")]
    [DataRow("/\\evil.example")]
    [DataRow("https://evil.example/")]
    public async Task Login_WithForeignReturnUrl_StaysOnSite(string returnUrl)
    {
        using var client = _app.CreateClient(cookies: true);

        var response = await LoginAsync(client, Password, returnUrl: returnUrl);

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.AreEqual("localhost", response.Headers.Location?.Host);
        Assert.AreEqual("/admin", response.Headers.Location?.AbsolutePath);
    }

    [TestMethod]
    public async Task Login_WithLocalReturnUrl_ReturnsThere()
    {
        using var client = _app.CreateClient(cookies: true);

        var response = await LoginAsync(client, Password, returnUrl: "/admin/poptavky");

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.AreEqual("localhost", response.Headers.Location?.Host);
        Assert.AreEqual("/admin/poptavky", response.Headers.Location?.AbsolutePath);
    }

    [TestMethod]
    public async Task Logout_WithoutAntiforgeryToken_IsRejected()
    {
        // A foreign website could otherwise sign the user out with a hidden form.
        using var client = _app.CreateClient(cookies: true);
        await LoginAsync(client, Password);

        var response = await client.PostAsync("/ucet/odhlasit", new FormUrlEncodedContent([]));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync("/admin/predmety")).StatusCode, "the user stays signed in");
    }

    [TestMethod]
    [DataRow("/admin/predmety?typ=abc&strana=-1&zeme=1e3")]
    [DataRow("/admin/poptavky?id=x&predmet=%20&nabidka=99999999999")]
    public async Task Admin_WithMalformedQuery_StillRenders(string path)
    {
        using var client = _app.CreateClient(cookies: true);
        await LoginAsync(client, Password);

        var response = await client.GetAsync(path);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string password, string userName = UserName, string? returnUrl = null)
    {
        var url = returnUrl is null ? "/prihlaseni" : $"/prihlaseni?ReturnUrl={Uri.EscapeDataString(returnUrl)}";
        var loginPage = await client.GetStringAsync(url);
        var hiddenFields = HiddenInputRegex().Matches(loginPage).ToDictionary(m => m.Groups["name"].Value, m => WebUtility.HtmlDecode(m.Groups["value"].Value));

        var form = new Dictionary<string, string>(hiddenFields)
        {
            ["Input.UserName"] = userName,
            ["Input.Password"] = password,
            ["Input.RememberMe"] = "true",
        };
        return await client.PostAsync(url, new FormUrlEncodedContent(form));
    }

    [GeneratedRegex("""<input[^>]*type="hidden"[^>]*name="(?<name>[^"]+)"[^>]*value="(?<value>[^"]*)""")]
    private static partial Regex HiddenInputRegex();
}
