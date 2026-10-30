using System.Threading.RateLimiting;

namespace CollectionCatalog.Web.Services.Inquiries;

/// <summary>
/// Limit of messages sent from one IP address (spam protection together with the "honeypot" field).
/// Kept in memory – the application runs as a single instance.
/// </summary>
public sealed class InquiryRateLimiter : IDisposable
{
    public const int PermitLimit = 5;
    public static readonly TimeSpan Window = TimeSpan.FromHours(1);

    private readonly PartitionedRateLimiter<string> _limiter = PartitionedRateLimiter.Create<string, string>(ip =>
        RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = PermitLimit,
            Window = Window,
            QueueLimit = 0,
        }));

    /// <returns><c>false</c> when the address sent too many messages in the last window.</returns>
    public bool TryAcquire(string? clientIp)
    {
        using var lease = _limiter.AttemptAcquire(clientIp ?? "unknown");
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}
