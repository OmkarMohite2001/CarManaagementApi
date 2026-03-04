using System;
using System.Collections.Generic;

namespace CarManaagementApi.Persistence.Entities;

public partial class RolePermission
{
    public string RoleCode { get; set; } = null!;

    public string ModuleName { get; set; } = null!;

    public bool CanView { get; set; }

    public bool CanCreate { get; set; }

    public bool CanEdit { get; set; }

    public bool CanDelete { get; set; }

    public bool CanApprove { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Role RoleCodeNavigation { get; set; } = null!;
}
