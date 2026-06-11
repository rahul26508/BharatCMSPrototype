namespace BharatCMS.Infrastructure.Repositories;

using BharatCMS.Core.Context;
using BharatCMS.Core.Interfaces;
using BharatCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

public class UserRepository : IUserRepository
{
    private readonly BharatDbContext _context;

    public UserRepository(BharatDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(object id, CancellationToken ct = default)
    {
        return await _context.Users.FindAsync(new object[] { id }, ct);
    }

    public async Task<IEnumerable<User>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Users.ToListAsync(ct);
    }

    public async Task<IEnumerable<User>> FindAsync(Expression<Func<User, bool>> predicate, CancellationToken ct = default)
    {
        return await _context.Users.Where(predicate).ToListAsync(ct);
    }

    public async Task<User> AddAsync(User entity, CancellationToken ct = default)
    {
        await _context.Users.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public Task UpdateAsync(User entity, CancellationToken ct = default)
    {
        _context.Users.Update(entity);
        return _context.SaveChangesAsync(ct);
    }

    public Task DeleteAsync(User entity, CancellationToken ct = default)
    {
        entity.IsDeleted = true;
        return UpdateAsync(entity, ct);
    }

    public async Task<int> CountAsync(Expression<Func<User, bool>>? predicate = null, CancellationToken ct = default)
    {
        return predicate == null
            ? await _context.Users.CountAsync(ct)
            : await _context.Users.CountAsync(predicate, ct);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task<User?> GetWithRoleAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.Users
            .Include(u => u.Role)
            .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
    }

    public async Task<bool> EmailExistsAsync(string email, Guid? excludeUserId = null, CancellationToken ct = default)
    {
        var query = _context.Users.Where(u => u.Email == email);
        if (excludeUserId.HasValue)
            query = query.Where(u => u.Id != excludeUserId.Value);
        return await query.AnyAsync(ct);
    }

    public async Task UpdateLastLoginAsync(Guid userId, string ipAddress, CancellationToken ct = default)
    {
        var user = await _context.Users.FindAsync(new object[] { userId }, ct);
        if (user != null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            user.LastLoginIp = ipAddress;
            await _context.SaveChangesAsync(ct);
        }
    }
}

public class TenantRepository : ITenantRepository
{
    private readonly BharatDbContext _context;

    public TenantRepository(BharatDbContext context)
    {
        _context = context;
    }

    public async Task<Tenant?> GetByIdAsync(object id, CancellationToken ct = default)
    {
        _context.BypassTenantFilter();
        return await _context.Tenants.FindAsync(new object[] { id }, ct);
    }

    public async Task<IEnumerable<Tenant>> GetAllAsync(CancellationToken ct = default)
    {
        _context.BypassTenantFilter();
        return await _context.Tenants.ToListAsync(ct);
    }

    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        _context.BypassTenantFilter();
        return await _context.Tenants.FirstOrDefaultAsync(t => t.Slug == slug.ToLowerInvariant(), ct);
    }

    public async Task<bool> SlugExistsAsync(string slug, Guid? excludeTenantId = null, CancellationToken ct = default)
    {
        _context.BypassTenantFilter();
        var query = _context.Tenants.Where(t => t.Slug == slug.ToLowerInvariant());
        if (excludeTenantId.HasValue)
            query = query.Where(t => t.Id != excludeTenantId.Value);
        return await query.AnyAsync(ct);
    }

    public async Task<IEnumerable<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default)
    {
        _context.BypassTenantFilter();
        return await _context.Tenants.Where(t => t.IsActive).ToListAsync(ct);
    }

    public async Task<Tenant> AddAsync(Tenant entity, CancellationToken ct = default)
    {
        await _context.Tenants.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public Task UpdateAsync(Tenant entity, CancellationToken ct = default)
    {
        _context.Tenants.Update(entity);
        return _context.SaveChangesAsync(ct);
    }

    public Task DeleteAsync(Tenant entity, CancellationToken ct = default)
    {
        entity.IsDeleted = true;
        return UpdateAsync(entity, ct);
    }

    public async Task<int> CountAsync(Expression<Func<Tenant, bool>>? predicate = null, CancellationToken ct = default)
    {
        return predicate == null
            ? await _context.Tenants.CountAsync(ct)
            : await _context.Tenants.CountAsync(predicate, ct);
    }
}

public class AuditLogRepository : IAuditLogRepository
{
    private readonly BharatDbContext _context;

    public AuditLogRepository(BharatDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(AuditLog log, CancellationToken ct = default)
    {
        // Audit logs are immutable - always insert
        _context.BypassTenantFilter();
        await _context.AuditLogs.AddAsync(log, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<AuditLog>> GetByTenantAsync(Guid tenantId, DateTime from, DateTime to, int page, int pageSize, CancellationToken ct = default)
    {
        return await _context.AuditLogs
            .Where(l => l.TenantId == tenantId)
            .Where(l => l.Timestamp >= from && l.Timestamp <= to)
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<AuditLog>> GetByUserAsync(Guid userId, DateTime from, DateTime to, int page, int pageSize, CancellationToken ct = default)
    {
        return await _context.AuditLogs
            .Where(l => l.UserId == userId)
            .Where(l => l.Timestamp >= from && l.Timestamp <= to)
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityName, long entityId, int page, int pageSize, CancellationToken ct = default)
    {
        return await _context.AuditLogs
            .Where(l => l.EntityName == entityName && l.EntityId == entityId)
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
    }
}

public class DocumentRepository : IDocumentRepository
{
    private readonly BharatDbContext _context;

    public DocumentRepository(BharatDbContext context)
    {
        _context = context;
    }

    public async Task<Document?> GetByIdAsync(object id, CancellationToken ct = default)
    {
        return await _context.Documents.FindAsync(new object[] { id }, ct);
    }

    public async Task<IEnumerable<Document>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.Documents.ToListAsync(ct);
    }

    public async Task<IEnumerable<Document>> FindAsync(Expression<Func<Document, bool>> predicate, CancellationToken ct = default)
    {
        return await _context.Documents.Where(predicate).ToListAsync(ct);
    }

    public async Task<Document> AddAsync(Document entity, CancellationToken ct = default)
    {
        await _context.Documents.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public Task UpdateAsync(Document entity, CancellationToken ct = default)
    {
        _context.Documents.Update(entity);
        return _context.SaveChangesAsync(ct);
    }

    public Task DeleteAsync(Document entity, CancellationToken ct = default)
    {
        entity.IsDeleted = true;
        return UpdateAsync(entity, ct);
    }

    public async Task<int> CountAsync(Expression<Func<Document, bool>>? predicate = null, CancellationToken ct = default)
    {
        return predicate == null
            ? await _context.Documents.CountAsync(ct)
            : await _context.Documents.CountAsync(predicate, ct);
    }

    public async Task<IEnumerable<Document>> SearchAsync(string query, Guid tenantId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var lowerQuery = query.ToLower();
        return await _context.Documents
            .Where(d => d.TenantId == tenantId)
            .Where(d => !d.IsDeleted)
            .Where(d => d.Title.ToLower().Contains(lowerQuery) ||
                       (d.Description != null && d.Description.ToLower().Contains(lowerQuery)) ||
                       (d.ExtractedText != null && d.ExtractedText.ToLower().Contains(lowerQuery)) ||
                       (d.Tags != null && d.Tags.ToLower().Contains(lowerQuery)))
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Document>> GetByCategoryAsync(string category, Guid tenantId, CancellationToken ct = default)
    {
        return await _context.Documents
            .Where(d => d.TenantId == tenantId && d.Category == category && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task UpdateExtractedTextAsync(long documentId, string extractedText, CancellationToken ct = default)
    {
        var doc = await _context.Documents.FindAsync(new object[] { documentId }, ct);
        if (doc != null)
        {
            doc.ExtractedText = extractedText;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task UpdateVectorEmbeddingAsync(long documentId, string vectorEmbedding, CancellationToken ct = default)
    {
        var doc = await _context.Documents.FindAsync(new object[] { documentId }, ct);
        if (doc != null)
        {
            doc.VectorEmbedding = vectorEmbedding;
            await _context.SaveChangesAsync(ct);
        }
    }
}

public class ChatRepository : IChatRepository
{
    private readonly BharatDbContext _context;

    public ChatRepository(BharatDbContext context)
    {
        _context = context;
    }

    public async Task<ChatSession> CreateSessionAsync(Guid? userId, string? visitorId, Guid tenantId, CancellationToken ct = default)
    {
        var session = new ChatSession
        {
            TenantId = tenantId,
            UserId = userId,
            VisitorId = visitorId,
            StartedAt = DateTime.UtcNow
        };
        await _context.ChatSessions.AddAsync(session, ct);
        await _context.SaveChangesAsync(ct);
        return session;
    }

    public async Task AddMessageAsync(ChatMessage message, CancellationToken ct = default)
    {
        await _context.ChatMessages.AddAsync(message, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<ChatMessage>> GetSessionHistoryAsync(Guid sessionId, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        return await _context.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.Timestamp)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Document>> GetRelevantDocumentsAsync(Guid tenantId, string query, int topK = 10, CancellationToken ct = default)
    {
        var lowerQuery = query.ToLower();
        return await _context.Documents
            .Where(d => d.TenantId == tenantId && !d.IsDeleted)
            .Where(d => !string.IsNullOrEmpty(d.ExtractedText) || !string.IsNullOrEmpty(d.AiSummary))
            .Where(d => d.Title.ToLower().Contains(lowerQuery) ||
                       (d.ExtractedText != null && d.ExtractedText.ToLower().Contains(lowerQuery)))
            .Take(topK)
            .ToListAsync(ct);
    }
}
