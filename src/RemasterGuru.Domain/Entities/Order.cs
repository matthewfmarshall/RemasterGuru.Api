using RemasterGuru.Domain.Enums;

namespace RemasterGuru.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AlbumId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public string? StripeCheckoutSessionId { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public string FulfillmentProvider { get; set; } = "rpi";
    public string? LabOrderId { get; set; }
    public string? TrackingUrl { get; set; }
    public int AmountCents { get; set; }
    public string ShippingAddressJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Album Album { get; set; } = null!;
}
