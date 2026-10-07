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
            ?? "Data Source=data/remasterguru.db";

        services.AddDbContext<RemasterGuruDbContext>(options =>
            options.UseSqlite(connectionString));

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

    public static async Task EnsureDatabaseCreatedAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RemasterGuruDbContext>();
        var connectionString = db.Database.GetConnectionString() ?? "Data Source=data/remasterguru.db";
        var dataSource = connectionString.Replace("Data Source=", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (!Path.IsPathRooted(dataSource))
        {
            var dir = Path.GetDirectoryName(dataSource);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        await db.Database.EnsureCreatedAsync(cancellationToken);
    }
}
