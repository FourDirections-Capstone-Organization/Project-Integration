using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DeliverySystem.Api.Services;

public class OperationalSystemClient : IOperationalSystemClient
{
    private readonly HttpClient _httpClient;
    private readonly ServiceAccountTokenStore _tokenStore;
    private readonly IConfiguration _configuration;

    public OperationalSystemClient(HttpClient httpClient, ServiceAccountTokenStore tokenStore, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        _configuration = configuration;
    }

    public async Task<Models.DTOs.OperationalProductResponse?> GetProductAsync(Guid productId)
    {
        await EnsureTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_configuration["ExternalSystems:Operational:BaseUrl"]}/api/integration/products/{productId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStore.Token);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Models.DTOs.OperationalProductResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task<List<Models.DTOs.OperationalProductResponse>> GetAllProductsAsync()
    {
        await EnsureTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_configuration["ExternalSystems:Operational:BaseUrl"]}/api/integration/products");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStore.Token);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return new List<Models.DTOs.OperationalProductResponse>();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<Models.DTOs.OperationalProductResponse>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? new List<Models.DTOs.OperationalProductResponse>();
    }

    private async Task EnsureTokenAsync()
    {
        if (_tokenStore.IsValid) return;

        var authBaseUrl = _configuration["AuthService:BaseUrl"];
        var loginPayload = new
        {
            employeeNumber = _configuration["ExternalSystems:Operational:ServiceAccountEmployeeNumber"],
            password = _configuration["ExternalSystems:Operational:ServiceAccountPassword"]
        };

        var loginResponse = await _httpClient.PostAsync(
            $"{authBaseUrl}/api/auth/login",
            new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json"));

        loginResponse.EnsureSuccessStatusCode();

        var json = await loginResponse.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<LoginResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (result?.AccessToken != null)
            _tokenStore.SetToken(result.AccessToken);
    }

    private class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
    }
}
