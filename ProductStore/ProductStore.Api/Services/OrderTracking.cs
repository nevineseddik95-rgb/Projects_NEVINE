namespace ProductStore.Api.Services;

public static class OrderTracking
{
    public static string NewReferenceNumber() => $"CMD-{Guid.NewGuid():N}".ToUpperInvariant();

    public static string NewTrackingNumber() => Guid.NewGuid().ToString("N").ToUpperInvariant();
}
