using CarManaagementApi.Persistence;
using CarManaagementApi.Persistence.Entities;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CarManaagementApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/me")]
public class MeController : ApiControllerBase
{
    private static readonly Regex CustomerPhoneRegex = new("^[0-9]{10}$", RegexOptions.Compiled);

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

        var response = await BuildRoleBasedProfileResponseAsync(user);
        return OkResponse(response);
    }

    // Single role-aware GET API for customer/admin/branch manager/branch supervisor profiles.
    [HttpGet("profile-by-role")]
    public async Task<IActionResult> GetProfileByRole()
    {
        return await GetProfile();
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

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                new ApiErrorItem { Field = "fullName", Code = "Required", Message = "fullName is required." }
            ]);
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                new ApiErrorItem { Field = "username", Code = "Required", Message = "username is required." }
            ]);
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                new ApiErrorItem { Field = "email", Code = "InvalidEmail", Message = "Email format is invalid." }
            ]);
        }

        var usernameExists = await _db.Users.AnyAsync(x => x.UserId != userId && x.Username == request.Username);
        if (usernameExists)
        {
            return ErrorResponse(StatusCodes.Status409Conflict, "Username already exists.");
        }

        var emailExists = await _db.Users.AnyAsync(x => x.UserId != userId && x.Email == request.Email);
        if (emailExists)
        {
            return ErrorResponse(StatusCodes.Status409Conflict, "Email already exists.");
        }

        var normalizedRole = NormalizeRole(user.RoleCode);
        var now = DateTime.UtcNow;
        var oldEmail = user.Email;
        var oldPhone = user.Phone;

        user.FullName = request.FullName.Trim();
        user.Username = request.Username.Trim();
        user.Email = request.Email.Trim();
        user.Phone = ToNullIfWhiteSpace(request.Phone);
        user.UpdatedAt = now;

        if (normalizedRole == "customer")
        {
            var customerPhone = request.Phone.Trim();
            if (!CustomerPhoneRegex.IsMatch(customerPhone))
            {
                return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                    new ApiErrorItem { Field = "phone", Code = "InvalidPhone", Message = "Customer phone must be 10 digits." }
                ]);
            }

            var customer = await FindLinkedCustomerForUpdateAsync(oldEmail, oldPhone, request.Email, request.Phone);
            if (customer is null)
            {
                var customerId = await IdGenerator.NextAsync(_db.Customers.Select(x => x.CustomerId), "CUS");
                _db.Customers.Add(new Customer
                {
                    CustomerId = customerId,
                    CustomerType = "individual",
                    Name = request.FullName.Trim(),
                    Phone = customerPhone,
                    Email = request.Email.Trim(),
                    Dob = request.Dob,
                    KycType = "aadhaar",
                    KycNumber = GeneratePendingKycNumber(),
                    DlNumber = null,
                    DlExpiry = null,
                    Address = ToNullIfWhiteSpace(request.Address),
                    City = ToNullIfWhiteSpace(request.City),
                    State = ToNullIfWhiteSpace(request.State),
                    Pincode = ToNullIfWhiteSpace(request.Pincode),
                    CreatedAt = now
                });
            }
            else
            {
                customer.Name = request.FullName.Trim();
                customer.Phone = customerPhone;
                customer.Email = request.Email.Trim();
                customer.Dob = request.Dob;
                customer.Address = ToNullIfWhiteSpace(request.Address);
                customer.City = ToNullIfWhiteSpace(request.City);
                customer.State = ToNullIfWhiteSpace(request.State);
                customer.Pincode = ToNullIfWhiteSpace(request.Pincode);
                customer.UpdatedAt = now;
            }
        }
        else if (normalizedRole == "branch_manager" || normalizedRole == "branch_supervisor")
        {
            var customer = await FindLinkedCustomerForUpdateAsync(oldEmail, oldPhone, request.Email, request.Phone);
            if (customer is not null)
            {
                if (!string.IsNullOrWhiteSpace(request.Phone) && CustomerPhoneRegex.IsMatch(request.Phone.Trim()))
                {
                    customer.Phone = request.Phone.Trim();
                }

                customer.Name = request.FullName.Trim();
                customer.Email = request.Email.Trim();
                customer.Dob = request.Dob;
                customer.Address = ToNullIfWhiteSpace(request.Address);
                customer.City = ToNullIfWhiteSpace(request.City);
                customer.State = ToNullIfWhiteSpace(request.State);
                customer.Pincode = ToNullIfWhiteSpace(request.Pincode);
                customer.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync();

        var response = await BuildRoleBasedProfileResponseAsync(user);
        return OkResponse(response, "Profile updated");
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
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAvatar(IFormFile? file)
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

    private async Task<object> BuildRoleBasedProfileResponseAsync(User user)
    {
        var normalizedRole = NormalizeRole(user.RoleCode);
        var (firstName, lastName) = SplitName(user.FullName);
        var phone = user.Phone ?? string.Empty;

        var customer = await _db.Customers
            .AsNoTracking()
            .Where(x =>
                (x.Email != null && x.Email == user.Email) ||
                (!string.IsNullOrWhiteSpace(phone) && x.Phone == phone))
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync();

        if (normalizedRole == "customer")
        {
            return new
            {
                profileType = "customer",
                role = normalizedRole,
                originalRole = user.RoleCode,
                userId = user.UserId,
                customerId = customer?.CustomerId,
                firstName,
                lastName,
                fullName = customer?.Name ?? user.FullName,
                username = user.Username,
                email = customer?.Email ?? user.Email,
                phone = customer?.Phone ?? phone,
                gender = string.Empty,
                dob = customer?.Dob,
                address = customer?.Address ?? string.Empty,
                city = customer?.City ?? string.Empty,
                state = customer?.State ?? string.Empty,
                pincode = customer?.Pincode ?? string.Empty,
                drivingLicenseNumber = customer?.DlNumber ?? string.Empty,
                drivingLicenseExpiry = customer?.DlExpiry,
                kycType = customer?.KycType ?? string.Empty,
                kycNumber = customer?.KycNumber ?? string.Empty,
                notifEmail = true,
                notifSms = false,
                notifWhatsApp = false,
                avatarUrl = "https://cdn.example.com/avatar/" + user.UserId + ".png"
            };
        }

        if (normalizedRole == "branch_manager" || normalizedRole == "branch_supervisor")
        {
            var linkedBranch = await _db.Branches
                .AsNoTracking()
                .Where(x =>
                    (x.Email != null && x.Email == user.Email) ||
                    (x.Phone != null && x.Phone == phone))
                .Select(x => new
                {
                    id = x.BranchId,
                    name = x.Name,
                    city = x.City ?? string.Empty,
                    state = x.State ?? string.Empty
                })
                .FirstOrDefaultAsync();

            return new
            {
                profileType = normalizedRole,
                role = normalizedRole,
                originalRole = user.RoleCode,
                userId = user.UserId,
                firstName,
                lastName,
                fullName = user.FullName,
                username = user.Username,
                email = user.Email,
                phone,
                gender = string.Empty,
                dob = customer?.Dob,
                address = customer?.Address ?? string.Empty,
                city = customer?.City ?? string.Empty,
                state = customer?.State ?? string.Empty,
                pincode = customer?.Pincode ?? string.Empty,
                branch = linkedBranch,
                lastLoginAt = user.LastLoginAt,
                emailVerified = user.IsEmailVerified,
                notifEmail = true,
                notifSms = false,
                notifWhatsApp = false,
                avatarUrl = "https://cdn.example.com/avatar/" + user.UserId + ".png"
            };
        }

        return new
        {
            profileType = "admin",
            role = normalizedRole,
            originalRole = user.RoleCode,
            userId = user.UserId,
            firstName,
            lastName,
            fullName = user.FullName,
            username = user.Username,
            email = user.Email,
            phone,
            gender = string.Empty,
            dob = customer?.Dob,
            address = customer?.Address ?? string.Empty,
            city = customer?.City ?? string.Empty,
            state = customer?.State ?? string.Empty,
            pincode = customer?.Pincode ?? string.Empty,
            lastLoginAt = user.LastLoginAt,
            emailVerified = user.IsEmailVerified,
            notifEmail = true,
            notifSms = false,
            notifWhatsApp = false,
            avatarUrl = "https://cdn.example.com/avatar/" + user.UserId + ".png"
        };
    }

    private static string NormalizeRole(string roleCode)
    {
        var role = roleCode.Trim().ToLowerInvariant();
        return role switch
        {
            "viewer" => "customer",
            "ops" => "branch_manager",
            "agent" => "branch_supervisor",
            _ => role
        };
    }

    private static (string firstName, string lastName) SplitName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return (string.Empty, string.Empty);
        }

        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            return (parts[0], string.Empty);
        }

        return (parts[0], string.Join(' ', parts.Skip(1)));
    }

    private async Task<Customer?> FindLinkedCustomerForUpdateAsync(string oldEmail, string? oldPhone, string newEmail, string? newPhone)
    {
        var hasOldEmail = !string.IsNullOrWhiteSpace(oldEmail);
        var hasOldPhone = !string.IsNullOrWhiteSpace(oldPhone);
        var hasNewEmail = !string.IsNullOrWhiteSpace(newEmail);
        var hasNewPhone = !string.IsNullOrWhiteSpace(newPhone);

        if (!hasOldEmail && !hasOldPhone && !hasNewEmail && !hasNewPhone)
        {
            return null;
        }

        return await _db.Customers
            .Where(x =>
                (hasOldEmail && x.Email == oldEmail) ||
                (hasOldPhone && x.Phone == oldPhone) ||
                (hasNewEmail && x.Email == newEmail) ||
                (hasNewPhone && x.Phone == newPhone))
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync();
    }

    private static string? ToNullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string GeneratePendingKycNumber()
    {
        return "PENDING-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
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
