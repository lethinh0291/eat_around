using Microsoft.EntityFrameworkCore;
using BackendAPI.Models;

namespace BackendAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<POI> POIs { get; set; }
}
g