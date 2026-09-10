# Working Agreement

## Before creating or modifying files

- **Code files** (`.cs`, and similar): show the full suggested code, then stop — do not call Edit/Write in the same turn. Wait for an explicit reply approving it (e.g. "yes," "go ahead," "looks good") in a separate message before creating or editing the actual file. Silence, a follow-up question, or moving on to a new topic is not approval. The one exception is the Readme. You should ask or be instructed to do so before updating the readme, but you don't have to post your idea before updating the readme with it.
- **Scene files** (`.tscn`) and other structural/data files: a plain-language explanation of what will be created or changed is sufficient instead of pasting full raw content — but the same rule applies otherwise: explain, then stop and wait for an explicit go-ahead before writing. Hand-authoring these directly (rather than giving editor instructions) is fine once that go-ahead is given.
- If it's ambiguous whether approval was actually given, ask rather than assume.

## Don't let tooling limitations bias technical decisions

- If the correct/idiomatic solution to something requires an editor-only step (e.g. baking a navmesh by hand, dragging a node reference in the inspector, wiring an exported array), don't quietly substitute a code-based workaround just to avoid that step. Flag it, explain the tradeoff, and give clear step-by-step editor instructions instead — let the human decide, don't route around your own limitations by default.
- Try to rely to the Godot Official Documentation to determine the best method of doing things.

## Project context

Godot 4 (C#) 3D tower-defense project. `README.md` is the design reference — **current-state design; reasoning only where a decision is still live or genuinely surprising.** See it before proposing design changes.

Keep it that way. Do not add implementation logs ("extracted X into Y so that..."), bug post-mortems, changelog entries, or rationale for decisions nobody is going to reopen — git history and the code itself already hold those, and the doc becomes unreadable when they accumulate in it. When a feature is finished, the README should describe what the game *does*, not how it got built.

## Code style notes

- **Naming/style standard (decided): follow Godot's official style guides exactly, no project-specific exceptions.** For C# code: [Godot's C# style guide](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_style_guide.html) — notably, private fields (instance or static, not `const`) use an underscore prefix + camelCase (`_aimingAt`), everything else at member level (methods, properties, constants, events, types, namespaces) is PascalCase, locals/parameters are camelCase, interfaces get an `I` prefix. For files and folders: [Godot's project organization guide](https://docs.godotengine.org/en/stable/tutorials/best_practices/project_organization.html) — snake_case for all files and folders, except `.cs` files, which stay PascalCase named after their class. Chosen deliberately over inventing a bespoke house style, specifically so there's one canonical, complete reference to defer to instead of re-litigating individual naming calls — see a specific rule isn't covered here, check the linked guide before improvising.
- **Non-public fields are `_camelCase`.** If a subclass needs access, expose a PascalCase `protected` property rather than widening the field. Godot's C# guide only specifies public members and private fields, so this keeps us inside it as written instead of inventing a third convention for `protected`.
- **A tower is a `Ritual`.** "Tower" was the working word early on and has been swept out of the code — use `Ritual` in class names, identifiers, input actions, physics layer names, and player-facing copy. (README still uses "tower" in design prose in places; that's prose, not a naming rule.)
- If GDScript is optimal for something, that's fine too, but we've been keeping things in C# to try to optimize.
- 1 unit in Godot should correspond to 1 meter, but this is standard in Godot anyway.
- **Comments describe the present, never the past.** Default to no comment — well-named identifiers already cover the *what*. Write one only when the *why* is non-obvious: a hidden constraint, a subtle invariant, an ordering requirement, or behavior that would genuinely surprise a reader. Never narrate how the code got here: no "extracted from X so that...", no "used to be Y", no "same as the old Z", no references to a fixed bug or a class that no longer exists. If a comment only makes sense to someone who saw the previous version of the file, delete it.

## Resources: where values live

- **Base gameplay values live on `Spell`/`Rune` resources**, authored in `.tres` and reachable from code.
- **Shapes and meshes inside `.tscn` files are unit placeholders that code sizes at runtime.** Construct them fresh in code rather than mutating the scene's copy — a sub-resource in a scene is shared by every instance of that scene, so writing to it stomps every other instance. (`resource_local_to_scene` is the alternative, and is the right tool when a scene genuinely authors per-instance content that code then tweaks — we don't currently have that case.)
- Physics layer bit masks live in `PhysicsLayers`; keep it in sync with `project.godot`'s `[layer_names]`.
- **Hand-authored `.tres` files for a C# `Resource` script must use the exact C# property name (PascalCase), not snake_case.** GDScript's own exported vars serialize as snake_case, but Godot looks up a C# `[Export]` property by its literal C# identifier — writing `icon = ...` instead of `Icon = ...` doesn't error, it just silently leaves the property at its default. If a hand-written `.tres` value doesn't seem to be taking effect, check the casing first.
