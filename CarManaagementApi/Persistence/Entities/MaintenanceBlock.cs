using System;
using System.Collections.Generic;

namespace CarManaagementApi.Persistence.Entities;

public partial class MaintenanceBlock
{
    public string MaintenanceId { get; set; } = null!;

    public string CarId { get; set; } = null!;

    public string MaintenanceType { get; set; } = null!;

    public DateOnly BlockFrom { get; set; }

    public DateOnly BlockTo { get; set; }

    public int Days { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Car Car { get; set; } = null!;
}
