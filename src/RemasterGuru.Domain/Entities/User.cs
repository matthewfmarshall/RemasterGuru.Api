namespace RemasterGuru.Domain.Entities;

public class User
{
    public Guid Id { get; set; }

    /// <summary>Auth0 <c>sub</c> claim; unique when set.</summary>
    public string? Auth0Subject { get; set; }

    public bool FreeTasteUsed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
