using Microsoft.EntityFrameworkCore;
using RemasterGuru.Domain.Entities;
using RemasterGuru.Infrastructure.Data;

namespace RemasterGuru.Infrastructure.Repositories;

public interface IAssetRepository
{
    Task<int> CountForAlbumAsync(Guid albumId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Asset>> ListForAlbumAsync(Guid albumId, Guid userId, CancellationToken cancellationToken = default);
    Task<Asset?> GetForUserAsync(Guid assetId, Guid userId, CancellationToken cancellationToken = default);
    Task<Asset?> GetWithVersionsForUserAsync(Guid assetId, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Asset asset, CancellationToken cancellationToken = default);
    Task AddVersionAsync(AssetVersion version, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class AssetRepository(RemasterGuruDbContext db) : IAssetRepository
{
    public Task<int> CountForAlbumAsync(Guid albumId, Guid userId, CancellationToken cancellationToken = default) =>
        db.Assets
            .Where(a => a.AlbumId == albumId && a.UserId == userId && a.DeletedAt == null)
            .Where(a => db.UploadSessions.Any(s => s.AssetId == a.Id && s.IsCompleted))
            .CountAsync(cancellationToken);

    public async Task<IReadOnlyList<Asset>> ListForAlbumAsync(Guid albumId, Guid userId, CancellationToken cancellationToken = default) =>
        await db.Assets
            .Include(a => a.Versions)
            .Where(a => a.AlbumId == albumId && a.UserId == userId && a.DeletedAt == null)
            .Where(a => db.UploadSessions.Any(s => s.AssetId == a.Id && s.IsCompleted))
            .OrderBy(a => a.OrderIndex)
            .ThenBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<Asset?> GetForUserAsync(Guid assetId, Guid userId, CancellationToken cancellationToken = default) =>
        db.Assets.FirstOrDefaultAsync(a => a.Id == assetId && a.UserId == userId && a.DeletedAt == null, cancellationToken);

    public Task<Asset?> GetWithVersionsForUserAsync(Guid assetId, Guid userId, CancellationToken cancellationToken = default) =>
        db.Assets
            .Include(a => a.Versions)
            .FirstOrDefaultAsync(a => a.Id == assetId && a.UserId == userId && a.DeletedAt == null, cancellationToken);

    public async Task AddAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        db.Assets.Add(asset);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddVersionAsync(AssetVersion version, CancellationToken cancellationToken = default)
    {
        db.AssetVersions.Add(version);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
