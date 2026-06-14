using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RALE.Server.Data;

public sealed class RaleDatabaseInitializer(
    IDbContextFactory<RALEContext> contextFactory,
    ILogger<RaleDatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("RALE database schema is current.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
