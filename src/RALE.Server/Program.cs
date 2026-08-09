// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Hosting;

namespace RALE.Server;

/// <summary>Provides the RALE MCP server application entry point.</summary>
internal static class Program
{
    /// <summary>Runs the RALE MCP server until its stdio transport is closed.</summary>
    /// <param name="args">The host command-line arguments.</param>
    /// <returns>A task that represents the server lifetime.</returns>
    internal static async Task Main(string[] args)
    {
        using var host = RaleMcpServerHostExtensions.CreateHost(args);
        await host.RunAsync().ConfigureAwait(false);
    }
}
