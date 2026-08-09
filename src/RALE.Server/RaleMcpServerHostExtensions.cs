// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using RALE.Server.Data;
using RALE.Server.Services;
using RALE.Server.Tools;

namespace RALE.Server;

/// <summary>Configures the Reactive Agentic Loop Engineer MCP host.</summary>
public static class RaleMcpServerHostExtensions
{
    /// <summary>Creates the configured MCP host.</summary>
    /// <param name="args">The host command-line arguments.</param>
    /// <returns>The configured host.</returns>
    public static IHost CreateHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        _ = builder.Logging.ClearProviders();
        _ = builder.Logging.AddConsole(static options => options.LogToStandardErrorThreshold = LogLevel.Trace);

        var dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
        _ = Directory.CreateDirectory(dataDirectory);

        var connectionString = builder.Configuration["ConnectionStrings:RALE"]
            ?? $"Data Source={Path.Combine(dataDirectory, "rale.db")}";

        _ = builder.Services.AddSingleton(TimeProvider.System);
        _ = builder.Services.AddRaleServices(connectionString);
        _ = builder.Services.AddRaleMcpServer(CreateServerInfo());

        return builder.Build();
    }

    /// <summary>Creates MCP server metadata from the RALE server assembly.</summary>
    /// <returns>The server implementation metadata.</returns>
    public static Implementation CreateServerInfo() => CreateServerInfo(typeof(RaleMcpServerHostExtensions).Assembly);

    /// <summary>Creates MCP server metadata from an assembly.</summary>
    /// <param name="assembly">The assembly that supplies the server version.</param>
    /// <returns>The server implementation metadata.</returns>
    public static Implementation CreateServerInfo(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var serverVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";

        return new Implementation
        {
            Name = "reactive-agentic-loop-engineer-mcp-server",
            Version = serverVersion,
            Title = "Reactive Agentic Loop Engineer MCP Server",
            Description = "Persisted prompt-loop engineering server for goal-bounded decomposition, claim-safe execution, pause/resume lifecycle, and SQLite-backed audit state.",
            WebsiteUrl = "https://github.com/ChrisPulman/ReactiveAgenticLoopEngineer",
            Icons =
            [
                new Icon { Source = "https://raw.githubusercontent.com/ChrisPulman/ReactiveAgenticLoopEngineer/main/images/rale-package-icon.png", MimeType = "image/png", Sizes = ["512x512"] },
            ],
        };
    }

    /// <summary>Provides RALE service-collection registration extensions.</summary>
    /// <param name="services">The service collection being configured.</param>
    extension(IServiceCollection services)
    {
        /// <summary>Registers the persistence and orchestration services.</summary>
        /// <param name="connectionString">The SQLite connection string.</param>
        /// <returns>The configured service collection.</returns>
        public IServiceCollection AddRaleServices(string connectionString)
        {
            _ = services.AddDbContextFactory<RALEContext>(options => options.UseSqlite(connectionString));
            _ = services.AddSingleton<ILoopEngineer, LoopEngineer>();
            _ = services.AddHttpClient<IAgentCapacityClient, HttpAgentCapacityClient>();
            _ = services.AddSingleton<IOrchestrationEngineer, OrchestrationEngineer>();
            _ = services.AddSingleton<IAgentToolClient, DeterministicAgentToolClient>();
            _ = services.AddSingleton<IAgentExecutor, AgentExecutor>();
            _ = services.AddHostedService<RaleDatabaseInitializer>();

            return services;
        }

        /// <summary>Registers the MCP server transport and tools.</summary>
        /// <param name="serverInfo">The protocol metadata to publish.</param>
        /// <returns>The configured MCP server builder.</returns>
        public IMcpServerBuilder AddRaleMcpServer(Implementation serverInfo) => services
            .AddMcpServer(options => options.ServerInfo = serverInfo)
            .WithStdioServerTransport()
            .WithTools([typeof(RaleLoopTools), typeof(RaleOrchestrationTools)]);
    }
}
