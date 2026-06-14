using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RALE.Server.Data.Migrations;

[DbContext(typeof(RALEContext))]
[Migration("20260614210000_InitialCreate")]
public sealed partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "Loops",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                PrimaryObjective = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                TokenLimit = table.Column<int>(type: "INTEGER", nullable: false),
                Version = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Loops", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Goals",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                LoopId = table.Column<Guid>(type: "TEXT", nullable: false),
                Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                Prompt = table.Column<string>(type: "TEXT", nullable: false),
                DependsOnJson = table.Column<string>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                Version = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Goals", x => x.Id);
                table.ForeignKey(
                    name: "FK_Goals_Loops_LoopId",
                    column: x => x.LoopId,
                    principalTable: "Loops",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Agents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                Capabilities = table.Column<string>(type: "TEXT", nullable: false),
                AssignedGoalId = table.Column<Guid>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Agents", x => x.Id);
                table.ForeignKey(
                    name: "FK_Agents_Goals_AssignedGoalId",
                    column: x => x.AssignedGoalId,
                    principalTable: "Goals",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "GoalResults",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                GoalId = table.Column<Guid>(type: "TEXT", nullable: false),
                Output = table.Column<string>(type: "TEXT", nullable: false),
                Metadata = table.Column<string>(type: "TEXT", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GoalResults", x => x.Id);
                table.ForeignKey(
                    name: "FK_GoalResults_Goals_GoalId",
                    column: x => x.GoalId,
                    principalTable: "Goals",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "LoopEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                LoopId = table.Column<Guid>(type: "TEXT", nullable: false),
                GoalId = table.Column<Guid>(type: "TEXT", nullable: true),
                Type = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Detail = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LoopEvents", x => x.Id);
                table.ForeignKey(
                    name: "FK_LoopEvents_Goals_GoalId",
                    column: x => x.GoalId,
                    principalTable: "Goals",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_LoopEvents_Loops_LoopId",
                    column: x => x.LoopId,
                    principalTable: "Loops",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_Agents_AssignedGoalId", table: "Agents", column: "AssignedGoalId");
        migrationBuilder.CreateIndex(name: "IX_GoalResults_GoalId", table: "GoalResults", column: "GoalId");
        migrationBuilder.CreateIndex(name: "IX_Goals_LoopId_Sequence", table: "Goals", columns: ["LoopId", "Sequence"], unique: true);
        migrationBuilder.CreateIndex(name: "IX_LoopEvents_GoalId", table: "LoopEvents", column: "GoalId");
        migrationBuilder.CreateIndex(name: "IX_LoopEvents_LoopId", table: "LoopEvents", column: "LoopId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable(name: "Agents");
        migrationBuilder.DropTable(name: "GoalResults");
        migrationBuilder.DropTable(name: "LoopEvents");
        migrationBuilder.DropTable(name: "Goals");
        migrationBuilder.DropTable(name: "Loops");
    }
}
