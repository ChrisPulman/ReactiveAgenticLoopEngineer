---
name: rale
description: Use when Codex works with Reactive Agentic Loop Engineer (RALE), including durable cross-session goal loops, companion memory and multi-agent orchestration, capacity-fit planning, governance-gated dispatch, pause/resume, bounded completion, and SQLite-backed audit state.
---

# RALE MCP Server

Use this skill when an agent needs to operate or extend the Reactive Agentic Loop Engineer MCP server.

## Required Companion MCP Servers

For Codex autonomous execution, treat these as hard companion-server requirements:

- Use `CP.ReactiveMemory.Mcp.Server` to search durable memory before planning and to store compact decisions, corrections, checkpoints, and verified outcomes for reuse across sessions, projects, and chats.
- Use `CP.Reactive.Multi.Agent.MCP.Server` to create or resume the durable multi-agent session, select specialists, manage named sub-agents, record checkpoints/results/heartbeats, apply recovery policy, and close every completed or failed sub-agent.

These packages are independently hosted .NET tool/MCP servers, not compiled RALE libraries. RALE owns persisted goal-loop state and audit history; the Codex-side orchestrator coordinates all three servers and remains responsible for user authorization and safe tool execution.

## Bounded Completion Loop

1. Search Reactive Memory for the objective, project, and prior loop/session identifiers. Resume the matching RALE loop and Reactive Multi-Agent session instead of creating duplicates.
2. Inspect `rale_get_loop` and `rale_list_goals`. Check dependencies, policy/approval state, retry count and limit, deadline, constraints, and required artifacts before selecting work.
3. Claim a simple-loop goal or assign a master-plan goal using the mutually exclusive workflows below. Execute the smallest useful unit with local tools or a meaningful specialist sub-agent.
4. Record heartbeats and companion-server checkpoints while useful work is active. Verify outputs and required artifacts, then complete, fail, re-split, or pause the persisted goal truthfully.
5. Re-inspect persisted state and continue until every required goal is terminal. If the user requests an interruption, pause active RALE work, checkpoint the companion session, store a compact memory, and resume from those identifiers later.

RALE does not currently host an autonomous scheduling/supervision loop, execute arbitrary agent tools in the background, expire heartbeats, or retry stranded work by itself. The Codex-side orchestrator performs that bounded loop and enforces persisted retry limits, deadlines, constraints, and artifact requirements.

## Single-Executor Workflow

1. Create a loop with `rale_create_loop` only when durable, auditable autonomous execution is needed. State the objective, constraints, artifacts, execution pattern, and a conservative `tokenLimit`.
2. Treat `tokenLimit` as a hard prompt-length ceiling. RALE must never emit a goal whose `Prompt.Length` exceeds that limit.
3. Inspect the persisted contract with `rale_get_loop` and `rale_list_goals`; do not infer a goal's ownership, dependency state, or approval state from local conversation alone.
4. Claim a single ready goal with `rale_claim_next_goal` immediately before executing it. A null result or lost optimistic-concurrency race means another executor owns the work; do not retry blindly.
5. Record `rale_record_goal_heartbeat` while a claimed goal runs long enough that another executor could mistake it for abandoned work.
6. Pause with `rale_pause_goal` before an intentional interruption and resume only with `rale_resume_goal`. Do not complete paused, unclaimed, or partially verified work.
7. Complete verified work with `rale_complete_goal`, including concise output and structured metadata that reconstructs decisions, artifacts, and downstream dependencies.

## Codex Autonomous Execution Contract

- Use RALE as the durable source of truth for a loop, but keep user authorization, tool permissions, and repository safety rules in force. A persisted goal never expands Codex's authority.
- For a simple loop created with `rale_create_loop`, execute one claimed goal at a time. For a master plan, do not call `rale_claim_next_goal`; use the assigned-goal workflow below. Always use the goal IDs returned by RALE, never locally invented IDs.
- Before any irreversible or external action, inspect the goal's policy and approval state. If RALE blocks it for human approval, use `rale_approve_goal` only when that approval has actually been obtained.
- Report evidence in completion metadata: files changed, commands/tests run, external effects, and unresolved risks. Keep secrets and raw credentials out of prompts, metadata, and loop events.
- If capacity, scope, or prompt accounting no longer fits, use `rale_resplit_goal` with a concrete reason and capacity limit. Do not silently truncate work or manufacture a successful completion.
- Prefer a local tool or process for local coordination. If work only needs an interval wait, use a bounded local timer/process monitor, report periodic progress, and wake for the next persisted-state inspection; do not spend an AI-agent request merely sleeping.
- Use sub-agents for meaningful independent tasks, not trivial commands. Give every build or potentially long process a hard timeout that the orchestrator can cancel, record its result, and close the sub-agent after success or failure.

## Multi-Agent Orchestration Workflow

1. Register only the needed agents with `rale_register_agent`, including capabilities, supported task types, max concurrent goals, max token capacity, trust level, endpoint, and tool scopes.
2. Create capacity-fit plans with `rale_create_master_plan`. It evaluates registered capacity; use `rale_discover_agent_capacity` when you need its current diagnostics explicitly. Choose `serial` when generated subtasks must run in dependency order and `parallel` when they can run independently.
3. For each registered executor, call `rale_assign_next_task(loopId, agentId)` and execute only the returned goal. A null result means no task is currently assignable; do not claim or fabricate work.
4. Assignment enforces dependencies, agent load, policy state, and human approval gates. Clear a human gate with `rale_approve_goal` only after that approval has been obtained; policy violations remain auditable.
5. Record liveness/provenance with `rale_record_goal_heartbeat` while agents iterate; heartbeats are not proof of completion. Complete verified assigned work with `rale_complete_goal`.
6. Use `rale_resplit_goal` when an agent reports capacity mismatch. Re-splitting is bounded by the goal iteration limit, preserves downstream dependencies, and records audit events.
7. Reconcile RALE goal state with Reactive Multi-Agent checkpoints/results and Reactive Memory, then stop only when `rale_get_loop` or `rale_list_goals` shows that every required goal has reached a terminal state.

## Operational Rules

- Keep MCP transport on stdio and logs on stderr.
- Store intermediate outputs in `GoalResults`; rely on `LoopEvents` for audit history.
- Split prompts early when accounting is uncertain.
- Sanitize prompts and restrict tool scope before delegating goal execution.
- Treat agent endpoints as open-world calls. Use cached capacity and human approval gates when discovery fails or trust/tool-scope checks are not satisfied.
- Treat heartbeats as persisted liveness evidence, not proof of completion or automatic recovery. Apply retries, reassignment, re-splitting, or escalation only when the persisted policy and user authorization permit them.
- Governance is persisted dispatch policy, not hard authentication or authorization. Continue to enforce MCP host credentials, operating-system permissions, secrets boundaries, and user approvals outside RALE.
- Prefer TUnit tests and TUnit assertions when changing decomposition, goal status transitions, or persistence behavior.
