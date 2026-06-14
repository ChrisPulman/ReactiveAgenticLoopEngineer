using Microsoft.EntityFrameworkCore;
using RALE.Server.Models;

namespace RALE.Server.Data;

public sealed class RALEContext(DbContextOptions<RALEContext> options) : DbContext(options)
{
    public DbSet<Loop> Loops => Set<Loop>();

    public DbSet<Goal> Goals => Set<Goal>();

    public DbSet<Agent> Agents => Set<Agent>();

    public DbSet<GoalResult> GoalResults => Set<GoalResult>();

    public DbSet<LoopEvent> LoopEvents => Set<LoopEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Loop>(entity =>
        {
            entity.ToTable("Loops");
            entity.HasKey(loop => loop.Id);
            entity.Property(loop => loop.PrimaryObjective).IsRequired();
            entity.Property(loop => loop.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(loop => loop.Version).IsConcurrencyToken();
            entity.HasMany(loop => loop.Goals).WithOne(goal => goal.Loop).HasForeignKey(goal => goal.LoopId);
            entity.HasMany(loop => loop.Events).WithOne(loopEvent => loopEvent.Loop).HasForeignKey(loopEvent => loopEvent.LoopId);
        });

        modelBuilder.Entity<Goal>(entity =>
        {
            entity.ToTable("Goals");
            entity.HasKey(goal => goal.Id);
            entity.HasIndex(goal => new { goal.LoopId, goal.Sequence }).IsUnique();
            entity.Property(goal => goal.Description).IsRequired().HasMaxLength(512);
            entity.Property(goal => goal.Prompt).IsRequired();
            entity.Property(goal => goal.DependsOnJson).IsRequired();
            entity.Property(goal => goal.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(goal => goal.Version).IsConcurrencyToken();
            entity.HasMany(goal => goal.Results).WithOne(result => result.Goal).HasForeignKey(result => result.GoalId);
        });

        modelBuilder.Entity<Agent>(entity =>
        {
            entity.ToTable("Agents");
            entity.HasKey(agent => agent.Id);
            entity.Property(agent => agent.Name).IsRequired().HasMaxLength(128);
            entity.Property(agent => agent.Capabilities).IsRequired();
            entity.HasOne(agent => agent.AssignedGoal)
                .WithMany()
                .HasForeignKey(agent => agent.AssignedGoalId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<GoalResult>(entity =>
        {
            entity.ToTable("GoalResults");
            entity.HasKey(result => result.Id);
            entity.Property(result => result.Output).IsRequired();
            entity.Property(result => result.Metadata).IsRequired();
        });

        modelBuilder.Entity<LoopEvent>(entity =>
        {
            entity.ToTable("LoopEvents");
            entity.HasKey(loopEvent => loopEvent.Id);
            entity.Property(loopEvent => loopEvent.Type).HasConversion<string>().HasMaxLength(64);
            entity.Property(loopEvent => loopEvent.Detail).IsRequired();
            entity.HasOne(loopEvent => loopEvent.Goal)
                .WithMany()
                .HasForeignKey(loopEvent => loopEvent.GoalId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
