using RemasterGuru.Domain.Enums;

namespace RemasterGuru.Domain.Entities;

public class RemasterJob
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public Guid UserId { get; set; }
    public RemasterJobStatus Status { get; set; }
    public RemasterPreset Preset { get; set; }
    public TargetResolution TargetResolution { get; set; }
    public string? PromptOverride { get; set; }
    public bool CreditCharged { get; set; }
    public Guid? ResultVersionId { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public Asset Asset { get; set; } = null!;
}
