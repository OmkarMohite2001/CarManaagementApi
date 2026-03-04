using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CarManaagementApi.Contracts;
using CarManaagementApi.Services;
using CarManaagementApi.Services.Models;
using CarManaagementApi.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CarManaagementApi.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ApiControllerBase
{
    private readonly IRentXStore _store;
    private readonly JwtSettings _jwtSettings;

    public AuthController(IRentXStore store, IOptions<JwtSettings> jwtOptions)
    {
        _store = store;
        _jwtSettings = jwtOptions.Value;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public IActionResult Login(LoginRequest request)
    {
        UserRecord? user;
        lock (_store.SyncRoot)
        {
            user = _store.Users.FirstOrDefault(x =>
                x.Active &&
                (x.Username.Equals(request.UsernameOrEmail, StringComparison.OrdinalIgnoreCase)
                || x.Email.Equals(request.UsernameOrEmail, StringComparison.OrdinalIgnoreCase))
                && x.Password == request.Password);

            if (user is null)
            {
                return ErrorResponse(StatusCodes.Status401Unauthorized, "Invalid username/email or password.");
            }

            user.LastLogin = DateTime.UtcNow;
        }

        var expiresInSeconds = _jwtSettings.AccessTokenExpiryMinutes * 60;
        var accessToken = CreateAccessToken(user);
        var refreshToken = CreateRefreshToken();

        lock (_store.SyncRoot)
        {
            _store.RefreshTokens.Add(new RefreshTokenRecord
            {
                Token = refreshToken,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
                Revoked = false
            });
        }

        return OkResponse(new
        {
            accessToken,
            refreshToken,
            expiresInSeconds,
            user = new
            {
                id = user.Id,
                name = user.Name,
                email = user.Email,
                role = user.Role
            }
        }, "Login successful");
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public IActionResult Register(RegisterRequest request)
    {
        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                new ApiErrorItem { Field = "confirmPassword", Code = "PasswordMismatch", Message = "Password and confirm password must match." }
            ]);
        }

        lock (_store.SyncRoot)
        {
            if (_store.Users.Any(x => x.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase)))
            {
                return ErrorResponse(StatusCodes.Status409Conflict, "Email already exists.");
            }

            var user = new UserRecord
            {
                Id = _store.NextId("U"),
                Name = request.FullName,
                Username = request.Email.Split('@')[0],
                Email = request.Email,
                Phone = string.Empty,
                Role = "viewer",
                Active = true,
                Password = request.Password,
                CreatedAt = DateTime.UtcNow
            };

            _store.Users.Add(user);
            _store.ProfilesByUserId[user.Id] = new ProfileRecord
            {
                FullName = request.FullName,
                Username = user.Username,
                Email = user.Email,
                Phone = string.Empty,
                Gender = "male",
                NotifEmail = true,
                NotifSms = false,
                NotifWhatsApp = false,
                AvatarUrl = string.Empty
            };

            return OkResponse(new
            {
                id = user.Id,
                fullName = user.Name,
                email = user.Email,
                role = user.Role
            }, "Registration successful");
        }
    }

    [HttpPost("forgot-password/send-code")]
    [AllowAnonymous]
    public IActionResult SendForgotPasswordCode(ForgotPasswordCodeRequest request)
    {
        return OkResponse(new { email = request.Email, sent = true }, "Recovery code sent");
    }

    [HttpPost("forgot-password/reset")]
    [AllowAnonymous]
    public IActionResult ResetForgotPassword(ResetPasswordRequest request)
    {
        if (!string.Equals(request.NewPassword, request.ConfirmNewPassword, StringComparison.Ordinal))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Validation failed", [
                new ApiErrorItem { Field = "confirmNewPassword", Code = "PasswordMismatch", Message = "New password and confirm new password must match." }
            ]);
        }

        lock (_store.SyncRoot)
        {
            var user = _store.Users.FirstOrDefault(x => x.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase));
            if (user is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "User not found.");
            }

            user.Password = request.NewPassword;
        }

        return OkResponse(new { reset = true }, "Password reset successful");
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public IActionResult Refresh(RefreshRequest request)
    {
        RefreshTokenRecord? refresh;
        UserRecord? user;

        lock (_store.SyncRoot)
        {
            refresh = _store.RefreshTokens.FirstOrDefault(x => x.Token == request.RefreshToken);
            if (refresh is null || refresh.Revoked || refresh.ExpiresAt <= DateTime.UtcNow)
            {
                return ErrorResponse(StatusCodes.Status401Unauthorized, "Invalid refresh token.");
            }

            user = _store.Users.FirstOrDefault(x => x.Id == refresh.UserId && x.Active);
            if (user is null)
            {
                return ErrorResponse(StatusCodes.Status401Unauthorized, "Invalid refresh token.");
            }

            refresh.Revoked = true;
        }

        var accessToken = CreateAccessToken(user);
        var newRefreshToken = CreateRefreshToken();
        var expiresInSeconds = _jwtSettings.AccessTokenExpiryMinutes * 60;

        lock (_store.SyncRoot)
        {
            _store.RefreshTokens.Add(new RefreshTokenRecord
            {
                Token = newRefreshToken,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
                Revoked = false
            });
        }

        return OkResponse(new
        {
            accessToken,
            refreshToken = newRefreshToken,
            expiresInSeconds,
            user = new
            {
                id = user.Id,
                name = user.Name,
                email = user.Email,
                role = user.Role
            }
        }, "Token refreshed");
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ErrorResponse(StatusCodes.Status401Unauthorized, "Unauthorized");
        }

        lock (_store.SyncRoot)
        {
            foreach (var token in _store.RefreshTokens.Where(x => x.UserId == userId && !x.Revoked))
            {
                token.Revoked = true;
            }
        }

        return OkResponse(new { loggedOut = true }, "Logout successful");
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ErrorResponse(StatusCodes.Status401Unauthorized, "Unauthorized");
        }

        lock (_store.SyncRoot)
        {
            var user = _store.Users.FirstOrDefault(x => x.Id == userId && x.Active);
            if (user is null)
            {
                return ErrorResponse(StatusCodes.Status404NotFound, "User not found");
            }

            return OkResponse(new
            {
                id = user.Id,
                name = user.Name,
                email = user.Email,
                role = user.Role
            });
        }
    }

    private string CreateAccessToken(UserRecord user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
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
