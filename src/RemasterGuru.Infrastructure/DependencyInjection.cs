using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
                options.UseSqlite(connectionString);
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
        var db = scope.ServiceProvider.GetRequiredService<RemasterGuruDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
