using BharatCMS.Api.Middleware;
using BharatCMS.Core.Context;
using BharatCMS.Core.Interfaces;
using BharatCMS.Infrastructure.Repositories;
using BharatCMS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// === SECURITY: Load secrets from environment/vault only ===
var jwtKey = Environment.GetEnvironmentVariable("BHARAT_JWT_KEY")
    ?? throw new InvalidOperationException("BHARAT_JWT_KEY environment variable is required");
var dbConnection = Environment.GetEnvironmentVariable("BHARAT_DB_CONNECTION")
    ?? throw new InvalidOperationException("BHARAT_DB_CONNECTION environment variable is required");

// Add JWT configuration
builder.Configuration["Jwt:Key"] = jwtKey;
builder.Configuration["Jwt:Issuer"] = "BharatCMS";
builder.Configuration["Jwt:Audience"] = "BharatCMS.Api";

// === DATABASE: SQL Server with multi-tenant context ===
builder.Services.AddDbContext<BharatDbContext>(options =>
{
    options.UseSqlServer(dbConnection, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(10), null);
        sqlOptions.CommandTimeout(30);
    });
    options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
});

// === MULTI-TENANCY ===
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<TenantContextData>();

// === AUTHENTICATION & AUTHORIZATION ===
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "BharatCMS",
            ValidAudience = "BharatCMS.Api",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(5)
        };
        // Tokens delivered via secure cookies only, not Bearer
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var cookieToken = context.Request.Cookies["bharat_auth"];
                if (!string.IsNullOrEmpty(cookieToken))
                {
                    context.Token = cookieToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// RBAC Policy-based authorization
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", policy => policy.RequireClaim("permission", "admin", "system.admin"))
    .AddPolicy("Manager", policy => policy.RequireClaim("permission", "admin", "manager", "system.admin"))
    .AddPolicy("Editor", policy => policy.RequireClaim("permission", "admin", "editor", "manager", "system.admin"))
    .AddPolicy("Viewer", policy => policy.RequireClaim("permission", "viewer", "editor", "admin", "system.admin"));

// === SERVICES ===
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IChatRepository, ChatRepository>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AiService>();
builder.Services.AddScoped<BhashiniService>();
builder.Services.AddScoped<RagChatbotService>();
builder.Services.AddScoped<FileValidationService>();

// === HOSTED SERVICES (Background Workers) ===
builder.Services.AddHostedService<ApiSyncScheduler>();  // Cron for API integrations
builder.Services.AddHostedService<OcrProcessor>();       // PDF OCR queue processor

// === API DOCUMENTATION ===
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BharatCMS API",
        Version = "v1.0.0",
        Description = "Indian Government Multi-Tenant CMS API - CERT-In Compliant"
    });
    c.AddSecurityDefinition("CookieAuth", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Name = "bharat_auth",
        Description = "JWT token passed via HttpOnly secure cookie"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "CookieAuth" } },
            Array.Empty<string>()
        }
    });
});

// === CORS ===
builder.Services.AddCors(options =>
{
    options.AddPolicy("GovernmentOrigin", policy =>
    {
        policy.WithOrigins("https://*.gov.in", "https://*.nic.in")
            .AllowCredentials()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// === HEALTH CHECKS ===
builder.Services.AddHealthChecks()
    .AddSqlServer(dbConnection, name: "sqlserver");

var app = builder.Build();

// === SECURITY MIDDLEWARE PIPELINE (order matters!) ===

// 1. Security headers (strip server info)
app.UseMiddleware<SecurityHeadersMiddleware>();

// 2. Input sanitization (LLM prompt injection prevention)
app.UseMiddleware<InputSanitizationMiddleware>();

// 3. Tenant resolution (sub-path routing)
app.UseMiddleware<TenantResolutionMiddleware>();

// 4. Exception handling (mask errors)
app.UseMiddleware<GlobalExceptionMiddleware>();

// 5. Authentication
app.UseMiddleware<AuthenticationMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

// === API ENDPOINTS ===
app.MapHealthChecks("/health");

// Auth endpoints
app.MapAuthEndpoints();

// Tenant endpoints (admin only)
app.MapTenantEndpoints();

// Content endpoints (notices, FAQs, documents)
app.MapContentEndpoints();

// Chatbot endpoint
app.MapChatbotEndpoints();

// File upload endpoint
app.MapFileEndpoints();

// Sync endpoints (PWA)
app.MapSyncEndpoints();

// Swagger (only in dev)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();

// === MINIMAL API EXTENSIONS ===

void MapAuthEndpoints()
{
    var group = app.MapGroup("/api/auth").WithTags("Authentication");

    group.MapPost("/login", async (LoginRequest request, AuthService auth, BharatDbContext db) =>
    {
        var user = await db.Users
            .Include(u => u.Role)
            .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

        if (user == null || !auth.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt ?? ""))
        {
            return Results.Unauthorized();
        }

        var permissions = user.Role.Permissions.Select(p => p.Permission).ToList();
        var token = auth.GenerateToken(user.Id, user.Email, user.TenantId, permissions);
        auth.SetAuthCookie(app.Response, token, request.RememberMe);

        await db.Users.FirstOrDefaultAsync(u => u.Id == user.Id)!;
        // Log audit
        await db.AuditLogs.AddAsync(new BharatCMS.Domain.Entities.AuditLog
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            UserEmail = user.Email,
            Action = "LOGIN",
            RequestPath = "/api/auth/login",
            IpAddress = app.Context?.Connection.RemoteIpAddress?.ToString(),
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return Results.Ok(new { userId = user.Id, email = user.Email, name = user.FullName });
    }).WithName("Login");

    group.MapPost("/logout", () =>
    {
        var authService = app.Services.GetRequiredService<AuthService>();
        authService.ClearAuthCookie(app.Response);
        return Results.Ok();
    }).WithName("Logout");

    group.MapGet("/me", (HttpContext context) =>
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
            return Results.Unauthorized();

        return Results.Ok(new
        {
            userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            email = context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
            tenantId = context.User.FindFirst("tenant_id")?.Value
        });
    }).RequireAuthorization().WithName("GetCurrentUser");
}

void MapTenantEndpoints()
{
    var group = app.MapGroup("/api/tenants").WithTags("Tenants");

    group.MapGet("/", async (TenantContextData resolver) =>
    {
        var db = app.Services.GetRequiredService<BharatDbContext>();
        db.BypassTenantFilter();
        var tenants = await db.Tenants.Where(t => t.IsActive).ToListAsync();
        return Results.Ok(tenants.Select(t => new { t.Id, t.Slug, t.Name, t.Type }));
    }).WithName("GetTenants");

    group.MapPost("/", async (CreateTenantRequest request, BharatDbContext db) =>
    {
        var tenant = new BharatCMS.Domain.Entities.Tenant
        {
            Slug = request.Slug.ToLowerInvariant(),
            Name = request.Name,
            Type = request.Type,
            Metadata = request.Metadata,
            SupportedLanguages = request.Languages ?? "en,hi",
            TenantId = Guid.Empty // System entity
        };

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        return Results.Created($"/api/tenants/{tenant.Id}", new { tenant.Id, tenant.Slug });
    }).RequireAuthorization("Admin").WithName("CreateTenant");
}

void MapContentEndpoints()
{
    var group = app.MapGroup("/api").WithTags("Content");

    // Notices
    group.MapGet("/notices", async (BharatDbContext db, int page = 1, int pageSize = 20, string? search = null) =>
    {
        var query = db.Notices.Where(n => n.IsPublished);

        if (!string.IsNullOrEmpty(search))
        {
            search = search.ToLower();
            query = query.Where(n => n.Title.ToLower().Contains(search) ||
                                     (n.TitleHi != null && n.TitleHi.Contains(search)));
        }

        var total = await query.CountAsync();
        var notices = await query
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.PublishFrom)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return Results.Ok(new { data = notices, total, page, pageSize });
    }).WithName("GetNotices");

    group.MapPost("/notices", async (CreateNoticeRequest request, BharatDbContext db, HttpContext context) =>
    {
        var tenantId = GetCurrentTenantId(context);
        var userId = GetCurrentUserId(context);

        var notice = new BharatCMS.Domain.Entities.Notice
        {
            TenantId = tenantId.Value,
            Title = request.Title,
            Content = request.Content,
            TitleHi = request.TitleHi,
            ContentHi = request.ContentHi,
            Priority = request.Priority,
            IsPinned = request.IsPinned,
            PublishFrom = request.PublishFrom,
            IsPublished = true,
            CreatedBy = userId ?? Guid.Empty
        };

        db.Notices.Add(notice);
        await db.SaveChangesAsync();

        return Results.Created($"/api/notices/{notice.Id}", notice);
    }).RequireAuthorization("Editor").WithName("CreateNotice");

    // FAQs
    group.MapGet("/faqs", async (BharatDbContext db, string? category = null) =>
    {
        var query = db.Faqs.Where(f => f.IsPublished);
        if (!string.IsNullOrEmpty(category))
            query = query.Where(f => f.Category == category);

        return Results.Ok(await query.ToListAsync());
    }).WithName("GetFaqs");

    group.MapPost("/faqs", async (CreateFaqRequest request, BharatDbContext db, HttpContext context) =>
    {
        var tenantId = GetCurrentTenantId(context);

        var faq = new BharatCMS.Domain.Entities.Faq
        {
            TenantId = tenantId.Value,
            Question = request.Question,
            Answer = request.Answer,
            QuestionHi = request.QuestionHi,
            AnswerHi = request.AnswerHi,
            Category = request.Category,
            Keywords = request.Keywords,
            CreatedBy = GetCurrentUserId(context) ?? Guid.Empty
        };

        db.Faqs.Add(faq);
        await db.SaveChangesAsync();

        return Results.Created($"/api/faqs/{faq.Id}", faq);
    }).RequireAuthorization("Editor").WithName("CreateFaq");
}

void MapChatbotEndpoints()
{
    var group = app.MapGroup("/api/chat").WithTags("Chatbot");

    group.MapPost("/message", async (ChatMessageRequest request, RagChatbotService chatbot, BharatDbContext db, HttpContext context) =>
    {
        var tenantId = GetCurrentTenantId(context);

        var response = await chatbot.ProcessMessageAsync(
            request.Message,
            tenantId.Value,
            request.Language,
            request.SessionId);

        // Store in history
        var sessionId = Guid.TryParse(request.SessionId, out var sid) ? sid : Guid.NewGuid();
        db.ChatMessages.Add(new BharatCMS.Domain.Entities.ChatMessage
        {
            SessionId = sessionId,
            Role = "user",
            Content = request.Message,
            Language = request.Language,
            TenantId = tenantId
        });
        db.ChatMessages.Add(new BharatCMS.Domain.Entities.ChatMessage
        {
            SessionId = sessionId,
            Role = "assistant",
            Content = response.Answer,
            Intent = response.Intent,
            Sources = response.Sources != null ? System.Text.Json.JsonSerializer.Serialize(response.Sources) : null,
            Confidence = response.Confidence,
            TenantId = tenantId
        });
        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            answer = response.Answer,
            intent = response.Intent,
            sources = response.Sources,
            confidence = response.Confidence,
            sessionId
        });
    }).WithName("ChatMessage");

    group.MapGet("/history/{sessionId}", async (Guid sessionId, BharatDbContext db) =>
    {
        var messages = await db.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.Timestamp)
            .Select(m => new { m.Role, m.Content, m.Timestamp })
            .ToListAsync();

        return Results.Ok(messages);
    }).WithName("GetChatHistory");
}

void MapFileEndpoints()
{
    var group = app.MapGroup("/api/files").WithTags("Files");

    group.MapPost("/upload", async (IFormFile file, FileValidationService validator, BharatDbContext db, HttpContext context) =>
    {
        var tenantId = GetCurrentTenantId(context);

        // Validate file
        var validation = await validator.ValidateAsync(file);
        if (!validation.IsValid)
            return Results.BadRequest(new { error = validation.Error });

        // Generate secure filename
        var secureName = validator.GenerateSecureFilename(file.FileName);
        var storagePath = $"/data/{tenantId}/files/{DateTime.UtcNow:yyyy/MM}/{secureName}";

        // Save file to isolated storage
        var uploadsDir = Path.Combine(app.Environment.ContentRootPath, "storage", tenantId.ToString());
        Directory.CreateDirectory(uploadsDir);
        var fullPath = Path.Combine(uploadsDir, secureName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        var document = new BharatCMS.Domain.Entities.Document
        {
            TenantId = tenantId.Value,
            Title = file.FileName,
            FileName = secureName,
            StoragePath = storagePath,
            ContentType = validation.DetectedContentType ?? file.ContentType,
            FileSize = file.Length,
            MagicBytes = validation.MagicBytes,
            Checksum = validation.Checksum,
            CreatedBy = GetCurrentUserId(context) ?? Guid.Empty
        };

        db.Documents.Add(document);
        await db.SaveChangesAsync();

        return Results.Created($"/api/files/{document.Id}", new
        {
            id = document.Id,
            filename = secureName,
            size = document.FileSize,
            contentType = document.ContentType
        });
    }).RequireAuthorization("Editor").WithName("UploadFile")
    .DisableAntiforgery(); // Multipart form handling

    group.MapGet("/{id}", async (long id, BharatDbContext db) =>
    {
        var doc = await db.Documents.FindAsync(id);
        if (doc == null) return Results.NotFound();

        var filePath = Path.Combine(app.Environment.ContentRootPath, "storage",
            doc.TenantId.ToString(), doc.FileName);

        if (!File.Exists(filePath)) return Results.NotFound();

        return Results.File(filePath, doc.ContentType, doc.FileName);
    }).WithName("GetFile");
}

void MapSyncEndpoints()
{
    var group = app.MapGroup("/api/sync").WithTags("OfflineSync");

    group.MapPost("/push", async (SyncPushRequest request, BharatDbContext db, HttpContext context) =>
    {
        var tenantId = GetCurrentTenantId(context);

        foreach (var item in request.Items)
        {
            var entity = new BharatCMS.Domain.Entities.SyncQueue
            {
                TenantId = tenantId.Value,
                EntityType = item.EntityType,
                LocalId = item.LocalId,
                Operation = item.Operation,
                Payload = System.Text.Json.JsonSerializer.Serialize(item.Data),
                Status = BharatCMS.Domain.Entities.SyncStatus.Completed, // Mark for immediate processing
                SyncedAt = DateTime.UtcNow
            };
            db.SyncQueues.Add(entity);
        }

        await db.SaveChangesAsync();

        return Results.Ok(new { processed = request.Items.Count });
    }).WithName("SyncPush");

    group.MapGet("/pull", async (DateTime since, BharatDbContext db, HttpContext context) =>
    {
        var tenantId = GetCurrentTenantId(context);

        var pending = await db.SyncQueues
            .Where(s => s.TenantId == tenantId.Value && s.Status == BharatCMS.Domain.Entities.SyncStatus.Pending)
            .OrderBy(s => s.CreatedAt)
            .Take(100)
            .ToListAsync();

        return Results.Ok(pending);
    }).WithName("SyncPull");
}

// === HELPERS ===
Guid? GetCurrentTenantId(HttpContext context)
{
    if (context.Items.TryGetValue("Tenant", out var tenant) && tenant is BharatCMS.Domain.Entities.Tenant t)
        return t.Id;
    return context.User.FindFirst("tenant_id")?.Value is string tid ? Guid.Parse(tid) : null;
}

Guid? GetCurrentUserId(HttpContext context)
{
    return context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value is string uid
        ? Guid.Parse(uid) : null;
}

// === REQUEST/RESPONSE DTOs ===
public record LoginRequest(string Email, string Password, bool RememberMe = false);
public record CreateTenantRequest(string Slug, string Name, BharatCMS.Domain.Entities.TenantType Type, string? Metadata = null, string? Languages = null);
public record CreateNoticeRequest(string Title, string Content, string? TitleHi = null, string? ContentHi = null, BharatCMS.Domain.Entities.NoticePriority Priority = 0, bool IsPinned = false, DateTime? PublishFrom = null);
public record CreateFaqRequest(string Question, string Answer, string? QuestionHi = null, string? AnswerHi = null, string? Category = null, string? Keywords = null);
public record ChatMessageRequest(string Message, string? Language = null, string? SessionId = null);
public record SyncPushRequest(IEnumerable<SyncItem> Items);
public record SyncItem(string EntityType, string? LocalId, string Operation, object Data);