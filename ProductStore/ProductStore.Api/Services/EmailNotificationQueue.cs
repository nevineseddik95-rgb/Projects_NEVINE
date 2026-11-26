using System.Threading.Channels;

namespace ProductStore.Api.Services;

public sealed record EmailNotification(string ToEmail, string ToName, string Subject, string HtmlBody);

public sealed class EmailNotificationQueue
{
    private readonly Channel<EmailNotification> _channel = Channel.CreateBounded<EmailNotification>(
        new BoundedChannelOptions(200)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public bool TryEnqueue(EmailNotification notification) => _channel.Writer.TryWrite(notification);

    public IAsyncEnumerable<EmailNotification> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
