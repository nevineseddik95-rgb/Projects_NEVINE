namespace ProductStore.Api.Services;

public sealed class EmailNotificationWorker(
    EmailNotificationQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<EmailNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var notification in queue.ReadAllAsync(stoppingToken))
        {
            logger.LogInformation("Sending email notification: {Subject}", notification.Subject);
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                    if (await sender.SendAsync(notification.ToEmail, notification.ToName,
                            notification.Subject, notification.HtmlBody, stoppingToken))
                    {
                        logger.LogInformation("Email notification sent: {Subject}", notification.Subject);
                        break;
                    }

                    if (attempt == 2)
                        logger.LogError("Email notification to {Recipient} failed after two attempts.", notification.ToEmail);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unexpected failure while sending an email notification to {Recipient}.", notification.ToEmail);
                    if (attempt == 2) break;
                }

                if (attempt == 1)
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}
