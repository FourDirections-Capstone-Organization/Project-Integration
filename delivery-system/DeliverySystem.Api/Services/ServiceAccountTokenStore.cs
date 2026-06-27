namespace DeliverySystem.Api.Services;

public class ServiceAccountTokenStore
{
    private string? _token;
    private DateTime _expiresAt;

    public string? Token => _token;
    public bool IsValid => _token != null && DateTime.UtcNow < _expiresAt;

    public void SetToken(string token, int expirationMinutes = 55)
    {
        _token = token;
        _expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);
    }
}
