namespace RemasterGuru.Domain.Enums;

public enum OrderStatus
{
    Draft,
    PendingPayment,
    Paid,
    AwaitingFulfillment,
    SubmittedToLab,
    Shipped,
    Cancelled
}
