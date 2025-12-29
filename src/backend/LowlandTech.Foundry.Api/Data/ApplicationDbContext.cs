using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LowlandTech.Foundry.Api.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserP2PSettings> UserP2PSettings => Set<UserP2PSettings>();

    public DbSet<PluginStateEntity> PluginStates => Set<PluginStateEntity>();

    public DbSet<FeatureStateEntity> FeatureStates => Set<FeatureStateEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.AddressLine1).HasMaxLength(200);
            entity.Property(e => e.AddressLine2).HasMaxLength(200);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.State).HasMaxLength(100);
            entity.Property(e => e.PostalCode).HasMaxLength(20);
            entity.Property(e => e.Country).HasMaxLength(100);
        });

        builder.Entity<UserP2PSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.PeerId).HasMaxLength(128);
            entity.Property(e => e.P2PDisplayName).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.StatusMessage).HasMaxLength(500);

            entity.HasOne(e => e.User)
                .WithOne()
                .HasForeignKey<UserP2PSettings>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PluginStateEntity>(entity =>
        {
            entity.HasKey(e => e.PluginId);
            entity.Property(e => e.State).IsRequired();
        });

        builder.Entity<FeatureStateEntity>(entity =>
        {
            entity.HasIndex(e => e.PluginId);
        });
    }
}
