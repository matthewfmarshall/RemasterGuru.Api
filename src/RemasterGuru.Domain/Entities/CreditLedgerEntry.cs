using RemasterGuru.Domain.Enums;

namespace RemasterGuru.Domain.Entities;

public class CreditLedgerEntry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int Amount { get; set; }
    public CreditLedgerReason Reason { get; set; }
    public Guid? ReferenceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
