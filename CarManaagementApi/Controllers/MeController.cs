using CarManaagementApi.Services;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/me")]
public class MeController : ApiControllerBase
{
    private readonly IRentXStore _store;

    public MeController(IRentXStore store)
    {
        _store = store;
    }

    [HttpGet("profile")]
    public IActionResult GetProfile()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ErrorResponse(StatusCodes.Status401Unauthorized, "Unauthorized");
        }

        lock (_store.SyncRoot)
        {
            if (!_store.ProfilesByUserId.TryGetValue(userId, out var profile))
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Profile not found");
            }

            return OkResponse(ToResponse(profile));
        }
    }

    [HttpPut("profile")]
    public IActionResult UpdateProfile(ProfileUpsertRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ErrorResponse(StatusCodes.Status401Unauthorized, "Unauthorized");
        }

        lock (_store.SyncRoot)
        {
            if (!_store.ProfilesByUserId.TryGetValue(userId, out var profile))
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Profile not found");
            }

            profile.FullName = request.FullName;
            profile.Username = request.Username;
            profile.Email = request.Email;
            profile.Phone = request.Phone;
            profile.Gender = request.Gender;
            profile.Dob = request.Dob;
            profile.Address = request.Address;
            profile.City = request.City;
            profile.State = request.State;
            profile.Pincode = request.Pincode;
            profile.NotifEmail = request.NotifEmail;
            profile.NotifSms = request.NotifSms;
            profile.NotifWhatsApp = request.NotifWhatsApp;

            var user = _store.Users.FirstOrDefault(x => x.Id == userId);
            if (user is not null)
            {
                user.Name = request.FullName;
                user.Username = request.Username;
                user.Email = request.Email;
                user.Phone = request.Phone;
            }

            return OkResponse(ToResponse(profile), "Profile updated");
        }
    }

    [HttpPut("password")]
    public IActionResult ChangePassword(ChangePasswordRequest request)
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

        lock (_store.SyncRoot)
        {
            var user = _store.Users.FirstOrDefault(x => x.Id == userId);
            if (user is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "User not found");
            }

            if (user.Password != request.CurrentPassword)
            {
                return ErrorResponse(StatusCodes.Status401Unauthorized, "Current password is incorrect");
            }

            user.Password = request.NewPassword;
            return OkResponse(new { passwordChanged = true }, "Password updated");
        }
    }

    [HttpPost("avatar")]
    public IActionResult UploadAvatar([FromForm] IFormFile? file)
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

        lock (_store.SyncRoot)
        {
            if (!_store.ProfilesByUserId.TryGetValue(userId, out var profile))
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "Profile not found");
            }

            var extension = Path.GetExtension(file.FileName);
            var avatarUrl = $"https://cdn.example.com/avatar/{userId}-{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
            profile.AvatarUrl = avatarUrl;

            return OkResponse(new { avatarUrl }, "Avatar updated");
        }
    }

    private static object ToResponse(Services.Models.ProfileRecord x)
    {
        return new
        {
            fullName = x.FullName,
            username = x.Username,
            email = x.Email,
            phone = x.Phone,
            gender = x.Gender,
            dob = x.Dob,
            address = x.Address,
            city = x.City,
            state = x.State,
            pincode = x.Pincode,
            notifEmail = x.NotifEmail,
            notifSms = x.NotifSms,
            notifWhatsApp = x.NotifWhatsApp,
            avatarUrl = x.AvatarUrl
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
