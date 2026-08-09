// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RALE.Server.Data;

/// <summary>Applies pending database migrations when the RALE server starts.</summary>
/// <param name="contextFactory">Creates database contexts for schema initialization.</param>
/// <param name="logger">Records database initialization events.</param>
public sealed partial class RaleDatabaseInitializer(
    IDbContextFactory<RALEContext> contextFactory,
    ILogger<RaleDatabaseInitializer> logger) : IHostedService
{
    /// <summary>Applies pending database migrations.</summary>
    /// <param name="cancellationToken">Signals that application startup has been cancelled.</param>
    /// <returns>A task that completes when the database schema is current.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        DatabaseSchemaCurrent(logger);
    }

    /// <summary>Stops the initializer.</summary>
    /// <param name="cancellationToken">Signals that application shutdown has been cancelled.</param>
    /// <returns>An already completed task because the initializer owns no background work.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "RALE database schema is current.")]
    private static partial void DatabaseSchemaCurrent(ILogger logger);
}
