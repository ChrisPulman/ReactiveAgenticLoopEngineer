using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RALE.Server.Data;

public sealed partial class RaleDatabaseInitializer(
    IDbContextFactory<RALEContext> contextFactory,
    ILogger<RaleDatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        DatabaseSchemaCurrent(logger);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "RALE database schema is current.")]
    private static partial void DatabaseSchemaCurrent(ILogger logger);
}
