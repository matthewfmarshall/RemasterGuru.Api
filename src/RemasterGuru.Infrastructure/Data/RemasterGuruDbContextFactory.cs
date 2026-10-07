using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RemasterGuru.Infrastructure.Data;

public sealed class RemasterGuruDbContextFactory : IDesignTimeDbContextFactory<RemasterGuruDbContext>
{
    public RemasterGuruDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Server=localhost,1433;Database=RemasterGuru;User Id=sa;Password=RemasterGuru_Dev1!;TrustServerCertificate=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<RemasterGuruDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new RemasterGuruDbContext(options);
    }
}
