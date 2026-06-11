using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace BharatCMS.Api.Middleware;

/// <summary>
/// JWT authentication with secure cookie delivery.
/// CERT-In: Tokens only via HttpOnly, Secure, SameSite=Strict cookies.
/// </summary>
public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _config;

    public AuthenticationMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var token = context.Request.Cookies["bharat_auth"];
        
        if (!string.IsNullOrEmpty(token) && context.User.Identity?.IsAuthenticated != true)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? throw new InvalidOperationException("JWT key not configured"));
                
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _config["Jwt:Issuer"],
                    ValidAudience = _config["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                var principal = handler.ValidateToken(token, validationParameters, out _);
                context.User = principal;
            }
            catch (SecurityTokenExpiredException)
            {
                // Clear expired token
                context.Response.Cookies.Delete("bharat_auth");
            }
            catch (Exception)
            {
                // Invalid token - clear it
                context.Response.Cookies.Delete("bharat_auth");
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Auth service for login/logout with secure cookie handling.
/// </summary>
public class AuthService
{
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IConfiguration config, ILogger<AuthService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public string GenerateToken(Guid userId, string email, Guid tenantId, IEnumerable<string> permissions)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new("tenant_id", tenantId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var perm in permissions)
        {
            claims.Add(new Claim("permission", perm));
        }

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public void SetAuthCookie(HttpResponse response, string token, bool rememberMe = false)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,      // No JS access - XSS protection
            Secure = true,        // HTTPS only
            SameSite = SameSiteMode.Strict, // CSRF protection
            Expires = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8),
            Path = "/",
            IsEssential = true
        };

        response.Cookies.Append("bharat_auth", token, cookieOptions);
    }

    public void ClearAuthCookie(HttpResponse response)
    {
        response.Cookies.Delete("bharat_auth", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });
    }

    /// <summary>
    /// BCrypt password hashing with salt.
    /// </summary>
    public string HashPassword(string password, out string salt)
    {
        salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        return BCrypt.Net.BCrypt.HashPassword(password + salt, workFactor: 12);
    }

    public bool VerifyPassword(string password, string hash, string salt)
    {
        return BCrypt.Net.BCrypt.Verify(password + salt, hash);
    }
}
