using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CarManaagementApi.Contracts;
using CarManaagementApi.Persistence;
using CarManaagementApi.Persistence.Entities;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CarManaagementApi.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ApiControllerBase
{
    private readonly RentXDbContext _db;
    private readonly JwtSettings _jwtSettings;

    public AuthController(RentXDbContext db, IOptions<JwtSettings> jwtOptions)
    {
        _db = db;
        _jwtSettings = jwtOptions.Value;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x =>
            x.IsActive &&
            (x.Username == request.UsernameOrEmail || x.Email == request.UsernameOrEmail) &&
            x.PasswordHash == request.Password);

        if (user is null)
        {
            return ErrorResponse(StatusCodes.Status401Unauthorized, "Invalid username/email or password.");
        }

        user.LastLoginAt = DateTime.UtcNow;

        var expiresInSeconds = _jwtSettings.AccessTokenExpiryMinutes * 60;
        var accessToken = CreateAccessToken(user);
        var refreshToken = CreateRefreshToken();

        var refreshExpiryDays = request.RememberMe
            ? Math.Max(_jwtSettings.RefreshTokenExpiryDays, 30)
            : _jwtSettings.RefreshTokenExpiryDays;

        _db.UserRefreshTokens.Add(new UserRefreshToken
        {
            UserId = user.UserId,
            TokenHash = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiryDays)
        });

        await _db.SaveChangesAsync();

        return OkResponse(new
        {
            accessToken,
            refreshToken,
            expiresInSeconds,
            user = new
            {
                id = user.UserId,
                name = user.FullName,
                email = user.Email,
                role = user.RoleCode
            }
        }, "Login successful");
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                new ApiErrorItem { Field = "confirmPassword", Code = "PasswordMismatch", Message = "Password and confirm password must match." }
            ]);
        }

        var emailExists = await _db.Users.AnyAsync(x => x.Email == request.Email);
        if (emailExists)
        {
            return ErrorResponse(StatusCodes.Status409Conflict, "Email already exists.");
        }

        var viewerRoleExists = await _db.Roles.AnyAsync(x => x.RoleCode == "viewer");
        if (!viewerRoleExists)
        {
            _db.Roles.Add(new Role
            {
                RoleCode = "viewer",
                RoleName = "Viewer",
                IsSystem = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        var userId = await IdGenerator.NextAsync(_db.Users.Select(x => x.UserId), "U");
        var username = request.Email.Split('@')[0];
        var usernameTaken = await _db.Users.AnyAsync(x => x.Username == username);
        if (usernameTaken)
        {
            username = $"{username}{DateTime.UtcNow:HHmmss}";
        }

        var user = new User
        {
            UserId = userId,
            Username = username,
            FullName = request.FullName,
            Email = request.Email,
            Phone = null,
            RoleCode = "viewer",
            PasswordHash = request.Password,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return OkResponse(new
        {
            id = user.UserId,
            fullName = user.FullName,
            email = user.Email,
            role = user.RoleCode
        }, "Registration successful");
    }

    [HttpPost("forgot-password/send-code")]
    [AllowAnonymous]
    public IActionResult SendForgotPasswordCode(ForgotPasswordCodeRequest request)
    {
        return OkResponse(new { email = request.Email, sent = true }, "Recovery code sent");
    }

    [HttpPost("forgot-password/reset")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetForgotPassword(ResetPasswordRequest request)
    {
        if (!string.Equals(request.NewPassword, request.ConfirmNewPassword, StringComparison.Ordinal))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                new ApiErrorItem { Field = "confirmNewPassword", Code = "PasswordMismatch", Message = "New password and confirm new password must match." }
            ]);
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == request.Email);
        if (user is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "User not found.");
        }

        user.PasswordHash = request.NewPassword;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return OkResponse(new { reset = true }, "Password reset successful");
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshRequest request)
    {
        var refresh = await _db.UserRefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == request.RefreshToken);

        if (refresh is null || refresh.RevokedAt.HasValue || refresh.ExpiresAt <= DateTime.UtcNow || !refresh.User.IsActive)
        {
            return ErrorResponse(StatusCodes.Status401Unauthorized, "Invalid refresh token.");
        }

        refresh.RevokedAt = DateTime.UtcNow;

        var accessToken = CreateAccessToken(refresh.User);
        var newRefreshToken = CreateRefreshToken();
        var expiresInSeconds = _jwtSettings.AccessTokenExpiryMinutes * 60;

        _db.UserRefreshTokens.Add(new UserRefreshToken
        {
            UserId = refresh.UserId,
            TokenHash = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays)
        });

        await _db.SaveChangesAsync();

        return OkResponse(new
        {
            accessToken,
            refreshToken = newRefreshToken,
            expiresInSeconds,
            user = new
            {
                id = refresh.User.UserId,
                name = refresh.User.FullName,
                email = refresh.User.Email,
                role = refresh.User.RoleCode
            }
        }, "Token refreshed");
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ErrorResponse(StatusCodes.Status401Unauthorized, "Unauthorized");
        }

        var tokens = await _db.UserRefreshTokens
            .Where(x => x.UserId == userId && !x.RevokedAt.HasValue)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return OkResponse(new { loggedOut = true }, "Logout successful");
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ErrorResponse(StatusCodes.Status401Unauthorized, "Unauthorized");
        }

        var user = await _db.Users.FirstOrDefaultAsync(x => x.UserId == userId && x.IsActive);
        if (user is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "User not found");
        }

        return OkResponse(new
        {
            id = user.UserId,
            name = user.FullName,
            email = user.Email,
            role = user.RoleCode
        });
    }

    private string CreateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.RoleCode)
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateRefreshToken()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    }

    public sealed class LoginRequest
    {
        public string UsernameOrEmail { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
    }

    public sealed class RegisterRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public sealed class ForgotPasswordCodeRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public sealed class ResetPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }

    public sealed class RefreshRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }
}
