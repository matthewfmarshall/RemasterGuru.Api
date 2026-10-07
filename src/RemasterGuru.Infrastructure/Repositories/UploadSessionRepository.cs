using Microsoft.EntityFrameworkCore;
using RemasterGuru.Domain.Entities;
using RemasterGuru.Infrastructure.Data;

namespace RemasterGuru.Infrastructure.Repositories;

public interface IUploadSessionRepository
{
    Task<UploadSession?> GetForUserAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default);
    Task<UploadSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, long>> GetCompletedByteSizesForAssetsAsync(
        IEnumerable<Guid> assetIds,
        CancellationToken cancellationToken = default);
    Task AddAsync(UploadSession session, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class UploadSessionRepository(RemasterGuruDbContext db) : IUploadSessionRepository
{
    public Task<UploadSession?> GetForUserAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default) =>
        db.UploadSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);

    public Task<UploadSession?> GetByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        db.UploadSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, long>> GetCompletedByteSizesForAssetsAsync(
        IEnumerable<Guid> assetIds,
        CancellationToken cancellationToken = default)
    {
        var ids = assetIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, long>();
        }

        var rows = await db.UploadSessions
            .Where(s => ids.Contains(s.AssetId) && s.IsCompleted)
            .GroupBy(s => s.AssetId)
            .Select(g => new { AssetId = g.Key, ByteSize = g.Max(s => s.ByteSize) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.AssetId, r => r.ByteSize);
    }

    public async Task AddAsync(UploadSession session, CancellationToken cancellationToken = default)
    {
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
