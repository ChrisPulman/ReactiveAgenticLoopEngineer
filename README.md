<p align="center">
  <img src="images/rale-package-icon.png" alt="RALE logo" width="500">
</p>

# Reactive Agentic Loop Engineer

<!-- mcp-name: io.github.chrispulman/reactive-agentic-loop-engineer-mcp-server -->

Reactive Agentic Loop Engineer (RALE) is a production-oriented C# Model Context Protocol server for decomposing large prompts into persisted, goal-bounded work loops that an agent can pause, resume, audit, and drive to completion.

The server uses .NET 10, `ModelContextProtocol` 2.1.0, `ReactiveUI.Primitives`, EF Core SQLite, and TUnit on Microsoft.Testing.Platform.

## Quick Install

Click to install in your preferred environment:

[![VS Code - Install RALE MCP](https://img.shields.io/badge/VS_Code-Install_RALE_MCP-0098FF?style=flat-square&logo=visualstudiocode&logoColor=white)](https://vscode.dev/redirect/mcp/install?name=reactive-agentic-loop-engineer-mcp-server&config=%7B%22type%22%3A%22stdio%22%2C%22command%22%3A%22dnx%22%2C%22args%22%3A%5B%22CP.Reactive.Agentic.Loop.Engineer.MCP.Server%401.*%22%2C%22--yes%22%5D%7D)
[![VS Code Insiders - Install RALE MCP](https://img.shields.io/badge/VS_Code_Insiders-Install_RALE_MCP-24bfa5?style=flat-square&logo=visualstudiocode&logoColor=white)](https://insiders.vscode.dev/redirect/mcp/install?name=reactive-agentic-loop-engineer-mcp-server&config=%7B%22type%22%3A%22stdio%22%2C%22command%22%3A%22dnx%22%2C%22args%22%3A%5B%22CP.Reactive.Agentic.Loop.Engineer.MCP.Server%401.*%22%2C%22--yes%22%5D%7D&quality=insiders)
[![Visual Studio - Install RALE MCP](https://img.shields.io/badge/Visual_Studio-Install_RALE_MCP-5C2D91?style=flat-square&logo=visualstudio&logoColor=white)](https://vs-open.link/mcp-install?%7B%22name%22%3A%22CP.Reactive.Agentic.Loop.Engineer.MCP.Server%22%2C%22type%22%3A%22stdio%22%2C%22command%22%3A%22dnx%22%2C%22args%22%3A%5B%22CP.Reactive.Agentic.Loop.Engineer.MCP.Server%401.*%22%2C%22--yes%22%5D%7D)

> **Note:** These install links are prepared for the intended NuGet package identity `CP.Reactive.Agentic.Loop.Engineer.MCP.Server`.
> If the latest package has not been published yet, use the manual source-build configuration below.

## What RALE Provides

- Persisted loops, goals, agents, goal results, and append-only loop events.
- A reactive `Signal<Goal>` pipeline for ready-goal emission.
- Prompt decomposition that never emits a goal whose `Prompt.Length` exceeds the configured limit.
- Agent-card registration with capabilities, capacity profile, trust posture, task types, SLA, endpoint, and least-privilege tool scopes.
- On-demand capacity discovery through agent endpoints with TTL-based cache/profile fallback.
- Master-plan decomposition into capacity-fit subtasks assigned to candidate agents with serial or parallel dependency graphs.
- Governance gates for trust level, task type support, required tool scopes, and human approval.
- Bounded reactive-loop recovery through heartbeats, iteration limits, and capacity-mismatch re-splitting.
- Optimistic database concurrency for goal claiming so duplicate execution is rejected.
- Goal pause/resume and complete/fail transitions.
- MCP tools for loop creation, agent registration, capacity discovery, master-plan creation, dispatch, approval, completion, pause, resume, heartbeat, and re-splitting.
- TUnit tests covering decomposition, persistence, ready-goal emission, claiming, completion, pause/resume, capacity fallback, governance, dispatch, and re-splitting.

RALE persists orchestration state and exposes the lifecycle primitives used by an agent-side orchestrator. It does not currently run a hosted autonomous scheduler, expire heartbeats, retry failed work by itself, or execute arbitrary agent tools in the background. Retry limits, deadlines, constraints, and required artifacts remain visible in the persisted contract so the orchestrating agent can enforce them. RALE governance is auditable dispatch policy; it does not replace MCP host authentication, operating-system permissions, or user approval.

## Required Companion MCP Servers for Codex

A Codex autonomous workflow must configure these companion MCP servers alongside RALE:

- `CP.ReactiveMemory.Mcp.Server` is the durable memory layer. Search it before planning, then store compact decisions and verified outcomes so later sessions, projects, chats, and resumed tasks can reuse them.
- `CP.Reactive.Multi.Agent.MCP.Server` owns durable multi-agent sessions, specialist selection, named sub-agent lifecycle, checkpoints, results, heartbeats, and recovery policy.

These are required companion MCP servers, not RALE in-process library references. Each package is an independently hosted .NET tool/MCP server. RALE remains the durable goal-loop and audit authority; Codex coordinates the three servers and retains responsibility for user authorization and tool safety.

## Repository Layout

```text
src/RALE.Server/
  Program.cs
  Data/
    RALEContext.cs
    RaleDatabaseInitializer.cs
    Migrations/
  Models/
  Services/
    LoopEngineer.cs
    AgentExecutor.cs
    OrchestrationEngineer.cs
    HttpAgentCapacityClient.cs
    PromptDecomposer.cs
  Tools/
    RaleLoopTools.cs
    RaleOrchestrationTools.cs
    LoopDto.cs
    GoalDto.cs
    AgentDto.cs
    RaleDtoExtensions.cs
tests/RALE.Tests/
images/
  rale-image.ico
  rale-image.png
  rale-package-icon.png
.mcp/server.json
skills/RALE/SKILL.md
```

## MCP Tools

| Tool | Purpose |
| --- | --- |
| `rale_create_loop` | Create a loop and decompose the primary prompt into ordered goals. |
| `rale_get_loop` | Fetch a loop and its goals. |
| `rale_list_goals` | List ordered goals for a loop. |
| `rale_claim_next_goal` | Claim the next ready goal with optimistic concurrency. |
| `rale_complete_goal` | Persist a result, complete a goal, and emit dependent goals. |
| `rale_pause_goal` | Pause a pending or in-progress goal. |
| `rale_resume_goal` | Resume a paused goal and re-emit when ready. |
| `rale_register_agent` | Register an agent card with capabilities, capacity, trust, task types, endpoint, and tool scopes. |
| `rale_list_agents` | List registered agents with load and cached capacity metadata. |
| `rale_discover_agent_capacity` | Query live agent capacity and fall back to cached/profile capacity when needed. |
| `rale_create_master_plan` | Create a capacity-fit multi-agent plan with serial or parallel dependencies and governance metadata. |
| `rale_assign_next_task` | Assign the next ready task for an agent while enforcing load, dependency, policy, and approval gates. |
| `rale_approve_goal` | Approve or reject a goal blocked by a human approval gate. |
| `rale_record_goal_heartbeat` | Persist execution heartbeat/provenance for long-running agent loops. |
| `rale_resplit_goal` | Replace a capacity-mismatched goal with smaller dependency-preserving subtasks. |

### Autonomous Codex Workflow

Search Reactive Memory first and resume an existing RALE loop when one exists. For a simple loop, inspect it and claim one ready goal before executing it. For a master plan, create or resume the corresponding Reactive Multi-Agent session, register the required agents, create the plan, and let each executor use `rale_assign_next_task`; do not mix assigned-task dispatch with `rale_claim_next_goal`.

After each unit of work, persist a checkpoint or result, verify the required evidence, complete or pause the RALE goal, and inspect persisted state again. Continue this bounded loop until all required goals are terminal. Send heartbeats during long operations, re-split a goal when capacity is insufficient, and complete only verified work with concise artifact and test metadata. `tokenLimit` is a character ceiling, not a token budget, and RALE never overrides user approval or tool-permission requirements.

Prefer local execution for local coordination. For example, an interval-based wait should use a bounded local timer or process monitor that reports progress and wakes the orchestrator for the next state inspection; it should not allocate an AI agent merely to sleep. Use sub-agents only for meaningful independent work and always close them after completion or failure.

## Database Schema

SQLite tables:

- `Loops`: primary objective, status, token limit, execution pattern, constraints, artifacts, priority, deadline, iteration limit, optimistic version.
- `Goals`: sequence, description, bounded prompt, JSON dependencies, assigned agent, task type, artifacts, approval state, policy state, retry and iteration limits, status, optimistic version.
- `Agents`: agent card metadata, endpoint, current load, trust level, tool scopes, capacity cache, optional assigned goal.
- `AgentEvents`: append-only audit trail for registration, capacity discovery, and capacity fallback.
- `GoalResults`: output, JSON metadata, completion time.
- `LoopEvents`: append-only audit trail for loop and goal transitions.

The schema is initialized through EF Core migrations at server startup.

## Installation

### Requirements

- .NET 10 SDK
- An MCP-capable client such as VS Code, Visual Studio, Claude Desktop, or another MCP 1.x host

### Install as a .NET tool

Once the NuGet package is published:

```powershell
dotnet tool install -g CP.Reactive.Agentic.Loop.Engineer.MCP.Server
```

Then configure your MCP client:

```json
{
  "type": "stdio",
  "command": "reactive-agentic-loop-engineer-mcp-server"
}
```

### Install via `dnx`

Use the badge links at the top of this file, or configure manually:

```json
{
  "type": "stdio",
  "command": "dnx",
  "args": ["CP.Reactive.Agentic.Loop.Engineer.MCP.Server@1.*", "--yes"]
}
```

### Manual configuration from source

Clone the repository and configure your MCP client to launch the server from source:

```json
{
  "name": "reactive-agentic-loop-engineer-mcp-server",
  "type": "stdio",
  "command": "dotnet",
  "args": [
    "run",
    "--project",
    "/path/to/ReactiveAgenticLoopEngineer/src/RALE.Server/RALE.Server.csproj"
  ]
}
```

### Run from source

```powershell
dotnet run --project src/RALE.Server/RALE.Server.csproj
```

By default RALE stores SQLite data at:

```text
src/RALE.Server/bin/<Configuration>/net10.0/data/rale.db
```

Override it with configuration key `ConnectionStrings:RALE`.

## Test

```powershell
dotnet test --solution src/ReactiveAgenticLoopEngineer.slnx --configuration Debug
```

Coverage with Microsoft.Testing.Platform:

```powershell
dotnet test --solution src/ReactiveAgenticLoopEngineer.slnx --configuration Debug --results-directory TestResults -- --coverage --coverage-output TestResults/coverage.cobertura.xml --coverage-output-format cobertura
```

## CI and Release Bounds

`BuildOnly.yml` runs the NUKE `Test` target once. Its local PowerShell monitor reports activity every 30 seconds, terminates the process tree after 20 minutes, and is contained by a 25-minute GitHub job timeout. `BuildDeploy.yml` gives build-and-pack the same 20-minute command limit inside a 30-minute release job; signing and NuGet publishing also have job and command bounds.

Release jobs use the protected `release` environment. A job shown by GitHub as **Waiting for approval** has not entered the build command and is not a build hang; an authorized reviewer must approve the environment before its runner work can start.

For a major release, CI derives the new major from the computed package version and rewrites exactly the three URL-encoded install references plus the one plain `dnx` reference in this README. It commits the resulting `major.*` update to the selected remote branch before packaging, then uses that new commit as the GitHub release/tag target. Major releases reject tag and SHA sources because those refs cannot safely persist the README update. Patch and minor releases leave the four references unchanged.

## Operational Notes

- Keep logs on stderr for stdio MCP transport.
- Treat MCP tool inputs as untrusted; tool methods validate required values and use `McpException` for client-visible validation errors.
- Keep `tokenLimit` conservative. RALE currently treats the limit as a character ceiling and exposes `EstimateTokens` for a conservative 4-chars-per-token estimate.
- Subscribers may receive the same pending goal event more than once; executors must claim before executing. Only one claim succeeds.
- Persisted results and loop events allow crash recovery and audit inspection.
- Register agents before creating a master plan. RALE requests live capacity from `GET /agents/{id}/capacity?taskProfile=...` when an endpoint is configured, then falls back to fresh cached capacity or the registered profile.
- Use `rale_create_master_plan` for multi-agent work. It stores policy violations on each generated goal and blocks assignment until `rale_approve_goal` clears human approval gates.
- Use `rale_record_goal_heartbeat` for long-running agent loops and `rale_resplit_goal` when an agent reports capacity mismatch. Re-splitting is bounded by each goal's iteration limit.
- For Codex-driven autonomous work, use RALE as the persisted execution record, Reactive Memory for cross-session recall, and Reactive Multi-Agent for durable sub-agent orchestration. Inspect and claim or assign each ready goal before work, send heartbeats during long operations, and complete it only with verified output and concise artifact metadata.
- Pause a goal before an intentional stop and resume from persisted state later. Heartbeats are liveness evidence, not automatic recovery; the agent-side orchestrator decides when policy permits retry, re-splitting, reassignment, or escalation.
