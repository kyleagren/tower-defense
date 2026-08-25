# Working Agreement

## Before creating or modifying files

- **Code files** (`.cs`, and similar): show the full suggested code, then stop — do not call Edit/Write in the same turn. Wait for an explicit reply approving it (e.g. "yes," "go ahead," "looks good") in a separate message before creating or editing the actual file. Silence, a follow-up question, or moving on to a new topic is not approval.
- **Scene files** (`.tscn`) and other structural/data files: a plain-language explanation of what will be created or changed is sufficient instead of pasting full raw content — but the same rule applies otherwise: explain, then stop and wait for an explicit go-ahead before writing. Hand-authoring these directly (rather than giving editor instructions) is fine once that go-ahead is given.
- If it's ambiguous whether approval was actually given, ask rather than assume.

## Don't let tooling limitations bias technical decisions

- If the correct/idiomatic solution to something requires an editor-only step (e.g. baking a navmesh by hand, dragging a node reference in the inspector, wiring an exported array), don't quietly substitute a code-based workaround just to avoid that step. Flag it, explain the tradeoff, and give clear step-by-step editor instructions instead — let the human decide, don't route around your own limitations by default.
- Try to rely to the Godot Official Documentation to determine the best method of doing things.

## Project context

Godot 4 (C#) 3D tower-defense project. `README.md` is the design reference — current-state design plus enough reasoning to avoid re-litigating settled decisions. See it before proposing design changes.

## Code style notes (observed from existing scripts)

- Follow standard Godot / C# best practices / conventions. If GDScript is optimal for soemthing, that's fine too, but we've been keeping things in C# to try to optimize.
- 1 unit in Godot should correspond to 1 meter, but this is standard in Godot anyway.
