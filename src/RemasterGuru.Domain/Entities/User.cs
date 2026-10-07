namespace RemasterGuru.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public bool FreeTasteUsed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
