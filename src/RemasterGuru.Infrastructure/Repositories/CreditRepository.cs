using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RemasterGuru.Domain.Entities;
using RemasterGuru.Domain.Enums;
using RemasterGuru.Infrastructure.Data;

namespace RemasterGuru.Infrastructure.Repositories;

public interface ICreditRepository
{
    Task<int> GetBalanceAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CreditLedgerEntry>> GetLedgerAsync(Guid userId, int skip, int take, CancellationToken cancellationToken = default);
    Task AddEntryAsync(CreditLedgerEntry entry, CancellationToken cancellationToken = default);
    Task<bool> HasLedgerEntryAsync(
        Guid userId,
        Guid referenceId,
        CreditLedgerReason reason,
        CancellationToken cancellationToken = default);
}

public sealed class CreditRepository(RemasterGuruDbContext db) : ICreditRepository
{
    public async Task<int> GetBalanceAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.CreditLedgerEntries
            .Where(e => e.UserId == userId)
            .SumAsync(e => e.Amount, cancellationToken);

    public async Task<IReadOnlyList<CreditLedgerEntry>> GetLedgerAsync(
        Guid userId,
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        await db.CreditLedgerEntries
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt.UtcDateTime)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task AddEntryAsync(CreditLedgerEntry entry, CancellationToken cancellationToken = default)
    {
        db.CreditLedgerEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> HasLedgerEntryAsync(
        Guid userId,
        Guid referenceId,
        CreditLedgerReason reason,
        CancellationToken cancellationToken = default) =>
        db.CreditLedgerEntries.AnyAsync(
            e => e.UserId == userId && e.ReferenceId == referenceId && e.Reason == reason,
            cancellationToken);
}

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<User?> GetByAuth0SubjectAsync(string auth0Subject, CancellationToken cancellationToken = default);
    Task<User> GetOrCreateAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<User> GetOrCreateByAuth0SubjectAsync(string auth0Subject, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class UserRepository(RemasterGuruDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public Task<User?> GetByAuth0SubjectAsync(string auth0Subject, CancellationToken cancellationToken = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Auth0Subject == auth0Subject, cancellationToken);

    public async Task<User> GetOrCreateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetByIdAsync(userId, cancellationToken);
        if (user is not null)
        {
            return user;
        }

        user = new User
        {
            Id = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return user;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            db.Entry(user).State = EntityState.Detached;
            return await GetByIdAsync(userId, cancellationToken)
                ?? throw new InvalidOperationException("User row missing after unique constraint on create.");
        }
    }

    public async Task<User> GetOrCreateByAuth0SubjectAsync(
        string auth0Subject,
        CancellationToken cancellationToken = default)
    {
        var user = await GetByAuth0SubjectAsync(auth0Subject, cancellationToken);
        if (user is not null)
        {
            return user;
        }

        user = new User
        {
            Id = Guid.NewGuid(),
            Auth0Subject = auth0Subject,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return user;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            db.Entry(user).State = EntityState.Detached;
            return await GetByAuth0SubjectAsync(auth0Subject, cancellationToken)
                ?? throw new InvalidOperationException("User row missing after unique constraint on Auth0Subject.");
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is SqliteException { SqliteErrorCode: 19 }
        || ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true;

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
