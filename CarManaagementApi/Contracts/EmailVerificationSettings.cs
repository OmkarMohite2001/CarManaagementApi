namespace CarManaagementApi.Contracts;

public sealed class EmailVerificationSettings
{
    public int CodeExpiryMinutes { get; set; } = 15;
    public int ResendCooldownSeconds { get; set; } = 60;
    public int MaxVerifyAttempts { get; set; } = 5;
    public bool ExposeCodeInResponseInDevelopment { get; set; }
}
