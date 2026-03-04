using System.ComponentModel.DataAnnotations;

namespace CarManaagementApi.Models;

public class Car
{
    public int CarId { get; set; }

    [Required]
    [StringLength(100)]
    public string Brand { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Model { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Variant { get; set; }

    [Required]
    [StringLength(20)]
    public string RegistrationNumber { get; set; } = string.Empty;

    [Range(1980, 2100)]
    public short ManufactureYear { get; set; }

    [Required]
    [StringLength(20)]
    public string FuelType { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Transmission { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    public bool IsAvailable { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
