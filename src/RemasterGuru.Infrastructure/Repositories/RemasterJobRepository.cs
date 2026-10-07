using Microsoft.EntityFrameworkCore;
using RemasterGuru.Domain.Entities;
using RemasterGuru.Domain.Enums;
using RemasterGuru.Infrastructure.Data;

namespace RemasterGuru.Infrastructure.Repositories;

public interface IRemasterJobRepository
{
    Task<RemasterJob?> GetForUserAsync(Guid jobId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RemasterJob>> ListForAssetAsync(Guid assetId, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(RemasterJob job, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RemasterJob>> GetQueuedBatchAsync(int take, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class RemasterJobRepository(RemasterGuruDbContext db) : IRemasterJobRepository
{
    public Task<RemasterJob?> GetForUserAsync(Guid jobId, Guid userId, CancellationToken cancellationToken = default) =>
        db.RemasterJobs.FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<RemasterJob>> ListForAssetAsync(Guid assetId, Guid userId, CancellationToken cancellationToken = default) =>
        await db.RemasterJobs
            .Where(j => j.AssetId == assetId && j.UserId == userId)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(RemasterJob job, CancellationToken cancellationToken = default)
    {
        db.RemasterJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RemasterJob>> GetQueuedBatchAsync(int take, CancellationToken cancellationToken = default) =>
        await db.RemasterJobs
            .Where(j => j.Status == RemasterJobStatus.Queued)
            .OrderBy(j => j.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
