using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TrafficFine.Web.Domain;

namespace TrafficFine.Web.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<FineRecord> Fines => Set<FineRecord>();
    public DbSet<ApprovalHistory> ApprovalHistory => Set<ApprovalHistory>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Vehicle>().HasIndex(x => x.Plate).IsUnique();
        builder.Entity<FineRecord>().HasIndex(x => x.Reference).IsUnique();
        builder.Entity<FineRecord>().Property(x => x.Amount).HasPrecision(18, 2);
        builder.Entity<FineRecord>().Property(x => x.Version).IsConcurrencyToken();
        builder.Entity<FineRecord>().HasIndex(x => new { x.Status, x.CreatedAt });
        builder.Entity<FineRecord>().HasOne(x => x.Vehicle).WithMany(x => x.Fines)
            .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<FineRecord>().HasOne(x => x.CreatedBy).WithMany()
            .HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ApprovalHistory>().HasOne(x => x.Actor).WithMany()
            .HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ApprovalHistory>().HasOne(x => x.FineRecord).WithMany(x => x.History)
            .HasForeignKey(x => x.TrafficFineId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ApprovalHistory>().HasIndex(x => new { x.TrafficFineId, x.OccurredAt });
        builder.Entity<FineRecord>().ToTable(table =>
            table.HasCheckConstraint("CK_Fine_Amount", "[Amount] > 0"));
    }
}
