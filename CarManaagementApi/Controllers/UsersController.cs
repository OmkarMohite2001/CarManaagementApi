using System.Text.RegularExpressions;
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
[Route("api/v1/users")]
public class UsersController : ApiControllerBase
{
    private static readonly Regex PhoneRegex = new("^[6-9]\\d{9}$", RegexOptions.Compiled);

    private readonly RentXDbContext _db;

    public UsersController(RentXDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? q,
        [FromQuery] string? role,
        [FromQuery] bool? active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                x.FullName.Contains(q)
                || x.Email.Contains(q)
                || (x.Phone != null && x.Phone.Contains(q))
                || x.UserId.Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(x => x.RoleCode == role);
        }

        if (active.HasValue)
        {
            query = query.Where(x => x.IsActive == active.Value);
        }

        var rows = await query
            .OrderBy(x => x.FullName)
            .Select(x => (object)new
            {
                id = x.UserId,
                name = x.FullName,
                email = x.Email,
                phone = x.Phone,
                role = x.RoleCode,
                active = x.IsActive,
                createdAt = x.CreatedAt,
                lastLogin = x.LastLoginAt
            })
            .ToListAsync();

        var (items, meta) = rows.Paginate(page, pageSize);
        return OkResponse<IEnumerable<object>>(items, meta: meta);
    }

    [HttpGet("login-activity")]
    public async Task<IActionResult> GetLoginActivity(
        [FromQuery] string? userId,
        [FromQuery] string? role,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.UserAuthLogs
            .AsNoTracking()
            .Include(x => x.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(x => x.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(x => x.RoleCode == role);
        }

        if (from.HasValue)
        {
            query = query.Where(x => x.LoginAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.LoginAt <= to.Value);
        }

        var rows = await query
            .OrderByDescending(x => x.LoginAt)
            .Select(x => (object)new
            {
                id = x.AuthLogId,
                userId = x.UserId,
                name = x.User.FullName,
                email = x.User.Email,
                role = x.RoleCode,
                loginAt = x.LoginAt,
                logoutAt = x.LogoutAt,
                loginIp = x.LoginIp,
                logoutIp = x.LogoutIp,
                userAgent = x.UserAgent,
                source = x.Source
            })
            .ToListAsync();

        var (items, meta) = rows.Paginate(page, pageSize);
        return OkResponse<IEnumerable<object>>(items, meta: meta);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserRequest request)
    {
        var validation = Validate(request.Name, request.Email, request.Phone, request.Role, request.Password);
        if (validation is not null)
        {
            return validation;
        }

        var emailExists = await _db.Users.AnyAsync(x => x.Email == request.Email);
        if (emailExists)
        {
            return ErrorResponse(StatusCodes.Status409Conflict, "User email already exists");
        }

        var roleExists = await _db.Roles.AnyAsync(x => x.RoleCode == request.Role);
        if (!roleExists)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Role not found");
        }

        var userId = await IdGenerator.NextAsync(_db.Users.Select(x => x.UserId), "U");

        var user = new User
        {
            UserId = userId,
            FullName = request.Name,
            Username = request.Email.Split('@')[0],
            Email = request.Email,
            Phone = request.Phone,
            RoleCode = request.Role.ToLowerInvariant(),
            IsActive = request.Active,
            PasswordHash = request.Password,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return OkResponse(ToResponse(user), "User created");
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetUserById(string userId)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
        if (user is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "User not found");
        }

        return OkResponse(ToResponse(user));
    }

    [HttpPut("{userId}")]
    public async Task<IActionResult> UpdateUser(string userId, UpdateUserRequest request)
    {
        var validation = Validate(request.Name, request.Email, request.Phone, request.Role, null);
        if (validation is not null)
        {
            return validation;
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == userId);
        if (user is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "User not found");
        }

        var emailExists = await _db.Users.AnyAsync(x => x.UserId != userId && x.Email == request.Email);
        if (emailExists)
        {
            return ErrorResponse(StatusCodes.Status409Conflict, "User email already exists");
        }

        user.FullName = request.Name;
        user.Email = request.Email;
        user.Phone = request.Phone;
        user.RoleCode = request.Role.ToLowerInvariant();
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return OkResponse(ToResponse(user), "User updated");
    }

    [HttpPatch("{userId}/active")]
    public async Task<IActionResult> PatchUserActive(string userId, PatchUserActiveRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == userId);
        if (user is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "User not found");
        }

        user.IsActive = request.Active;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return OkResponse(new { id = user.UserId, active = user.IsActive }, "User active status updated");
    }

    [HttpPost("{userId}/reset-password")]
    public async Task<IActionResult> ResetPassword(string userId, ResetUserPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TemporaryPassword))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                new ApiErrorItem { Field = "temporaryPassword", Code = "Required", Message = "temporaryPassword is required." }
            ]);
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == userId);
        if (user is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "User not found");
        }

        user.PasswordHash = request.TemporaryPassword;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return OkResponse(new { id = user.UserId, passwordReset = true }, "Password reset successful");
    }

    [HttpDelete("{userId}")]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == userId);
        if (user is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "User not found");
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private IActionResult? Validate(string name, string email, string phone, string role, string? password)
    {
        var errors = new List<ApiErrorItem>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new ApiErrorItem { Field = "name", Code = "Required", Message = "name is required." });
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            errors.Add(new ApiErrorItem { Field = "email", Code = "InvalidEmail", Message = "Email format is invalid." });
        }

        if (!string.IsNullOrWhiteSpace(phone) && !PhoneRegex.IsMatch(phone))
        {
            errors.Add(new ApiErrorItem { Field = "phone", Code = "InvalidPhone", Message = "Phone must match ^[6-9]\\d{9}$." });
        }

        if (!RentXConstants.IsValid(RentXConstants.Roles, role))
        {
            errors.Add(new ApiErrorItem { Field = "role", Code = "InvalidRole", Message = "Invalid role." });
        }

        if (password is not null && string.IsNullOrWhiteSpace(password))
        {
            errors.Add(new ApiErrorItem { Field = "password", Code = "Required", Message = "password is required." });
        }

        if (errors.Count > 0)
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", errors);
        }

        return null;
    }

    private static object ToResponse(User x)
    {
        return new
        {
            id = x.UserId,
            name = x.FullName,
            email = x.Email,
            phone = x.Phone,
            role = x.RoleCode,
            active = x.IsActive,
            createdAt = x.CreatedAt,
            lastLogin = x.LastLoginAt
        };
    }

    public sealed class CreateUserRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool Active { get; set; }
        public string Password { get; set; } = string.Empty;
    }

    public sealed class UpdateUserRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public sealed class PatchUserActiveRequest
    {
        public bool Active { get; set; }
    }

    public sealed class ResetUserPasswordRequest
    {
        public string TemporaryPassword { get; set; } = string.Empty;
    }
}
