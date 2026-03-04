using System.Text.RegularExpressions;
using CarManaagementApi.Services;
using CarManaagementApi.Services.Models;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/branches")]
public class BranchesController : ApiControllerBase
{
    private static readonly Regex IdRegex = new("^[A-Z0-9]{2,6}$", RegexOptions.Compiled);
    private static readonly Regex PincodeRegex = new("^\\d{6}$", RegexOptions.Compiled);

    private readonly IRentXStore _store;

    public BranchesController(IRentXStore store)
    {
        _store = store;
    }

    [HttpGet]
    public IActionResult GetBranches(
        [FromQuery] string? q,
        [FromQuery] bool? active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        lock (_store.SyncRoot)
        {
            var query = _store.Branches.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(x =>
                    x.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.City.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.Email.Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            if (active.HasValue)
            {
                query = query.Where(x => x.Active == active.Value);
            }

            var shaped = query.OrderBy(x => x.Name).Select(ToResponse);
            var (items, meta) = shaped.Paginate(page, pageSize);
            return OkResponse<IEnumerable<object>>(items, meta: meta);
        }
    }

    [HttpPost]
    public IActionResult CreateBranch(BranchUpsertRequest request)
    {
        var validation = ValidateBranch(request, true);
        if (validation is not null)
        {
            return validation;
        }

        lock (_store.SyncRoot)
        {
            if (_store.Branches.Any(x => x.Id.Equals(request.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return ErrorResponse(StatusCodes.Status409Conflict, "Branch id already exists");
            }

            var branch = MapBranch(request);
            _store.Branches.Add(branch);
            return OkResponse(ToResponse(branch), "Branch created");
        }
    }

    [HttpPut("{branchId}")]
    public IActionResult UpdateBranch(string branchId, BranchUpsertRequest request)
    {
        var validation = ValidateBranch(request, false);
        if (validation is not null)
        {
            return validation;
        }

        lock (_store.SyncRoot)
        {
            var existing = _store.Branches.FirstOrDefault(x => x.Id.Equals(branchId, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Branch not found");
            }

            existing.Name = request.Name;
            existing.Phone = request.Phone;
            existing.Email = request.Email;
            existing.Address = request.Address;
            existing.City = request.City;
            existing.State = request.State;
            existing.Pincode = request.Pincode;
            existing.OpenAt = request.OpenAt;
            existing.CloseAt = request.CloseAt;
            existing.Active = request.Active;

            return OkResponse(ToResponse(existing), "Branch updated");
        }
    }

    [HttpPatch("{branchId}/active")]
    public IActionResult PatchActive(string branchId, BranchActiveRequest request)
    {
        lock (_store.SyncRoot)
        {
            var existing = _store.Branches.FirstOrDefault(x => x.Id.Equals(branchId, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Branch not found");
            }

            existing.Active = request.Active;
            return OkResponse(new { id = existing.Id, active = existing.Active }, "Branch active status updated");
        }
    }

    [HttpDelete("{branchId}")]
    public IActionResult DeleteBranch(string branchId)
    {
        lock (_store.SyncRoot)
        {
            var existing = _store.Branches.FirstOrDefault(x => x.Id.Equals(branchId, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Branch not found");
            }

            _store.Branches.Remove(existing);
            return NoContent();
        }
    }

    private IActionResult? ValidateBranch(BranchUpsertRequest request, bool idRequired)
    {
        var errors = new List<ApiErrorItem>();

        if (idRequired && !IdRegex.IsMatch(request.Id ?? string.Empty))
        {
            errors.Add(new ApiErrorItem { Field = "id", Code = "InvalidId", Message = "id must match ^[A-Z0-9]{2,6}$." });
        }

        if (!string.IsNullOrWhiteSpace(request.Pincode) && !PincodeRegex.IsMatch(request.Pincode))
        {
            errors.Add(new ApiErrorItem { Field = "pincode", Code = "InvalidPincode", Message = "pincode must be 6 digits." });
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && !request.Email.Contains('@'))
        {
            errors.Add(new ApiErrorItem { Field = "email", Code = "InvalidEmail", Message = "Email format is invalid." });
        }

        if (errors.Count > 0)
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", errors);
        }

        return null;
    }

    private static BranchRecord MapBranch(BranchUpsertRequest request)
    {
        return new BranchRecord
        {
            Id = request.Id,
            Name = request.Name,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
            City = request.City,
            State = request.State,
            Pincode = request.Pincode,
            OpenAt = request.OpenAt,
            CloseAt = request.CloseAt,
            Active = request.Active
        };
    }

    private static object ToResponse(BranchRecord x)
    {
        return new
        {
            id = x.Id,
            name = x.Name,
            phone = x.Phone,
            email = x.Email,
            address = x.Address,
            city = x.City,
            state = x.State,
            pincode = x.Pincode,
            openAt = x.OpenAt.ToString("HH:mm"),
            closeAt = x.CloseAt.ToString("HH:mm"),
            active = x.Active
        };
    }

    public sealed class BranchUpsertRequest
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Pincode { get; set; } = string.Empty;
        public TimeOnly OpenAt { get; set; }
        public TimeOnly CloseAt { get; set; }
        public bool Active { get; set; }
    }

    public sealed class BranchActiveRequest
    {
        public bool Active { get; set; }
    }
}
