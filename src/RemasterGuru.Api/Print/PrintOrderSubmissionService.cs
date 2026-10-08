using RemasterGuru.Domain.Entities;
using RemasterGuru.Domain.Enums;
using RemasterGuru.Infrastructure.Repositories;

namespace RemasterGuru.Api.Print;

public interface IPrintOrderSubmissionService
{
    Task<Order?> SubmitOrderToLabAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);
}

public sealed class PrintOrderSubmissionService(
    IOrderRepository orders,
    IAlbumRepository albums,
    IAssetRepository assets,
    IPrintFulfillmentProvider fulfillmentProvider) : IPrintOrderSubmissionService
{
    public async Task<Order?> SubmitOrderToLabAsync(
        Guid orderId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var order = await orders.GetForUserAsync(orderId, userId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        if (order.Status is OrderStatus.SubmittedToLab or OrderStatus.InProduction or OrderStatus.Shipped)
        {
            return order;
        }

        if (order.Status is not (OrderStatus.Paid or OrderStatus.AwaitingFulfillment))
        {
            throw new InvalidOperationException(
                $"Order must be paid or awaiting_fulfillment to submit to lab (current: {order.Status}).");
        }

        var album = await albums.GetForUserAsync(order.AlbumId, userId, cancellationToken);
        if (album is null)
        {
            throw new InvalidOperationException("Album not found for order.");
        }

        var assetList = await assets.ListForAlbumAsync(order.AlbumId, userId, cancellationToken);
        var context = new PrintAlbumContext(album.Id, album.Title, album.TemplateId, assetList.Count);

        var result = await fulfillmentProvider.SubmitOrderAsync(order, context, cancellationToken);

        order.Status = OrderStatus.SubmittedToLab;
        order.FulfillmentProvider = "rpi";
        order.LabOrderId = result.LabOrderId;
        order.LabPayloadJson = result.RequestPayloadJson;
        order.SubmittedToLabAt = DateTimeOffset.UtcNow;

        await orders.SaveChangesAsync(cancellationToken);
        return order;
    }
}
