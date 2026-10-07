using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RemasterGuru.Domain.Entities;
using RemasterGuru.Domain.Enums;
using RemasterGuru.Infrastructure;
using RemasterGuru.Infrastructure.Data;

namespace RemasterGuru.Worker;

public sealed class RemasterJobWorker(
    IServiceProvider services,
    ILogger<RemasterJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RemasterGuru remaster worker started (poll every 5s; xAI not integrated)");

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

        var jobs = await db.RemasterJobs
            .Where(j => j.Status == RemasterJobStatus.Queued)
            .OrderBy(j => j.CreatedAt)
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

                if (original is null)
                {
                    job.Status = RemasterJobStatus.Failed;
                    job.Error = "No original version on asset.";
                    job.CompletedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(cancellationToken);
                    continue;
                }

                var restored = new AssetVersion
                {
                    Id = Guid.NewGuid(),
                    AssetId = asset.Id,
                    Kind = AssetVersionKind.Restored,
                    StorageKey = original.StorageKey,
                    Width = original.Width,
                    Height = original.Height,
                    ContentType = original.ContentType,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                db.AssetVersions.Add(restored);
                asset.ActiveVersionId = restored.Id;
                job.ResultVersionId = restored.Id;
                job.Status = RemasterJobStatus.Succeeded;
                job.CompletedAt = DateTimeOffset.UtcNow;
                job.Error = null;

                await db.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Fake remaster complete for job {JobId} (copied original as restored; xAI not integrated)",
                    job.Id);
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
