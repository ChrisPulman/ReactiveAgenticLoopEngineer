using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RALE.Server.Data.Migrations;

[DbContext(typeof(RALEContext))]
[Migration("20260616150000_MultiAgentOrchestration")]
public sealed partial class MultiAgentOrchestration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddColumn<string>(
            name: "ConstraintsJson",
            table: "Loops",
            type: "TEXT",
            nullable: false,
            defaultValue: "{}");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "Deadline",
            table: "Loops",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ExecutionPattern",
            table: "Loops",
            type: "TEXT",
            maxLength: 32,
            nullable: false,
            defaultValue: "serial");

        migrationBuilder.AddColumn<int>(
            name: "IterationLimit",
            table: "Loops",
            type: "INTEGER",
            nullable: false,
            defaultValue: 3);

        migrationBuilder.AddColumn<int>(
            name: "Priority",
            table: "Loops",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "RequiredArtifactsJson",
            table: "Loops",
            type: "TEXT",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<bool>(
            name: "ApprovalRequired",
            table: "Goals",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "ApprovalState",
            table: "Goals",
            type: "TEXT",
            maxLength: 32,
            nullable: false,
            defaultValue: "NotRequired");

        migrationBuilder.AddColumn<Guid>(
            name: "AssignedAgentId",
            table: "Goals",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "Deadline",
            table: "Goals",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "IterationCount",
            table: "Goals",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "IterationLimit",
            table: "Goals",
            type: "INTEGER",
            nullable: false,
            defaultValue: 3);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastHeartbeatAt",
            table: "Goals",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PolicyState",
            table: "Goals",
            type: "TEXT",
            maxLength: 32,
            nullable: false,
            defaultValue: "Allowed");

        migrationBuilder.AddColumn<string>(
            name: "PolicyViolationsJson",
            table: "Goals",
            type: "TEXT",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<int>(
            name: "Priority",
            table: "Goals",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "RequiredArtifactsJson",
            table: "Goals",
            type: "TEXT",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<int>(
            name: "RetryCount",
            table: "Goals",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "RetryLimit",
            table: "Goals",
            type: "INTEGER",
            nullable: false,
            defaultValue: 2);

        migrationBuilder.AddColumn<string>(
            name: "TaskType",
            table: "Goals",
            type: "TEXT",
            maxLength: 128,
            nullable: false,
            defaultValue: "general");

        migrationBuilder.AddColumn<int>(
            name: "CachedCapacity",
            table: "Agents",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CachedCapacityConstraintsJson",
            table: "Agents",
            type: "TEXT",
            nullable: false,
            defaultValue: "{}");

        migrationBuilder.AddColumn<int>(
            name: "CapacityCacheTtlSeconds",
            table: "Agents",
            type: "INTEGER",
            nullable: false,
            defaultValue: 300);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CapacityCheckedAt",
            table: "Agents",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CapacityExpiresAt",
            table: "Agents",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "CurrentLoad",
            table: "Agents",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "Endpoint",
            table: "Agents",
            type: "TEXT",
            maxLength: 512,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<int>(
            name: "MaxConcurrentGoals",
            table: "Agents",
            type: "INTEGER",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "MaxTokenCapacity",
            table: "Agents",
            type: "INTEGER",
            nullable: false,
            defaultValue: 4096);

        migrationBuilder.AddColumn<string>(
            name: "SecurityPosture",
            table: "Agents",
            type: "TEXT",
            maxLength: 128,
            nullable: false,
            defaultValue: "unverified");

        migrationBuilder.AddColumn<string>(
            name: "Sla",
            table: "Agents",
            type: "TEXT",
            maxLength: 128,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "SupportedTaskTypesJson",
            table: "Agents",
            type: "TEXT",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "ToolScopesJson",
            table: "Agents",
            type: "TEXT",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<int>(
            name: "TrustLevel",
            table: "Agents",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<long>(
            name: "Version",
            table: "Agents",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.CreateTable(
            name: "AgentEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                AgentId = table.Column<Guid>(type: "TEXT", nullable: false),
                Type = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Detail = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AgentEvents", x => x.Id);
                table.ForeignKey(
                    name: "FK_AgentEvents_Agents_AgentId",
                    column: x => x.AgentId,
                    principalTable: "Agents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AgentEvents_AgentId",
            table: "AgentEvents",
            column: "AgentId");

        migrationBuilder.CreateIndex(
            name: "IX_Goals_AssignedAgentId",
            table: "Goals",
            column: "AssignedAgentId");

        migrationBuilder.AddForeignKey(
            name: "FK_Goals_Agents_AssignedAgentId",
            table: "Goals",
            column: "AssignedAgentId",
            principalTable: "Agents",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropForeignKey(name: "FK_Goals_Agents_AssignedAgentId", table: "Goals");
        migrationBuilder.DropTable(name: "AgentEvents");
        migrationBuilder.DropIndex(name: "IX_Goals_AssignedAgentId", table: "Goals");

        migrationBuilder.DropColumn(name: "ConstraintsJson", table: "Loops");
        migrationBuilder.DropColumn(name: "Deadline", table: "Loops");
        migrationBuilder.DropColumn(name: "ExecutionPattern", table: "Loops");
        migrationBuilder.DropColumn(name: "IterationLimit", table: "Loops");
        migrationBuilder.DropColumn(name: "Priority", table: "Loops");
        migrationBuilder.DropColumn(name: "RequiredArtifactsJson", table: "Loops");

        migrationBuilder.DropColumn(name: "ApprovalRequired", table: "Goals");
        migrationBuilder.DropColumn(name: "ApprovalState", table: "Goals");
        migrationBuilder.DropColumn(name: "AssignedAgentId", table: "Goals");
        migrationBuilder.DropColumn(name: "Deadline", table: "Goals");
        migrationBuilder.DropColumn(name: "IterationCount", table: "Goals");
        migrationBuilder.DropColumn(name: "IterationLimit", table: "Goals");
        migrationBuilder.DropColumn(name: "LastHeartbeatAt", table: "Goals");
        migrationBuilder.DropColumn(name: "PolicyState", table: "Goals");
        migrationBuilder.DropColumn(name: "PolicyViolationsJson", table: "Goals");
        migrationBuilder.DropColumn(name: "Priority", table: "Goals");
        migrationBuilder.DropColumn(name: "RequiredArtifactsJson", table: "Goals");
        migrationBuilder.DropColumn(name: "RetryCount", table: "Goals");
        migrationBuilder.DropColumn(name: "RetryLimit", table: "Goals");
        migrationBuilder.DropColumn(name: "TaskType", table: "Goals");

        migrationBuilder.DropColumn(name: "CachedCapacity", table: "Agents");
        migrationBuilder.DropColumn(name: "CachedCapacityConstraintsJson", table: "Agents");
        migrationBuilder.DropColumn(name: "CapacityCacheTtlSeconds", table: "Agents");
        migrationBuilder.DropColumn(name: "CapacityCheckedAt", table: "Agents");
        migrationBuilder.DropColumn(name: "CapacityExpiresAt", table: "Agents");
        migrationBuilder.DropColumn(name: "CurrentLoad", table: "Agents");
        migrationBuilder.DropColumn(name: "Endpoint", table: "Agents");
        migrationBuilder.DropColumn(name: "MaxConcurrentGoals", table: "Agents");
        migrationBuilder.DropColumn(name: "MaxTokenCapacity", table: "Agents");
        migrationBuilder.DropColumn(name: "SecurityPosture", table: "Agents");
        migrationBuilder.DropColumn(name: "Sla", table: "Agents");
        migrationBuilder.DropColumn(name: "SupportedTaskTypesJson", table: "Agents");
        migrationBuilder.DropColumn(name: "ToolScopesJson", table: "Agents");
        migrationBuilder.DropColumn(name: "TrustLevel", table: "Agents");
        migrationBuilder.DropColumn(name: "Version", table: "Agents");
    }
}
