using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RALE.Server.Data;
using RALE.Server.Services;
using RALE.Server.Tools;

namespace RALE.Server;

public static class RaleMcpServerHost
{
    public static IHost CreateHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        var dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDirectory);

        var connectionString = builder.Configuration["ConnectionStrings:RALE"]
            ?? $"Data Source={Path.Combine(dataDirectory, "rale.db")}";

        builder.Services.AddRaleServices(connectionString);
        builder.Services.AddRaleMcpServer(CreateServerInfo());

        return builder.Build();
    }

    public static Implementation CreateServerInfo(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetExecutingAssembly();

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
                new Icon
                {
                    Source = "https://raw.githubusercontent.com/ChrisPulman/ReactiveAgenticLoopEngineer/main/images/rale-package-icon.png",
                    MimeType = "image/png",
                    Sizes = ["512x512"],
                },
            ],
        };
    }

    public static IServiceCollection AddRaleServices(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<RALEContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<ILoopEngineer, LoopEngineer>();
        services.AddHttpClient<IAgentCapacityClient, HttpAgentCapacityClient>();
        services.AddSingleton<IOrchestrationEngineer, OrchestrationEngineer>();
        services.AddSingleton<IAgentToolClient, DeterministicAgentToolClient>();
        services.AddSingleton<IAgentExecutor, AgentExecutor>();
        services.AddHostedService<RaleDatabaseInitializer>();

        return services;
    }

    public static IMcpServerBuilder AddRaleMcpServer(this IServiceCollection services, Implementation serverInfo)
        => services
            .AddMcpServer(options => options.ServerInfo = serverInfo)
            .WithStdioServerTransport()
            .WithTools([typeof(RaleLoopTools), typeof(RaleOrchestrationTools)]);
}
