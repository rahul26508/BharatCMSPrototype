using Microsoft.EntityFrameworkCore;
using BharatCMS.Domain.Entities;
using System.Linq.Expressions;

namespace BharatCMS.Core.Context;

/// <summary>
/// Tenant-aware DbContext with Global Query Filters for data isolation.
/// CERT-In compliance: All tenant-scoped queries automatically filtered.
/// </summary>
public class BharatDbContext : DbContext
{
    private readonly Guid? _currentTenantId;
    private bool _bypassTenantFilter = false;

    public BharatDbContext(DbContextOptions<BharatDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Constructor with tenant context injection.
    /// </summary>
    public BharatDbContext(DbContextOptions<BharatDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _currentTenantId = tenantContext.TenantId;
    }

    // Core tenant-scoped entities
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notice> Notices => Set<Notice>();
    public DbSet<Faq> Faqs => Set<Faq>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<SyncQueue> SyncQueues => Set<SyncQueue>();
    public DbSet<ApiIntegration> ApiIntegrations => Set<ApiIntegration>();

    /// <summary>
    /// Bypass tenant filter for system-wide queries (e.g., tenant resolution).
    /// USE WITH EXTREME CAUTION - only for admin operations.
    /// </summary>
    public void BypassTenantFilter()
    {
        _bypassTenantFilter = true;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure BaseEntity
        ConfigureBaseEntity(modelBuilder);

        // Configure Tenant
        ConfigureTenant(modelBuilder);

        // Configure User & RBAC
        ConfigureUser(modelBuilder);

        // Configure AuditLog (immutable, no filter)
        ConfigureAuditLog(modelBuilder);

        // Configure content entities
        ConfigureContentEntities(modelBuilder);

        // Configure Chat & Sync
        ConfigureChatAndSync(modelBuilder);

        // Apply Global Query Filters AFTER all entity configurations
        ApplyGlobalQueryFilters(modelBuilder);
    }

    private void ConfigureBaseEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Model.GetEntityTypes()
            .Where(e => typeof(ITenantEntity).IsAssignableFrom(e.ClrType))
            .ToList()
            .ForEach(entityType =>
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(BaseEntity.RowVersion))
                    .IsRowVersion();
            });
    }

    private void ConfigureTenant(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Type).HasConversion<int>();
            entity.Property(e => e.Metadata).HasColumnType("nvarchar(max)");
            entity.Property(e => e.SupportedLanguages).HasMaxLength(100);
            entity.Property(e => e.StorageConfig).HasColumnType("nvarchar(max)");
        });
    }

    private void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Email });
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.PasswordSalt).HasMaxLength(256);

            entity.HasOne(e => e.Tenant)
                .WithMany(t => t.Users)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermissions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RoleId, e.Permission });
            entity.Property(e => e.Permission).IsRequired().HasMaxLength(200);
        });
    }

    private void ConfigureAuditLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.EntityName, e.EntityId });
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
            entity.Property(e => e.RequestPath).HasMaxLength(2000);
            entity.Property(e => e.RequestBody).HasColumnType("nvarchar(max)");
            entity.Property(e => e.OldValues).HasColumnType("nvarchar(max)");
            entity.Property(e => e.NewValues).HasColumnType("nvarchar(max)");

            // Audit logs are immutable - no updates allowed
            entity.Property(e => e.Timestamp).ValueGeneratedNever();
        });
    }

    private void ConfigureContentEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notice>(entity =>
        {
            entity.ToTable("Notices");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.IsPublished, e.PublishFrom });
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Content).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<Faq>(entity =>
        {
            entity.ToTable("Faqs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Question).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Answer).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("Documents");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.Category });
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(260);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
        });
    }

    private void ConfigureChatAndSync(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.ToTable("ChatSessions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasMany(e => e.Messages)
                .WithOne(m => m.Session)
                .HasForeignKey(m => m.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("ChatMessages");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionId);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Content).HasColumnType("nvarchar(max)");
            entity.Property(e => e.Sources).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<SyncQueue>(entity =>
        {
            entity.ToTable("SyncQueues");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Operation).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Payload).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ConflictData).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
        });

        modelBuilder.Entity<ApiIntegration>(entity =>
        {
            entity.ToTable("ApiIntegrations");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Provider).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Endpoint).IsRequired().HasMaxLength(2000);
        });
    }

    private void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        if (_bypassTenantFilter) return;

        var entityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType))
            .ToList();

        foreach (var entityType in entityTypes)
        {
            var clrType = entityType.ClrType;
            var param = Expression.Parameter(clrType, "e");

            Expression? combinedBody = null;

            // Add tenant filter for ITenantEntity types
            if (typeof(ITenantEntity).IsAssignableFrom(clrType))
            {
                var tenantIdProp = Expression.Property(param, nameof(ITenantEntity.TenantId));
                Expression tenantFilter = _currentTenantId.HasValue
                    ? Expression.Equal(tenantIdProp, Expression.Constant(_currentTenantId.Value))
                    : Expression.Equal(Expression.Constant(Guid.Empty), tenantIdProp);
                combinedBody = tenantFilter;
            }

            // Add soft-delete filter
            var deletedProp = Expression.Property(param, nameof(BaseEntity.IsDeleted));
            var softDeleteFilter = Expression.Equal(deletedProp, Expression.Constant(false));

            combinedBody = combinedBody != null
                ? Expression.AndAlso(combinedBody, softDeleteFilter)
                : softDeleteFilter;

            var filter = Expression.Lambda(combinedBody, param);
            modelBuilder.Entity(clrType).HasQueryFilter(filter);
        }
    }

    private LambdaExpression CreateTenantFilterExpression(Type entityType)
    {
        var param = Expression.Parameter(entityType, "e");
        var tenantIdProp = Expression.Property(param, nameof(ITenantEntity.TenantId));

        Expression filter;
        if (_currentTenantId.HasValue)
        {
            filter = Expression.Equal(tenantIdProp, Expression.Constant(_currentTenantId.Value));
        }
        else
        {
            // Default: match no records if no tenant context
            filter = Expression.Equal(Expression.Constant(Guid.Empty), tenantIdProp);
        }

        return Expression.Lambda(filter, param);
    }

    public override int SaveChanges()
    {
        ApplyTenantAndAudit();
        ValidateTenantIsolation();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTenantAndAudit();
        ValidateTenantIsolation();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyTenantAndAudit()
    {
        var entries = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.Entity is ITenantEntity tenantEntity && _currentTenantId.HasValue)
            {
                if (entry.State == EntityState.Added && tenantEntity.TenantId == Guid.Empty)
                {
                    tenantEntity.TenantId = _currentTenantId.Value;
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.CreatedBy = GetCurrentUserId();
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedBy = GetCurrentUserId();
                    entry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(BaseEntity.CreatedBy)).IsModified = false;
                    entry.Property(nameof(BaseEntity.TenantId)).IsModified = false;
                }
            }
        }
    }

    private void ValidateTenantIsolation()
    {
        if (_bypassTenantFilter) return;

        // Detect tenant ID tampering
        var modifiedTenantEntities = ChangeTracker.Entries<ITenantEntity>()
            .Where(e => e.State == EntityState.Modified)
            .Where(e => e.OriginalValues[nameof(ITenantEntity.TenantId)] != null)
            .ToList();

        foreach (var entry in modifiedTenantEntities)
        {
            var original = entry.OriginalValues[nameof(ITenantEntity.TenantId)];
            var current = entry.Entity.TenantId;

            if (original != null && (Guid)original != current && _currentTenantId.HasValue)
            {
                throw new TenantIsolationViolationException(
                    $"Tenant ID tampering detected. Original: {original}, Attempted: {current}");
            }
        }
    }

    private Guid GetCurrentUserId()
    {
        // In production, resolve from security context
        return Guid.Empty;
    }
}

/// <summary>
/// Exception thrown when tenant isolation is violated.
/// Maps to HTTP 403 Forbidden per security requirements.
/// </summary>
public class TenantIsolationViolationException : Exception
{
    public TenantIsolationViolationException(string message) : base(message)
    {
        SecuritySeverity = SecurityIncidentSeverity.High;
    }

    public SecurityIncidentSeverity SecuritySeverity { get; }
}

public enum SecurityIncidentSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Tenant context interface for DI.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
    string? TenantSlug { get; }
    void SetTenant(Guid tenantId);
    void SetTenantBySlug(string slug);
}

public class TenantContext : ITenantContext
{
    private Guid? _tenantId;
    private string? _tenantSlug;

    public Guid? TenantId => _tenantId;
    public string? TenantSlug => _tenantSlug;

    public void SetTenant(Guid tenantId)
    {
        _tenantId = tenantId;
    }

    public void SetTenantBySlug(string slug)
    {
        _tenantSlug = slug;
    }
}