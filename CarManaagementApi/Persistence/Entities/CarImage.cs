using System;
using System.Collections.Generic;

namespace CarManaagementApi.Persistence.Entities;

public partial class CarImage
{
    public long CarImageId { get; set; }

    public string CarId { get; set; } = null!;

    public string ImageUrl { get; set; } = null!;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Car Car { get; set; } = null!;
}
