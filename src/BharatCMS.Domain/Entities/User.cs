namespace BharatCMS.Domain.Entities;

/// <summary>
/// Government employee/user within a department.
/// All access is role-based per CERT-In access governance requirements.
/// </summary>
public class User : BaseEntity, ITenantEntity
{
    public required string Email { get; set; }

    /// <summary>
    /// Stored as BCrypt hash only. Never plaintext.
    /// </summary>
    public required string PasswordHash { get; set; }

    public required string FullName { get; set; }

    /// <summary>
    /// Employee ID as per government records.
    /// </summary>
    public string? EmployeeId { get; set; }

    public string? Department { get; set; }
    public string? Designation { get; set; }

    /// <summary>
    /// Profile photo or avatar URL.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Phone number stored with country code.
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Preferred language for Bhashini AI pipeline.
    /// </summary>
    public string PreferredLanguage { get; set; } = "en";

    /// <summary>
    /// OTP-based 2FA for additional security.
    /// </summary>
    public bool TwoFactorEnabled { get; set; } = false;
    public string? TwoFactorSecret { get; set; }

    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }

    /// <summary>
    /// Is account verified and active?
    /// </summary>
    public bool IsActive { get; set; } = true;
    public DateTime? VerifyExpiry { get; set; }

    /// <summary>
    /// Salt used for additional password hashing layer.
    /// </summary>
    public string? PasswordSalt { get; set; }

    // Navigation properties
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

/// <summary>
/// Role definitions for RBAC.
/// Enforces least-privilege access per CERT-In guidelines.
/// </summary>
public class Role : BaseEntity, ITenantEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// Predefined system roles vs custom department roles.
    /// </summary>
    public bool IsSystemRole { get; set; } = false;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
}

/// <summary>
/// Granular permission assignment per role.
/// Enables policy-based authorization.
/// </summary>
public class RolePermission : BaseEntity, ITenantEntity
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    /// <summary>
    /// Permission identifier (e.g., "notices.create", "users.manage").
    /// </summary>
    public required string Permission { get; set; }

    /// <summary>
    /// Whether this permission is explicitly denied.
    /// Deny takes precedence over Allow.
    /// </summary>
    public bool IsDenied { get; set; } = false;
}