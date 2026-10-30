using CollectionCatalog.Tests.Infrastructure;
using CollectionCatalog.Web.Data;
using CollectionCatalog.Web.Services.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CollectionCatalog.Tests.Users;

/// <summary>Account management from the command line and lockout after repeated wrong passwords.</summary>
[TestClass]
public sealed class UserAccountTests
{
    private const string Password = "test-password-123";

    private static TestWebApp _app = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext _) => _app = new TestWebApp();

    [ClassCleanup]
    public static void ClassCleanup() => _app.Dispose();

    [TestMethod]
    [DataRow(new[] { UserSetPasswordCommand.Name })]
    [DataRow(new[] { UserSetPasswordCommand.Name, "--username" })]
    [DataRow(new[] { UserSetPasswordCommand.Name, "--username", "x", "--admin" })]
    public async Task SetPassword_WithInvalidArguments_PrintsUsage(string[] args)
    {
        var output = new StringWriter();

        var exitCode = await UserSetPasswordCommand.RunAsync(_app.Services, args, output, () => throw new AssertFailedException("the password must not be read"));

        Assert.AreEqual(2, exitCode);
        StringAssert.Contains(output.ToString(), "Usage:");
    }

    [TestMethod]
    public async Task SetPassword_CreatesUser_ThenResetsPasswordAndUnlocks()
    {
        var userName = NewUserName();
        var output = new StringWriter();

        var created = await UserSetPasswordCommand.RunAsync(_app.Services, [UserSetPasswordCommand.Name, "--username", userName, "--name", "Curator"], output, () => Password);

        Assert.AreEqual(0, created, output.ToString());
        await using (var scope = _app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = await users.FindByNameAsync(userName);
            Assert.IsNotNull(user);
            Assert.AreEqual("Curator", user.DisplayName);
            Assert.IsTrue(await users.CheckPasswordAsync(user, Password));
            await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(10));
        }

        var reset = await UserSetPasswordCommand.RunAsync(_app.Services, [UserSetPasswordCommand.Name, "--username", userName], output, () => "nove-heslo-456");

        Assert.AreEqual(0, reset, output.ToString());
        await using (var scope = _app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByNameAsync(userName))!;
            Assert.IsTrue(await users.CheckPasswordAsync(user, "nove-heslo-456"));
            Assert.IsFalse(await users.IsLockedOutAsync(user), "a new password unlocks the account");
            Assert.AreEqual("Curator", user.DisplayName, "without --name the display name stays");
        }
    }

    [TestMethod]
    public async Task SetPassword_MismatchOrWeakPassword_Fails()
    {
        var output = new StringWriter();

        Assert.AreEqual(1, await UserSetPasswordCommand.RunAsync(_app.Services, [UserSetPasswordCommand.Name, "--username", NewUserName()], output, () => null));
        StringAssert.Contains(output.ToString(), "do not match");

        Assert.AreEqual(1, await UserSetPasswordCommand.RunAsync(_app.Services, [UserSetPasswordCommand.Name, "--username", NewUserName()], output, () => "abc"));
    }

    [TestMethod]
    public async Task Account_LocksAfterRepeatedWrongPasswords()
    {
        await using var scope = _app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<AppUser>>();
        var user = new AppUser { UserName = NewUserName() };
        Assert.IsTrue((await users.CreateAsync(user, Password)).Succeeded);

        for (var i = 0; i < users.Options.Lockout.MaxFailedAccessAttempts; i++)
        {
            await signIn.CheckPasswordSignInAsync(user, "wrong-password", lockoutOnFailure: true);
        }

        var result = await signIn.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: true);
        Assert.IsTrue(result.IsLockedOut, "not even the right password unlocks a locked account");
    }

    private static string NewUserName() => "user-" + Guid.NewGuid().ToString("N")[..8];
}
