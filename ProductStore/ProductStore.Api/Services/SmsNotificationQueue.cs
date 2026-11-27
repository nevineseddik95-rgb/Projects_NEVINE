using System.Threading.Channels;

namespace ProductStore.Api.Services;

public sealed record SmsNotification(string ToPhone, string Message);

public sealed class SmsNotificationQueue
{
    private readonly Channel<SmsNotification> _channel = Channel.CreateBounded<SmsNotification>(
        new BoundedChannelOptions(200)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public bool TryEnqueue(SmsNotification notification) => _channel.Writer.TryWrite(notification);

    public IAsyncEnumerable<SmsNotification> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
