using System;
using System.Collections.Generic;

namespace CarManaagementApi.Persistence.Entities;

public partial class Role
{
    public string RoleCode { get; set; } = null!;

    public string RoleName { get; set; } = null!;

    public bool IsSystem { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
