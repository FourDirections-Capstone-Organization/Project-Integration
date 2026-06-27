using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OperationalSystem.Api.Services;

public class DeliverySystemClient : IDeliverySystemClient
{
    private readonly HttpClient _httpClient;
    private readonly ServiceAccountTokenStore _tokenStore;
    private readonly IConfiguration _configuration;

    public DeliverySystemClient(HttpClient httpClient, ServiceAccountTokenStore tokenStore, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        _configuration = configuration;
    }

    public async Task<Models.DTOs.DeliveryOrderResponse?> GetOrderAsync(Guid orderId)
    {
        await EnsureTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_configuration["ExternalSystems:Delivery:BaseUrl"]}/api/integration/orders/{orderId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStore.Token);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Models.DTOs.DeliveryOrderResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task<List<Models.DTOs.DeliveryOrderResponse>> GetAllOrdersAsync()
    {
        await EnsureTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_configuration["ExternalSystems:Delivery:BaseUrl"]}/api/integration/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStore.Token);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return new List<Models.DTOs.DeliveryOrderResponse>();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<Models.DTOs.DeliveryOrderResponse>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? new List<Models.DTOs.DeliveryOrderResponse>();
    }

    private async Task EnsureTokenAsync()
    {
        if (_tokenStore.IsValid) return;

        var authBaseUrl = _configuration["AuthService:BaseUrl"];
        var loginPayload = new
        {
            employeeNumber = _configuration["ExternalSystems:Delivery:ServiceAccountEmployeeNumber"],
            password = _configuration["ExternalSystems:Delivery:ServiceAccountPassword"]
        };

        var loginResponse = await _httpClient.PostAsync(
            $"{authBaseUrl}/api/auth/login",
            new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json"));

        loginResponse.EnsureSuccessStatusCode();

        var json = await loginResponse.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<Models.DTOs.LoginResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (result?.AccessToken != null)
            _tokenStore.SetToken(result.AccessToken);
    }

    private class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
    }
}
