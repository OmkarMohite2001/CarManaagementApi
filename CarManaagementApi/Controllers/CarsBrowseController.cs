using CarManaagementApi.Persistence;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/cars")]
public class CarsBrowseController : ApiControllerBase
{
    private readonly RentXDbContext _db;

    public CarsBrowseController(RentXDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetCars(
        [FromQuery] string? q,
        [FromQuery] string? type,
        [FromQuery] string? transmission,
        [FromQuery] string? fuel,
        [FromQuery] int? seatsMin,
        [FromQuery] decimal? priceMax,
        [FromQuery] string? locationCode,
        [FromQuery] string? sortKey,
        [FromQuery] string? sortDir,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.Cars
            .AsNoTracking()
            .Where(x => x.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                x.Brand.Contains(q) ||
                x.Model.Contains(q) ||
                x.CarId.Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(x => x.CarType == type);
        }

        if (!string.IsNullOrWhiteSpace(transmission))
        {
            query = query.Where(x => x.Transmission == transmission);
        }

        if (!string.IsNullOrWhiteSpace(fuel))
        {
            query = query.Where(x => x.Fuel == fuel);
        }

        if (seatsMin.HasValue)
        {
            query = query.Where(x => x.Seats >= seatsMin.Value);
        }

        if (priceMax.HasValue)
        {
            query = query.Where(x => x.DailyPrice <= priceMax.Value);
        }

        if (!string.IsNullOrWhiteSpace(locationCode))
        {
            query = query.Where(x => x.BranchId == locationCode);
        }

        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = (sortKey ?? string.Empty).ToLowerInvariant() switch
        {
            "price" => desc ? query.OrderByDescending(x => x.DailyPrice) : query.OrderBy(x => x.DailyPrice),
            "rating" => desc ? query.OrderByDescending(x => x.Rating) : query.OrderBy(x => x.Rating),
            "seats" => desc ? query.OrderByDescending(x => x.Seats) : query.OrderBy(x => x.Seats),
            _ => query.OrderBy(x => x.Brand).ThenBy(x => x.Model)
        };

        var shaped = await query.Select(x => (object)new
        {
            id = x.CarId,
            brand = x.Brand,
            model = x.Model,
            type = x.CarType,
            seats = x.Seats,
            transmission = x.Transmission,
            fuel = x.Fuel,
            dailyPrice = x.DailyPrice,
            rating = x.Rating ?? 0,
            imageUrl = x.PrimaryImageUrl,
            locationCodes = new[] { x.BranchId },
            active = x.IsActive
        }).ToListAsync();

        var (items, meta) = shaped.Paginate(page, pageSize);
        return OkResponse<IEnumerable<object>>(items, meta: meta);
    }

    [HttpGet("{carId}")]
    public async Task<IActionResult> GetCarById(string carId)
    {
        var car = await _db.Cars
            .AsNoTracking()
            .Include(x => x.CarImages)
            .FirstOrDefaultAsync(x => x.CarId == carId);

        if (car is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Car not found");
        }

        return OkResponse(new
        {
            id = car.CarId,
            brand = car.Brand,
            model = car.Model,
            type = car.CarType,
            seats = car.Seats,
            transmission = car.Transmission,
            fuel = car.Fuel,
            dailyPrice = car.DailyPrice,
            rating = car.Rating ?? 0,
            imageUrl = car.PrimaryImageUrl,
            imageUrls = car.CarImages.OrderBy(x => x.SortOrder).Select(x => x.ImageUrl).ToList(),
            locationCodes = new[] { car.BranchId },
            regNo = car.RegNo,
            odometer = car.Odometer,
            active = car.IsActive
        });
    }

    [HttpGet("{carId}/availability")]
    public async Task<IActionResult> GetAvailability(
        string carId,
        [FromQuery] DateTime pickupAt,
        [FromQuery] DateTime dropAt,
        [FromQuery] string? locationCode)
    {
        if (dropAt <= pickupAt)
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "dropAt must be later than pickupAt");
        }

        var car = await _db.Cars
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CarId == carId && x.IsActive);

        if (car is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Car not found");
        }

        if (!string.IsNullOrWhiteSpace(locationCode) && car.BranchId != locationCode)
        {
            return OkResponse(new
            {
                carId,
                available = false,
                reason = "Car is not available at location " + locationCode + "."
            });
        }

        var hasBookingOverlap = await _db.Bookings.AnyAsync(x =>
            x.CarId == carId
            && x.Status != "cancelled"
            && pickupAt < x.DropAt
            && dropAt > x.PickAt);

        var pickupDate = DateOnly.FromDateTime(pickupAt);
        var dropDate = DateOnly.FromDateTime(dropAt);
        var hasMaintenanceOverlap = await _db.MaintenanceBlocks.AnyAsync(x =>
            x.CarId == carId
            && pickupDate <= x.BlockTo
            && dropDate >= x.BlockFrom);

        return OkResponse(new
        {
            carId,
            available = !hasBookingOverlap && !hasMaintenanceOverlap,
            reason = hasBookingOverlap
                ? "Overlaps with existing booking"
                : hasMaintenanceOverlap ? "Overlaps with maintenance block" : null
        });
    }
}
