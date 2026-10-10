using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RemasterGuru.Domain.Entities;
using RemasterGuru.Domain.Enums;
using RemasterGuru.Infrastructure;
using RemasterGuru.Infrastructure.Data;
using RemasterGuru.Infrastructure.Storage;
using RemasterGuru.Worker.Services;

namespace RemasterGuru.Worker;

public sealed class RemasterJobWorker(
    IServiceProvider services,
    ILogger<RemasterJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RemasterGuru remaster worker started (poll every 5s)");

        await services.MigrateDatabaseAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueuedJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Remaster job poll failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessQueuedJobsAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RemasterGuruDbContext>();
        var blobs = scope.ServiceProvider.GetRequiredService<IBlobStorage>();
        var remaster = scope.ServiceProvider.GetRequiredService<IImageRemasterService>();

        var jobs = await db.RemasterJobs
            .Where(j => j.Status == RemasterJobStatus.Queued)
            .OrderBy(j => j.CreatedAt.UtcDateTime)
            .Take(5)
            .ToListAsync(cancellationToken);

        foreach (var job in jobs)
        {
            job.Status = RemasterJobStatus.Running;
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Processing remaster job {JobId} for asset {AssetId}", job.Id, job.AssetId);

            try
            {
                var asset = await db.Assets
                    .Include(a => a.Versions)
                    .FirstOrDefaultAsync(a => a.Id == job.AssetId, cancellationToken);

                if (asset is null)
                {
                    job.Status = RemasterJobStatus.Failed;
                    job.Error = "Asset not found.";
                    job.CompletedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(cancellationToken);
                    continue;
                }

                var original = asset.Versions.FirstOrDefault(v => v.Kind == AssetVersionKind.Original)
                    ?? asset.Versions.OrderBy(v => v.CreatedAt).FirstOrDefault();

                if (original is null || !blobs.Exists(original.StorageKey))
                {
                    job.Status = RemasterJobStatus.Failed;
                    job.Error = "No original version on asset.";
                    job.CompletedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(cancellationToken);
                    continue;
                }

                await using var originalStream = blobs.OpenRead(original.StorageKey);
                using var ms = new MemoryStream();
                await originalStream.CopyToAsync(ms, cancellationToken);
                var originalBytes = ms.ToArray();

                var result = await remaster.RemasterAsync(
                    originalBytes,
                    original.ContentType,
                    job.Preset,
                    job.TargetResolution,
                    job.PromptOverride,
                    cancellationToken);

                var restoredId = Guid.NewGuid();
                var storageKey = $"blobs/{asset.Id}/restored/{restoredId}";

                await using var outStream = new MemoryStream(result.Bytes);
                await blobs.SaveAsync(storageKey, outStream, cancellationToken);

                var restored = new AssetVersion
                {
                    Id = restoredId,
                    AssetId = asset.Id,
                    Kind = AssetVersionKind.Restored,
                    StorageKey = storageKey,
                    Width = original.Width,
                    Height = original.Height,
                    ContentType = result.ContentType,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                db.AssetVersions.Add(restored);
                asset.ActiveVersionId = restored.Id;
                job.ResultVersionId = restored.Id;
                job.Status = RemasterJobStatus.Succeeded;
                job.CompletedAt = DateTimeOffset.UtcNow;
                job.Error = null;

                await db.SaveChangesAsync(cancellationToken);

                if (remaster.IsConfigured)
                {
                    logger.LogInformation("Remaster job {JobId} succeeded via xAI", job.Id);
                }
                else
                {
                    logger.LogInformation(
                        "Remaster job {JobId} succeeded with stub output (XAI_API_KEY not set)",
                        job.Id);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Remaster job {JobId} failed", job.Id);
                job.Status = RemasterJobStatus.Failed;
                job.Error = ex.Message;
                job.CompletedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
