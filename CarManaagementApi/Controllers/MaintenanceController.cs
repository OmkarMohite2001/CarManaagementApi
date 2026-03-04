using CarManaagementApi.Contracts;
using CarManaagementApi.Services;
using CarManaagementApi.Services.Models;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/maintenance")]
public class MaintenanceController : ApiControllerBase
{
    private readonly IRentXStore _store;

    public MaintenanceController(IRentXStore store)
    {
        _store = store;
    }

    [HttpGet("blocks")]
    public IActionResult GetBlocks(
        [FromQuery] string? carId,
        [FromQuery] string? types,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var typeSet = SplitCsv(types);

        lock (_store.SyncRoot)
        {
            var query = _store.MaintenanceBlocks.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(carId))
            {
                query = query.Where(x => x.CarId.Equals(carId, StringComparison.OrdinalIgnoreCase));
            }

            if (typeSet.Count > 0)
            {
                query = query.Where(x => typeSet.Contains(x.Type, StringComparer.OrdinalIgnoreCase));
            }

            if (from.HasValue)
            {
                query = query.Where(x => x.To >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(x => x.From <= to.Value);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(x =>
                    x.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.CarId.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || (x.Notes?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var shaped = query
                .OrderByDescending(x => x.From)
                .Select(x => (object)new
                {
                    id = x.Id,
                    carId = x.CarId,
                    type = x.Type,
                    from = x.From,
                    to = x.To,
                    days = x.Days,
                    notes = x.Notes
                });

            var (items, meta) = shaped.Paginate(page, pageSize);
            return OkResponse<IEnumerable<object>>(items, meta: meta);
        }
    }

    [HttpPost("blocks")]
    public IActionResult CreateBlock(CreateMaintenanceBlockRequest request)
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

        lock (_store.SyncRoot)
        {
            var car = _store.Cars.FirstOrDefault(x => x.Id.Equals(request.CarId, StringComparison.OrdinalIgnoreCase));
            if (car is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Car not found");
            }

            var overlap = _store.MaintenanceBlocks.FirstOrDefault(x =>
                x.CarId.Equals(request.CarId, StringComparison.OrdinalIgnoreCase)
                && request.From <= x.To
                && request.To >= x.From);

            if (overlap is not null)
            {
                return ErrorResponse(StatusCodes.Status409Conflict, $"Overlaps with existing maintenance block {overlap.Id}");
            }

            var block = new MaintenanceBlockRecord
            {
                Id = _store.NextId("MT"),
                CarId = request.CarId,
                Type = request.Type.ToLowerInvariant(),
                From = request.From,
                To = request.To,
                Days = request.To.DayNumber - request.From.DayNumber + 1,
                Notes = request.Notes
            };

            _store.MaintenanceBlocks.Add(block);

            return OkResponse(new
            {
                id = block.Id,
                carId = block.CarId,
                type = block.Type,
                from = block.From,
                to = block.To,
                days = block.Days,
                notes = block.Notes
            }, "Maintenance block created");
        }
    }

    [HttpGet("blocks/{maintenanceId}")]
    public IActionResult GetBlockById(string maintenanceId)
    {
        lock (_store.SyncRoot)
        {
            var block = _store.MaintenanceBlocks.FirstOrDefault(x => x.Id.Equals(maintenanceId, StringComparison.OrdinalIgnoreCase));
            if (block is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Maintenance block not found");
            }

            return OkResponse(new
            {
                id = block.Id,
                carId = block.CarId,
                type = block.Type,
                from = block.From,
                to = block.To,
                days = block.Days,
                notes = block.Notes
            });
        }
    }

    [HttpDelete("blocks/{maintenanceId}")]
    public IActionResult DeleteBlock(string maintenanceId)
    {
        lock (_store.SyncRoot)
        {
            var block = _store.MaintenanceBlocks.FirstOrDefault(x => x.Id.Equals(maintenanceId, StringComparison.OrdinalIgnoreCase));
            if (block is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Maintenance block not found");
            }

            _store.MaintenanceBlocks.Remove(block);
            return NoContent();
        }
    }

    [HttpGet("overlap-check")]
    public IActionResult OverlapCheck(
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

        lock (_store.SyncRoot)
        {
            var overlap = _store.MaintenanceBlocks.FirstOrDefault(x =>
                x.CarId.Equals(carId, StringComparison.OrdinalIgnoreCase)
                && from <= x.To
                && to >= x.From);

            return OkResponse(new
            {
                carId,
                from,
                to,
                hasOverlap = overlap is not null,
                overlapWith = overlap?.Id
            });
        }
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
