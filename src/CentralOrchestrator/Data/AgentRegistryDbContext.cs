using CentralOrchestrator.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CentralOrchestrator.Data;

public class AgentRegistryDbContext : DbContext
{
    public AgentRegistryDbContext(DbContextOptions<AgentRegistryDbContext> options)
        : base(options)
    {
    }

    public DbSet<AgentEntity> Agents => Set<AgentEntity>();
    public DbSet<AgentCapabilityEntity> Capabilities => Set<AgentCapabilityEntity>();
    public DbSet<HumanTaskEntity> HumanTasks => Set<HumanTaskEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AgentEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.EndpointUrl).IsRequired();
            entity.HasMany(e => e.Capabilities)
                  .WithOne(c => c.Agent)
                  .HasForeignKey(c => c.AgentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentCapabilityEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CapabilityName).IsRequired();
        });

        modelBuilder.Entity<HumanTaskEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired();
            entity.Property(e => e.Status).IsRequired();
        });
    }
}
