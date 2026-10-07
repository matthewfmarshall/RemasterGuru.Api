namespace RemasterGuru.Domain.Entities;

public class UploadSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AlbumId { get; set; }
    public Guid AssetId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long ByteSize { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Asset Asset { get; set; } = null!;
}
