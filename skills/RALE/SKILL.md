---
name: rale
description: Use when working with Reactive Agentic Loop Engineer MCP Server, including persisted prompt loops, token or character bounded goal decomposition, goal claiming, pause/resume lifecycle, completion, and SQLite-backed audit state.
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

## Operational Rules

- Keep MCP transport on stdio and logs on stderr.
- Store intermediate outputs in `GoalResults`; rely on `LoopEvents` for audit history.
- Split prompts early when accounting is uncertain.
- Sanitize prompts and restrict tool scope before delegating goal execution.
- Prefer TUnit tests and TUnit assertions when changing decomposition, goal status transitions, or persistence behavior.
