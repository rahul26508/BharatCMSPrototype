namespace BharatCMS.Domain.Entities;

/// <summary>
/// Base entity with multi-tenant support and soft-delete capability.
/// All tenant-scoped entities must inherit from this class.
/// CERT-In: TenantId column ensures data isolation per government department.
/// </summary>
public abstract class BaseEntity
{
    public long Id { get; set; }

    /// <summary>
    /// Maps to government department/organization. Enforces data isolation.
    /// </summary>
    public required Guid TenantId { get; set; }

    /// <summary>
    /// Soft-delete flag - records are never physically deleted for audit compliance.
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// Row version for optimistic concurrency control.
    /// Prevents race conditions in multi-user government environments.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>
/// Interface to mark entities requiring tenant isolation.
/// Enables compile-time verification of multi-tenancy requirements.
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}

/// <summary>
/// Interface for entities that can be audited.
/// All sensitive records implement this for CERT-In compliance.
/// </summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    Guid CreatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
}