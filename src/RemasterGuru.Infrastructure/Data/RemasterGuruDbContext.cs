using Microsoft.EntityFrameworkCore;
using RemasterGuru.Domain.Entities;
using RemasterGuru.Domain.Enums;

namespace RemasterGuru.Infrastructure.Data;

public class RemasterGuruDbContext(DbContextOptions<RemasterGuruDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetVersion> AssetVersions => Set<AssetVersion>();
    public DbSet<RemasterJob> RemasterJobs => Set<RemasterJob>();
    public DbSet<CreditLedgerEntry> CreditLedgerEntries => Set<CreditLedgerEntry>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<UploadSession> UploadSessions => Set<UploadSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Album>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId);
            e.Property(x => x.Title).HasMaxLength(500);
            e.Property(x => x.TemplateId).HasMaxLength(100);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<Asset>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AlbumId);
            e.HasIndex(x => new { x.AlbumId, x.OrderIndex });
            e.Property(x => x.DisplayVersion).HasConversion<string>().HasMaxLength(16);
            e.HasOne(x => x.Album).WithMany(a => a.Assets).HasForeignKey(x => x.AlbumId);
        });

        modelBuilder.Entity<AssetVersion>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AssetId);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.StorageKey).HasMaxLength(500);
            e.Property(x => x.ContentType).HasMaxLength(128);
            e.HasOne(x => x.Asset).WithMany(a => a.Versions).HasForeignKey(x => x.AssetId);
        });

        modelBuilder.Entity<RemasterJob>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => x.AssetId);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Preset).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.TargetResolution).HasConversion<string>().HasMaxLength(8);
            e.HasOne(x => x.Asset).WithMany(a => a.RemasterJobs).HasForeignKey(x => x.AssetId);
        });

        modelBuilder.Entity<CreditLedgerEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId);
            e.Property(x => x.Reason).HasConversion<string>().HasMaxLength(32);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Sku).HasMaxLength(64);
            e.Property(x => x.StripeCheckoutSessionId).HasMaxLength(256);
            e.Property(x => x.StripePaymentIntentId).HasMaxLength(256);
            e.Property(x => x.FulfillmentProvider).HasMaxLength(32);
            e.HasIndex(x => x.StripeCheckoutSessionId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Album).WithMany(a => a.Orders).HasForeignKey(x => x.AlbumId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UploadSession>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).HasMaxLength(500);
            e.Property(x => x.ContentType).HasMaxLength(128);
            e.Property(x => x.StorageKey).HasMaxLength(500);
            e.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId);
        });
    }
}
