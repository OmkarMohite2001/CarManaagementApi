using System.Text;
using CarManaagementApi.Persistence;
using CarManaagementApi.Persistence.Entities;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/bookings")]
public class BookingsController : ApiControllerBase
{
    private readonly RentXDbContext _db;

    public BookingsController(RentXDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetBookings(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? statuses,
        [FromQuery] string? types,
        [FromQuery] string? locations,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var statusSet = SplitCsv(statuses);
        var typeSet = SplitCsv(types);
        var locationSet = SplitCsv(locations);

        var query = _db.Bookings
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Car)
            .AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(x => x.PickAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.DropAt <= to.Value);
        }

        if (statusSet.Count > 0)
        {
            query = query.Where(x => statusSet.Contains(x.Status));
        }

        if (typeSet.Count > 0)
        {
            query = query.Where(x => typeSet.Contains(x.Car.CarType));
        }

        if (locationSet.Count > 0)
        {
            query = query.Where(x => locationSet.Contains(x.LocationCode));
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                x.BookingId.Contains(q) ||
                x.Customer.Name.Contains(q) ||
                x.Car.Brand.Contains(q) ||
                x.Car.Model.Contains(q));
        }

        var rows = await query
            .OrderByDescending(x => x.PickAt)
            .Select(x => (object)new
            {
                id = x.BookingId,
                pickAt = x.PickAt,
                dropAt = x.DropAt,
                locationCode = x.LocationCode,
                customerId = x.CustomerId,
                customerName = x.Customer.Name,
                carId = x.CarId,
                carName = x.Car.Brand + " " + x.Car.Model,
                carType = x.Car.CarType,
                status = x.Status,
                days = x.Days,
                dailyPrice = x.DailyPrice,
                cancelReason = x.CancelReason
            })
            .ToListAsync();

        var (items, meta) = rows.Paginate(page, pageSize);
        return OkResponse<IEnumerable<object>>(items, meta: meta);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBooking(CreateBookingRequest request)
    {
        if (request.DropAt <= request.PickAt)
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                new ApiErrorItem { Field = "dropAt", Code = "InvalidRange", Message = "dropAt must be later than pickAt." }
            ]);
        }

        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.CustomerId == request.CustomerId);
        if (customer is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Customer not found");
        }

        var car = await _db.Cars.FirstOrDefaultAsync(x => x.CarId == request.CarId && x.IsActive);
        if (car is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Car not found");
        }

        if (car.BranchId != request.LocationCode)
        {
            return ErrorResponse(StatusCodes.Status422UnprocessableEntity, "Car is not available at selected location");
        }

        var hasOverlap = await _db.Bookings.AnyAsync(x =>
            x.CarId == request.CarId
            && x.Status != "cancelled"
            && request.PickAt < x.DropAt
            && request.DropAt > x.PickAt);

        if (hasOverlap)
        {
            return ErrorResponse(StatusCodes.Status409Conflict, "Car is already booked for selected date range");
        }

        var bookingId = await IdGenerator.NextAsync(_db.Bookings.Select(x => x.BookingId), "BK");
        var days = Math.Max(1, (int)Math.Ceiling((request.DropAt - request.PickAt).TotalDays));

        var booking = new Booking
        {
            BookingId = bookingId,
            PickAt = DateTime.SpecifyKind(request.PickAt, DateTimeKind.Utc),
            DropAt = DateTime.SpecifyKind(request.DropAt, DateTimeKind.Utc),
            LocationCode = request.LocationCode,
            CustomerId = customer.CustomerId,
            CarId = car.CarId,
            Status = "pending",
            Days = days,
            DailyPrice = request.DailyPrice,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        return OkResponse(new
        {
            id = booking.BookingId,
            pickAt = booking.PickAt,
            dropAt = booking.DropAt,
            locationCode = booking.LocationCode,
            customerId = booking.CustomerId,
            customerName = customer.Name,
            carId = booking.CarId,
            carName = car.Brand + " " + car.Model,
            carType = car.CarType,
            status = booking.Status,
            days = booking.Days,
            dailyPrice = booking.DailyPrice,
            cancelReason = booking.CancelReason
        }, "Booking created");
    }

    [HttpGet("{bookingId}")]
    public async Task<IActionResult> GetBookingById(string bookingId)
    {
        var booking = await _db.Bookings
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Car)
            .FirstOrDefaultAsync(x => x.BookingId == bookingId);

        if (booking is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Booking not found");
        }

        return OkResponse(new
        {
            id = booking.BookingId,
            pickAt = booking.PickAt,
            dropAt = booking.DropAt,
            locationCode = booking.LocationCode,
            customerId = booking.CustomerId,
            customerName = booking.Customer.Name,
            carId = booking.CarId,
            carName = booking.Car.Brand + " " + booking.Car.Model,
            carType = booking.Car.CarType,
            status = booking.Status,
            days = booking.Days,
            dailyPrice = booking.DailyPrice,
            notes = booking.Notes,
            cancelReason = booking.CancelReason
        });
    }

    [HttpPatch("{bookingId}/status")]
    public async Task<IActionResult> PatchBookingStatus(string bookingId, PatchBookingStatusRequest request)
    {
        if (!IsAllowedAction(request.Action))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                new ApiErrorItem { Field = "action", Code = "InvalidAction", Message = "Allowed actions: approve, start, complete, cancel." }
            ]);
        }

        var booking = await _db.Bookings.FirstOrDefaultAsync(x => x.BookingId == bookingId);
        if (booking is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Booking not found");
        }

        var transition = MapTransition(booking.Status, request.Action);
        if (transition is null)
        {
            return ErrorResponse(StatusCodes.Status422UnprocessableEntity, "Cannot '" + request.Action + "' when status is '" + booking.Status + "'.");
        }

        booking.Status = transition;
        booking.CancelReason = request.Action.Equals("cancel", StringComparison.OrdinalIgnoreCase) ? request.Reason : null;
        booking.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return OkResponse(new
        {
            id = booking.BookingId,
            status = booking.Status,
            cancelReason = booking.CancelReason
        }, "Booking status updated");
    }

    [HttpPost("bulk-status")]
    public async Task<IActionResult> BulkStatus(BulkStatusRequest request)
    {
        if (!IsAllowedAction(request.Action))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                new ApiErrorItem { Field = "action", Code = "InvalidAction", Message = "Allowed actions: approve, start, complete, cancel." }
            ]);
        }

        var ids = request.BookingIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var bookings = await _db.Bookings.Where(x => ids.Contains(x.BookingId)).ToListAsync();

        var updated = new List<object>();
        var failed = new List<object>();

        foreach (var id in ids)
        {
            var booking = bookings.FirstOrDefault(x => x.BookingId.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (booking is null)
            {
                failed.Add(new { bookingId = id, reason = "Not found" });
                continue;
            }

            var transition = MapTransition(booking.Status, request.Action);
            if (transition is null)
            {
                failed.Add(new { bookingId = id, reason = "Cannot '" + request.Action + "' from '" + booking.Status + "'" });
                continue;
            }

            booking.Status = transition;
            booking.CancelReason = request.Action.Equals("cancel", StringComparison.OrdinalIgnoreCase) ? request.Reason : null;
            booking.UpdatedAt = DateTime.UtcNow;
            updated.Add(new { bookingId = id, status = booking.Status });
        }

        await _db.SaveChangesAsync();

        return OkResponse(new { updated, failed }, "Bulk status operation completed");
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportCsv([FromQuery] string format = "csv")
    {
        if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Only csv export is supported.");
        }

        var bookings = await _db.Bookings
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Car)
            .OrderByDescending(x => x.PickAt)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("id,pickAt,dropAt,locationCode,customerName,carName,carType,status,days,dailyPrice,cancelReason");
        foreach (var b in bookings)
        {
            sb.AppendLine($"{b.BookingId},{b.PickAt:O},{b.DropAt:O},{b.LocationCode},{EscapeCsv(b.Customer.Name)},{EscapeCsv(b.Car.Brand + " " + b.Car.Model)},{b.Car.CarType},{b.Status},{b.Days},{b.DailyPrice},{EscapeCsv(b.CancelReason ?? string.Empty)}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", "bookings-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".csv");
    }

    private static string? MapTransition(string currentStatus, string action)
    {
        var normalizedAction = action.ToLowerInvariant();
        var normalizedStatus = currentStatus.ToLowerInvariant();

        return (normalizedStatus, normalizedAction) switch
        {
            ("pending", "approve") => "approved",
            ("pending", "cancel") => "cancelled",
            ("approved", "start") => "ongoing",
            ("approved", "cancel") => "cancelled",
            ("ongoing", "complete") => "completed",
            _ => null
        };
    }

    private static bool IsAllowedAction(string action)
    {
        return action.Equals("approve", StringComparison.OrdinalIgnoreCase)
            || action.Equals("start", StringComparison.OrdinalIgnoreCase)
            || action.Equals("complete", StringComparison.OrdinalIgnoreCase)
            || action.Equals("cancel", StringComparison.OrdinalIgnoreCase);
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

    public sealed class CreateBookingRequest
    {
        public string CustomerId { get; set; } = string.Empty;
        public string CarId { get; set; } = string.Empty;
        public string LocationCode { get; set; } = string.Empty;
        public DateTime PickAt { get; set; }
        public DateTime DropAt { get; set; }
        public decimal DailyPrice { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class PatchBookingStatusRequest
    {
        public string Action { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }

    public sealed class BulkStatusRequest
    {
        public List<string> BookingIds { get; set; } = [];
        public string Action { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }
}
