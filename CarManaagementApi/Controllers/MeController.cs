using CarManaagementApi.Persistence;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/me")]
public class MeController : ApiControllerBase
{
    private readonly RentXDbContext _db;

    public MeController(RentXDbContext db)
    {
        _db = db;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ErrorResponse(StatusCodes.Status401Unauthorized, "Unauthorized");
        }

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
        if (user is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Profile not found");
        }

        return OkResponse(ToResponse(user));
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(ProfileUpsertRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ErrorResponse(StatusCodes.Status401Unauthorized, "Unauthorized");
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == userId);
        if (user is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Profile not found");
        }

        user.FullName = request.FullName;
        user.Username = request.Username;
        user.Email = request.Email;
        user.Phone = request.Phone;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return OkResponse(ToResponse(user), "Profile updated");
    }

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                new ApiErrorItem { Field = "confirmPassword", Code = "PasswordMismatch", Message = "newPassword and confirmPassword must match." }
            ]);
        }

        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ErrorResponse(StatusCodes.Status401Unauthorized, "Unauthorized");
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == userId);
        if (user is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "User not found");
        }

        if (user.PasswordHash != request.CurrentPassword)
        {
            return ErrorResponse(StatusCodes.Status401Unauthorized, "Current password is incorrect");
        }

        user.PasswordHash = request.NewPassword;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return OkResponse(new { passwordChanged = true }, "Password updated");
    }

    [HttpPost("avatar")]
    public async Task<IActionResult> UploadAvatar([FromForm] IFormFile? file)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ErrorResponse(StatusCodes.Status401Unauthorized, "Unauthorized");
        }

        if (file is null)
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                new ApiErrorItem { Field = "file", Code = "Required", Message = "Avatar file is required." }
            ]);
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == userId);
        if (user is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "Profile not found");
        }

        var extension = Path.GetExtension(file.FileName);
        var avatarUrl = "https://cdn.example.com/avatar/" + userId + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + extension;

        return OkResponse(new { avatarUrl }, "Avatar updated");
    }

    private static object ToResponse(Persistence.Entities.User user)
    {
        return new
        {
            fullName = user.FullName,
            username = user.Username,
            email = user.Email,
            phone = user.Phone ?? string.Empty,
            gender = "male",
            dob = (DateOnly?)null,
            address = string.Empty,
            city = string.Empty,
            state = string.Empty,
            pincode = string.Empty,
            notifEmail = true,
            notifSms = false,
            notifWhatsApp = false,
            avatarUrl = "https://cdn.example.com/avatar/" + user.UserId + ".png"
        };
    }

    public sealed class ProfileUpsertRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public DateOnly? Dob { get; set; }
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Pincode { get; set; } = string.Empty;
        public bool NotifEmail { get; set; }
        public bool NotifSms { get; set; }
        public bool NotifWhatsApp { get; set; }
    }

    public sealed class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
