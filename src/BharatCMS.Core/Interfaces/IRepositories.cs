using BharatCMS.Domain.Entities;

namespace BharatCMS.Core.Interfaces;

/// <summary>
/// Generic repository interface for tenant-scoped entities.
/// All repositories enforce data isolation via BharatDbContext Global Query Filters.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id, CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<T>> FindAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(T entity, CancellationToken ct = default);
    Task<int> CountAsync(System.Linq.Expressions.Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);
}

/// <summary>
/// User repository with auth-specific operations.
/// </summary>
public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetWithRoleAsync(Guid userId, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, Guid? excludeUserId = null, CancellationToken ct = default);
    Task UpdateLastLoginAsync(Guid userId, string ipAddress, CancellationToken ct = default);
}

/// <summary>
/// Tenant repository for multi-tenant operations.
/// </summary>
public interface ITenantRepository : IRepository<Tenant>
{
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, Guid? excludeTenantId = null, CancellationToken ct = default);
    Task<IEnumerable<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default);
}

/// <summary>
/// Audit log repository for immutable event capture.
/// </summary>
public interface IAuditLogRepository
{
    Task LogAsync(AuditLog log, CancellationToken ct = default);
    Task<IEnumerable<AuditLog>> GetByTenantAsync(Guid tenantId, DateTime from, DateTime to, int page, int pageSize, CancellationToken ct = default);
    Task<IEnumerable<AuditLog>> GetByUserAsync(Guid userId, DateTime from, DateTime to, int page, int pageSize, CancellationToken ct = default);
    Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityName, long entityId, int page, int pageSize, CancellationToken ct = default);
}

/// <summary>
/// Document repository with AI/OCR support.
/// </summary>
public interface IDocumentRepository : IRepository<Document>
{
    Task<IEnumerable<Document>> SearchAsync(string query, Guid tenantId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<IEnumerable<Document>> GetByCategoryAsync(string category, Guid tenantId, CancellationToken ct = default);
    Task UpdateExtractedTextAsync(long documentId, string extractedText, CancellationToken ct = default);
    Task UpdateVectorEmbeddingAsync(long documentId, string vectorEmbedding, CancellationToken ct = default);
}

/// <summary>
/// Chat repository for RAG chatbot.
/// </summary>
public interface IChatRepository
{
    Task<ChatSession> CreateSessionAsync(Guid? userId, string? visitorId, Guid tenantId, CancellationToken ct = default);
    Task AddMessageAsync(ChatMessage message, CancellationToken ct = default);
    Task<IEnumerable<ChatMessage>> GetSessionHistoryAsync(Guid sessionId, int skip = 0, int take = 50, CancellationToken ct = default);
    Task<IEnumerable<Document>> GetRelevantDocumentsAsync(Guid tenantId, string query, int topK = 10, CancellationToken ct = default);
}