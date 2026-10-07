namespace RemasterGuru.Domain.Entities;

public class Asset
{
    public Guid Id { get; set; }
    public Guid AlbumId { get; set; }
    public Guid UserId { get; set; }
    public string? Caption { get; set; }
    public Guid? ActiveVersionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Album Album { get; set; } = null!;
    public ICollection<AssetVersion> Versions { get; set; } = [];
    public ICollection<RemasterJob> RemasterJobs { get; set; } = [];
}
