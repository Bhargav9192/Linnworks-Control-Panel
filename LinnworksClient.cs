using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Linnworks.Automation.Api.Linnworks;

public class LinnworksClient : ILinnworksClient
{
    private readonly HttpClient _http;
    private readonly string _token;

    public LinnworksClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _token = config["Linnworks:Token"];
    }

    public async Task CreateOrdersAsync(object request)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        content.Headers.Add("Authorization", _token);

        await _http.PostAsync("https://eu-ext.linnworks.net/api/Orders/CreateOrders", content);
    }

    public async Task SplitOrderAsync(Guid orderId, object splitRequest)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(splitRequest),
            Encoding.UTF8,
            "application/json");

        content.Headers.Add("Authorization", _token);

        await _http.PostAsync("https://eu-ext.linnworks.net/api/Orders/SplitOrder", content);
    }

    public async Task<object> GetOrderAsync(Guid orderId)
    {
        var response = await _http.GetAsync($"https://eu-ext.linnworks.net/api/Orders/GetOrderById?orderId={orderId}&token={_token}");
        return await response.Content.ReadAsStringAsync();
    }
}