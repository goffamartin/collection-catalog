using CollectionCatalog.Tests.Infrastructure;
using CollectionCatalog.Web.Services.Users;

namespace CollectionCatalog.Tests.Users;

[TestClass]
public sealed class LoginAttemptLimiterTests
{
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public void BlocksAfterMaxFailures_OnlyForThatIp()
    {
        var limiter = new LoginAttemptLimiter(_time);

        for (var i = 0; i < LoginAttemptLimiter.MaxFailures - 1; i++)
        {
            limiter.RegisterFailure("1.2.3.4");
        }
        Assert.IsFalse(limiter.IsBlocked("1.2.3.4"), "one attempt is left");

        limiter.RegisterFailure("1.2.3.4");
        Assert.IsTrue(limiter.IsBlocked("1.2.3.4"));
        Assert.IsFalse(limiter.IsBlocked("5.6.7.8"));
    }

    [TestMethod]
    public void UnblocksAfterWindow()
    {
        var limiter = new LoginAttemptLimiter(_time);
        for (var i = 0; i < LoginAttemptLimiter.MaxFailures; i++)
        {
            limiter.RegisterFailure("1.2.3.4");
        }

        _time.Now = _time.Now.Add(LoginAttemptLimiter.Window).AddSeconds(-1);
        Assert.IsTrue(limiter.IsBlocked("1.2.3.4"));

        _time.Now = _time.Now.AddSeconds(1);
        Assert.IsFalse(limiter.IsBlocked("1.2.3.4"));

        // A new window starts with the next failure.
        limiter.RegisterFailure("1.2.3.4");
        Assert.IsFalse(limiter.IsBlocked("1.2.3.4"));
    }

    [TestMethod]
    public void SuccessfulLogin_ResetsFailures()
    {
        var limiter = new LoginAttemptLimiter(_time);
        for (var i = 0; i < LoginAttemptLimiter.MaxFailures - 1; i++)
        {
            limiter.RegisterFailure("1.2.3.4");
        }

        limiter.Reset("1.2.3.4");
        limiter.RegisterFailure("1.2.3.4");

        Assert.IsFalse(limiter.IsBlocked("1.2.3.4"));
    }

    [TestMethod]
    public void UnknownIp_IsTrackedAsOneClient()
    {
        var limiter = new LoginAttemptLimiter(_time);
        for (var i = 0; i < LoginAttemptLimiter.MaxFailures; i++)
        {
            limiter.RegisterFailure(null);
        }

        Assert.IsTrue(limiter.IsBlocked(null));
    }
}
