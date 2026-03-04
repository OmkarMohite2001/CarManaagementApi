using System.Text;
using CarManaagementApi.Services;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/reports")]
public class ReportsController : ApiControllerBase
{
    private readonly IRentXStore _store;

    public ReportsController(IRentXStore store)
    {
        _store = store;
    }

    [HttpGet("bookings")]
    public IActionResult GetBookingReport(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? locations,
        [FromQuery] string? types,
        [FromQuery] string? statuses,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var locationSet = SplitCsv(locations);
        var typeSet = SplitCsv(types);
        var statusSet = SplitCsv(statuses);

        lock (_store.SyncRoot)
        {
            var query = _store.Bookings.AsEnumerable();

            if (from.HasValue)
            {
                query = query.Where(x => DateOnly.FromDateTime(x.PickAt) >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(x => DateOnly.FromDateTime(x.PickAt) <= to.Value);
            }

            if (locationSet.Count > 0)
            {
                query = query.Where(x => locationSet.Contains(x.LocationCode, StringComparer.OrdinalIgnoreCase));
            }

            if (typeSet.Count > 0)
            {
                query = query.Where(x => typeSet.Contains(x.CarType, StringComparer.OrdinalIgnoreCase));
            }

            if (statusSet.Count > 0)
            {
                query = query.Where(x => statusSet.Contains(x.Status, StringComparer.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(x =>
                    x.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.CustomerName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.CarName.Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            var shaped = query
                .OrderByDescending(x => x.PickAt)
                .Select(x => (object)new
                {
                    id = x.Id,
                    date = DateOnly.FromDateTime(x.PickAt),
                    locationCode = x.LocationCode,
                    customerName = x.CustomerName,
                    carName = x.CarName,
                    carType = x.CarType,
                    status = x.Status,
                    days = x.Days,
                    dailyPrice = x.DailyPrice
                });

            var (items, meta) = shaped.Paginate(page, pageSize);
            return OkResponse<IEnumerable<object>>(items, meta: meta);
        }
    }

    [HttpGet("bookings/summary")]
    public IActionResult GetBookingSummary(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? locations,
        [FromQuery] string? statuses)
    {
        var locationSet = SplitCsv(locations);
        var statusSet = SplitCsv(statuses);

        lock (_store.SyncRoot)
        {
            var query = _store.Bookings.AsEnumerable();

            if (from.HasValue)
            {
                query = query.Where(x => DateOnly.FromDateTime(x.PickAt) >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(x => DateOnly.FromDateTime(x.PickAt) <= to.Value);
            }

            if (locationSet.Count > 0)
            {
                query = query.Where(x => locationSet.Contains(x.LocationCode, StringComparer.OrdinalIgnoreCase));
            }

            if (statusSet.Count > 0)
            {
                query = query.Where(x => statusSet.Contains(x.Status, StringComparer.OrdinalIgnoreCase));
            }

            var list = query.ToList();
            var totalBookings = list.Count;
            var totalRevenue = list.Where(x => x.Status is "approved" or "ongoing" or "completed").Sum(x => x.DailyPrice * x.Days);
            var cancelled = list.Count(x => x.Status == "cancelled");
            var avgTicket = totalBookings == 0 ? 0 : (int)Math.Round(totalRevenue / totalBookings, MidpointRounding.AwayFromZero);

            return OkResponse(new
            {
                totalBookings,
                totalRevenue,
                cancelled,
                avgTicket
            });
        }
    }

    [HttpGet("bookings/export")]
    public IActionResult ExportBookings([FromQuery] string format = "csv")
    {
        if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Only csv export is supported.");
        }

        List<object> rows;
        lock (_store.SyncRoot)
        {
            rows = _store.Bookings
                .OrderByDescending(x => x.PickAt)
                .Select(x => (object)new
                {
                    x.Id,
                    Date = DateOnly.FromDateTime(x.PickAt),
                    x.LocationCode,
                    x.CustomerName,
                    x.CarName,
                    x.CarType,
                    x.Status,
                    x.Days,
                    x.DailyPrice
                }).ToList();
        }

        var sb = new StringBuilder();
        sb.AppendLine("id,date,locationCode,customerName,carName,carType,status,days,dailyPrice");
        foreach (dynamic row in rows)
        {
            sb.AppendLine($"{row.Id},{row.Date},{row.LocationCode},{EscapeCsv(row.CustomerName)},{EscapeCsv(row.CarName)},{row.CarType},{row.Status},{row.Days},{row.DailyPrice}");
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"report-bookings-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    private static List<string> SplitCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Contains(',') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
