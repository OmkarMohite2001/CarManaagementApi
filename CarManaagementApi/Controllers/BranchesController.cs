using System.Text.RegularExpressions;
using CarManaagementApi.Persistence;
using CarManaagementApi.Persistence.Entities;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/branches")]
public class BranchesController : ApiControllerBase
{
    private static readonly Regex IdRegex = new("^[A-Z0-9]{2,6}$", RegexOptions.Compiled);
    private static readonly Regex PincodeRegex = new("^\\d{6}$", RegexOptions.Compiled);

    private readonly RentXDbContext _db;

    public BranchesController(RentXDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetBranches(
        [FromQuery] string? q,
        [FromQuery] bool? active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.Branches.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                x.BranchId.Contains(q)
                || x.Name.Contains(q)
                || (x.City != null && x.City.Contains(q))
                || (x.Email != null && x.Email.Contains(q)));
        }

        if (active.HasValue)
        {
            query = query.Where(x => x.IsActive == active.Value);
        }

        var rows = await query
            .OrderBy(x => x.Name)
            .Select(x => (object)new
            {
                id = x.BranchId,
                name = x.Name,
                phone = x.Phone,
                email = x.Email,
                address = x.Address,
                city = x.City,
                state = x.State,
                pincode = x.Pincode,
                openAt = x.OpenAt.ToString("HH:mm"),
                closeAt = x.CloseAt.ToString("HH:mm"),
                active = x.IsActive
            })
            .ToListAsync();

        var (items, meta) = rows.Paginate(page, pageSize);
        return OkResponse<IEnumerable<object>>(items, meta: meta);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBranch(BranchUpsertRequest request)
    {
        var validation = ValidateBranch(request, true);
        if (validation is not null)
        {
            return validation;
        }

        var exists = await _db.Branches.AnyAsync(x => x.BranchId == request.Id);
        if (exists)
        {
            return ErrorResponse(StatusCodes.Status409Conflict, "Branch id already exists");
        }

        var branch = MapBranch(request);
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();
        return OkResponse(ToResponse(branch), "Branch created");
    }

    [HttpPut("{branchId}")]
    public async Task<IActionResult> UpdateBranch(string branchId, BranchUpsertRequest request)
    {
        var validation = ValidateBranch(request, false);
        if (validation is not null)
        {
            return validation;
        }

        var existing = await _db.Branches.FirstOrDefaultAsync(x => x.BranchId == branchId);
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
        existing.IsActive = request.Active;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return OkResponse(ToResponse(existing), "Branch updated");
    }

    [HttpPatch("{branchId}/active")]
    public async Task<IActionResult> PatchActive(string branchId, BranchActiveRequest request)
    {
        var existing = await _db.Branches.FirstOrDefaultAsync(x => x.BranchId == branchId);
        if (existing is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Branch not found");
        }

        existing.IsActive = request.Active;
        existing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return OkResponse(new { id = existing.BranchId, active = existing.IsActive }, "Branch active status updated");
    }

    [HttpDelete("{branchId}")]
    public async Task<IActionResult> DeleteBranch(string branchId)
    {
        var existing = await _db.Branches.FirstOrDefaultAsync(x => x.BranchId == branchId);
        if (existing is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Branch not found");
        }

        _db.Branches.Remove(existing);
        await _db.SaveChangesAsync();
        return NoContent();
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

    private static Branch MapBranch(BranchUpsertRequest request)
    {
        return new Branch
        {
            BranchId = request.Id,
            Name = request.Name,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
            City = request.City,
            State = request.State,
            Pincode = request.Pincode,
            OpenAt = request.OpenAt,
            CloseAt = request.CloseAt,
            IsActive = request.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static object ToResponse(Branch x)
    {
        return new
        {
            id = x.BranchId,
            name = x.Name,
            phone = x.Phone,
            email = x.Email,
            address = x.Address,
            city = x.City,
            state = x.State,
            pincode = x.Pincode,
            openAt = x.OpenAt.ToString("HH:mm"),
            closeAt = x.CloseAt.ToString("HH:mm"),
            active = x.IsActive
        };
    }

    public sealed class BranchUpsertRequest
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }
        public TimeOnly OpenAt { get; set; }
        public TimeOnly CloseAt { get; set; }
        public bool Active { get; set; }
    }

    public sealed class BranchActiveRequest
    {
        public bool Active { get; set; }
    }
}
