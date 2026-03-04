using System;
using System.Collections.Generic;

namespace CarManaagementApi.Persistence.Entities;

public partial class Car
{
    public string CarId { get; set; } = null!;

    public string Brand { get; set; } = null!;

    public string Model { get; set; } = null!;

    public string CarType { get; set; } = null!;

    public string Fuel { get; set; } = null!;

    public string Transmission { get; set; } = null!;

    public byte Seats { get; set; }

    public decimal DailyPrice { get; set; }

    public string RegNo { get; set; } = null!;

    public int Odometer { get; set; }

    public string BranchId { get; set; } = null!;

    public decimal? Rating { get; set; }

    public string? PrimaryImageUrl { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual Branch Branch { get; set; } = null!;

    public virtual ICollection<CarImage> CarImages { get; set; } = new List<CarImage>();

    public virtual ICollection<MaintenanceBlock> MaintenanceBlocks { get; set; } = new List<MaintenanceBlock>();

    public virtual ICollection<ReturnInspection> ReturnInspections { get; set; } = new List<ReturnInspection>();
}
