using System;

namespace CarManaagementApi.Persistence.Entities;

public partial class UserEmailVerification
{
    public long VerificationId { get; set; }

    public string UserId { get; set; } = null!;

    public string VerificationCode { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public byte FailedAttempts { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
