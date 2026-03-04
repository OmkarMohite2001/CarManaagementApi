using CarManaagementApi.Persistence;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public class DashboardController : ApiControllerBase
{
    private readonly RentXDbContext _db;

    public DashboardController(RentXDbContext db)
    {
        _db = db;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? branchCodes)
    {
        var today = DateTime.UtcNow.Date;
        var branches = SplitCsv(branchCodes);

        var query = _db.Bookings.AsNoTracking().AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(x => x.PickAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.DropAt <= to.Value);
        }

        if (branches.Count > 0)
        {
            query = query.Where(x => branches.Contains(x.LocationCode));
        }

        var bookings = await query.ToListAsync();
        var totalRevenueToday = bookings
            .Where(x => x.PickAt.Date == today && (x.Status == "approved" || x.Status == "ongoing" || x.Status == "completed"))
            .Sum(x => x.DailyPrice * x.Days);
        var activeRentals = bookings.Count(x => x.Status == "ongoing");
        var newBookings = bookings.Count(x => x.CreatedAt.Date == today);

        var activeCars = await _db.Cars.CountAsync(x => x.IsActive);
        var utilizedCars = bookings
            .Where(x => x.Status == "approved" || x.Status == "ongoing")
            .Select(x => x.CarId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var fleetUtilizationPercent = activeCars == 0 ? 0 : (int)Math.Round((double)utilizedCars * 100 / activeCars, MidpointRounding.AwayFromZero);

        return OkResponse(new
        {
            totalRevenueToday,
            activeRentals,
            newBookings,
            fleetUtilizationPercent
        });
    }

    [HttpGet("fleet-by-location")]
    public async Task<IActionResult> GetFleetByLocation()
    {
        var branches = await _db.Branches.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        var cars = await _db.Cars.AsNoTracking().Where(x => x.IsActive).ToListAsync();
        var activeBookingCarIds = await _db.Bookings
            .AsNoTracking()
            .Where(x => x.Status == "approved" || x.Status == "ongoing")
            .Select(x => x.CarId)
            .Distinct()
            .ToListAsync();

        var activeSet = activeBookingCarIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var data = branches.Select(branch =>
        {
            var carsAtBranch = cars.Where(x => x.BranchId.Equals(branch.BranchId, StringComparison.OrdinalIgnoreCase)).ToList();
            var used = carsAtBranch.Count(x => activeSet.Contains(x.CarId));
            var total = carsAtBranch.Count;
            return (object)new
            {
                locationName = branch.Name,
                used,
                total
            };
        }).ToList();

        return OkResponse<IEnumerable<object>>(data);
    }

    [HttpGet("revenue-trend")]
    public async Task<IActionResult> GetRevenueTrend([FromQuery] int days = 7)
    {
        var safeDays = days <= 0 ? 7 : Math.Min(days, 60);
        var start = DateTime.UtcNow.Date.AddDays(-safeDays + 1);

        var bookings = await _db.Bookings
            .AsNoTracking()
            .Where(x => x.PickAt >= start && (x.Status == "approved" || x.Status == "ongoing" || x.Status == "completed"))
            .ToListAsync();

        var points = Enumerable.Range(0, safeDays)
            .Select(offset =>
            {
                var day = start.AddDays(offset);
                var total = bookings
                    .Where(x => x.PickAt.Date == day)
                    .Sum(x => x.DailyPrice * x.Days);
                return (int)total;
            })
            .ToList();

        return OkResponse(new { days = safeDays, points });
    }

    [HttpGet("recent-bookings")]
    public async Task<IActionResult> GetRecentBookings([FromQuery] int limit = 10)
    {
        var safeLimit = limit <= 0 ? 10 : Math.Min(limit, 100);

        var data = await _db.Bookings
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Car)
            .OrderByDescending(x => x.CreatedAt)
            .Take(safeLimit)
            .Select(x => (object)new
            {
                id = x.BookingId,
                pickAt = x.PickAt,
                dropAt = x.DropAt,
                locationCode = x.LocationCode,
                customerName = x.Customer.Name,
                carName = x.Car.Brand + " " + x.Car.Model,
                status = x.Status,
                days = x.Days,
                dailyPrice = x.DailyPrice
            })
            .ToListAsync();

        return OkResponse<IEnumerable<object>>(data);
    }

    [HttpGet("activity-timeline")]
    public async Task<IActionResult> GetActivityTimeline([FromQuery] int limit = 20)
    {
        var safeLimit = limit <= 0 ? 20 : Math.Min(limit, 100);

        var bookingEvents = await _db.Bookings
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(safeLimit)
            .Select(x => new
            {
                occurredAt = x.CreatedAt,
                label = "Booking " + x.BookingId + " created",
                type = "booking"
            })
            .ToListAsync();

        var maintenanceEvents = await _db.MaintenanceBlocks
            .AsNoTracking()
            .OrderByDescending(x => x.BlockFrom)
            .Take(safeLimit)
            .Select(x => new
            {
                occurredAt = x.CreatedAt,
                label = "Maintenance " + x.MaintenanceId + " scheduled for " + x.CarId,
                type = "maintenance"
            })
            .ToListAsync();

        var data = bookingEvents
            .Concat(maintenanceEvents)
            .OrderByDescending(x => x.occurredAt)
            .Take(safeLimit)
            .Cast<object>()
            .ToList();

        return OkResponse<IEnumerable<object>>(data);
    }

    [HttpGet("fleet-health")]
    public async Task<IActionResult> GetFleetHealth()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var totalCars = await _db.Cars.CountAsync(x => x.IsActive);
        var blockedCars = await _db.MaintenanceBlocks
            .Where(x => x.BlockFrom <= today && x.BlockTo >= today)
            .Select(x => x.CarId)
            .Distinct()
            .CountAsync();

        var readyPercent = totalCars == 0 ? 100 : Math.Max(0, 100 - (int)Math.Round((double)blockedCars * 100 / totalCars));

        var serviceDueSoon = await _db.MaintenanceBlocks.CountAsync(x => x.BlockFrom <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)));
        var criticalAlerts = await _db.MaintenanceBlocks.CountAsync(x => x.MaintenanceType == "repair");

        var score = Math.Max(0, Math.Min(100, (readyPercent + (100 - Math.Min(100, serviceDueSoon * 5)) + (100 - Math.Min(100, criticalAlerts * 10))) / 3));

        return OkResponse(new
        {
            score,
            metrics = new object[]
            {
                new { label = "Ready for dispatch", value = readyPercent, tone = "good" },
                new { label = "Service due soon", value = serviceDueSoon, tone = "warn" },
                new { label = "Critical alerts", value = criticalAlerts, tone = "risk" }
            }
        });
    }

    private static List<string> SplitCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
