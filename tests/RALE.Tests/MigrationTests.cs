// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RALE.Server.Data;

namespace RALE.Tests;

/// <summary>Verifies that the EF Core migrations apply to real SQLite databases, as the server does at startup.</summary>
public sealed class MigrationTests
{
    /// <summary>Identifies the initial schema migration.</summary>
    private const string InitialCreateMigration = "20260614210000_InitialCreate";

    /// <summary>Identifies the multi-agent orchestration migration.</summary>
    private const string MultiAgentOrchestrationMigration = "20260616150000_MultiAgentOrchestration";

    /// <summary>Identifies the goal-to-agent foreign key column.</summary>
    private const string AssignedAgentIdColumn = "AssignedAgentId";

    /// <summary>Verifies that every migration applies to a new database and leaves no migration lock behind.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Migrate_applies_all_migrations_to_a_new_database()
    {
        await using var database = await MigrationDatabase.CreateAsync();

        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        var applied = await database.GetAppliedMigrationsAsync();
        _ = await Assert.That(applied).IsEquivalentTo([InitialCreateMigration, MultiAgentOrchestrationMigration]);
        _ = await Assert.That(await database.CountMigrationLocksAsync()).IsEqualTo(0L);
    }

    /// <summary>Verifies that a database created by the initial release upgrades to the latest schema.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Migrate_upgrades_a_database_created_by_the_initial_migration()
    {
        await using var database = await MigrationDatabase.CreateAsync();

        await using (var context = database.CreateContext())
        {
            await context.GetService<IMigrator>().MigrateAsync(InitialCreateMigration);
        }

        await using (var context = database.CreateContext())
        {
            _ = await Assert.That(await context.Database.GetPendingMigrationsAsync()).IsEquivalentTo([MultiAgentOrchestrationMigration]);
            await context.Database.MigrateAsync();
            _ = await Assert.That(await context.Database.GetPendingMigrationsAsync()).IsEmpty();
        }

        _ = await Assert.That(await database.CountMigrationLocksAsync()).IsEqualTo(0L);
    }

    /// <summary>Verifies that the migrated goal table enforces the assigned-agent foreign key with SET NULL deletes.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Migrate_creates_the_goal_assigned_agent_foreign_key()
    {
        await using var database = await MigrationDatabase.CreateAsync();

        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        var foreignKeys = await database.GetGoalForeignKeysAsync();
        _ = await Assert.That(foreignKeys).Contains($"Agents|{AssignedAgentIdColumn}|Id|SET NULL");

        var indexes = await database.GetGoalIndexesAsync();
        _ = await Assert.That(indexes).Contains("IX_Goals_AssignedAgentId");
    }

    /// <summary>Verifies that reverting the multi-agent orchestration migration restores the initial schema and can be re-applied.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task Migrate_reverts_to_the_initial_schema_and_reapplies()
    {
        await using var initialOnly = await MigrationDatabase.CreateAsync();
        await using (var context = initialOnly.CreateContext())
        {
            await context.GetService<IMigrator>().MigrateAsync(InitialCreateMigration);
        }

        var initialSchema = await initialOnly.GetSchemaColumnsAsync();

        await using var database = await MigrationDatabase.CreateAsync();
        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
        }

        await using (var context = database.CreateContext())
        {
            await context.GetService<IMigrator>().MigrateAsync(InitialCreateMigration);
        }

        _ = await Assert.That(await database.GetAppliedMigrationsAsync()).IsEquivalentTo([InitialCreateMigration]);
        _ = await Assert.That(await database.GetSchemaColumnsAsync()).IsEquivalentTo(initialSchema);
        _ = await Assert.That(await database.GetGoalColumnsAsync()).DoesNotContain(AssignedAgentIdColumn);

        await using (var context = database.CreateContext())
        {
            await context.Database.MigrateAsync();
            _ = await Assert.That(await context.Database.GetPendingMigrationsAsync()).IsEmpty();
        }

        _ = await Assert.That(await database.GetGoalForeignKeysAsync()).Contains($"Agents|{AssignedAgentIdColumn}|Id|SET NULL");
    }

    /// <summary>Owns an isolated on-disk SQLite database so migrations run exactly as they do in the server.</summary>
    private sealed class MigrationDatabase : IAsyncDisposable
    {
        /// <summary>Selects the applied migration identifiers.</summary>
        private const string AppliedMigrationsSql = """SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";""";

        /// <summary>Selects the goal table foreign keys.</summary>
        private const string GoalForeignKeysSql = """SELECT "table" || '|' || "from" || '|' || "to" || '|' || "on_delete" FROM pragma_foreign_key_list('Goals');""";

        /// <summary>Selects the goal table index names.</summary>
        private const string GoalIndexesSql = """SELECT "name" FROM pragma_index_list('Goals');""";

        /// <summary>Selects the goal table column names.</summary>
        private const string GoalColumnsSql = """SELECT "name" FROM pragma_table_info('Goals');""";

        /// <summary>Selects every application table column as <c>table.column type</c>, ignoring EF Core bookkeeping tables.</summary>
        private const string SchemaColumnsSql = """
            SELECT m."name" || '.' || p."name" || ' ' || p."type"
            FROM sqlite_master AS m
            JOIN pragma_table_info(m."name") AS p
            WHERE m."type" = 'table'
              AND m."name" NOT LIKE '\_\_EF%' ESCAPE '\'
              AND m."name" NOT LIKE 'sqlite\_%' ESCAPE '\'
            ORDER BY 1;
            """;

        /// <summary>Selects the EF Core migration lock table when it exists.</summary>
        private const string MigrationLockTableSql = """SELECT "name" FROM sqlite_master WHERE "type" = 'table' AND "name" = '__EFMigrationsLock';""";

        /// <summary>Counts the EF Core migration lock rows.</summary>
        private const string MigrationLockCountSql = """SELECT CAST(COUNT(*) AS TEXT) FROM "__EFMigrationsLock";""";

        /// <summary>The directory that holds the test database file.</summary>
        private readonly string _directory;

        /// <summary>The connection string for the test database file.</summary>
        private readonly string _connectionString;

        /// <summary>Initializes a new instance of the <see cref="MigrationDatabase"/> class.</summary>
        /// <param name="directory">The directory that holds the test database file.</param>
        /// <param name="connectionString">The connection string for the test database file.</param>
        private MigrationDatabase(string directory, string connectionString)
        {
            _directory = directory;
            _connectionString = connectionString;
        }

        /// <summary>Creates an empty database location.</summary>
        /// <returns>A task whose result is the isolated database.</returns>
        public static Task<MigrationDatabase> CreateAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), "rale-migration-tests", Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(directory);
            var connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "rale.db"), Pooling = false }.ToString();

            return Task.FromResult(new MigrationDatabase(directory, connectionString));
        }

        /// <summary>Creates a context for the test database.</summary>
        /// <returns>A context backed by the test database.</returns>
        public RALEContext CreateContext() =>
            new(new DbContextOptionsBuilder<RALEContext>().UseSqlite(_connectionString).Options, TimeProvider.System);

        /// <summary>Gets the applied migration identifiers in order.</summary>
        /// <returns>A task whose result is the applied migration identifiers.</returns>
        public Task<IReadOnlyList<string>> GetAppliedMigrationsAsync() =>
            ReadStringsAsync(static command => command.CommandText = AppliedMigrationsSql);

        /// <summary>Gets the goal table foreign keys as <c>table|from|to|on_delete</c> values.</summary>
        /// <returns>A task whose result is the goal table foreign keys.</returns>
        public Task<IReadOnlyList<string>> GetGoalForeignKeysAsync() =>
            ReadStringsAsync(static command => command.CommandText = GoalForeignKeysSql);

        /// <summary>Gets the goal table index names.</summary>
        /// <returns>A task whose result is the goal table index names.</returns>
        public Task<IReadOnlyList<string>> GetGoalIndexesAsync() =>
            ReadStringsAsync(static command => command.CommandText = GoalIndexesSql);

        /// <summary>Gets the goal table column names.</summary>
        /// <returns>A task whose result is the goal table column names.</returns>
        public Task<IReadOnlyList<string>> GetGoalColumnsAsync() =>
            ReadStringsAsync(static command => command.CommandText = GoalColumnsSql);

        /// <summary>Gets every application table column as <c>table.column type</c>.</summary>
        /// <returns>A task whose result is the application schema columns.</returns>
        public Task<IReadOnlyList<string>> GetSchemaColumnsAsync() =>
            ReadStringsAsync(static command => command.CommandText = SchemaColumnsSql);

        /// <summary>Counts rows left in the EF Core migration lock table.</summary>
        /// <returns>A task whose result is the number of lock rows, or zero when the table does not exist.</returns>
        public async Task<long> CountMigrationLocksAsync()
        {
            var tables = await ReadStringsAsync(static command => command.CommandText = MigrationLockTableSql);
            if (tables.Count == 0)
            {
                return 0L;
            }

            var rows = await ReadStringsAsync(static command => command.CommandText = MigrationLockCountSql);
            return long.Parse(rows[0], System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Deletes the test database directory.</summary>
        /// <returns>A value task that represents the asynchronous disposal operation.</returns>
        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
                // A lingering file handle only leaves a temp directory behind; it must not fail the test.
            }

            return ValueTask.CompletedTask;
        }

        /// <summary>Runs a fixed query and returns the first column of each row as text.</summary>
        /// <param name="setQuery">Assigns one of the fixed query constants to the command.</param>
        /// <returns>A task whose result is the first column of each row.</returns>
        private async Task<IReadOnlyList<string>> ReadStringsAsync(Action<SqliteCommand> setQuery)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            setQuery(command);
            await using var reader = await command.ExecuteReaderAsync();
            var values = new List<string>();
            while (await reader.ReadAsync())
            {
                values.Add(reader.GetString(0));
            }

            return values;
        }
    }
}
