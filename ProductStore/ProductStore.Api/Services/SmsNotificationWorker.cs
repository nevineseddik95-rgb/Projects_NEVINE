namespace ProductStore.Api.Services;

public sealed class SmsNotificationWorker(
    SmsNotificationQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<SmsNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var notification in queue.ReadAllAsync(stoppingToken))
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var sender = scope.ServiceProvider.GetRequiredService<ISmsSender>();
                    if (await sender.SendAsync(notification.ToPhone, notification.Message))
                        break;

                    if (attempt == 2)
                        logger.LogError("SMS notification to {Recipient} failed after two attempts.", Mask(notification.ToPhone));
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unexpected failure while sending an SMS notification to {Recipient}.", Mask(notification.ToPhone));
                    if (attempt == 2) break;
                }

                if (attempt == 1)
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }

    private static string Mask(string phone) =>
        phone.Length > 4 ? new string('*', phone.Length - 4) + phone[^4..] : "****";
}
