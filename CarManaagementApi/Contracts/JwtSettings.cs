namespace CarManaagementApi.Contracts;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "RentX";
    public string Audience { get; set; } = "RentXClient";
    public string SecretKey { get; set; } = "THIS_IS_ONLY_FOR_LOCAL_DEVELOPMENT_CHANGE_ME_123456";
    public int AccessTokenExpiryMinutes { get; set; } = 60;
    public int RefreshTokenExpiryDays { get; set; } = 7;
}
