using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RemasterGuru.Infrastructure.Data;
using RemasterGuru.Infrastructure.Repositories;
using RemasterGuru.Infrastructure.Storage;

namespace RemasterGuru.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRemasterGuruInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is not configured. Set ConnectionStrings:Default.");

        var provider = configuration["Database:Provider"]?.Trim() ?? "SqlServer";
        services.AddDbContext<RemasterGuruDbContext>(options =>
        {
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                // Migrations are authored against SQL Server; at runtime SQLite maps the same
                // schema but EF compares against a SQL Server snapshot and raises a false
                // PendingModelChangesWarning. SQL Server deployments keep the strict check.
                options.UseSqlite(connectionString);
                options.ConfigureWarnings(w =>
                    w.Ignore(RelationalEventId.PendingModelChangesWarning));
            }
            else
            {
                options.UseSqlServer(connectionString);
            }
        });

        var blobRoot = configuration["Storage:BlobRoot"] ?? "data/blobs";
        services.AddSingleton<IBlobStorage>(_ => new LocalBlobStorage(blobRoot));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICreditRepository, CreditRepository>();
        services.AddScoped<IAlbumRepository, AlbumRepository>();
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<IRemasterJobRepository, RemasterJobRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IUploadSessionRepository, UploadSessionRepository>();

        return services;
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var provider = configuration["Database:Provider"]?.Trim() ?? "SqlServer";
        var db = scope.ServiceProvider.GetRequiredService<RemasterGuruDbContext>();

        if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(DependencyInjection));
            logger.LogInformation(
                "SQLite provider: ensuring database schema from current model (HF staging; SQL Server migrations are not applied).");
            await db.Database.EnsureCreatedAsync(cancellationToken);
            return;
        }

        await db.Database.MigrateAsync(cancellationToken);
    }
}
