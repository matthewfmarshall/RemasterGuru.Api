using RemasterGuru.Domain.Enums;

namespace RemasterGuru.Domain.Entities;

public class Album
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public AlbumStatus Status { get; set; } = AlbumStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<Asset> Assets { get; set; } = [];
    public ICollection<Order> Orders { get; set; } = [];
}
