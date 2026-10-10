using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RemasterGuru.Infrastructure.Data;

namespace RemasterGuru.Infrastructure;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> honors <c>Database__Provider</c> and
/// <c>ConnectionStrings__Default</c> (e.g. SQLite for HF staging).
/// </summary>
public sealed class RemasterGuruDbContextFactory : IDesignTimeDbContextFactory<RemasterGuruDbContext>
{
    public RemasterGuruDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Data Source=remasterguru-design.db";

        var provider = Environment.GetEnvironmentVariable("Database__Provider")?.Trim() ?? "SqlServer";

        var optionsBuilder = new DbContextOptionsBuilder<RemasterGuruDbContext>();
        if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseSqlite(connectionString);
        }
        else
        {
            optionsBuilder.UseSqlServer(connectionString);
        }

        return new RemasterGuruDbContext(optionsBuilder.Options);
    }
}
