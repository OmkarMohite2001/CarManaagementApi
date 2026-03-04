using CarManaagementApi.Contracts;
using CarManaagementApi.Persistence;
using CarManaagementApi.Persistence.Entities;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/return-inspections")]
public class ReturnInspectionsController : ApiControllerBase
{
    private readonly RentXDbContext _db;

    public ReturnInspectionsController(RentXDbContext db)
    {
        _db = db;
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
        return OkResponse(new
        {
            totalDamage = result.totalDamage,
            fuelCharge = result.fuelCharge,
            cleaningCharge = result.cleaningCharge,
            lateFee = result.lateFee,
            subTotal = result.subTotal,
            deposit = request.Deposit,
            netPayable = result.netPayable,
            refund = result.refund
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(ReturnInspectionUpsertRequest request)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        var bookingExists = await _db.Bookings.AnyAsync(x => x.BookingId == request.BookingId);
        if (!bookingExists)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Booking not found");
        }

        var carExists = await _db.Cars.AnyAsync(x => x.CarId == request.CarId);
        if (!carExists)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Car not found");
        }

        var totals = CalculateTotals(request);
        var inspectionId = await IdGenerator.NextAsync(_db.ReturnInspections.Select(x => x.InspectionId), "INSP");

        var inspection = new ReturnInspection
        {
            InspectionId = inspectionId,
            BookingId = request.BookingId,
            CarId = request.CarId,
            Odometer = request.Odometer,
            FuelPercent = (byte)request.FuelPercent,
            CleaningRequired = request.CleaningRequired,
            LateHours = request.LateHours,
            LateFeePerHour = request.LateFeePerHour,
            Deposit = request.Deposit,
            Notes = request.Notes,
            TotalDamage = totals.totalDamage,
            FuelCharge = totals.fuelCharge,
            CleaningCharge = totals.cleaningCharge,
            LateFee = totals.lateFee,
            SubTotal = totals.subTotal,
            NetPayable = totals.netPayable,
            Refund = totals.refund,
            CreatedAt = DateTime.UtcNow
        };

        _db.ReturnInspections.Add(inspection);
        await _db.SaveChangesAsync();

        await UpsertDamages(inspection.InspectionId, request.Damages);

        var payload = await BuildInspectionResponse(inspection.InspectionId);
        return OkResponse(payload!, "Return inspection saved");
    }

    [HttpGet("{inspectionId}")]
    public async Task<IActionResult> GetById(string inspectionId)
    {
        var payload = await BuildInspectionResponse(inspectionId);
        if (payload is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Return inspection not found");
        }

        return OkResponse(payload);
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? bookingId,
        [FromQuery] string? carId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.ReturnInspections.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(bookingId))
        {
            query = query.Where(x => x.BookingId == bookingId);
        }

        if (!string.IsNullOrWhiteSpace(carId))
        {
            query = query.Where(x => x.CarId == carId);
        }

        if (from.HasValue)
        {
            query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt) >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt) <= to.Value);
        }

        var ids = await query.OrderByDescending(x => x.CreatedAt).Select(x => x.InspectionId).ToListAsync();
        var rows = new List<object>();
        foreach (var id in ids)
        {
            var payload = await BuildInspectionResponse(id);
            if (payload is not null)
            {
                rows.Add(payload);
            }
        }

        var (items, meta) = rows.Paginate(page, pageSize);
        return OkResponse<IEnumerable<object>>(items, meta: meta);
    }

    [HttpPut("{inspectionId}")]
    public async Task<IActionResult> Update(string inspectionId, ReturnInspectionUpsertRequest request)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        var existing = await _db.ReturnInspections.FirstOrDefaultAsync(x => x.InspectionId == inspectionId);
        if (existing is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Return inspection not found");
        }

        var totals = CalculateTotals(request);

        existing.BookingId = request.BookingId;
        existing.CarId = request.CarId;
        existing.Odometer = request.Odometer;
        existing.FuelPercent = (byte)request.FuelPercent;
        existing.CleaningRequired = request.CleaningRequired;
        existing.LateHours = request.LateHours;
        existing.LateFeePerHour = request.LateFeePerHour;
        existing.Deposit = request.Deposit;
        existing.Notes = request.Notes;
        existing.TotalDamage = totals.totalDamage;
        existing.FuelCharge = totals.fuelCharge;
        existing.CleaningCharge = totals.cleaningCharge;
        existing.LateFee = totals.lateFee;
        existing.SubTotal = totals.subTotal;
        existing.NetPayable = totals.netPayable;
        existing.Refund = totals.refund;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await UpsertDamages(existing.InspectionId, request.Damages);

        var payload = await BuildInspectionResponse(existing.InspectionId);
        return OkResponse(payload!, "Return inspection updated");
    }

    private async Task UpsertDamages(string inspectionId, List<DamageItemRequest> damages)
    {
        var existingDamages = await _db.ReturnInspectionDamages
            .Include(x => x.ReturnInspectionDamagePhotos)
            .Where(x => x.InspectionId == inspectionId)
            .ToListAsync();

        if (existingDamages.Count > 0)
        {
            var existingPhotos = existingDamages.SelectMany(x => x.ReturnInspectionDamagePhotos).ToList();
            _db.ReturnInspectionDamagePhotos.RemoveRange(existingPhotos);
            _db.ReturnInspectionDamages.RemoveRange(existingDamages);
            await _db.SaveChangesAsync();
        }

        foreach (var damageRequest in damages)
        {
            var damage = new ReturnInspectionDamage
            {
                InspectionId = inspectionId,
                Part = damageRequest.Part,
                Severity = damageRequest.Severity.ToLowerInvariant(),
                EstCost = damageRequest.EstCost,
                Notes = damageRequest.Notes
            };

            _db.ReturnInspectionDamages.Add(damage);
            await _db.SaveChangesAsync();

            if (damageRequest.PhotoUrls is not null)
            {
                foreach (var photoUrl in damageRequest.PhotoUrls)
                {
                    _db.ReturnInspectionDamagePhotos.Add(new ReturnInspectionDamagePhoto
                    {
                        DamageId = damage.DamageId,
                        PhotoUrl = photoUrl
                    });
                }
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task<object?> BuildInspectionResponse(string inspectionId)
    {
        var inspection = await _db.ReturnInspections
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.InspectionId == inspectionId);

        if (inspection is null)
        {
            return null;
        }

        var damages = await _db.ReturnInspectionDamages
            .AsNoTracking()
            .Where(x => x.InspectionId == inspectionId)
            .ToListAsync();

        var damageIds = damages.Select(x => x.DamageId).ToList();
        var photos = await _db.ReturnInspectionDamagePhotos
            .AsNoTracking()
            .Where(x => damageIds.Contains(x.DamageId))
            .ToListAsync();

        return new
        {
            id = inspection.InspectionId,
            bookingId = inspection.BookingId,
            carId = inspection.CarId,
            odometer = inspection.Odometer,
            fuelPercent = inspection.FuelPercent,
            cleaningRequired = inspection.CleaningRequired,
            lateHours = inspection.LateHours,
            lateFeePerHour = inspection.LateFeePerHour,
            deposit = inspection.Deposit,
            notes = inspection.Notes,
            damages = damages.Select(d => new
            {
                part = d.Part,
                severity = d.Severity,
                estCost = d.EstCost,
                notes = d.Notes,
                photoUrls = photos.Where(p => p.DamageId == d.DamageId).Select(p => p.PhotoUrl).ToList()
            }),
            totalDamage = inspection.TotalDamage,
            fuelCharge = inspection.FuelCharge,
            cleaningCharge = inspection.CleaningCharge,
            lateFee = inspection.LateFee,
            subTotal = inspection.SubTotal,
            netPayable = inspection.NetPayable,
            refund = inspection.Refund,
            createdAt = inspection.CreatedAt
        };
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
