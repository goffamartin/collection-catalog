namespace CollectionCatalog.Web.Infrastructure;

/// <summary>
/// Local time of the application. Times are stored in UTC; for display and "today" (e.g. the sale date) they are
/// converted explicitly to the zone of the collection (configuration <c>Site:TimeZone</c>, IANA id such as
/// <c>Europe/Prague</c>; UTC by default) – a container has UTC as its local zone, so <see cref="DateTime.ToLocalTime"/>
/// would be off.
/// </summary>
public static class AppTime
{
    public static TimeZoneInfo Zone { get; private set; } = TimeZoneInfo.Utc;

    /// <summary>Set once at startup from the configuration.</summary>
    public static void Configure(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            Zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
    }

    /// <summary>UTC time from the database (SQLite returns <see cref="DateTimeKind.Unspecified"/>) to local time.</summary>
    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    public static DateTime LocalNow(TimeProvider timeProvider) => ToLocal(timeProvider.GetUtcNow().UtcDateTime);

    public static DateOnly Today(TimeProvider timeProvider) => DateOnly.FromDateTime(LocalNow(timeProvider));
}
