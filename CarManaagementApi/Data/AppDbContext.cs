using CarManaagementApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CarManaagementApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Car> Cars => Set<Car>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Car>(entity =>
        {
            entity.HasKey(x => x.CarId);
            entity.HasIndex(x => x.RegistrationNumber).IsUnique();

            entity.Property(x => x.Brand).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Model).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Variant).HasMaxLength(100);
            entity.Property(x => x.RegistrationNumber).HasMaxLength(20).IsRequired();
            entity.Property(x => x.FuelType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Transmission).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Price).HasColumnType("decimal(18,2)");
            entity.Property(x => x.IsAvailable).HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        base.OnModelCreating(modelBuilder);
    }
}
