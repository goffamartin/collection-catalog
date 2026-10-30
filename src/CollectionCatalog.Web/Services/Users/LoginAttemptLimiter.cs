using System.Collections.Concurrent;

namespace CollectionCatalog.Web.Services.Users;

/// <summary>
/// Limits failed sign-ins from one IP address. Complements the account lockout of Identity: that protects the password,
/// but an attacker could use it to keep the account locked forever – the per-IP limit stops them sooner.
/// Kept in memory (the app runs as a single instance); a restart resets the limits.
/// </summary>
public sealed class LoginAttemptLimiter(TimeProvider timeProvider)
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(30);

    // Safeguard against filling the memory during an attack from many addresses – expired entries are cleaned up then.
    private const int CleanupThreshold = 10_000;

    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    /// <summary>The address has made <see cref="MaxFailures"/> failed attempts within <see cref="Window"/>.</summary>
    public bool IsBlocked(string? clientIp) =>
        _entries.TryGetValue(Key(clientIp), out var entry) && !IsExpired(entry) && entry.Failures >= MaxFailures;

    public void RegisterFailure(string? clientIp)
    {
        if (_entries.Count > CleanupThreshold)
        {
            foreach (var (key, entry) in _entries)
            {
                if (IsExpired(entry))
                {
                    _entries.TryRemove(key, out _);
                }
            }
        }

        var now = timeProvider.GetUtcNow();
        _entries.AddOrUpdate(
            Key(clientIp),
            _ => new Entry(1, now),
            (_, entry) => IsExpired(entry) ? new Entry(1, now) : entry with { Failures = entry.Failures + 1 });
    }

    /// <summary>Successful sign-in – earlier typos no longer count.</summary>
    public void Reset(string? clientIp) => _entries.TryRemove(Key(clientIp), out _);

    private bool IsExpired(Entry entry) => timeProvider.GetUtcNow() - entry.FirstFailure >= Window;

    private static string Key(string? clientIp) => clientIp ?? "unknown";

    private sealed record Entry(int Failures, DateTimeOffset FirstFailure);
}
