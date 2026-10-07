using Microsoft.EntityFrameworkCore;
using RemasterGuru.Domain.Entities;
using RemasterGuru.Infrastructure.Data;

namespace RemasterGuru.Infrastructure.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetForUserAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Order order, CancellationToken cancellationToken = default);
}

public sealed class OrderRepository(RemasterGuruDbContext db) : IOrderRepository
{
    public Task<Order?> GetForUserAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default) =>
        db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<Order>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
    }
}
