using System;
using System.Collections.Generic;
using CarManaagementApi.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarManaagementApi.Persistence;

public partial class RentXDbContext : DbContext
{
    public RentXDbContext(DbContextOptions<RentXDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Booking> Bookings { get; set; }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<Car> Cars { get; set; }

    public virtual DbSet<CarImage> CarImages { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<MaintenanceBlock> MaintenanceBlocks { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<ReturnInspection> ReturnInspections { get; set; }

    public virtual DbSet<ReturnInspectionDamage> ReturnInspectionDamages { get; set; }

    public virtual DbSet<ReturnInspectionDamagePhoto> ReturnInspectionDamagePhotos { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<RolePermission> RolePermissions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRefreshToken> UserRefreshTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.BookingId).HasName("PK__Bookings__73951AED097858AE");

            entity.ToTable("Bookings", "rentx");

            entity.HasIndex(e => new { e.CarId, e.PickAt, e.DropAt }, "IX_Bookings_Car_Time");

            entity.HasIndex(e => e.PickAt, "IX_Bookings_PickAt").IsDescending();

            entity.HasIndex(e => new { e.Status, e.LocationCode }, "IX_Bookings_Status");

            entity.Property(e => e.BookingId)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.CarId)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CustomerId)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.DailyPrice).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.DropAt).HasPrecision(0);
            entity.Property(e => e.LocationCode)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.PickAt).HasPrecision(0);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);

            entity.HasOne(d => d.Car).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.CarId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Bookings_Cars");

            entity.HasOne(d => d.Customer).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Bookings_Customers");

            entity.HasOne(d => d.LocationCodeNavigation).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.LocationCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Bookings_Branches");
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(e => e.BranchId).HasName("PK__Branches__A1682FC55422CA83");

            entity.ToTable("Branches", "rentx");

            entity.Property(e => e.BranchId)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Address).HasMaxLength(300);
            entity.Property(e => e.City).HasMaxLength(120);
            entity.Property(e => e.CloseAt).HasPrecision(0);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(120);
            entity.Property(e => e.OpenAt).HasPrecision(0);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Pincode)
                .HasMaxLength(6)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.State).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
        });

        modelBuilder.Entity<Car>(entity =>
        {
            entity.HasKey(e => e.CarId).HasName("PK__Cars__68A0342E63766721");

            entity.ToTable("Cars", "rentx");

            entity.HasIndex(e => new { e.BranchId, e.IsActive }, "IX_Cars_Branch_Active");

            entity.HasIndex(e => e.RegNo, "UX_Cars_RegNo").IsUnique();

            entity.Property(e => e.CarId)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.BranchId)
                .HasMaxLength(6)
                .IsUnicode(false);
            entity.Property(e => e.Brand).HasMaxLength(80);
            entity.Property(e => e.CarType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DailyPrice).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Fuel)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Model).HasMaxLength(80);
            entity.Property(e => e.PrimaryImageUrl).HasMaxLength(500);
            entity.Property(e => e.Rating).HasColumnType("decimal(3, 2)");
            entity.Property(e => e.RegNo)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Transmission)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);

            entity.HasOne(d => d.Branch).WithMany(p => p.Cars)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Cars_Branches");
        });

        modelBuilder.Entity<CarImage>(entity =>
        {
            entity.HasKey(e => e.CarImageId).HasName("PK__CarImage__614BE6AFBBEFE911");

            entity.ToTable("CarImages", "rentx");

            entity.HasIndex(e => new { e.CarId, e.SortOrder }, "IX_CarImages_CarId");

            entity.Property(e => e.CarId)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ImageUrl).HasMaxLength(500);

            entity.HasOne(d => d.Car).WithMany(p => p.CarImages)
                .HasForeignKey(d => d.CarId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CarImages_Cars");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId).HasName("PK__Customer__A4AE64D87578CBD5");

            entity.ToTable("Customers", "rentx");

            entity.HasIndex(e => e.Name, "IX_Customers_Name");

            entity.Property(e => e.CustomerId)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Address).HasMaxLength(300);
            entity.Property(e => e.City).HasMaxLength(120);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CustomerType)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("individual");
            entity.Property(e => e.DlNumber).HasMaxLength(40);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.KycNumber).HasMaxLength(60);
            entity.Property(e => e.KycType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(120);
            entity.Property(e => e.Phone)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Pincode)
                .HasMaxLength(6)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.State).HasMaxLength(50);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
        });

        modelBuilder.Entity<MaintenanceBlock>(entity =>
        {
            entity.HasKey(e => e.MaintenanceId).HasName("PK__Maintena__E60542D5388A8CAB");

            entity.ToTable("MaintenanceBlocks", "rentx");

            entity.HasIndex(e => new { e.CarId, e.BlockFrom, e.BlockTo }, "IX_MaintenanceBlocks_Car_Date");

            entity.Property(e => e.MaintenanceId)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CarId)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.MaintenanceType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(d => d.Car).WithMany(p => p.MaintenanceBlocks)
                .HasForeignKey(d => d.CarId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MaintenanceBlocks_Cars");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E1260486144");

            entity.ToTable("Notifications", "rentx");

            entity.HasIndex(e => new { e.UserId, e.IsRead, e.CreatedAt }, "IX_Notifications_User_Read").IsDescending(false, false, true);

            entity.Property(e => e.NotificationId)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Message).HasMaxLength(500);
            entity.Property(e => e.ReadAt).HasPrecision(0);
            entity.Property(e => e.Title).HasMaxLength(160);
            entity.Property(e => e.UserId)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_Notifications_Users");
        });

        modelBuilder.Entity<ReturnInspection>(entity =>
        {
            entity.HasKey(e => e.InspectionId).HasName("PK__ReturnIn__30B2DC085814FBFC");

            entity.ToTable("ReturnInspections", "rentx");

            entity.Property(e => e.InspectionId)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.BookingId)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CarId)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CleaningCharge).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Deposit).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.FuelCharge).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.LateFee).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.LateFeePerHour).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.NetPayable).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.Refund).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.SubTotal).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TotalDamage).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.UpdatedAt).HasPrecision(0);

            entity.HasOne(d => d.Booking).WithMany(p => p.ReturnInspections)
                .HasForeignKey(d => d.BookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnInspections_Bookings");

            entity.HasOne(d => d.Car).WithMany(p => p.ReturnInspections)
                .HasForeignKey(d => d.CarId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnInspections_Cars");
        });

        modelBuilder.Entity<ReturnInspectionDamage>(entity =>
        {
            entity.HasKey(e => e.DamageId).HasName("PK__ReturnIn__8A0F21625ABB127C");

            entity.ToTable("ReturnInspectionDamages", "rentx");

            entity.Property(e => e.EstCost).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.InspectionId)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.Part).HasMaxLength(120);
            entity.Property(e => e.Severity)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Inspection).WithMany(p => p.ReturnInspectionDamages)
                .HasForeignKey(d => d.InspectionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnInspectionDamages_Inspections");
        });

        modelBuilder.Entity<ReturnInspectionDamagePhoto>(entity =>
        {
            entity.HasKey(e => e.DamagePhotoId).HasName("PK__ReturnIn__E711BCBBF88BE136");

            entity.ToTable("ReturnInspectionDamagePhotos", "rentx");

            entity.Property(e => e.PhotoUrl).HasMaxLength(500);

            entity.HasOne(d => d.Damage).WithMany(p => p.ReturnInspectionDamagePhotos)
                .HasForeignKey(d => d.DamageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnInspectionDamagePhotos_Damages");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleCode).HasName("PK__Roles__D62CB59D0A5C66A8");

            entity.ToTable("Roles", "rentx");

            entity.Property(e => e.RoleCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsSystem).HasDefaultValue(true);
            entity.Property(e => e.RoleName).HasMaxLength(50);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => new { e.RoleCode, e.ModuleName });

            entity.ToTable("RolePermissions", "rentx");

            entity.Property(e => e.RoleCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ModuleName)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.RoleCodeNavigation).WithMany(p => p.RolePermissions)
                .HasForeignKey(d => d.RoleCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RolePermissions_Roles");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4C654070D8");

            entity.ToTable("Users", "rentx");

            entity.HasIndex(e => e.Email, "UX_Users_Email").IsUnique();

            entity.HasIndex(e => e.Username, "UX_Users_Username").IsUnique();

            entity.Property(e => e.UserId)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.FullName).HasMaxLength(120);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LastLoginAt).HasPrecision(0);
            entity.Property(e => e.PasswordHash).HasMaxLength(512);
            entity.Property(e => e.Phone)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.RoleCode)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
            entity.Property(e => e.Username).HasMaxLength(100);

            entity.HasOne(d => d.RoleCodeNavigation).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleCode)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Users_Roles");
        });

        modelBuilder.Entity<UserRefreshToken>(entity =>
        {
            entity.HasKey(e => e.RefreshTokenId).HasName("PK__UserRefr__F5845E39CA91DC41");

            entity.ToTable("UserRefreshTokens", "rentx");

            entity.HasIndex(e => e.TokenHash, "UX_UserRefreshTokens_TokenHash").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ExpiresAt).HasPrecision(0);
            entity.Property(e => e.RevokedAt).HasPrecision(0);
            entity.Property(e => e.TokenHash).HasMaxLength(512);
            entity.Property(e => e.UserId)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.User).WithMany(p => p.UserRefreshTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserRefreshTokens_Users");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
