using Microsoft.EntityFrameworkCore;
using RemasterGuru.Domain.Entities;
using RemasterGuru.Domain.Enums;
using RemasterGuru.Infrastructure.Data;

namespace RemasterGuru.Infrastructure.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetForUserAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<Order?> GetByStripeCheckoutSessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<Order?> GetByLabOrderIdAsync(string labOrderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> ListReadyForLabSubmissionAsync(int take, CancellationToken cancellationToken = default);
    Task AddAsync(Order order, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class OrderRepository(RemasterGuruDbContext db) : IOrderRepository
{
    public Task<Order?> GetForUserAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default) =>
        db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, cancellationToken);

    public Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

    public Task<Order?> GetByStripeCheckoutSessionIdAsync(string sessionId, CancellationToken cancellationToken = default) =>
        db.Orders.FirstOrDefaultAsync(o => o.StripeCheckoutSessionId == sessionId, cancellationToken);

    public Task<Order?> GetByLabOrderIdAsync(string labOrderId, CancellationToken cancellationToken = default) =>
        db.Orders.FirstOrDefaultAsync(o => o.LabOrderId == labOrderId, cancellationToken);

    public async Task<IReadOnlyList<Order>> ListReadyForLabSubmissionAsync(
        int take,
        CancellationToken cancellationToken = default) =>
        await db.Orders
            .Where(o => o.LabOrderId == null
                && (o.Status == OrderStatus.Paid || o.Status == OrderStatus.AwaitingFulfillment))
            .OrderBy(o => o.CreatedAt)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken);

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

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
