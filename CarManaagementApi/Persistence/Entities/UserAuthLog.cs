using System;

namespace CarManaagementApi.Persistence.Entities;

public partial class UserAuthLog
{
    public long AuthLogId { get; set; }

    public string UserId { get; set; } = null!;

    public string RoleCode { get; set; } = null!;

    public DateTime LoginAt { get; set; }

    public DateTime? LogoutAt { get; set; }

    public string? LoginIp { get; set; }

    public string? LogoutIp { get; set; }

    public string? UserAgent { get; set; }

    public string Source { get; set; } = "web";

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
