using CollectionCatalog.Web.Services.Inquiries;

namespace CollectionCatalog.Tests.Inquiries;

[TestClass]
public sealed class InquiryRateLimiterTests
{
    [TestMethod]
    public void AllowsLimitPerIp_ThenRejects()
    {
        using var limiter = new InquiryRateLimiter();

        for (var i = 0; i < InquiryRateLimiter.PermitLimit; i++)
        {
            Assert.IsTrue(limiter.TryAcquire("1.2.3.4"), $"dotaz {i + 1}");
        }

        Assert.IsFalse(limiter.TryAcquire("1.2.3.4"));
        Assert.IsTrue(limiter.TryAcquire("5.6.7.8"), "another IP has its own limit");
    }

    [TestMethod]
    public void UnknownIp_SharesOneLimit()
    {
        using var limiter = new InquiryRateLimiter();

        for (var i = 0; i < InquiryRateLimiter.PermitLimit; i++)
        {
            limiter.TryAcquire(null);
        }

        Assert.IsFalse(limiter.TryAcquire(null));
    }
}
