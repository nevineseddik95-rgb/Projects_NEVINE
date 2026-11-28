using Microsoft.AspNetCore.SignalR;
using ProductStore.Api.Hubs;

namespace ProductStore.Api.Services;

public sealed class AdminChangeNotifier(
    IHubContext<AdminHub> hub,
    ILogger<AdminChangeNotifier> logger)
{
    public async Task NotifyAsync(IReadOnlyCollection<string> resources, CancellationToken cancellationToken = default)
    {
        if (resources.Count == 0) return;

        try
        {
            await hub.Clients.All.SendAsync("changed", resources.ToArray(), cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Admin realtime notification failed.");
        }
    }
}
