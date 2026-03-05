using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CarManaagementApi.Contracts;
using CarManaagementApi.Persistence;
using CarManaagementApi.Persistence.Entities;
using CarManaagementApi.Services;
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
    private readonly EmailVerificationSettings _emailVerificationSettings;
    private readonly IEmailSender _emailSender;
    private readonly IWebHostEnvironment _environment;

    public AuthController(
        RentXDbContext db,
        IOptions<JwtSettings> jwtOptions,
        IOptions<EmailVerificationSettings> emailVerificationOptions,
        IEmailSender emailSender,
        IWebHostEnvironment environment)
    {
        _db = db;
        _jwtSettings = jwtOptions.Value;
        _emailVerificationSettings = emailVerificationOptions.Value;
        _emailSender = emailSender;
        _environment = environment;
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

        if (!user.IsEmailVerified)
        {
            return ErrorResponse(StatusCodes.Status403Forbidden, "Email not verified. Please verify your email before login.");
        }

        var now = DateTime.UtcNow;
        var clientIp = GetClientIp();
        var userAgent = GetUserAgent();
        user.LastLoginAt = now;

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
            ExpiresAt = now.AddDays(refreshExpiryDays)
        });

        _db.UserAuthLogs.Add(new UserAuthLog
        {
            UserId = user.UserId,
            RoleCode = user.RoleCode,
            LoginAt = now,
            LoginIp = clientIp,
            UserAgent = userAgent,
            Source = "web",
            CreatedAt = now
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
            IsEmailVerified = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        string verificationCode;
        try
        {
            verificationCode = await CreateAndSendVerificationCodeAsync(user);
        }
        catch
        {
            return ErrorResponse(StatusCodes.Status500InternalServerError, "Account created, but failed to send verification email.");
        }

        var response = new Dictionary<string, object?>
        {
            ["id"] = user.UserId,
            ["fullName"] = user.FullName,
            ["email"] = user.Email,
            ["role"] = user.RoleCode,
            ["emailVerified"] = user.IsEmailVerified,
            ["verificationSent"] = true
        };

        if (ShouldExposeVerificationCode())
        {
            response["verificationCode"] = verificationCode;
        }

        return OkResponse(response, "Registration successful. Verification code sent to email.");
    }

    [HttpPost("email-verification/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == request.Email);
        if (user is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "User not found.");
        }

        if (user.IsEmailVerified)
        {
            return OkResponse(new { email = user.Email, verified = true }, "Email already verified.");
        }

        var now = DateTime.UtcNow;
        var maxAttempts = GetMaxVerifyAttempts();

        var token = await _db.UserEmailVerifications
            .Where(x => x.UserId == user.UserId && x.VerifiedAt == null && x.ExpiresAt > now)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (token is null)
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Invalid or expired verification code.");
        }

        if (token.FailedAttempts >= maxAttempts)
        {
            return ErrorResponse(StatusCodes.Status429TooManyRequests, "Maximum verification attempts reached. Please request a new code.");
        }

        var providedCode = request.Code?.Trim() ?? string.Empty;
        if (!string.Equals(token.VerificationCode, providedCode, StringComparison.Ordinal))
        {
            token.FailedAttempts = (byte)Math.Min(byte.MaxValue, token.FailedAttempts + 1);
            var remainingAttempts = Math.Max(0, maxAttempts - token.FailedAttempts);

            if (remainingAttempts == 0)
            {
                token.ExpiresAt = now.AddSeconds(-1);
            }

            await _db.SaveChangesAsync();

            if (remainingAttempts == 0)
            {
                return ErrorResponse(StatusCodes.Status429TooManyRequests, "Maximum verification attempts reached. Please request a new code.");
            }

            return ErrorResponse(StatusCodes.Status400BadRequest, "Invalid verification code.", [
                new ApiErrorItem
                {
                    Field = "code",
                    Code = "InvalidCode",
                    Message = $"Invalid verification code. {remainingAttempts} attempt(s) remaining."
                }
            ]);
        }

        token.VerifiedAt = now;
        user.IsEmailVerified = true;
        user.UpdatedAt = now;

        await _db.SaveChangesAsync();

        return OkResponse(new { email = user.Email, verified = true }, "Email verified successfully.");
    }

    [HttpPost("email-verification/resend")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendVerificationCode(ResendVerificationRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == request.Email);
        if (user is null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, "User not found.");
        }

        if (user.IsEmailVerified)
        {
            return ErrorResponse(StatusCodes.Status409Conflict, "Email is already verified.");
        }

        var now = DateTime.UtcNow;
        var cooldownSeconds = GetResendCooldownSeconds();
        var maxAttempts = GetMaxVerifyAttempts();
        if (cooldownSeconds > 0)
        {
            var latestOpenCode = await _db.UserEmailVerifications
                .Where(x => x.UserId == user.UserId && x.VerifiedAt == null && x.ExpiresAt > now)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (latestOpenCode is not null && latestOpenCode.FailedAttempts < maxAttempts)
            {
                var elapsedSeconds = (int)Math.Floor((now - latestOpenCode.CreatedAt).TotalSeconds);
                var retryAfterSeconds = cooldownSeconds - elapsedSeconds;
                if (retryAfterSeconds > 0)
                {
                    return ErrorResponse(StatusCodes.Status429TooManyRequests, $"Please wait {retryAfterSeconds} seconds before requesting a new code.");
                }
            }
        }

        string verificationCode;
        try
        {
            verificationCode = await CreateAndSendVerificationCodeAsync(user);
        }
        catch
        {
            return ErrorResponse(StatusCodes.Status500InternalServerError, "Failed to send verification email.");
        }

        var response = new Dictionary<string, object?>
        {
            ["email"] = user.Email,
            ["sent"] = true
        };

        if (ShouldExposeVerificationCode())
        {
            response["verificationCode"] = verificationCode;
        }

        return OkResponse(response, "Verification code sent.");
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

        var now = DateTime.UtcNow;
        var clientIp = GetClientIp();

        var tokens = await _db.UserRefreshTokens
            .Where(x => x.UserId == userId && !x.RevokedAt.HasValue)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.RevokedAt = now;
        }

        var openLogs = await _db.UserAuthLogs
            .Where(x => x.UserId == userId && !x.LogoutAt.HasValue)
            .ToListAsync();

        foreach (var log in openLogs)
        {
            log.LogoutAt = now;
            log.LogoutIp = clientIp;
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
            role = user.RoleCode,
            emailVerified = user.IsEmailVerified
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
            new(ClaimTypes.Role, user.RoleCode),
            new("email_verified", user.IsEmailVerified ? "true" : "false")
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> CreateAndSendVerificationCodeAsync(User user)
    {
        var now = DateTime.UtcNow;
        var codeExpiryMinutes = GetCodeExpiryMinutes();
        var openTokens = await _db.UserEmailVerifications
            .Where(x => x.UserId == user.UserId && x.VerifiedAt == null && x.ExpiresAt > now)
            .ToListAsync();

        foreach (var token in openTokens)
        {
            token.ExpiresAt = now;
        }

        var code = GenerateVerificationCode();
        _db.UserEmailVerifications.Add(new UserEmailVerification
        {
            UserId = user.UserId,
            VerificationCode = code,
            ExpiresAt = now.AddMinutes(codeExpiryMinutes),
            FailedAttempts = 0,
            CreatedAt = now
        });

        await _db.SaveChangesAsync();

        var subject = "RentX Email Verification Code";
        var htmlBody = $"<p>Hello {user.FullName},</p><p>Your verification code is <b>{code}</b>.</p><p>This code will expire in {codeExpiryMinutes} minutes.</p>";

        await _emailSender.SendEmailAsync(user.Email, subject, htmlBody);
        return code;
    }

    private static string GenerateVerificationCode()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }

    private static string CreateRefreshToken()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    }

    private string? GetClientIp()
    {
        if (Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) && !string.IsNullOrWhiteSpace(forwardedFor))
        {
            var ip = forwardedFor.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(ip))
            {
                return ip;
            }
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string? GetUserAgent()
    {
        var userAgent = Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        return userAgent.Length <= 512 ? userAgent : userAgent[..512];
    }

    private int GetCodeExpiryMinutes()
    {
        return Math.Clamp(_emailVerificationSettings.CodeExpiryMinutes, 1, 60);
    }

    private int GetResendCooldownSeconds()
    {
        return Math.Clamp(_emailVerificationSettings.ResendCooldownSeconds, 0, 300);
    }

    private byte GetMaxVerifyAttempts()
    {
        return (byte)Math.Clamp(_emailVerificationSettings.MaxVerifyAttempts, 1, 10);
    }

    private bool ShouldExposeVerificationCode()
    {
        return _environment.IsDevelopment() && _emailVerificationSettings.ExposeCodeInResponseInDevelopment;
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

    public sealed class VerifyEmailRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public sealed class ResendVerificationRequest
    {
        public string Email { get; set; } = string.Empty;
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
