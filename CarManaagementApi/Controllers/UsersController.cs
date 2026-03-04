using System.Text.RegularExpressions;
using CarManaagementApi.Contracts;
using CarManaagementApi.Services;
using CarManaagementApi.Services.Models;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/users")]
public class UsersController : ApiControllerBase
{
    private static readonly Regex PhoneRegex = new("^[6-9]\\d{9}$", RegexOptions.Compiled);

    private readonly IRentXStore _store;

    public UsersController(IRentXStore store)
    {
        _store = store;
    }

    [HttpGet]
    public IActionResult GetUsers(
        [FromQuery] string? q,
        [FromQuery] string? role,
        [FromQuery] bool? active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        lock (_store.SyncRoot)
        {
            var query = _store.Users.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(x =>
                    x.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.Email.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.Phone.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.Id.Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(x => x.Role.Equals(role, StringComparison.OrdinalIgnoreCase));
            }

            if (active.HasValue)
            {
                query = query.Where(x => x.Active == active.Value);
            }

            var shaped = query
                .OrderBy(x => x.Name)
                .Select(ToResponse);

            var (items, meta) = shaped.Paginate(page, pageSize);
            return OkResponse<IEnumerable<object>>(items, meta: meta);
        }
    }

    [HttpPost]
    public IActionResult CreateUser(CreateUserRequest request)
    {
        var validation = Validate(request.Name, request.Email, request.Phone, request.Role, request.Password);
        if (validation is not null)
        {
            return validation;
        }

        lock (_store.SyncRoot)
        {
            if (_store.Users.Any(x => x.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase)))
            {
                return ErrorResponse(StatusCodes.Status409Conflict, "User email already exists");
            }

            var user = new UserRecord
            {
                Id = _store.NextId("U"),
                Name = request.Name,
                Username = request.Email.Split('@')[0],
                Email = request.Email,
                Phone = request.Phone,
                Role = request.Role.ToLowerInvariant(),
                Active = request.Active,
                Password = request.Password,
                CreatedAt = DateTime.UtcNow
            };

            _store.Users.Add(user);
            _store.ProfilesByUserId[user.Id] = new ProfileRecord
            {
                FullName = user.Name,
                Username = user.Username,
                Email = user.Email,
                Phone = user.Phone,
                Gender = "male",
                NotifEmail = true,
                NotifSms = false,
                NotifWhatsApp = false,
                AvatarUrl = string.Empty
            };

            return OkResponse(ToResponse(user), "User created");
        }
    }

    [HttpGet("{userId}")]
    public IActionResult GetUserById(string userId)
    {
        lock (_store.SyncRoot)
        {
            var user = _store.Users.FirstOrDefault(x => x.Id.Equals(userId, StringComparison.OrdinalIgnoreCase));
            if (user is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "User not found");
            }

            return OkResponse(ToResponse(user));
        }
    }

    [HttpPut("{userId}")]
    public IActionResult UpdateUser(string userId, UpdateUserRequest request)
    {
        var validation = Validate(request.Name, request.Email, request.Phone, request.Role, null);
        if (validation is not null)
        {
            return validation;
        }

        lock (_store.SyncRoot)
        {
            var user = _store.Users.FirstOrDefault(x => x.Id.Equals(userId, StringComparison.OrdinalIgnoreCase));
            if (user is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "User not found");
            }

            if (_store.Users.Any(x =>
                    !x.Id.Equals(userId, StringComparison.OrdinalIgnoreCase)
                    && x.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase)))
            {
                return ErrorResponse(StatusCodes.Status409Conflict, "User email already exists");
            }

            user.Name = request.Name;
            user.Email = request.Email;
            user.Phone = request.Phone;
            user.Role = request.Role.ToLowerInvariant();

            if (_store.ProfilesByUserId.TryGetValue(user.Id, out var profile))
            {
                profile.FullName = user.Name;
                profile.Email = user.Email;
                profile.Phone = user.Phone;
            }

            return OkResponse(ToResponse(user), "User updated");
        }
    }

    [HttpPatch("{userId}/active")]
    public IActionResult PatchUserActive(string userId, PatchUserActiveRequest request)
    {
        lock (_store.SyncRoot)
        {
            var user = _store.Users.FirstOrDefault(x => x.Id.Equals(userId, StringComparison.OrdinalIgnoreCase));
            if (user is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "User not found");
            }

            user.Active = request.Active;
            return OkResponse(new { id = user.Id, active = user.Active }, "User active status updated");
        }
    }

    [HttpPost("{userId}/reset-password")]
    public IActionResult ResetPassword(string userId, ResetUserPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TemporaryPassword))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                new ApiErrorItem { Field = "temporaryPassword", Code = "Required", Message = "temporaryPassword is required." }
            ]);
        }

        lock (_store.SyncRoot)
        {
            var user = _store.Users.FirstOrDefault(x => x.Id.Equals(userId, StringComparison.OrdinalIgnoreCase));
            if (user is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "User not found");
            }

            user.Password = request.TemporaryPassword;
            return OkResponse(new { id = user.Id, passwordReset = true }, "Password reset successful");
        }
    }

    [HttpDelete("{userId}")]
    public IActionResult DeleteUser(string userId)
    {
        lock (_store.SyncRoot)
        {
            var user = _store.Users.FirstOrDefault(x => x.Id.Equals(userId, StringComparison.OrdinalIgnoreCase));
            if (user is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "User not found");
            }

            _store.Users.Remove(user);
            _store.ProfilesByUserId.Remove(user.Id);
            return NoContent();
        }
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

    private static object ToResponse(UserRecord x)
    {
        return new
        {
            id = x.Id,
            name = x.Name,
            email = x.Email,
            phone = x.Phone,
            role = x.Role,
            active = x.Active,
            createdAt = x.CreatedAt,
            lastLogin = x.LastLogin
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
