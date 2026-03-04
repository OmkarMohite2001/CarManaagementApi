using System;
using System.Collections.Generic;

namespace CarManaagementApi.Persistence.Entities;

public partial class Customer
{
    public string CustomerId { get; set; } = null!;

    public string CustomerType { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string? Email { get; set; }

    public DateOnly? Dob { get; set; }

    public string KycType { get; set; } = null!;

    public string KycNumber { get; set; } = null!;

    public string? DlNumber { get; set; }

    public DateOnly? DlExpiry { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? Pincode { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
