namespace CollectionCatalog.Web.Services.Inquiries;

/// <summary>
/// Once a day deletes handled inquiries older than a year (personal data of visitors – GDPR).
/// </summary>
public sealed class InquiryRetentionService(IServiceScopeFactory scopeFactory, ILogger<InquiryRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var deleted = await scope.ServiceProvider.GetRequiredService<InquiryService>().DeleteExpiredAsync(stoppingToken);
                if (deleted > 0)
                {
                    logger.LogInformation("Deleted {Count} handled inquiries older than a year.", deleted);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Deleting old inquiries failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
