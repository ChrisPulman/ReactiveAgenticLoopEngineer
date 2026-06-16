---
name: rale
description: Use when working with Reactive Agentic Loop Engineer MCP Server, including persisted prompt loops, agent-card registration, capacity discovery, capacity-fit master-plan decomposition, dispatch gates, pause/resume lifecycle, completion, and SQLite-backed audit state.
---

# RALE MCP Server

Use this skill when an agent needs to operate or extend the Reactive Agentic Loop Engineer MCP server.

## Workflow

1. Create a loop with `rale_create_loop` and a conservative `tokenLimit`.
2. Treat `tokenLimit` as a hard prompt-length ceiling. RALE must never emit a goal whose `Prompt.Length` exceeds that limit.
3. List or inspect state with `rale_get_loop` and `rale_list_goals`.
4. Claim a goal with `rale_claim_next_goal` before executing it. Only one executor should succeed because claims use optimistic concurrency.
5. Pause or resume active work with `rale_pause_goal` and `rale_resume_goal` when work must stop before final completion.
6. Complete work with `rale_complete_goal`; include result metadata that helps reconstruct decisions and downstream dependencies.

## Multi-Agent Orchestration Workflow

1. Register agents with `rale_register_agent`, including capabilities, supported task types, max concurrent goals, max token capacity, trust level, endpoint, and tool scopes.
2. Use `rale_discover_agent_capacity` before planning or when a task profile changes. RALE queries `GET /agents/{id}/capacity?taskProfile=...` when an endpoint is configured and falls back to cached/profile capacity when needed.
3. Create capacity-fit plans with `rale_create_master_plan`. Choose `serial` when generated subtasks must run in dependency order and `parallel` when they can run independently.
4. Dispatch with `rale_assign_next_task`. Assignment enforces dependencies, agent load, policy state, and human approval gates.
5. Clear human gates with `rale_approve_goal`; policy violations remain visible on each goal for audit.
6. Record liveness/provenance with `rale_record_goal_heartbeat` while agents iterate.
7. Use `rale_resplit_goal` when an agent reports capacity mismatch. Re-splitting is bounded by the goal iteration limit and records audit events.

## Operational Rules

- Keep MCP transport on stdio and logs on stderr.
- Store intermediate outputs in `GoalResults`; rely on `LoopEvents` for audit history.
- Split prompts early when accounting is uncertain.
- Sanitize prompts and restrict tool scope before delegating goal execution.
- Treat agent endpoints as open-world calls. Use cached capacity and human approval gates when discovery fails or trust/tool-scope checks are not satisfied.
- Prefer TUnit tests and TUnit assertions when changing decomposition, goal status transitions, or persistence behavior.
