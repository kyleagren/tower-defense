# Working Agreement

## Before creating or modifying files

- **Code files** (`.cs`, and similar): show the full suggested code in the conversation first, and wait for it to be reviewed/approved before creating or editing the actual file.
- **Scene files** (`.tscn`) and other structural/data files: a plain-language explanation of what will be created or changed is sufficient before proceeding — no need to paste raw file contents first. Hand-authoring these directly (rather than giving editor instructions) is fine once given the go-ahead.

## Don't let tooling limitations bias technical decisions

- If the correct/idiomatic solution to something requires an editor-only step (e.g. baking a navmesh by hand, dragging a node reference in the inspector, wiring an exported array), don't quietly substitute a code-based workaround just to avoid that step. Flag it, explain the tradeoff, and give clear step-by-step editor instructions instead — let the human decide, don't route around your own limitations by default.
- Try to rely to the Godot Official Documentation to determine the best method of doing things.

## Project context

Godot 4 (C#) 3D tower-defense project. `GDD.md` is the current-state design reference; `README.md` is the design journal (reasoning history, rejected alternatives). See both before proposing design changes.

## Code style notes (observed from existing scripts)

- Follow standard Godot / C# best practices / conventions. If GDScript is optimal for soemthing, that's fine too, but we've been keeping things in C# to try to optimize.
- 1 unit in Godot should correspond to 1 meter, but this is standard in Godot anyway.
