using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RALE.Server.Data;
using RALE.Server.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

var assembly = Assembly.GetExecutingAssembly();
var serverVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? assembly.GetName().Version?.ToString()
    ?? "0.0.0";

var dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
Directory.CreateDirectory(dataDirectory);

var connectionString = builder.Configuration["ConnectionStrings:RALE"]
    ?? $"Data Source={Path.Combine(dataDirectory, "rale.db")}";

builder.Services.AddDbContextFactory<RALEContext>(options => options.UseSqlite(connectionString));
builder.Services.AddSingleton<ILoopEngineer, LoopEngineer>();
builder.Services.AddSingleton<IAgentToolClient, DeterministicAgentToolClient>();
builder.Services.AddSingleton<IAgentExecutor, AgentExecutor>();
builder.Services.AddHostedService<RaleDatabaseInitializer>();

builder.Services
    .AddMcpServer(options => options.ServerInfo = new Implementation
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
                Source = "https://raw.githubusercontent.com/ChrisPulman/ReactiveAgenticLoopEngineer/main/images/rale-icon.png",
                MimeType = "image/png",
                Sizes = ["1254x1254"],
            },
        ],
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
