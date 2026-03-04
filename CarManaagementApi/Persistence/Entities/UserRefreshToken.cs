using System;
using System.Collections.Generic;

namespace CarManaagementApi.Persistence.Entities;

public partial class UserRefreshToken
{
    public long RefreshTokenId { get; set; }

    public string UserId { get; set; } = null!;

    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
