using System;
using System.Collections.Generic;

namespace CarManaagementApi.Persistence.Entities;

public partial class ReturnInspection
{
    public string InspectionId { get; set; } = null!;

    public string BookingId { get; set; } = null!;

    public string CarId { get; set; } = null!;

    public int Odometer { get; set; }

    public byte FuelPercent { get; set; }

    public bool CleaningRequired { get; set; }

    public int LateHours { get; set; }

    public decimal LateFeePerHour { get; set; }

    public decimal Deposit { get; set; }

    public string? Notes { get; set; }

    public decimal TotalDamage { get; set; }

    public decimal FuelCharge { get; set; }

    public decimal CleaningCharge { get; set; }

    public decimal LateFee { get; set; }

    public decimal SubTotal { get; set; }

    public decimal NetPayable { get; set; }

    public decimal Refund { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Booking Booking { get; set; } = null!;

    public virtual Car Car { get; set; } = null!;

    public virtual ICollection<ReturnInspectionDamage> ReturnInspectionDamages { get; set; } = new List<ReturnInspectionDamage>();
}
