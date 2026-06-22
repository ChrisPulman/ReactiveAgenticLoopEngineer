using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RALE.Server;

namespace RALE.Tests;

public sealed class McpToolRegistrationTests
{
    private static readonly string[] ExpectedToolNames =
    [
        "rale_approve_goal",
        "rale_assign_next_task",
        "rale_claim_next_goal",
        "rale_complete_goal",
        "rale_create_loop",
        "rale_create_master_plan",
        "rale_discover_agent_capacity",
        "rale_get_loop",
        "rale_list_agents",
        "rale_list_goals",
        "rale_pause_goal",
        "rale_record_goal_heartbeat",
        "rale_register_agent",
        "rale_resplit_goal",
        "rale_resume_goal",
    ];

    [Test]
    public async Task AddRaleMcpServer_registers_all_public_RALE_tools()
    {
        var services = new ServiceCollection();
        services.AddRaleMcpServer(new Implementation
        {
            Name = "rale-test",
            Version = "0.0.0",
        });

        await using var provider = services.BuildServiceProvider();
        var names = provider
            .GetServices<McpServerTool>()
            .Select(GetToolName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        await Assert.That(names).IsEquivalentTo(ExpectedToolNames);
    }

    [Test]
    public async Task Packaged_mcp_manifest_points_to_current_package_version()
    {
        var manifestPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".mcp", "server.json"));
        using var document = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        var root = document.RootElement;
        var package = root.GetProperty("packages")[0];

        await Assert.That(root.GetProperty("version").GetString()).IsEqualTo("1.0.2");
        await Assert.That(package.GetProperty("version").GetString()).IsEqualTo("1.0.2");
    }

    private static string GetToolName(McpServerTool tool)
    {
        var protocolTool = tool.GetType().GetProperty("ProtocolTool")?.GetValue(tool)
            ?? throw new InvalidOperationException("McpServerTool.ProtocolTool was not found.");

        return protocolTool.GetType().GetProperty("Name")?.GetValue(protocolTool) as string
            ?? throw new InvalidOperationException("Protocol tool name was not found.");
    }
}
