using Microsoft.EntityFrameworkCore;
using Website.Models;

namespace Website.Data;

public class AppDbContext : DbContext
{
    public DbSet<Ride> Rides { get; set; }
    public DbSet<TrackPoint> TrackPoints {get;set;}

    public string DbPath { get; }

    public AppDbContext()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "data");
        DbPath = Path.Join(path, "app.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");
}
