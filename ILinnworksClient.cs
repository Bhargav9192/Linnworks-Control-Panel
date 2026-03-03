namespace Linnworks.Automation.Api.Linnworks;

public interface ILinnworksClient
{
    Task CreateOrdersAsync(object request);
    Task SplitOrderAsync(Guid orderId, object splitRequest);
    Task<object> GetOrderAsync(Guid orderId);
}