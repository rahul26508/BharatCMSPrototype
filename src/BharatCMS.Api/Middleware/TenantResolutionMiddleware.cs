using BharatCMS.Core.Context;

namespace BharatCMS.Api.Middleware;

/// <summary>
/// Intercepts requests to resolve tenant from sub-path.
/// CERT-In: Enforces tenant isolation at routing layer.
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    // Reserved paths that don't require tenant resolution
    private static readonly string[] ExcludedPaths = 
    {
        "/swagger", "/health", "/api/health", "/api/tenants", "/identity", "/.well-known"
    };

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, TenantContextData tenantResolver)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip excluded paths
        if (ExcludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Extract tenant slug from sub-path (first segment)
        var slug = ExtractTenantSlug(path);
        
        if (string.IsNullOrEmpty(slug))
        {
            _logger.LogWarning("No tenant slug in path: {Path}", path);
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant identifier required" });
            return;
        }

        // Resolve tenant
        var tenant = await tenantResolver.GetBySlugAsync(slug);
        
        if (tenant == null)
        {
            _logger.LogWarning("Tenant not found: {Slug}", slug);
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new { error = "Department not found" });
            return;
        }

        if (!tenant.IsActive)
        {
            _logger.LogWarning("Inactive tenant access attempt: {Slug}", slug);
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { error = "Department service is currently inactive" });
            return;
        }

        // Set tenant context
        tenantContext.SetTenant(tenant.Id);
        tenantContext.SetTenantBySlug(slug);

        // Store tenant info for downstream use
        context.Items["Tenant"] = tenant;
        context.Items["TenantSlug"] = slug;

        // Rewrite path to remove tenant segment (if not root)
        if (path.Length > slug.Length + 1)
        {
            var newPath = path[(slug.Length + 1)..];
            context.Request.Path = newPath;
        }
        else
        {
            context.Request.Path = "/";
        }

        await _next(context);
    }

    private static string? ExtractTenantSlug(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/") return null;

        // Handle both /tenant and /tenant/ patterns
        var segments = path.Trim('/').Split('/');
        if (segments.Length > 0 && !string.IsNullOrEmpty(segments[0]))
        {
            // Validate slug format (alphanumeric, lowercase, hyphen/underscore allowed)
            var slug = segments[0].ToLowerInvariant();
            if (slug.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
            {
                return slug;
            }
        }
        return null;
    }
}

/// <summary>
/// Service for resolving tenant data.
/// </summary>
public class TenantContextData
{
    private readonly IServiceProvider _serviceProvider;

    public TenantContextData(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<BharatCMS.Domain.Entities.Tenant?> GetBySlugAsync(string slug)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BharatDbContext>();
        
        // Bypass tenant filter for lookup
        dbContext.BypassTenantFilter();
        
        return await dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Slug == slug.ToLowerInvariant());
    }
}
using Microsoft.EntityFrameworkCore;
