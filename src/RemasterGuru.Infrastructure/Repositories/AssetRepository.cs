using Microsoft.EntityFrameworkCore;
using RemasterGuru.Domain.Entities;
using RemasterGuru.Infrastructure.Data;

namespace RemasterGuru.Infrastructure.Repositories;

public interface IAssetRepository
{
    Task<int> CountForAlbumAsync(Guid albumId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Asset>> ListForAlbumAsync(Guid albumId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Asset>>> ListGroupedByAlbumForUserAsync(
        Guid userId,
        IReadOnlyList<Guid> albumIds,
        CancellationToken cancellationToken = default);
    Task<Asset?> GetForUserAsync(Guid assetId, Guid userId, CancellationToken cancellationToken = default);
    Task<Asset?> GetWithVersionsForUserAsync(Guid assetId, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Asset asset, CancellationToken cancellationToken = default);
    Task AddVersionAsync(AssetVersion version, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Asset>> ListAllForAlbumPurgeAsync(
        Guid albumId,
        Guid userId,
        CancellationToken cancellationToken = default);
    Task<int> HardDeleteAssetsAsync(IReadOnlyList<Asset> assets, CancellationToken cancellationToken = default);
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

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Asset>>> ListGroupedByAlbumForUserAsync(
        Guid userId,
        IReadOnlyList<Guid> albumIds,
        CancellationToken cancellationToken = default)
    {
        if (albumIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Asset>>();
        }

        var assets = await db.Assets
            .Include(a => a.Versions)
            .Where(a => a.UserId == userId && a.DeletedAt == null && albumIds.Contains(a.AlbumId))
            .Where(a => db.UploadSessions.Any(s => s.AssetId == a.Id && s.IsCompleted))
            .OrderBy(a => a.OrderIndex)
            .ThenBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return assets
            .GroupBy(a => a.AlbumId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Asset>)g.ToList());
    }

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

    public async Task<IReadOnlyList<Asset>> ListAllForAlbumPurgeAsync(
        Guid albumId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await db.Assets
            .Include(a => a.Versions)
            .Include(a => a.RemasterJobs)
            .Where(a => a.AlbumId == albumId && a.UserId == userId && a.DeletedAt == null)
            .ToListAsync(cancellationToken);

    public async Task<int> HardDeleteAssetsAsync(
        IReadOnlyList<Asset> assets,
        CancellationToken cancellationToken = default)
    {
        if (assets.Count == 0)
        {
            return 0;
        }

        var assetIds = assets.Select(a => a.Id).ToList();
        var sessions = await db.UploadSessions
            .Where(s => assetIds.Contains(s.AssetId))
            .ToListAsync(cancellationToken);
        db.UploadSessions.RemoveRange(sessions);

        foreach (var asset in assets)
        {
            db.RemasterJobs.RemoveRange(asset.RemasterJobs);
            db.AssetVersions.RemoveRange(asset.Versions);
        }

        db.Assets.RemoveRange(assets);
        await db.SaveChangesAsync(cancellationToken);
        return assets.Count;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
