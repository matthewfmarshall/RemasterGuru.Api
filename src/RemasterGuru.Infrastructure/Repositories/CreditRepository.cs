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
            .OrderByDescending(e => e.CreatedAt)
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
    Task<User> GetOrCreateAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class UserRepository(RemasterGuruDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

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
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
