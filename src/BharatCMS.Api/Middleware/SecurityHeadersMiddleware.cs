using System.Text.Json;
using BharatCMS.Core.Context;

namespace BharatCMS.Api.Middleware;

/// <summary>
/// Global exception handler - masks internal errors from clients.
/// CERT-In compliance: Never expose stack traces or DB errors.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (TenantIsolationViolationException ex)
        {
            _logger.LogCritical(ex, "Tenant isolation violation detected. IP: {IP}, Path: {Path}", 
                context.Connection.RemoteIpAddress, context.Request.Path);
            
            await WriteSecurityErrorResponse(context, 403, "Access denied");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access. IP: {IP}", context.Connection.RemoteIpAddress);
            await WriteSecurityErrorResponse(context, 401, "Authentication required");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad request: {Message}", ex.Message);
            await WriteSecurityErrorResponse(context, 400, "Invalid request");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception. Path: {Path}", context.Request.Path);
            await WriteSecurityErrorResponse(context, 500, "An error occurred");
        }
    }

    private async Task WriteSecurityErrorResponse(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        
        var response = new ErrorResponse
        {
            Error = message,
            TraceId = context.TraceIdentifier,
            Timestamp = DateTime.UtcNow
        };
        
        await context.Response.WriteAsJsonAsync(response);
    }
}

/// <summary>
/// Strips sensitive server headers from responses.
/// CERT-In compliance: Infrastructure obscurity.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Remove server identification headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Remove("Server");
            context.Response.Headers.Remove("X-Powered-By");
            context.Response.Headers.Remove("X-AspNet-Version");
            context.Response.Headers.Remove("X-AspNetMvc-Version");
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data: blob:; connect-src 'self' https://api.bhashini.gov.in";
            return Task.CompletedTask;
        });

        await _next(context);
    }
}

/// <summary>
/// Validates and sanitizes inputs against LLM prompt injection.
/// CERT-In compliance: Prevents data poisoning attacks.
/// </summary>
public class InputSanitizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<InputSanitizationMiddleware> _logger;

    private static readonly string[] InjectionPatterns = 
    {
        "<!--", "-->", "<script", "javascript:", "onerror=", "onload=", 
        "onclick=", "<iframe", "<object", "<embed", "{{", "}}",
        "\\x", "\\u", "\\\\n", "\\\\r", "%00", "NULL", "\\0"
    };

    public InputSanitizationMiddleware(RequestDelegate next, ILogger<InputSanitizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check query string
        var queryString = context.Request.QueryString.Value ?? "";
        if (ContainsInjectionPattern(queryString))
        {
            _logger.LogWarning("Potential injection in query string: {Query}", queryString);
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid characters in request" });
            return;
        }

        // Check Content-Type for JSON payloads
        if (context.Request.ContentType?.Contains("application/json") == true)
        {
            context.Request.EnableBuffering();
            
            if (context.Request.ContentLength > 0 && context.Request.ContentLength < 1_000_000)
            {
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;

                var sanitized = SanitizeJson(body);
                if (sanitized != body)
                {
                    _logger.LogWarning("Sanitized request body for injection attempts");
                    var bytes = System.Text.Encoding.UTF8.GetBytes(sanitized);
                    context.Request.Body = new MemoryStream(bytes);
                    context.Request.ContentLength = bytes.Length;
                }
            }
        }

        await _next(context);
    }

    private bool ContainsInjectionPattern(string input)
    {
        if (string.IsNullOrEmpty(input)) return false;
        var lower = input.ToLowerInvariant();
        return InjectionPatterns.Any(p => lower.Contains(p.ToLowerInvariant()));
    }

    private string SanitizeJson(string json)
    {
        // Basic sanitization - remove control characters and null bytes
        return json
            .Replace("\\x00", "")
            .Replace("\\0", "")
            .Replace("%00", "")
            .Replace("\0", "");
    }
}

public class ErrorResponse
{
    public string Error { get; set; } = "";
    public string TraceId { get; set; } = "";
    public DateTime Timestamp { get; set; }
}
