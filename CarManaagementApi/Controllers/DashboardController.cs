using CarManaagementApi.Services;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public class DashboardController : ApiControllerBase
{
    private readonly IRentXStore _store;

    public DashboardController(IRentXStore store)
    {
        _store = store;
    }

    [HttpGet("summary")]
    public IActionResult GetSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? branchCodes)
    {
        var today = DateTime.UtcNow.Date;
        var branches = SplitCsv(branchCodes);

        lock (_store.SyncRoot)
        {
            var filtered = _store.Bookings.AsEnumerable();
            if (from.HasValue)
            {
                filtered = filtered.Where(x => x.PickAt >= from.Value);
            }

            if (to.HasValue)
            {
                filtered = filtered.Where(x => x.DropAt <= to.Value);
            }

            if (branches.Count > 0)
            {
                filtered = filtered.Where(x => branches.Contains(x.LocationCode, StringComparer.OrdinalIgnoreCase));
            }

            var filteredList = filtered.ToList();
            var totalRevenueToday = filteredList
                .Where(x => x.PickAt.Date == today && x.Status is "approved" or "ongoing" or "completed")
                .Sum(x => x.DailyPrice * x.Days);
            var activeRentals = filteredList.Count(x => x.Status == "ongoing");
            var newBookings = filteredList.Count(x => x.CreatedAt.Date == today);
            var activeCars = _store.Cars.Count(x => x.Active);
            var utilizedCars = filteredList
                .Where(x => x.Status is "approved" or "ongoing")
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
    }

    [HttpGet("fleet-by-location")]
    public IActionResult GetFleetByLocation()
    {
        lock (_store.SyncRoot)
        {
            var activeBookingCarIds = _store.Bookings
                .Where(x => x.Status is "approved" or "ongoing")
                .Select(x => x.CarId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var data = _store.Branches.Select(branch =>
            {
                var carsAtBranch = _store.Cars.Where(x => x.BranchId.Equals(branch.Id, StringComparison.OrdinalIgnoreCase) && x.Active).ToList();
                var used = carsAtBranch.Count(x => activeBookingCarIds.Contains(x.Id));
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
    }

    [HttpGet("revenue-trend")]
    public IActionResult GetRevenueTrend([FromQuery] int days = 7)
    {
        var safeDays = days <= 0 ? 7 : Math.Min(days, 60);
        var start = DateTime.UtcNow.Date.AddDays(-safeDays + 1);

        lock (_store.SyncRoot)
        {
            var points = Enumerable.Range(0, safeDays)
                .Select(offset =>
                {
                    var day = start.AddDays(offset);
                    var total = _store.Bookings
                        .Where(x => x.PickAt.Date == day && x.Status is "approved" or "ongoing" or "completed")
                        .Sum(x => x.DailyPrice * x.Days);
                    return (int)total;
                })
                .ToList();

            return OkResponse(new { days = safeDays, points });
        }
    }

    [HttpGet("recent-bookings")]
    public IActionResult GetRecentBookings([FromQuery] int limit = 10)
    {
        var safeLimit = limit <= 0 ? 10 : Math.Min(limit, 100);

        lock (_store.SyncRoot)
        {
            var data = _store.Bookings
                .OrderByDescending(x => x.CreatedAt)
                .Take(safeLimit)
                .Select(x => (object)new
                {
                    id = x.Id,
                    pickAt = x.PickAt,
                    dropAt = x.DropAt,
                    locationCode = x.LocationCode,
                    customerName = x.CustomerName,
                    carName = x.CarName,
                    status = x.Status,
                    days = x.Days,
                    dailyPrice = x.DailyPrice
                })
                .ToList();

            return OkResponse<IEnumerable<object>>(data);
        }
    }

    [HttpGet("activity-timeline")]
    public IActionResult GetActivityTimeline([FromQuery] int limit = 20)
    {
        var safeLimit = limit <= 0 ? 20 : Math.Min(limit, 100);

        lock (_store.SyncRoot)
        {
            var bookingEvents = _store.Bookings.Select(x => new
            {
                occurredAt = x.CreatedAt,
                label = $"Booking {x.Id} created",
                type = "booking"
            });

            var maintenanceEvents = _store.MaintenanceBlocks.Select(x => new
            {
                occurredAt = x.From.ToDateTime(new TimeOnly(0, 0), DateTimeKind.Utc),
                label = $"Maintenance {x.Id} scheduled for {x.CarId}",
                type = "maintenance"
            });

            var data = bookingEvents
                .Concat(maintenanceEvents)
                .OrderByDescending(x => x.occurredAt)
                .Take(safeLimit)
                .Cast<object>()
                .ToList();

            return OkResponse<IEnumerable<object>>(data);
        }
    }

    [HttpGet("fleet-health")]
    public IActionResult GetFleetHealth()
    {
        lock (_store.SyncRoot)
        {
            var totalCars = _store.Cars.Count(x => x.Active);
            var blockedCars = _store.MaintenanceBlocks
                .Where(x => x.From <= DateOnly.FromDateTime(DateTime.UtcNow) && x.To >= DateOnly.FromDateTime(DateTime.UtcNow))
                .Select(x => x.CarId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var readyPercent = totalCars == 0 ? 100 : Math.Max(0, 100 - (int)Math.Round((double)blockedCars * 100 / totalCars));
            var serviceDueSoon = _store.MaintenanceBlocks.Count(x => x.From <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)));
            var criticalAlerts = _store.MaintenanceBlocks.Count(x => x.Type == "repair");

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
