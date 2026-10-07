using RemasterGuru.Domain.Entities;
using RemasterGuru.Domain.Enums;
using RemasterGuru.Infrastructure.Storage;

namespace RemasterGuru.Api.Albums;

public static class PrintReadinessEvaluator
{
    public sealed record PrintWarning(
        Guid AssetId,
        string Severity,
        string Code,
        string Message);

    public sealed record PrintReadinessResult(
        int SoftPhotoCount,
        int WarningCount,
        IReadOnlyList<PrintWarning> Warnings);

    public static PrintReadinessResult Evaluate(
        IReadOnlyList<Asset> assets,
        IReadOnlyDictionary<Guid, long> uploadByteSizes,
        IBlobStorage blobs)
    {
        var warnings = new List<PrintWarning>();

        foreach (var asset in assets)
        {
            var version = ResolvePrintVersion(asset);
            if (version is null)
            {
                continue;
            }

            var longEdge = Math.Max(version.Width, version.Height);
            if (longEdge > 0 && longEdge < AlbumTemplateCatalog.FullPageMinLongEdgePx)
            {
                warnings.Add(new PrintWarning(
                    asset.Id,
                    "warning",
                    "low_resolution",
                    $"Photo may look soft at full page ({longEdge}px on the long edge; {AlbumTemplateCatalog.FullPageMinLongEdgePx}px recommended)."));
            }

            long byteSize = 0;
            if (uploadByteSizes.TryGetValue(asset.Id, out var sessionSize))
            {
                byteSize = sessionSize;
            }
            else if (blobs.Exists(version.StorageKey))
            {
                byteSize = new FileInfo(blobs.GetAbsolutePath(version.StorageKey)).Length;
            }

            if (byteSize > 0 && byteSize < AlbumTemplateCatalog.SmallFileByteThreshold
                && (longEdge == 0 || longEdge < AlbumTemplateCatalog.FullPageMinLongEdgePx))
            {
                warnings.Add(new PrintWarning(
                    asset.Id,
                    "warning",
                    "small_file",
                    "Original file is small; it may not print sharply at full page size."));
            }

            if (longEdge == 0 && byteSize == 0)
            {
                warnings.Add(new PrintWarning(
                    asset.Id,
                    "warning",
                    "unknown_dimensions",
                    "Could not verify print resolution for this photo."));
            }
        }

        var softCount = warnings
            .Where(w => w.Code is "low_resolution" or "small_file")
            .Select(w => w.AssetId)
            .Distinct()
            .Count();

        return new PrintReadinessResult(softCount, warnings.Count, warnings);
    }

    private static AssetVersion? ResolvePrintVersion(Asset asset)
    {
        if (asset.ActiveVersionId is not null)
        {
            var active = asset.Versions.FirstOrDefault(v => v.Id == asset.ActiveVersionId);
            if (active is not null)
            {
                return active;
            }
        }

        var restored = asset.Versions
            .Where(v => v.Kind == AssetVersionKind.Restored)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefault();
        if (restored is not null)
        {
            return restored;
        }

        return asset.Versions.FirstOrDefault(v => v.Kind == AssetVersionKind.Original)
            ?? asset.Versions.OrderBy(v => v.CreatedAt).FirstOrDefault();
    }
}
