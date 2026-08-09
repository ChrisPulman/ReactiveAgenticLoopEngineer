// Copyright (c) 2023-2026 Chris Pulman and Contributors. All rights reserved.
// Chris Pulman and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RALE.Server.Services;

/// <summary>Represents the output and metadata produced by an agent tool.</summary>
/// <param name="Output">The output produced by the agent.</param>
/// <param name="Metadata">The serialized execution metadata.</param>
public sealed record AgentExecutionResult(string Output, string Metadata);
