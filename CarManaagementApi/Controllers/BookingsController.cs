using System.Text;
using CarManaagementApi.Contracts;
using CarManaagementApi.Services;
using CarManaagementApi.Services.Models;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/bookings")]
public class BookingsController : ApiControllerBase
{
    private readonly IRentXStore _store;

    public BookingsController(IRentXStore store)
    {
        _store = store;
    }

    [HttpGet]
    public IActionResult GetBookings(
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

        lock (_store.SyncRoot)
        {
            var query = _store.Bookings.AsEnumerable();

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
                query = query.Where(x => statusSet.Contains(x.Status, StringComparer.OrdinalIgnoreCase));
            }

            if (typeSet.Count > 0)
            {
                query = query.Where(x => typeSet.Contains(x.CarType, StringComparer.OrdinalIgnoreCase));
            }

            if (locationSet.Count > 0)
            {
                query = query.Where(x => locationSet.Contains(x.LocationCode, StringComparer.OrdinalIgnoreCase));
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
                    pickAt = x.PickAt,
                    dropAt = x.DropAt,
                    locationCode = x.LocationCode,
                    customerId = x.CustomerId,
                    customerName = x.CustomerName,
                    carId = x.CarId,
                    carName = x.CarName,
                    carType = x.CarType,
                    status = x.Status,
                    days = x.Days,
                    dailyPrice = x.DailyPrice,
                    cancelReason = x.CancelReason
                });

            var (items, meta) = shaped.Paginate(page, pageSize);
            return OkResponse<IEnumerable<object>>(items, meta: meta);
        }
    }

    [HttpPost]
    public IActionResult CreateBooking(CreateBookingRequest request)
    {
        if (request.DropAt <= request.PickAt)
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                new ApiErrorItem { Field = "dropAt", Code = "InvalidRange", Message = "dropAt must be later than pickAt." }
            ]);
        }

        lock (_store.SyncRoot)
        {
            var customer = _store.Customers.FirstOrDefault(x => x.Id.Equals(request.CustomerId, StringComparison.OrdinalIgnoreCase));
            if (customer is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Customer not found");
            }

            var car = _store.Cars.FirstOrDefault(x => x.Id.Equals(request.CarId, StringComparison.OrdinalIgnoreCase) && x.Active);
            if (car is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Car not found");
            }

            if (!car.LocationCodes.Contains(request.LocationCode, StringComparer.OrdinalIgnoreCase))
            {
                return ErrorResponse(StatusCodes.Status422UnprocessableEntity, "Car is not available at selected location");
            }

            var hasOverlap = _store.Bookings.Any(x =>
                x.CarId.Equals(request.CarId, StringComparison.OrdinalIgnoreCase)
                && x.Status is not "cancelled"
                && request.PickAt < x.DropAt
                && request.DropAt > x.PickAt);

            if (hasOverlap)
            {
                return ErrorResponse(StatusCodes.Status409Conflict, "Car is already booked for selected date range");
            }

            var days = Math.Max(1, (int)Math.Ceiling((request.DropAt - request.PickAt).TotalDays));
            var booking = new BookingRecord
            {
                Id = _store.NextId("BK"),
                PickAt = DateTime.SpecifyKind(request.PickAt, DateTimeKind.Utc),
                DropAt = DateTime.SpecifyKind(request.DropAt, DateTimeKind.Utc),
                LocationCode = request.LocationCode,
                CustomerId = customer.Id,
                CustomerName = customer.Name,
                CarId = car.Id,
                CarName = $"{car.Brand} {car.Model}",
                CarType = car.Type,
                Status = "pending",
                Days = days,
                DailyPrice = request.DailyPrice,
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow
            };

            _store.Bookings.Add(booking);

            return OkResponse(new
            {
                id = booking.Id,
                pickAt = booking.PickAt,
                dropAt = booking.DropAt,
                locationCode = booking.LocationCode,
                customerId = booking.CustomerId,
                customerName = booking.CustomerName,
                carId = booking.CarId,
                carName = booking.CarName,
                carType = booking.CarType,
                status = booking.Status,
                days = booking.Days,
                dailyPrice = booking.DailyPrice,
                cancelReason = booking.CancelReason
            }, "Booking created");
        }
    }

    [HttpGet("{bookingId}")]
    public IActionResult GetBookingById(string bookingId)
    {
        lock (_store.SyncRoot)
        {
            var booking = _store.Bookings.FirstOrDefault(x => x.Id.Equals(bookingId, StringComparison.OrdinalIgnoreCase));
            if (booking is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Booking not found");
            }

            return OkResponse(new
            {
                id = booking.Id,
                pickAt = booking.PickAt,
                dropAt = booking.DropAt,
                locationCode = booking.LocationCode,
                customerId = booking.CustomerId,
                customerName = booking.CustomerName,
                carId = booking.CarId,
                carName = booking.CarName,
                carType = booking.CarType,
                status = booking.Status,
                days = booking.Days,
                dailyPrice = booking.DailyPrice,
                notes = booking.Notes,
                cancelReason = booking.CancelReason
            });
        }
    }

    [HttpPatch("{bookingId}/status")]
    public IActionResult PatchBookingStatus(string bookingId, PatchBookingStatusRequest request)
    {
        if (!IsAllowedAction(request.Action))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                new ApiErrorItem { Field = "action", Code = "InvalidAction", Message = "Allowed actions: approve, start, complete, cancel." }
            ]);
        }

        lock (_store.SyncRoot)
        {
            var booking = _store.Bookings.FirstOrDefault(x => x.Id.Equals(bookingId, StringComparison.OrdinalIgnoreCase));
            if (booking is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Booking not found");
            }

            var transition = MapTransition(booking.Status, request.Action);
            if (transition is null)
            {
                return ErrorResponse(StatusCodes.Status422UnprocessableEntity, $"Cannot '{request.Action}' when status is '{booking.Status}'.");
            }

            booking.Status = transition;
            booking.CancelReason = request.Action.Equals("cancel", StringComparison.OrdinalIgnoreCase) ? request.Reason : null;

            return OkResponse(new
            {
                id = booking.Id,
                status = booking.Status,
                cancelReason = booking.CancelReason
            }, "Booking status updated");
        }
    }

    [HttpPost("bulk-status")]
    public IActionResult BulkStatus(BulkStatusRequest request)
    {
        if (!IsAllowedAction(request.Action))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                new ApiErrorItem { Field = "action", Code = "InvalidAction", Message = "Allowed actions: approve, start, complete, cancel." }
            ]);
        }

        var updated = new List<object>();
        var failed = new List<object>();

        lock (_store.SyncRoot)
        {
            foreach (var id in request.BookingIds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var booking = _store.Bookings.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                if (booking is null)
                {
                    failed.Add(new { bookingId = id, reason = "Not found" });
                    continue;
                }

                var transition = MapTransition(booking.Status, request.Action);
                if (transition is null)
                {
                    failed.Add(new { bookingId = id, reason = $"Cannot '{request.Action}' from '{booking.Status}'" });
                    continue;
                }

                booking.Status = transition;
                booking.CancelReason = request.Action.Equals("cancel", StringComparison.OrdinalIgnoreCase) ? request.Reason : null;
                updated.Add(new { bookingId = id, status = booking.Status });
            }
        }

        return OkResponse(new { updated, failed }, "Bulk status operation completed");
    }

    [HttpGet("export")]
    public IActionResult ExportCsv([FromQuery] string format = "csv")
    {
        if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Only csv export is supported.");
        }

        List<BookingRecord> bookings;
        lock (_store.SyncRoot)
        {
            bookings = _store.Bookings.OrderByDescending(x => x.PickAt).ToList();
        }

        var sb = new StringBuilder();
        sb.AppendLine("id,pickAt,dropAt,locationCode,customerName,carName,carType,status,days,dailyPrice,cancelReason");
        foreach (var b in bookings)
        {
            sb.AppendLine($"{b.Id},{b.PickAt:O},{b.DropAt:O},{b.LocationCode},{EscapeCsv(b.CustomerName)},{EscapeCsv(b.CarName)},{b.CarType},{b.Status},{b.Days},{b.DailyPrice},{EscapeCsv(b.CancelReason ?? string.Empty)}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"bookings-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
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

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Contains(',') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
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
