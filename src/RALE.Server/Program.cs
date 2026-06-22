using Microsoft.Extensions.Hosting;
using RALE.Server;

using var host = RaleMcpServerHost.CreateHost(args);
await host.RunAsync();
