// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RALE.Server.Models;

namespace RALE.Server.Data;

/// <summary>Provides persisted state for reactive loops and multi-agent orchestration.</summary>
/// <param name="options">Configures the database provider and connection.</param>
/// <param name="timeProvider">Supplies timestamps for newly persisted audit entities.</param>
public sealed class RALEContext(DbContextOptions<RALEContext> options, TimeProvider timeProvider) : DbContext(options)
{
    /// <summary>Defines the maximum length of a goal description.</summary>
    private const int DescriptionMaxLength = 512;

    /// <summary>Defines the maximum length of an event type.</summary>
    private const int EventTypeMaxLength = 64;

    /// <summary>Defines the maximum length of agent metadata values.</summary>
    private const int MetadataMaxLength = 128;

    /// <summary>Defines the maximum length of persisted status values.</summary>
    private const int StatusMaxLength = 32;

    /// <summary>Gets the persisted loops.</summary>
    public DbSet<Loop> Loops => Set<Loop>();

    /// <summary>Gets the persisted goals.</summary>
    public DbSet<Goal> Goals => Set<Goal>();

    /// <summary>Gets the persisted agents.</summary>
    public DbSet<Agent> Agents => Set<Agent>();

    /// <summary>Gets the persisted agent events.</summary>
    public DbSet<AgentEvent> AgentEvents => Set<AgentEvent>();

    /// <summary>Gets the persisted goal results.</summary>
    public DbSet<GoalResult> GoalResults => Set<GoalResult>();

    /// <summary>Gets the persisted loop events.</summary>
    public DbSet<LoopEvent> LoopEvents => Set<LoopEvent>();

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampNewEntities();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampNewEntities();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        _ = modelBuilder.Entity<Loop>(ConfigureLoop);
        _ = modelBuilder.Entity<Goal>(ConfigureGoal);
        _ = modelBuilder.Entity<Agent>(ConfigureAgent);
        _ = modelBuilder.Entity<AgentEvent>(ConfigureAgentEvent);
        _ = modelBuilder.Entity<GoalResult>(ConfigureGoalResult);
        _ = modelBuilder.Entity<LoopEvent>(ConfigureLoopEvent);
    }

    /// <summary>Configures persistence for loops.</summary>
    /// <param name="entity">The loop entity configuration.</param>
    private static void ConfigureLoop(EntityTypeBuilder<Loop> entity)
    {
        _ = entity.ToTable(nameof(Loops));
        _ = entity.HasKey(static loop => loop.Id);
        _ = entity.Property(static loop => loop.PrimaryObjective).IsRequired();
        _ = entity.Property(static loop => loop.Status).HasConversion<string>().HasMaxLength(StatusMaxLength);
        _ = entity.Property(static loop => loop.ConstraintsJson).IsRequired();
        _ = entity.Property(static loop => loop.RequiredArtifactsJson).IsRequired();
        _ = entity.Property(static loop => loop.ExecutionPattern).IsRequired().HasMaxLength(StatusMaxLength);
        _ = entity.Property(static loop => loop.Version).IsConcurrencyToken();
        _ = entity.HasMany(static loop => loop.Goals).WithOne(static goal => goal.Loop).HasForeignKey(static goal => goal.LoopId);
        _ = entity.HasMany(static loop => loop.Events).WithOne(static loopEvent => loopEvent.Loop).HasForeignKey(static loopEvent => loopEvent.LoopId);
    }

    /// <summary>Configures persistence for goals.</summary>
    /// <param name="entity">The goal entity configuration.</param>
    private static void ConfigureGoal(EntityTypeBuilder<Goal> entity)
    {
        _ = entity.ToTable(nameof(Goals));
        _ = entity.HasKey(static goal => goal.Id);
        _ = entity.HasIndex(static goal => new { goal.LoopId, goal.Sequence }).IsUnique();
        _ = entity.Property(static goal => goal.Description).IsRequired().HasMaxLength(DescriptionMaxLength);
        _ = entity.Property(static goal => goal.Prompt).IsRequired();
        _ = entity.Property(static goal => goal.DependsOnJson).IsRequired();
        _ = entity.Property(static goal => goal.TaskType).IsRequired().HasMaxLength(MetadataMaxLength);
        _ = entity.Property(static goal => goal.RequiredArtifactsJson).IsRequired();
        _ = entity.Property(static goal => goal.ApprovalState).IsRequired().HasMaxLength(StatusMaxLength);
        _ = entity.Property(static goal => goal.PolicyState).IsRequired().HasMaxLength(StatusMaxLength);
        _ = entity.Property(static goal => goal.PolicyViolationsJson).IsRequired();
        _ = entity.Property(static goal => goal.Status).HasConversion<string>().HasMaxLength(StatusMaxLength);
        _ = entity.Property(static goal => goal.Version).IsConcurrencyToken();
        _ = entity.HasMany(static goal => goal.Results).WithOne(static result => result.Goal).HasForeignKey(static result => result.GoalId);
        _ = entity.HasOne(static goal => goal.AssignedAgent)
            .WithMany()
            .HasForeignKey(static goal => goal.AssignedAgentId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    /// <summary>Configures persistence for agents.</summary>
    /// <param name="entity">The agent entity configuration.</param>
    private static void ConfigureAgent(EntityTypeBuilder<Agent> entity)
    {
        _ = entity.ToTable(nameof(Agents));
        _ = entity.HasKey(static agent => agent.Id);
        _ = entity.Property(static agent => agent.Name).IsRequired().HasMaxLength(MetadataMaxLength);
        _ = entity.Property(static agent => agent.Capabilities).IsRequired();
        _ = entity.Property(static agent => agent.Endpoint).IsRequired().HasMaxLength(DescriptionMaxLength);
        _ = entity.Property(static agent => agent.SupportedTaskTypesJson).IsRequired();
        _ = entity.Property(static agent => agent.Sla).IsRequired().HasMaxLength(MetadataMaxLength);
        _ = entity.Property(static agent => agent.SecurityPosture).IsRequired().HasMaxLength(MetadataMaxLength);
        _ = entity.Property(static agent => agent.ToolScopesJson).IsRequired();
        _ = entity.Property(static agent => agent.CachedCapacityConstraintsJson).IsRequired();
        _ = entity.Property(static agent => agent.Version).IsConcurrencyToken();
        _ = entity.HasMany(static agent => agent.Events).WithOne(static agentEvent => agentEvent.Agent).HasForeignKey(static agentEvent => agentEvent.AgentId);
        _ = entity.HasOne(static agent => agent.AssignedGoal)
            .WithMany()
            .HasForeignKey(static agent => agent.AssignedGoalId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    /// <summary>Configures persistence for agent events.</summary>
    /// <param name="entity">The agent-event entity configuration.</param>
    private static void ConfigureAgentEvent(EntityTypeBuilder<AgentEvent> entity)
    {
        _ = entity.ToTable(nameof(AgentEvents));
        _ = entity.HasKey(static agentEvent => agentEvent.Id);
        _ = entity.Property(static agentEvent => agentEvent.Type).HasConversion<string>().HasMaxLength(EventTypeMaxLength);
        _ = entity.Property(static agentEvent => agentEvent.Detail).IsRequired();
    }

    /// <summary>Configures persistence for goal results.</summary>
    /// <param name="entity">The goal-result entity configuration.</param>
    private static void ConfigureGoalResult(EntityTypeBuilder<GoalResult> entity)
    {
        _ = entity.ToTable(nameof(GoalResults));
        _ = entity.HasKey(static result => result.Id);
        _ = entity.Property(static result => result.Output).IsRequired();
        _ = entity.Property(static result => result.Metadata).IsRequired();
    }

    /// <summary>Configures persistence for loop events.</summary>
    /// <param name="entity">The loop-event entity configuration.</param>
    private static void ConfigureLoopEvent(EntityTypeBuilder<LoopEvent> entity)
    {
        _ = entity.ToTable(nameof(LoopEvents));
        _ = entity.HasKey(static loopEvent => loopEvent.Id);
        _ = entity.Property(static loopEvent => loopEvent.Type).HasConversion<string>().HasMaxLength(EventTypeMaxLength);
        _ = entity.Property(static loopEvent => loopEvent.Detail).IsRequired();
        _ = entity.HasOne(static loopEvent => loopEvent.Goal)
            .WithMany()
            .HasForeignKey(static loopEvent => loopEvent.GoalId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    /// <summary>Assigns testable UTC timestamps to newly persisted entities.</summary>
    private void StampNewEntities()
    {
        var timestamp = timeProvider.GetUtcNow();

        StampTimestamp<Loop>(timestamp, static entity => entity.CreatedAt, static (entity, value) => entity.CreatedAt = value);
        StampTimestamp<Agent>(timestamp, static entity => entity.CreatedAt, static (entity, value) => entity.CreatedAt = value);
        StampTimestamp<AgentEvent>(timestamp, static entity => entity.CreatedAt, static (entity, value) => entity.CreatedAt = value);
        StampTimestamp<LoopEvent>(timestamp, static entity => entity.CreatedAt, static (entity, value) => entity.CreatedAt = value);
        StampTimestamp<GoalResult>(timestamp, static entity => entity.CompletedAt, static (entity, value) => entity.CompletedAt = value);
    }

    /// <summary>Assigns a timestamp to added entities with an unset timestamp.</summary>
    /// <typeparam name="TEntity">The entity type to stamp.</typeparam>
    /// <param name="timestamp">The timestamp to assign.</param>
    /// <param name="getTimestamp">Gets the entity timestamp.</param>
    /// <param name="setTimestamp">Sets the entity timestamp.</param>
    private void StampTimestamp<TEntity>(DateTimeOffset timestamp, Func<TEntity, DateTimeOffset> getTimestamp, Action<TEntity, DateTimeOffset> setTimestamp)
        where TEntity : class
    {
        foreach (var entry in ChangeTracker.Entries<TEntity>())
        {
            if (entry.State == EntityState.Added && getTimestamp(entry.Entity) == default)
            {
                setTimestamp(entry.Entity, timestamp);
            }
        }
    }
}
