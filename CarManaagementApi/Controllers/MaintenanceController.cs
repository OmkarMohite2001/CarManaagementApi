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
[Route("api/v1/maintenance")]
public class MaintenanceController : ApiControllerBase
{
    private readonly RentXDbContext _db;

    public MaintenanceController(RentXDbContext db)
    {
        _db = db;
    }

    [HttpGet("blocks")]
    public async Task<IActionResult> GetBlocks(
        [FromQuery] string? carId,
        [FromQuery] string? types,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var typeSet = SplitCsv(types);

        var query = _db.MaintenanceBlocks.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(carId))
        {
            query = query.Where(x => x.CarId == carId);
        }

        if (typeSet.Count > 0)
        {
            query = query.Where(x => typeSet.Contains(x.MaintenanceType));
        }

        if (from.HasValue)
        {
            query = query.Where(x => x.BlockTo >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.BlockFrom <= to.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                x.MaintenanceId.Contains(q)
                || x.CarId.Contains(q)
                || (x.Notes != null && x.Notes.Contains(q)));
        }

        var rows = await query
            .OrderByDescending(x => x.BlockFrom)
            .Select(x => (object)new
            {
                id = x.MaintenanceId,
                carId = x.CarId,
                type = x.MaintenanceType,
                from = x.BlockFrom,
                to = x.BlockTo,
                days = x.Days,
                notes = x.Notes
            })
            .ToListAsync();

        var (items, meta) = rows.Paginate(page, pageSize);
        return OkResponse<IEnumerable<object>>(items, meta: meta);
    }

    [HttpPost("blocks")]
    public async Task<IActionResult> CreateBlock(CreateMaintenanceBlockRequest request)
    {
        if (!RentXConstants.IsValid(RentXConstants.MaintenanceTypes, request.Type))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                new ApiErrorItem { Field = "type", Code = "InvalidMaintenanceType", Message = "Invalid maintenance type." }
            ]);
        }

        if (request.To < request.From)
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                new ApiErrorItem { Field = "to", Code = "InvalidRange", Message = "to must be greater than or equal to from." }
            ]);
        }

        var carExists = await _db.Cars.AnyAsync(x => x.CarId == request.CarId);
        if (!carExists)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Car not found");
        }

        var overlap = await _db.MaintenanceBlocks.FirstOrDefaultAsync(x =>
            x.CarId == request.CarId
            && request.From <= x.BlockTo
            && request.To >= x.BlockFrom);

        if (overlap is not null)
        {
            return ErrorResponse(StatusCodes.Status409Conflict, "Overlaps with existing maintenance block " + overlap.MaintenanceId);
        }

        var maintenanceId = await IdGenerator.NextAsync(_db.MaintenanceBlocks.Select(x => x.MaintenanceId), "MT");

        var block = new MaintenanceBlock
        {
            MaintenanceId = maintenanceId,
            CarId = request.CarId,
            MaintenanceType = request.Type.ToLowerInvariant(),
            BlockFrom = request.From,
            BlockTo = request.To,
            Days = request.To.DayNumber - request.From.DayNumber + 1,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _db.MaintenanceBlocks.Add(block);
        await _db.SaveChangesAsync();

        return OkResponse(new
        {
            id = block.MaintenanceId,
            carId = block.CarId,
            type = block.MaintenanceType,
            from = block.BlockFrom,
            to = block.BlockTo,
            days = block.Days,
            notes = block.Notes
        }, "Maintenance block created");
    }

    [HttpGet("blocks/{maintenanceId}")]
    public async Task<IActionResult> GetBlockById(string maintenanceId)
    {
        var block = await _db.MaintenanceBlocks.AsNoTracking().FirstOrDefaultAsync(x => x.MaintenanceId == maintenanceId);
        if (block is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Maintenance block not found");
        }

        return OkResponse(new
        {
            id = block.MaintenanceId,
            carId = block.CarId,
            type = block.MaintenanceType,
            from = block.BlockFrom,
            to = block.BlockTo,
            days = block.Days,
            notes = block.Notes
        });
    }

    [HttpDelete("blocks/{maintenanceId}")]
    public async Task<IActionResult> DeleteBlock(string maintenanceId)
    {
        var block = await _db.MaintenanceBlocks.FirstOrDefaultAsync(x => x.MaintenanceId == maintenanceId);
        if (block is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Maintenance block not found");
        }

        _db.MaintenanceBlocks.Remove(block);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("overlap-check")]
    public async Task<IActionResult> OverlapCheck(
        [FromQuery] string carId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to)
    {
        if (to < from)
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                new ApiErrorItem { Field = "to", Code = "InvalidRange", Message = "to must be greater than or equal to from." }
            ]);
        }

        var overlap = await _db.MaintenanceBlocks.FirstOrDefaultAsync(x =>
            x.CarId == carId
            && from <= x.BlockTo
            && to >= x.BlockFrom);

        return OkResponse(new
        {
            carId,
            from,
            to,
            hasOverlap = overlap is not null,
            overlapWith = overlap?.MaintenanceId
        });
    }

    private static List<string> SplitCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    public sealed class CreateMaintenanceBlockRequest
    {
        public string CarId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public string? Notes { get; set; }
    }
}
