using RemasterGuru.Domain.Enums;

namespace RemasterGuru.Worker.Services;

public static class RemasterPrompts
{
    public static string Build(RemasterPreset preset, TargetResolution resolution, string? promptOverride)
    {
        if (!string.IsNullOrWhiteSpace(promptOverride))
        {
            return promptOverride.Trim();
        }

        var resolutionHint = resolution == TargetResolution.TwoK
            ? "Output at high detail suitable for a 2K print master."
            : "Output at web-friendly 1K detail while preserving fine structure.";

        var presetPrompt = preset switch
        {
            RemasterPreset.Fade =>
                "Faithfully restore this faded vintage photograph: recover natural contrast and color without oversaturation. " +
                "Remove yellowing and dust. Do not change faces, expressions, or identity. Keep composition identical.",
            RemasterPreset.Conservative =>
                "Gently restore this photograph with minimal intervention: reduce noise and minor scratches only. " +
                "Preserve original grain, color character, and imperfections that look authentic. Never alter faces or identity.",
            _ =>
                "Restore this damaged photograph: repair tears, scratches, stains, and missing areas using plausible surrounding detail. " +
                "Keep faces, expressions, and identity unchanged. Maintain the original composition and era-appropriate look."
        };

        return $"{presetPrompt} {resolutionHint}";
    }
}
