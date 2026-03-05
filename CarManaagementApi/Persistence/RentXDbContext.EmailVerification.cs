using CarManaagementApi.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarManaagementApi.Persistence;

public partial class RentXDbContext
{
    public virtual DbSet<UserAuthLog> UserAuthLogs { get; set; }

    public virtual DbSet<UserEmailVerification> UserEmailVerifications { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.IsEmailVerified)
                .HasColumnName("IsEmailVerified")
                .HasDefaultValue(false);
        });

        modelBuilder.Entity<UserAuthLog>(entity =>
        {
            entity.HasKey(e => e.AuthLogId);

            entity.ToTable("UserAuthLogs", "rentx");

            entity.Property(e => e.AuthLogId)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.UserId)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.Property(e => e.RoleCode)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.Property(e => e.LoginAt)
                .HasPrecision(0);

            entity.Property(e => e.LogoutAt)
                .HasPrecision(0);

            entity.Property(e => e.LoginIp)
                .HasMaxLength(64)
                .IsUnicode(false);

            entity.Property(e => e.LogoutIp)
                .HasMaxLength(64)
                .IsUnicode(false);

            entity.Property(e => e.UserAgent)
                .HasMaxLength(512);

            entity.Property(e => e.Source)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasDefaultValue("web");

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasIndex(e => new { e.UserId, e.LoginAt })
                .HasDatabaseName("IX_UserAuthLogs_User_LoginAt");

            entity.HasIndex(e => e.RoleCode)
                .HasDatabaseName("IX_UserAuthLogs_Role");

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserAuthLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserAuthLogs_Users");
        });

        modelBuilder.Entity<UserEmailVerification>(entity =>
        {
            entity.HasKey(e => e.VerificationId);

            entity.ToTable("UserEmailVerifications", "rentx");

            entity.Property(e => e.VerificationId)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.UserId)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.Property(e => e.VerificationCode)
                .HasMaxLength(10)
                .IsUnicode(false);

            entity.Property(e => e.ExpiresAt)
                .HasPrecision(0);

            entity.Property(e => e.FailedAttempts)
                .HasDefaultValue((byte)0);

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.Property(e => e.VerifiedAt)
                .HasPrecision(0);

            entity.HasIndex(e => new { e.UserId, e.VerificationCode })
                .HasDatabaseName("IX_UserEmailVerifications_User_Code");

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserEmailVerifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserEmailVerifications_Users");
        });
    }
}
