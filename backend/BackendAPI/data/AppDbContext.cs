using Microsoft.EntityFrameworkCore;
using BackendAPI.Models;
using SharedLib.Models;

namespace BackendAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<POI> POIs { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<StoreRegistration> StoreRegistrations { get; set; }
    public DbSet<AdBanner> AdBanners { get; set; }
    public DbSet<ListenLog> ListenLogs { get; set; }
    public DbSet<SystemLogEntry> SystemLogEntries { get; set; }
    public DbSet<Tour> Tours { get; set; }
    public DbSet<TourStop> TourStops { get; set; }
    public DbSet<PoiTranslation> PoiTranslations { get; set; }
    public DbSet<QRTrigger> QRTriggers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(user => user.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(user => user.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .Property(user => user.Role)
            .HasMaxLength(20);

        modelBuilder.Entity<User>()
            .Property(user => user.AvatarUrl)
            .HasMaxLength(1000);

        modelBuilder.Entity<AdBanner>()
            .Property(banner => banner.ImageUrl)
            .HasMaxLength(1000);

        modelBuilder.Entity<AdBanner>()
            .HasIndex(banner => new { banner.IsActive, banner.SortOrder });

        modelBuilder.Entity<ListenLog>()
            .HasIndex(log => new { log.PlayedAtUtc, log.PoiId, log.LanguageCode });

        modelBuilder.Entity<SystemLogEntry>()
            .HasIndex(log => new { log.CreatedAtUtc, log.Category, log.Level });

        modelBuilder.Entity<Tour>()
            .HasMany(tour => tour.Stops)
            .WithOne(stop => stop.Tour)
            .HasForeignKey(stop => stop.TourId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TourStop>()
            .HasIndex(stop => new { stop.TourId, stop.SortOrder })
            .IsUnique();

        modelBuilder.Entity<PoiTranslation>()
            .HasIndex(translation => new { translation.PoiId, translation.LanguageCode })
            .IsUnique(false);

        modelBuilder.Entity<QRTrigger>()
            .HasIndex(qr => new { qr.PoiId, qr.LanguageCode })
            .IsUnique();

        modelBuilder.Entity<QRTrigger>()
            .HasIndex(qr => new { qr.CreatedAtUtc, qr.Status });

        modelBuilder.Entity<QRTrigger>()
            .HasOne(qr => qr.POI)
            .WithMany()
            .HasForeignKey(qr => qr.PoiId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}