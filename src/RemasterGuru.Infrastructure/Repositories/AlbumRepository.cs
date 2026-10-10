using Microsoft.EntityFrameworkCore;
using RemasterGuru.Domain.Entities;
using RemasterGuru.Infrastructure.Data;

namespace RemasterGuru.Infrastructure.Repositories;

public interface IAlbumRepository
{
    Task<IReadOnlyList<Album>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Album?> GetForUserAsync(Guid albumId, Guid userId, CancellationToken cancellationToken = default);
    Task<Album?> GetByIdAsync(Guid albumId, CancellationToken cancellationToken = default);
    Task AddAsync(Album album, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class AlbumRepository(RemasterGuruDbContext db) : IAlbumRepository
{
    public async Task<IReadOnlyList<Album>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.Albums
            .Where(a => a.UserId == userId && a.DeletedAt == null)
            .OrderByDescending(a => a.UpdatedAt.UtcDateTime)
            .ToListAsync(cancellationToken);

    public Task<Album?> GetForUserAsync(Guid albumId, Guid userId, CancellationToken cancellationToken = default) =>
        db.Albums.FirstOrDefaultAsync(a => a.Id == albumId && a.UserId == userId && a.DeletedAt == null, cancellationToken);

    public Task<Album?> GetByIdAsync(Guid albumId, CancellationToken cancellationToken = default) =>
        db.Albums.FirstOrDefaultAsync(a => a.Id == albumId && a.DeletedAt == null, cancellationToken);

    public async Task AddAsync(Album album, CancellationToken cancellationToken = default)
    {
        db.Albums.Add(album);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
