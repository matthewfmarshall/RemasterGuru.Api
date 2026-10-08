using System.Text.Json;
using RemasterGuru.Api.Auth;
using RemasterGuru.Api.Contracts;
using RemasterGuru.Domain.Enums;
using RemasterGuru.Infrastructure.Repositories;

namespace RemasterGuru.Api.Print;

public static class PrintFulfillmentEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapPrintFulfillment(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");

        api.MapPost("/orders/{orderId:guid}/submit-to-lab", SubmitToLabAsync);

        app.MapPost("/api/v1/webhooks/rpi", HandleRpiWebhookAsync)
            .DisableAntiforgery();
    }

    private static async Task<IResult> SubmitToLabAsync(
        Guid orderId,
        ICurrentUser user,
        IPrintOrderSubmissionService submission,
        CancellationToken ct)
    {
        try
        {
            var order = await submission.SubmitOrderToLabAsync(orderId, user.UserId, ct);
            if (order is null)
            {
                return Results.Problem("Order not found.", statusCode: StatusCodes.Status404NotFound);
            }

            return Results.Json(ContractMaps.ToOrderDto(order));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> HandleRpiWebhookAsync(
        RpiWebhookPayload body,
        HttpRequest request,
        IOrderRepository orders,
        IConfiguration config,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var log = loggerFactory.CreateLogger("RpiWebhook");
        var configuredSecret = config["Rpi:WebhookSecret"];
        if (!string.IsNullOrWhiteSpace(configuredSecret))
        {
            var headerSecret = request.Headers["X-Rpi-Webhook-Secret"].ToString();
            if (!string.Equals(headerSecret, configuredSecret, StringComparison.Ordinal))
            {
                log.LogWarning("RPI webhook rejected: invalid or missing X-Rpi-Webhook-Secret.");
                return Results.Problem("Invalid webhook secret.", statusCode: StatusCodes.Status401Unauthorized);
            }
        }

        if (string.IsNullOrWhiteSpace(body.LabOrderId))
        {
            return Results.Problem("labOrderId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(body.Status))
        {
            return Results.Problem("status is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var order = await orders.GetByLabOrderIdAsync(body.LabOrderId, ct);
        if (order is null)
        {
            log.LogWarning("RPI webhook for unknown labOrderId {LabOrderId}", body.LabOrderId);
            return Results.Problem("Order not found.", statusCode: StatusCodes.Status404NotFound);
        }

        var targetStatus = MapRpiStatus(body.Status);
        if (targetStatus is null)
        {
            return Results.Problem(
                "status must be shipped, in_production, or cancelled.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (order.Status == targetStatus.Value
            && (body.TrackingUrl is null || order.TrackingUrl == body.TrackingUrl))
        {
            return Results.Ok(new { idempotent = true, orderId = order.Id, status = ContractMaps.ToApi(order.Status) });
        }

        if (order.Status == OrderStatus.Shipped && targetStatus != OrderStatus.Shipped)
        {
            return Results.Ok(new { idempotent = true, orderId = order.Id, status = ContractMaps.ToApi(order.Status) });
        }

        order.Status = targetStatus.Value;
        if (!string.IsNullOrWhiteSpace(body.TrackingUrl))
        {
            order.TrackingUrl = body.TrackingUrl;
        }

        var webhookPayload = JsonSerializer.Serialize(body, JsonOptions);
        order.LabPayloadJson = webhookPayload;

        await orders.SaveChangesAsync(ct);
        log.LogInformation(
            "RPI webhook updated order {OrderId} labOrderId={LabOrderId} status={Status}",
            order.Id,
            body.LabOrderId,
            ContractMaps.ToApi(order.Status));

        return Results.Json(ContractMaps.ToOrderDto(order));
    }

    private static OrderStatus? MapRpiStatus(string status) => status.Trim().ToLowerInvariant() switch
    {
        "shipped" => OrderStatus.Shipped,
        "in_production" => OrderStatus.InProduction,
        "cancelled" => OrderStatus.Cancelled,
        _ => null
    };

    public sealed record RpiWebhookPayload(string? LabOrderId, string? Status, string? TrackingUrl);
}
