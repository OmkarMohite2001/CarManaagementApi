using System.Text;
using CarManaagementApi.Persistence;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/reports")]
public class ReportsController : ApiControllerBase
{
    private readonly RentXDbContext _db;

    public ReportsController(RentXDbContext db)
    {
        _db = db;
    }

    [HttpGet("bookings")]
    public async Task<IActionResult> GetBookingReport(
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

        var query = _db.Bookings
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Car)
            .AsQueryable();

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
            query = query.Where(x => locationSet.Contains(x.LocationCode));
        }

        if (typeSet.Count > 0)
        {
            query = query.Where(x => typeSet.Contains(x.Car.CarType));
        }

        if (statusSet.Count > 0)
        {
            query = query.Where(x => statusSet.Contains(x.Status));
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                x.BookingId.Contains(q)
                || x.Customer.Name.Contains(q)
                || x.Car.Brand.Contains(q)
                || x.Car.Model.Contains(q));
        }

        var rows = await query
            .OrderByDescending(x => x.PickAt)
            .Select(x => (object)new
            {
                id = x.BookingId,
                date = DateOnly.FromDateTime(x.PickAt),
                locationCode = x.LocationCode,
                customerName = x.Customer.Name,
                carName = x.Car.Brand + " " + x.Car.Model,
                carType = x.Car.CarType,
                status = x.Status,
                days = x.Days,
                dailyPrice = x.DailyPrice
            })
            .ToListAsync();

        var (items, meta) = rows.Paginate(page, pageSize);
        return OkResponse<IEnumerable<object>>(items, meta: meta);
    }

    [HttpGet("bookings/summary")]
    public async Task<IActionResult> GetBookingSummary(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? locations,
        [FromQuery] string? statuses)
    {
        var locationSet = SplitCsv(locations);
        var statusSet = SplitCsv(statuses);

        var query = _db.Bookings.AsNoTracking().AsQueryable();

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
            query = query.Where(x => locationSet.Contains(x.LocationCode));
        }

        if (statusSet.Count > 0)
        {
            query = query.Where(x => statusSet.Contains(x.Status));
        }

        var list = await query.ToListAsync();
        var totalBookings = list.Count;
        var totalRevenue = list.Where(x => x.Status == "approved" || x.Status == "ongoing" || x.Status == "completed").Sum(x => x.DailyPrice * x.Days);
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

    [HttpGet("bookings/export")]
    public async Task<IActionResult> ExportBookings([FromQuery] string format = "csv")
    {
        if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Only csv export is supported.");
        }

        var rows = await _db.Bookings
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Car)
            .OrderByDescending(x => x.PickAt)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("id,date,locationCode,customerName,carName,carType,status,days,dailyPrice");
        foreach (var row in rows)
        {
            sb.AppendLine($"{row.BookingId},{DateOnly.FromDateTime(row.PickAt)},{row.LocationCode},{EscapeCsv(row.Customer.Name)},{EscapeCsv(row.Car.Brand + " " + row.Car.Model)},{row.Car.CarType},{row.Status},{row.Days},{row.DailyPrice}");
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "report-bookings-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".csv");
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

        return value.Contains(',') ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
