// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RALE.Server;

namespace RALE.Tests;

/// <summary>Verifies the MCP server exposes its public tools and packages an aligned manifest.</summary>
public sealed class McpToolRegistrationTests
{
    /// <summary>Gets the tool names that must be registered by the server.</summary>
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

    /// <summary>Ensures all public RALE tools are registered in the MCP service provider.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Test]
    public async Task AddRaleMcpServer_registers_all_public_RALE_tools()
    {
        var services = new ServiceCollection();
        _ = services.AddRaleMcpServer(new Implementation { Name = "rale-test", Version = "0.0.0" });

        await using var provider = services.BuildServiceProvider();
        var names = new List<string>();
        foreach (var tool in provider.GetServices<McpServerTool>())
        {
            names.Add(GetToolName(tool));
        }

        await Assert.That(names).IsEquivalentTo(ExpectedToolNames);
    }

    /// <summary>Ensures the packaged MCP manifest uses the current package version.</summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
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

    /// <summary>Gets the protocol name registered for an MCP tool.</summary>
    /// <param name="tool">The registered MCP tool.</param>
    /// <returns>The protocol name for <paramref name="tool" />.</returns>
    private static string GetToolName(McpServerTool tool)
    {
        var protocolTool = tool.GetType().GetProperty("ProtocolTool")?.GetValue(tool)
            ?? throw new InvalidOperationException("McpServerTool.ProtocolTool was not found.");

        return protocolTool.GetType().GetProperty("Name")?.GetValue(protocolTool) as string
            ?? throw new InvalidOperationException("Protocol tool name was not found.");
    }
}
