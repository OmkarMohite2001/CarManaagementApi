using System;
using System.Collections.Generic;

namespace CarManaagementApi.Persistence.Entities;

public partial class Booking
{
    public string BookingId { get; set; } = null!;

    public string CustomerId { get; set; } = null!;

    public string CarId { get; set; } = null!;

    public string LocationCode { get; set; } = null!;

    public DateTime PickAt { get; set; }

    public DateTime DropAt { get; set; }

    public decimal DailyPrice { get; set; }

    public int Days { get; set; }

    public string Status { get; set; } = null!;

    public string? CancelReason { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Car Car { get; set; } = null!;

    public virtual Customer Customer { get; set; } = null!;

    public virtual Branch LocationCodeNavigation { get; set; } = null!;

    public virtual ICollection<ReturnInspection> ReturnInspections { get; set; } = new List<ReturnInspection>();
}
