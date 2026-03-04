using CarManaagementApi.Contracts;
using CarManaagementApi.Services;
using CarManaagementApi.Services.Models;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/return-inspections")]
public class ReturnInspectionsController : ApiControllerBase
{
    private readonly IRentXStore _store;

    public ReturnInspectionsController(IRentXStore store)
    {
        _store = store;
    }

    [HttpPost("calculate")]
    public IActionResult Calculate(ReturnInspectionCalculationRequest request)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        var result = CalculateTotals(request);
        return OkResponse(result);
    }

    [HttpPost]
    public IActionResult Create(ReturnInspectionUpsertRequest request)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        lock (_store.SyncRoot)
        {
            if (!_store.Bookings.Any(x => x.Id.Equals(request.BookingId, StringComparison.OrdinalIgnoreCase)))
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Booking not found");
            }

            if (!_store.Cars.Any(x => x.Id.Equals(request.CarId, StringComparison.OrdinalIgnoreCase)))
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Car not found");
            }

            var totals = CalculateTotals(request);
            var inspection = new ReturnInspectionRecord
            {
                Id = _store.NextId("INSP"),
                BookingId = request.BookingId,
                CarId = request.CarId,
                Odometer = request.Odometer,
                FuelPercent = request.FuelPercent,
                CleaningRequired = request.CleaningRequired,
                LateHours = request.LateHours,
                LateFeePerHour = request.LateFeePerHour,
                Deposit = request.Deposit,
                Notes = request.Notes,
                Damages = request.Damages.Select(ToDamageRecord).ToList(),
                TotalDamage = totals.totalDamage,
                FuelCharge = totals.fuelCharge,
                CleaningCharge = totals.cleaningCharge,
                LateFee = totals.lateFee,
                SubTotal = totals.subTotal,
                NetPayable = totals.netPayable,
                Refund = totals.refund,
                CreatedAt = DateTime.UtcNow
            };

            _store.ReturnInspections.Add(inspection);

            return OkResponse(ToResponse(inspection), "Return inspection saved");
        }
    }

    [HttpGet("{inspectionId}")]
    public IActionResult GetById(string inspectionId)
    {
        lock (_store.SyncRoot)
        {
            var inspection = _store.ReturnInspections.FirstOrDefault(x => x.Id.Equals(inspectionId, StringComparison.OrdinalIgnoreCase));
            if (inspection is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Return inspection not found");
            }

            return OkResponse(ToResponse(inspection));
        }
    }

    [HttpGet]
    public IActionResult GetList(
        [FromQuery] string? bookingId,
        [FromQuery] string? carId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        lock (_store.SyncRoot)
        {
            var query = _store.ReturnInspections.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(bookingId))
            {
                query = query.Where(x => x.BookingId.Equals(bookingId, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(carId))
            {
                query = query.Where(x => x.CarId.Equals(carId, StringComparison.OrdinalIgnoreCase));
            }

            if (from.HasValue)
            {
                query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt) >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt) <= to.Value);
            }

            var shaped = query.OrderByDescending(x => x.CreatedAt).Select(ToResponse);
            var (items, meta) = shaped.Paginate(page, pageSize);
            return OkResponse<IEnumerable<object>>(items, meta: meta);
        }
    }

    [HttpPut("{inspectionId}")]
    public IActionResult Update(string inspectionId, ReturnInspectionUpsertRequest request)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        lock (_store.SyncRoot)
        {
            var existing = _store.ReturnInspections.FirstOrDefault(x => x.Id.Equals(inspectionId, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Return inspection not found");
            }

            var totals = CalculateTotals(request);

            existing.BookingId = request.BookingId;
            existing.CarId = request.CarId;
            existing.Odometer = request.Odometer;
            existing.FuelPercent = request.FuelPercent;
            existing.CleaningRequired = request.CleaningRequired;
            existing.LateHours = request.LateHours;
            existing.LateFeePerHour = request.LateFeePerHour;
            existing.Deposit = request.Deposit;
            existing.Notes = request.Notes;
            existing.Damages = request.Damages.Select(ToDamageRecord).ToList();
            existing.TotalDamage = totals.totalDamage;
            existing.FuelCharge = totals.fuelCharge;
            existing.CleaningCharge = totals.cleaningCharge;
            existing.LateFee = totals.lateFee;
            existing.SubTotal = totals.subTotal;
            existing.NetPayable = totals.netPayable;
            existing.Refund = totals.refund;

            return OkResponse(ToResponse(existing), "Return inspection updated");
        }
    }

    private IActionResult? ValidateRequest(ReturnInspectionCalculationRequest request)
    {
        var errors = new List<ApiErrorItem>();

        if (request.FuelPercent < 0 || request.FuelPercent > 100)
        {
            errors.Add(new ApiErrorItem { Field = "fuelPercent", Code = "InvalidFuelPercent", Message = "fuelPercent must be between 0 and 100." });
        }

        if (request.LateHours < 0)
        {
            errors.Add(new ApiErrorItem { Field = "lateHours", Code = "InvalidLateHours", Message = "lateHours must be >= 0." });
        }

        if (request.LateFeePerHour < 0)
        {
            errors.Add(new ApiErrorItem { Field = "lateFeePerHour", Code = "InvalidLateFeePerHour", Message = "lateFeePerHour must be >= 0." });
        }

        if (request.Deposit < 0)
        {
            errors.Add(new ApiErrorItem { Field = "deposit", Code = "InvalidDeposit", Message = "deposit must be >= 0." });
        }

        foreach (var damage in request.Damages)
        {
            if (!RentXConstants.IsValid(RentXConstants.DamageSeverities, damage.Severity))
            {
                errors.Add(new ApiErrorItem { Field = "damages.severity", Code = "InvalidDamageSeverity", Message = "Invalid damage severity." });
                break;
            }
        }

        if (errors.Count > 0)
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", errors);
        }

        return null;
    }

    private static (decimal totalDamage, decimal fuelCharge, decimal cleaningCharge, decimal lateFee, decimal subTotal, decimal netPayable, decimal refund)
        CalculateTotals(ReturnInspectionCalculationRequest request)
    {
        var totalDamage = request.Damages.Sum(x => x.EstCost);
        var fuelCharge = request.FuelPercent >= 80 ? 0 : (80 - request.FuelPercent) * 30;
        var cleaningCharge = request.CleaningRequired ? 500 : 0;
        var lateFee = request.LateHours * request.LateFeePerHour;
        var subTotal = totalDamage + fuelCharge + cleaningCharge + lateFee;
        var netPayable = Math.Max(subTotal - request.Deposit, 0);
        var refund = Math.Max(request.Deposit - subTotal, 0);

        return (totalDamage, fuelCharge, cleaningCharge, lateFee, subTotal, netPayable, refund);
    }

    private static DamageRecord ToDamageRecord(DamageItemRequest request)
    {
        return new DamageRecord
        {
            Part = request.Part,
            Severity = request.Severity.ToLowerInvariant(),
            EstCost = request.EstCost,
            Notes = request.Notes,
            PhotoUrls = request.PhotoUrls ?? []
        };
    }

    private static object ToResponse(ReturnInspectionRecord x)
    {
        return new
        {
            id = x.Id,
            bookingId = x.BookingId,
            carId = x.CarId,
            odometer = x.Odometer,
            fuelPercent = x.FuelPercent,
            cleaningRequired = x.CleaningRequired,
            lateHours = x.LateHours,
            lateFeePerHour = x.LateFeePerHour,
            deposit = x.Deposit,
            notes = x.Notes,
            damages = x.Damages.Select(d => new
            {
                part = d.Part,
                severity = d.Severity,
                estCost = d.EstCost,
                notes = d.Notes,
                photoUrls = d.PhotoUrls
            }),
            totalDamage = x.TotalDamage,
            fuelCharge = x.FuelCharge,
            cleaningCharge = x.CleaningCharge,
            lateFee = x.LateFee,
            subTotal = x.SubTotal,
            netPayable = x.NetPayable,
            refund = x.Refund,
            createdAt = x.CreatedAt
        };
    }

    public class ReturnInspectionCalculationRequest
    {
        public int FuelPercent { get; set; }
        public bool CleaningRequired { get; set; }
        public int LateHours { get; set; }
        public decimal LateFeePerHour { get; set; }
        public decimal Deposit { get; set; }
        public List<DamageItemRequest> Damages { get; set; } = [];
    }

    public sealed class ReturnInspectionUpsertRequest : ReturnInspectionCalculationRequest
    {
        public string BookingId { get; set; } = string.Empty;
        public string CarId { get; set; } = string.Empty;
        public int Odometer { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class DamageItemRequest
    {
        public string Part { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public decimal EstCost { get; set; }
        public string? Notes { get; set; }
        public List<string>? PhotoUrls { get; set; }
    }
}
