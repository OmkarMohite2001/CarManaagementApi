using System;
using System.Collections.Generic;

namespace CarManaagementApi.Persistence.Entities;

public partial class ReturnInspectionDamagePhoto
{
    public long DamagePhotoId { get; set; }

    public long DamageId { get; set; }

    public string PhotoUrl { get; set; } = null!;

    public virtual ReturnInspectionDamage Damage { get; set; } = null!;
}
