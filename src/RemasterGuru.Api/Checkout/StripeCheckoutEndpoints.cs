using System.Text;
using RemasterGuru.Api.Auth;
using RemasterGuru.Domain.Entities;
using RemasterGuru.Domain.Enums;
using RemasterGuru.Infrastructure.Repositories;
using Stripe;
using Stripe.Checkout;

namespace RemasterGuru.Api.Checkout;

public static class StripeCheckoutEndpoints
{
    public static void MapStripeCheckout(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");

        api.MapGet("/checkout/products", () =>
        {
            var products = CheckoutProductCatalog.List().Select(p => new
            {
                sku = p.Sku,
                name = p.Name,
                description = p.Description,
                amountCents = p.AmountCents,
                currency = "usd",
                includedRemasterCredits = p.IncludedRemasterCredits
            });
            return Results.Json(products);
        });

        api.MapPost("/checkout/sessions", CreateCheckoutSessionAsync);

        app.MapPost("/api/v1/webhooks/stripe", HandleStripeWebhookAsync)
            .DisableAntiforgery();
    }

    private static async Task<IResult> CreateCheckoutSessionAsync(
        CreateCheckoutSessionRequest body,
        ICurrentUser user,
        IAlbumRepository albums,
        IOrderRepository orders,
        IConfiguration config,
        CancellationToken ct)
    {
        if (body.albumId == Guid.Empty)
        {
            return Results.Problem("albumId is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var product = CheckoutProductCatalog.TryGet(body.productSku);
        if (product is null)
        {
            return Results.Problem("Unknown productSku.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!string.Equals(body.shippingCountry, "US", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem(
                "US-only shipping in v1. shippingCountry must be US.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var album = await albums.GetForUserAsync(body.albumId, user.UserId, ct);
        if (album is null)
        {
            return Results.Problem("Album not found.", statusCode: StatusCodes.Status404NotFound);
        }

        if (album.Status != AlbumStatus.ReadyForPrint)
        {
            return Results.Problem(
                "Album must be ready_for_print before checkout.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var secretKey = config["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return Results.Problem(
                "Stripe is not configured. Set Stripe:SecretKey.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        StripeConfiguration.ApiKey = secretKey;

        var webBase = (config["App:WebBaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var order = new Order
        {
            Id = orderId,
            UserId = user.UserId,
            AlbumId = body.albumId,
            Sku = product.Sku,
            Status = OrderStatus.PendingPayment,
            AmountCents = product.AmountCents,
            FulfillmentProvider = "rpi",
            ShippingAddressJson = """{"country":"US"}""",
            CreatedAt = now
        };
        await orders.AddAsync(order, ct);

        var sessionOptions = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = $"{webBase}/app/checkout/success?albumId={body.albumId}&session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{webBase}/app/checkout/cancel?albumId={body.albumId}",
            ClientReferenceId = orderId.ToString(),
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = user.UserId.ToString(),
                ["albumId"] = body.albumId.ToString(),
                ["productSku"] = product.Sku,
                ["orderId"] = orderId.ToString()
            },
            ShippingAddressCollection = new SessionShippingAddressCollectionOptions
            {
                AllowedCountries = ["US"]
            },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "usd",
                        UnitAmount = product.AmountCents,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = product.Name,
                            Description = product.Description
                        }
                    }
                }
            ]
        };

        var sessionService = new SessionService();
        Session session;
        try
        {
            session = await sessionService.CreateAsync(sessionOptions, cancellationToken: ct);
        }
        catch (StripeException ex)
        {
            return Results.Problem(
                $"Stripe error: {ex.StripeError?.Message ?? ex.Message}",
                statusCode: StatusCodes.Status502BadGateway);
        }

        order.StripeCheckoutSessionId = session.Id;
        await orders.SaveChangesAsync(ct);

        return Results.Json(new { sessionId = session.Id, url = session.Url });
    }

    private static async Task<IResult> HandleStripeWebhookAsync(
        HttpRequest request,
        IOrderRepository orders,
        IAlbumRepository albums,
        ICreditRepository credits,
        IConfiguration config,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var webhookSecret = config["Stripe:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            return Results.Problem(
                "Stripe webhook secret is not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        request.EnableBuffering();
        string json;
        using (var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
        {
            json = await reader.ReadToEndAsync(ct);
        }

        request.Body.Position = 0;

        Event stripeEvent;
        try
        {
            var signature = request.Headers["Stripe-Signature"].ToString();
            stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);
        }
        catch (StripeException ex)
        {
            var log = loggerFactory.CreateLogger("StripeWebhook");
            log.LogWarning(ex, "Stripe webhook signature verification failed.");
            return Results.Problem("Invalid Stripe signature.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
        {
            var session = stripeEvent.Data.Object as Session;
            if (session is not null)
            {
                await CompleteCheckoutSessionAsync(session, orders, albums, credits, ct);
            }
        }

        return Results.Ok();
    }

    internal static async Task CompleteCheckoutSessionAsync(
        Session session,
        IOrderRepository orders,
        IAlbumRepository albums,
        ICreditRepository credits,
        CancellationToken ct)
    {
        var order = await orders.GetByStripeCheckoutSessionIdAsync(session.Id, ct);
        if (order is null && session.Metadata.TryGetValue("orderId", out var orderIdRaw)
            && Guid.TryParse(orderIdRaw, out var orderId))
        {
            order = await orders.GetByIdAsync(orderId, ct);
        }

        if (order is null)
        {
            return;
        }

        if (order.Status is OrderStatus.Paid or OrderStatus.AwaitingFulfillment or OrderStatus.SubmittedToLab
            or OrderStatus.InProduction or OrderStatus.Shipped)
        {
            return;
        }

        order.StripeCheckoutSessionId = session.Id;
        order.StripePaymentIntentId = session.PaymentIntentId;
        order.Status = OrderStatus.AwaitingFulfillment;
        order.AmountCents = (int)(session.AmountTotal ?? order.AmountCents);

        var album = await albums.GetByIdAsync(order.AlbumId, ct);
        if (album is not null)
        {
            album.Status = AlbumStatus.Ordered;
            album.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var product = CheckoutProductCatalog.TryGet(order.Sku);
        if (product is { IncludedRemasterCredits: > 0 }
            && !await credits.HasLedgerEntryAsync(order.UserId, order.Id, CreditLedgerReason.BookBundle, ct))
        {
            await credits.AddEntryAsync(new CreditLedgerEntry
            {
                Id = Guid.NewGuid(),
                UserId = order.UserId,
                Amount = product.IncludedRemasterCredits,
                Reason = CreditLedgerReason.BookBundle,
                ReferenceId = order.Id,
                CreatedAt = DateTimeOffset.UtcNow
            }, ct);
        }

        await orders.SaveChangesAsync(ct);
        if (album is not null)
        {
            await albums.SaveChangesAsync(ct);
        }
    }

    public sealed record CreateCheckoutSessionRequest(
        Guid albumId,
        string? productSku,
        string? shippingCountry);
}
