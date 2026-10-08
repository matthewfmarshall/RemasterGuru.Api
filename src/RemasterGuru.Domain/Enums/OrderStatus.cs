namespace RemasterGuru.Domain.Enums;

public enum OrderStatus
{
    Draft,
    PendingPayment,
    Paid,
    AwaitingFulfillment,
    SubmittedToLab,
    InProduction,
    Shipped,
    Cancelled
}
