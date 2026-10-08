using RemasterGuru.Api.Print;
using RemasterGuru.Infrastructure.Repositories;

namespace RemasterGuru.Api.HostedServices;

public sealed class PrintAutoSubmitHostedService(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger<PrintAutoSubmitHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = configuration.GetValue("Print:AutoSubmitIntervalSeconds", 30);
        var interval = TimeSpan.FromSeconds(Math.Clamp(intervalSeconds, 5, 600));
        logger.LogInformation(
            "Print auto-submit enabled (interval {IntervalSeconds}s).",
            interval.TotalSeconds);

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var orders = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
                var submission = scope.ServiceProvider.GetRequiredService<IPrintOrderSubmissionService>();

                var candidates = await orders.ListReadyForLabSubmissionAsync(20, stoppingToken);
                foreach (var order in candidates)
                {
                    try
                    {
                        await submission.SubmitOrderToLabAsync(order.Id, order.UserId, stoppingToken);
                        logger.LogInformation("Auto-submitted order {OrderId} to lab.", order.Id);
                    }
                    catch (InvalidOperationException ex)
                    {
                        logger.LogWarning(ex, "Auto-submit skipped order {OrderId}.", order.Id);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Print auto-submit tick failed.");
            }
        }
    }
}
