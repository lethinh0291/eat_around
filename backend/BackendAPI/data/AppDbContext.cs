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
    }
}