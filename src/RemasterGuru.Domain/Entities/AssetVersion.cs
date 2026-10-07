using RemasterGuru.Domain.Enums;

namespace RemasterGuru.Domain.Entities;

public class AssetVersion
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public AssetVersionKind Kind { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public Asset Asset { get; set; } = null!;
}
