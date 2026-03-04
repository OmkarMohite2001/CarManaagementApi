using System;
using System.Collections.Generic;

namespace CarManaagementApi.Persistence.Entities;

public partial class ReturnInspectionDamage
{
    public long DamageId { get; set; }

    public string InspectionId { get; set; } = null!;

    public string Part { get; set; } = null!;

    public string Severity { get; set; } = null!;

    public decimal EstCost { get; set; }

    public string? Notes { get; set; }

    public virtual ReturnInspection Inspection { get; set; } = null!;

    public virtual ICollection<ReturnInspectionDamagePhoto> ReturnInspectionDamagePhotos { get; set; } = new List<ReturnInspectionDamagePhoto>();
}
