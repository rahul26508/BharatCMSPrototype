namespace BharatCMS.Domain.Entities;

/// <summary>
/// Represents a government department/organization tenant.
/// Uses flat sub-path routing (e.g., /school1, /anganwadi12).
/// </summary>
public class Tenant : BaseEntity, ITenantEntity
{
    /// <summary>
    /// Sub-path identifier for routing (unique, URL-safe).
    /// Example: "school1", "anganwadi12", "district-mumbai"
    /// </summary>
    public required string Slug { get; set; }

    /// <summary>
    /// Full official name of the department.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Type determines template configuration and available modules.
    /// Types: School, University, Anganwadi, Mining, District
    /// </summary>
    public required TenantType Type { get; set; }

    /// <summary>
    /// JSON configuration for template engine.
    /// Defines layout, navigation, forms specific to tenant type.
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Supported languages for this tenant.
    /// Maps to Bhashini AI pipeline support.
    /// </summary>
    public string SupportedLanguages { get; set; } = "en,hi";

    /// <summary>
    /// Is this tenant actively serving citizens?
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// CDN/asset storage configuration for this tenant.
    /// </summary>
    public string? StorageConfig { get; set; }

    public DateTime? SubscriptionExpiry { get; set; }

    // Navigation properties
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Notice> Notices { get; set; } = new List<Notice>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<Faq> Faqs { get; set; } = new List<Faq>();
}

/// <summary>
/// Predefined tenant types for Indian government organizations.
/// </summary>
public enum TenantType
{
    School = 1,
    University = 2,
    Anganwadi = 3,
    Mining = 4,
    District = 5
}