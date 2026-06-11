namespace BharatCMS.Domain.Entities;

/// <summary>
/// Immutable audit log for all CRUD operations.
/// CERT-In compliance: All actions must be traceable.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    /// <summary>
    /// Tenant scope of the action.
    /// </summary>
    public required Guid TenantId { get; set; }

    /// <summary>
    /// Who performed the action.
    /// </summary>
    public required Guid UserId { get; set; }
    public string? UserEmail { get; set; }

    /// <summary>
    /// What entity was affected.
    /// </summary>
    public required string EntityName { get; set; }
    public long? EntityId { get; set; }

    /// <summary>
    /// Create, Read, Update, Delete, Login, Logout, Export, etc.
    /// </summary>
    public required string Action { get; set; }

    /// <summary>
    /// Full request path.
    /// </summary>
    public required string RequestPath { get; set; }
    public string? RequestMethod { get; set; }

    /// <summary>
    /// Sanitized request body (no secrets).
    /// </summary>
    public string? RequestBody { get; set; }

    /// <summary>
    /// Response status code.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Client IP address.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Session or correlation ID for tracing.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// JSON snapshot of entity state before change.
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// JSON snapshot of entity state after change.
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// API key identifier for programmatic access.
    /// </summary>
    public string? ApiKeyHash { get; set; }

    /// <summary>
    /// Timestamp of the action (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Error message if action failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public long? DurationMs { get; set; }
}

/// <summary>
/// Document storage with metadata for multi-tenant search.
/// </summary>
public class Document : BaseEntity, ITenantEntity
{
    public required Guid TenantId { get; set; }

    public required string Title { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// Original filename (sanitized on upload).
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// Secure storage path (not directly accessible).
    /// </summary>
    public required string StoragePath { get; set; }

    /// <summary>
    /// MIME type validated via magic bytes.
    /// </summary>
    public required string ContentType { get; set; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Magic byte signature for validation.
    /// </summary>
    public string? MagicBytes { get; set; }

    /// <summary>
    /// SHA-256 hash for integrity verification.
    /// </summary>
    public string? Checksum { get; set; }

    /// <summary>
    /// Category for organization (circulars, orders, notices).
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// OCR-extracted text for search.
    /// </summary>
    public string? ExtractedText { get; set; }

    /// <summary>
    /// AI-generated summary.
    /// </summary>
    public string? AiSummary { get; set; }

    /// <summary>
    /// Vector embedding for semantic search.
    /// </summary>
    public string? VectorEmbedding { get; set; }

    /// <summary>
    /// Tags for additional searchability.
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// Is this a public document?
    /// </summary>
    public bool IsPublic { get; set; } = false;

    /// <summary>
    /// Expiry date for time-limited documents.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// Notice/announcement for citizens.
/// </summary>
public class Notice : BaseEntity, ITenantEntity
{
    public required Guid TenantId { get; set; }

    public required string Title { get; set; }
    public required string Content { get; set; }

    /// <summary>
    /// Hindi/Marathi/etc. versions for multilingual.
    /// </summary>
    public string? TitleHi { get; set; }
    public string? ContentHi { get; set; }
    public string? TitleMr { get; set; }
    public string? ContentMr { get; set; }
    public string? TitleTe { get; set; }
    public string? ContentTe { get; set; }
    public string? TitleTa { get; set; }
    public string? ContentTa { get; set; }
    public string? TitleBn { get; set; }
    public string? ContentBn { get; set; }

    /// <summary>
    /// For prioritization.
    /// </summary>
    public NoticePriority Priority { get; set; } = NoticePriority.Normal;
    public bool IsPinned { get; set; } = false;

    public DateTime? PublishFrom { get; set; }
    public DateTime? PublishUntil { get; set; }
    public bool IsPublished { get; set; } = false;

    /// <summary>
    /// Views/read count.
    /// </summary>
    public int ViewCount { get; set; } = 0;

    /// <summary>
    /// Associated document IDs.
    /// </summary>
    public string? AttachmentIds { get; set; }
}

public enum NoticePriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

/// <summary>
/// FAQ for chatbot and citizen support.
/// </summary>
public class Faq : BaseEntity, ITenantEntity
{
    public required Guid TenantId { get; set; }

    public required string Question { get; set; }
    public required string Answer { get; set; }

    /// <summary>
    /// Multilingual versions.
    /// </summary>
    public string? QuestionHi { get; set; }
    public string? AnswerHi { get; set; }

    /// <summary>
    /// Category for grouping.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Search keywords.
    /// </summary>
    public string? Keywords { get; set; }

    /// <summary>
    /// For ranking popular FAQs.
    /// </summary>
    public int UsageCount { get; set; } = 0;

    public bool IsPublished { get; set; } = true;
}

/// <summary>
/// Chat session for RAG chatbot.
/// </summary>
public class ChatSession : BaseEntity, ITenantEntity
{
    public required Guid TenantId { get; set; }

    /// <summary>
    /// Visitor/anonymous or logged-in user.
    /// </summary>
    public Guid? UserId { get; set; }
    public string? VisitorId { get; set; }

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

    /// <summary>
    /// Session started timestamp.
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
}

/// <summary>
/// Individual chat message.
/// </summary>
public class ChatMessage : BaseEntity
{
    /// <summary>
    /// Optional tenant context (for history queries).
    /// </summary>
    public Guid? TenantId { get; set; }

    public Guid SessionId { get; set; }
    public ChatSession Session { get; set; } = null!;

    /// <summary>
    /// User or Assistant.
    /// </summary>
    public required string Role { get; set; }

    /// <summary>
    /// Plain text message content.
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Original language of user message.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Detected intent/category.
    /// </summary>
    public string? Intent { get; set; }

    /// <summary>
    /// Sources used for RAG response.
    /// </summary>
    public string? Sources { get; set; }

    /// <summary>
    /// Confidence score for AI response.
    /// </summary>
    public float? Confidence { get; set; }

    /// <summary>
    /// Token usage stats.
    /// </summary>
    public int? TokenCount { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Offline sync queue for PWA.
/// </summary>
public class SyncQueue : BaseEntity, ITenantEntity
{
    public required Guid TenantId { get; set; }

    /// <summary>
    /// Entity type being synced.
    /// </summary>
    public required string EntityType { get; set; }

    /// <summary>
    /// Entity ID (local before sync).
    /// </summary>
    public string? LocalId { get; set; }

    /// <summary>
    /// Server ID (assigned after sync).
    /// </summary>
    public long? ServerId { get; set; }

    /// <summary>
    /// Operation type.
    /// </summary>
    public required string Operation { get; set; }

    /// <summary>
    /// Serialized payload.
    /// </summary>
    public required string Payload { get; set; }

    /// <summary>
    /// Pending, Syncing, Completed, Failed, Conflict
    /// </summary>
    public SyncStatus Status { get; set; } = SyncStatus.Pending;

    /// <summary>
    /// Retry count.
    /// </summary>
    public int RetryCount { get; set; } = 0;

    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Conflict resolution data.
    /// </summary>
    public string? ConflictData { get; set; }

    public DateTime? SyncedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public enum SyncStatus
{
    Pending = 0,
    Syncing = 1,
    Completed = 2,
    Failed = 3,
    Conflict = 4
}

/// <summary>
/// External API configuration for integrations.
/// </summary>
public class ApiIntegration : BaseEntity, ITenantEntity
{
    public required Guid TenantId { get; set; }

    public required string Name { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// DigiLocker, GIS, etc.
    /// </summary>
    public required string Provider { get; set; }

    /// <summary>
    /// API endpoint URL.
    /// </summary>
    public required string Endpoint { get; set; }

    /// <summary>
    /// Encrypted API key.
    /// </summary>
    public string? EncryptedApiKey { get; set; }

    /// <summary>
    /// Cron schedule (e.g., "0 */6 * * *").
    /// </summary>
    public string? CronSchedule { get; set; }

    /// <summary>
    /// Last successful sync.
    /// </summary>
    public DateTime? LastSyncAt { get; set; }

    /// <summary>
    /// Is integration active?
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Custom headers JSON.
    /// </summary>
    public string? CustomHeaders { get; set; }
}