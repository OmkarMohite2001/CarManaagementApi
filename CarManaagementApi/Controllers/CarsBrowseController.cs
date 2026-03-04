using CarManaagementApi.Services;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/cars")]
public class CarsBrowseController : ApiControllerBase
{
    private readonly IRentXStore _store;

    public CarsBrowseController(IRentXStore store)
    {
        _store = store;
    }

    [HttpGet]
    public IActionResult GetCars(
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
        lock (_store.SyncRoot)
        {
            var query = _store.Cars.Where(x => x.Active).AsEnumerable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(x =>
                    x.Brand.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    x.Model.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    x.Id.Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(x => x.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(transmission))
            {
                query = query.Where(x => x.Transmission.Equals(transmission, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(fuel))
            {
                query = query.Where(x => x.Fuel.Equals(fuel, StringComparison.OrdinalIgnoreCase));
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
                query = query.Where(x => x.LocationCodes.Contains(locationCode, StringComparer.OrdinalIgnoreCase));
            }

            var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
            query = (sortKey ?? string.Empty).ToLowerInvariant() switch
            {
                "price" => desc ? query.OrderByDescending(x => x.DailyPrice) : query.OrderBy(x => x.DailyPrice),
                "rating" => desc ? query.OrderByDescending(x => x.Rating) : query.OrderBy(x => x.Rating),
                "seats" => desc ? query.OrderByDescending(x => x.Seats) : query.OrderBy(x => x.Seats),
                _ => query.OrderBy(x => x.Brand).ThenBy(x => x.Model)
            };

            var shaped = query.Select(x => (object)new
            {
                id = x.Id,
                brand = x.Brand,
                model = x.Model,
                type = x.Type,
                seats = x.Seats,
                transmission = x.Transmission,
                fuel = x.Fuel,
                dailyPrice = x.DailyPrice,
                rating = x.Rating,
                imageUrl = x.ImageUrl,
                locationCodes = x.LocationCodes,
                active = x.Active
            });

            var (items, meta) = shaped.Paginate(page, pageSize);
            return OkResponse<IEnumerable<object>>(items, meta: meta);
        }
    }

    [HttpGet("{carId}")]
    public IActionResult GetCarById(string carId)
    {
        lock (_store.SyncRoot)
        {
            var car = _store.Cars.FirstOrDefault(x => x.Id.Equals(carId, StringComparison.OrdinalIgnoreCase));
            if (car is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Car not found");
            }

            return OkResponse(new
            {
                id = car.Id,
                brand = car.Brand,
                model = car.Model,
                type = car.Type,
                seats = car.Seats,
                transmission = car.Transmission,
                fuel = car.Fuel,
                dailyPrice = car.DailyPrice,
                rating = car.Rating,
                imageUrl = car.ImageUrl,
                imageUrls = car.ImageUrls,
                locationCodes = car.LocationCodes,
                regNo = car.RegNo,
                odometer = car.Odometer,
                active = car.Active
            });
        }
    }

    [HttpGet("{carId}/availability")]
    public IActionResult GetAvailability(
        string carId,
        [FromQuery] DateTime pickupAt,
        [FromQuery] DateTime dropAt,
        [FromQuery] string? locationCode)
    {
        if (dropAt <= pickupAt)
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "dropAt must be later than pickupAt");
        }

        lock (_store.SyncRoot)
        {
            var car = _store.Cars.FirstOrDefault(x => x.Id.Equals(carId, StringComparison.OrdinalIgnoreCase) && x.Active);
            if (car is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Car not found");
            }

            if (!string.IsNullOrWhiteSpace(locationCode) && !car.LocationCodes.Contains(locationCode, StringComparer.OrdinalIgnoreCase))
            {
                return OkResponse(new
                {
                    carId,
                    available = false,
                    reason = $"Car is not available at location {locationCode}."
                });
            }

            var hasBookingOverlap = _store.Bookings.Any(x =>
                x.CarId.Equals(carId, StringComparison.OrdinalIgnoreCase)
                && x.Status is not "cancelled"
                && pickupAt < x.DropAt
                && dropAt > x.PickAt);

            var pickupDate = DateOnly.FromDateTime(pickupAt);
            var dropDate = DateOnly.FromDateTime(dropAt);
            var hasMaintenanceOverlap = _store.MaintenanceBlocks.Any(x =>
                x.CarId.Equals(carId, StringComparison.OrdinalIgnoreCase)
                && pickupDate <= x.To
                && dropDate >= x.From);

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
}
