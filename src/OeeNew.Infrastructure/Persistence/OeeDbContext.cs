using Microsoft.EntityFrameworkCore;
using OeeNew.Domain.Identity;
using OeeNew.Domain.MasterData;

namespace OeeNew.Infrastructure.Persistence;

/// <summary>
/// EF Core + Postgres 18 (Architecture Spine — Stack table). Each Site/Central instance runs its
/// own local Postgres (AD-2) — this DbContext is shared, only the connection string differs.
/// </summary>
public sealed class OeeDbContext(DbContextOptions<OeeDbContext> options) : DbContext(options)
{
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Line> Lines => Set<Line>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<ShiftSchedule> ShiftSchedules => Set<ShiftSchedule>();
    public DbSet<ReasonCode> ReasonCodes => Set<ReasonCode>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Site>(site =>
        {
            site.ToTable("Site");
            site.HasKey(s => s.Id);
            // AD-6: uuidv7() is Postgres 18's native generator, not .NET's Guid.CreateVersion7()
            // (which has a documented byte-ordering bug that fragments the B-tree index).
            site.Property(s => s.Id).HasColumnType("uuid").HasDefaultValueSql("uuidv7()").ValueGeneratedOnAdd();
            site.Property(s => s.Name).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Line>(line =>
        {
            line.ToTable("Line");
            line.HasKey(l => l.Id);
            line.Property(l => l.Id).HasColumnType("uuid").HasDefaultValueSql("uuidv7()").ValueGeneratedOnAdd();
            line.Property(l => l.Name).IsRequired().HasMaxLength(200);
            line.Property(l => l.SiteId).HasColumnType("uuid");
            // ON DELETE RESTRICT: no cascade-delete in the MVP (Story 1.2 AC #5, [ASSUMPTION]) —
            // the Application layer blocks deletion of a Site with child Lines before this is hit.
            line.HasOne<Site>().WithMany().HasForeignKey(l => l.SiteId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Machine>(machine =>
        {
            machine.ToTable("Machine");
            machine.HasKey(m => m.Id);
            machine.Property(m => m.Id).HasColumnType("uuid").HasDefaultValueSql("uuidv7()").ValueGeneratedOnAdd();
            machine.Property(m => m.Name).IsRequired().HasMaxLength(200);
            machine.Property(m => m.LineId).HasColumnType("uuid");
            machine.HasOne<Line>().WithMany().HasForeignKey(m => m.LineId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ShiftSchedule>(shift =>
        {
            shift.ToTable("ShiftSchedule");
            shift.HasKey(s => s.Id);
            shift.Property(s => s.Id).HasColumnType("uuid").HasDefaultValueSql("uuidv7()").ValueGeneratedOnAdd();
            shift.Property(s => s.Name).IsRequired().HasMaxLength(200);
            shift.Property(s => s.SiteId).HasColumnType("uuid");
            shift.Property(s => s.LineId).HasColumnType("uuid");
            shift.Property(s => s.StartTime).HasColumnType("time");
            shift.Property(s => s.EndTime).HasColumnType("time");
            // No cascade (consistent with Story 1.2's Site/Line/Machine FKs) — the Application layer
            // verifies the parent Site/Line exists before insert; delete-blocking isn't needed here
            // since nothing references ShiftSchedule as a parent.
            shift.HasOne<Site>().WithMany().HasForeignKey(s => s.SiteId).OnDelete(DeleteBehavior.Restrict);
            shift.HasOne<Line>().WithMany().HasForeignKey(s => s.LineId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReasonCode>(reasonCode =>
        {
            reasonCode.ToTable("ReasonCode");
            reasonCode.HasKey(r => r.Id);
            reasonCode.Property(r => r.Id).HasColumnType("uuid").HasDefaultValueSql("uuidv7()").ValueGeneratedOnAdd();
            reasonCode.Property(r => r.SiteId).HasColumnType("uuid");
            reasonCode.Property(r => r.Name).IsRequired().HasMaxLength(200);
            // Mapped as a plain smallint NOT NULL (AD-5) — Postgres rejects any insert (including raw
            // SQL bypassing the API) that omits it, not just an application-layer check.
            reasonCode.Property(r => r.LossCategory).HasConversion<short>().IsRequired();
            reasonCode.Property(r => r.IsActive).IsRequired().HasDefaultValue(true);
            reasonCode.HasOne<Site>().WithMany().HasForeignKey(r => r.SiteId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("User");
            user.HasKey(u => u.Id);
            user.Property(u => u.Id).HasColumnType("uuid").HasDefaultValueSql("uuidv7()").ValueGeneratedOnAdd();
            user.Property(u => u.Username).IsRequired().HasMaxLength(100);
            user.HasIndex(u => u.Username).IsUnique();
            user.Property(u => u.PasswordHash).IsRequired();
            user.Property(u => u.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
            // Site/Line scope as native Postgres arrays (Npgsql maps Guid[] <-> uuid[] natively) —
            // element-level FK enforcement isn't supported by Postgres for arrays; the Application
            // layer validates each id exists before writing (UserManagementUseCase.EnsureScopeExistsAsync),
            // consistent with how Story 1.2/1.3's other existence checks are app-level, not DB-level.
            user.Property(u => u.SiteIds).HasColumnType("uuid[]");
            user.Property(u => u.LineIds).HasColumnType("uuid[]");
        });
    }
}
