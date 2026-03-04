using CarManaagementApi.Data;
using CarManaagementApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarManaagementApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CarsController : ControllerBase
{
    private static readonly HashSet<string> AllowedFuelTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Petrol",
        "Diesel",
        "CNG",
        "EV",
        "Hybrid"
    };

    private static readonly HashSet<string> AllowedTransmissions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Manual",
        "Automatic"
    };

    private readonly AppDbContext _context;

    public CarsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Car>>> GetAll()
    {
        var cars = await _context.Cars
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(cars);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Car>> GetById(int id)
    {
        var car = await _context.Cars
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CarId == id);

        if (car is null)
        {
            return NotFound();
        }

        return Ok(car);
    }

    [HttpPost]
    public async Task<ActionResult<Car>> Create(Car request)
    {
        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        if (await _context.Cars.AnyAsync(x => x.RegistrationNumber == request.RegistrationNumber))
        {
            return BadRequest("Registration number already exists.");
        }

        request.CreatedAt = DateTime.UtcNow;
        request.UpdatedAt = null;
        request.FuelType = NormalizeFuelType(request.FuelType);
        request.Transmission = NormalizeTransmission(request.Transmission);

        _context.Cars.Add(request);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = request.CarId }, request);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Car>> Update(int id, Car request)
    {
        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var existingCar = await _context.Cars.FirstOrDefaultAsync(x => x.CarId == id);
        if (existingCar is null)
        {
            return NotFound();
        }

        var duplicateRegNumberExists = await _context.Cars.AnyAsync(x =>
            x.CarId != id && x.RegistrationNumber == request.RegistrationNumber);

        if (duplicateRegNumberExists)
        {
            return BadRequest("Registration number already exists.");
        }

        existingCar.Brand = request.Brand;
        existingCar.Model = request.Model;
        existingCar.Variant = request.Variant;
        existingCar.RegistrationNumber = request.RegistrationNumber;
        existingCar.ManufactureYear = request.ManufactureYear;
        existingCar.FuelType = NormalizeFuelType(request.FuelType);
        existingCar.Transmission = NormalizeTransmission(request.Transmission);
        existingCar.Price = request.Price;
        existingCar.IsAvailable = request.IsAvailable;
        existingCar.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(existingCar);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var car = await _context.Cars.FirstOrDefaultAsync(x => x.CarId == id);
        if (car is null)
        {
            return NotFound();
        }

        _context.Cars.Remove(car);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static string? ValidateRequest(Car request)
    {
        var maxAllowedYear = DateTime.UtcNow.Year + 1;
        if (request.ManufactureYear < 1980 || request.ManufactureYear > maxAllowedYear)
        {
            return $"ManufactureYear must be between 1980 and {maxAllowedYear}.";
        }

        if (!AllowedFuelTypes.Contains(request.FuelType))
        {
            return "FuelType must be one of: Petrol, Diesel, CNG, EV, Hybrid.";
        }

        if (!AllowedTransmissions.Contains(request.Transmission))
        {
            return "Transmission must be one of: Manual, Automatic.";
        }

        return null;
    }

    private static string NormalizeFuelType(string value)
    {
        return AllowedFuelTypes.First(x => x.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeTransmission(string value)
    {
        return AllowedTransmissions.First(x => x.Equals(value, StringComparison.OrdinalIgnoreCase));
    }
}
