using Microsoft.EntityFrameworkCore;
using RemasterGuru.Domain.Entities;
using RemasterGuru.Infrastructure.Data;

namespace RemasterGuru.Infrastructure.Repositories;

public interface IUploadSessionRepository
{
    Task<UploadSession?> GetForUserAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default);
    Task<UploadSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task AddAsync(UploadSession session, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class UploadSessionRepository(RemasterGuruDbContext db) : IUploadSessionRepository
{
    public Task<UploadSession?> GetForUserAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default) =>
        db.UploadSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);

    public Task<UploadSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        db.UploadSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

    public async Task AddAsync(UploadSession session, CancellationToken cancellationToken = default)
    {
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
