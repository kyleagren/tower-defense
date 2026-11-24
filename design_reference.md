# Runewarder — Design Reference

_(Working title — open to change.)_

What the game currently is, and what's still undecided. Reasoning appears only where a decision is still live or where the obvious approach is wrong for a non-obvious reason. Not a history — that's what git is for.

This file is the index. The design itself lives in one file per topic under [`docs/design/`](docs/design/) — read the one the work touches, whole, rather than searching across all of them.

---

## High Concept

3D, low-poly stylized tower defense combining:

- **Dungeon Defenders** — the player has direct combat presence, can't solo-carry the defense, and picks from a roster of fixed-kit characters.
- **Path of Exile** — towers cast a base skill ("Spell") plus stacked support "Runes." Composition over inheritance, deep build expression.

**Theme.** The Architect is a time-traveler. Years of hopping between eras left cracks in reality behind him, and those fractures are letting monsters — and some imprisoned boss-tier figures whose bonds have weakened — loose in the periods he damaged. The campaign is him sealing each fracture. The Sentinel roster is drawn from people he met along the way, which is what lets it keep growing without needing a shared origin. The vertical slice stays in a single era; multi-era content (distinct rosters and biomes per period, not reskins) is the long-term vision.

**Engine:** Godot 4, C#. C# over GDScript for the simulation-heavy load (many enemies, projectiles and modifiers updating every frame).

**Co-op is a hard requirement** of the finished game — invite-a-friend only, no public matchmaking. Single-player is built first, but the architecture assumes co-op from day one (see [Architecture](docs/design/architecture.md)).

---

## Topics

| File | What's in it |
| --- | --- |
| [core_loop.md](docs/design/core_loop.md) | Phases (Planning, Build, Combat, PostGame), ready checks, mana, capacity and the economy. |
| [characters.md](docs/design/characters.md) | Sentinels and the Builder: Soldier and Tank kits, abilities, movement, building. |
| [controls_and_ui.md](docs/design/controls_and_ui.md) | Input and devices, controller support, the hint bar, pause menu, options, rebinding. |
| [rituals.md](docs/design/rituals.md) | What a Ritual is: spells, runes and tags, mana cost, targeting, line of sight, visuals, rune acquisition, damage and repair, placement, tokens, editing/copying, the radial build menu. |
| [spells.md](docs/design/spells.md) | Every built spell, one heading each, plus planned spells. |
| [runes.md](docs/design/runes.md) | Triggers, trigger runes, targeting runes, rune ideas not yet built. |
| [reactions.md](docs/design/reactions.md) | Status, elemental reactions (the reaction table) and crowd-control limits. |
| [enemies.md](docs/design/enemies.md) | Enemy types, movement and lanes, waves, the spawn wave preview, lane steering and Earth Wall. |
| [items.md](docs/design/items.md) | Items, rarities, mods, armor and shield, inventory, drops, stations (stash, vendor, crafting), tooltips. |
| [architecture.md](docs/design/architecture.md) | Cast pipeline, class notes, folder layout, save format, multiplayer/networking, deferred cleanups. |
| [hub_and_tools.md](docs/design/hub_and_tools.md) | The hub, dev tools, video options, main menu and save profiles. |
| [open_questions.md](docs/design/open_questions.md) | Parked topics, undecided design, rune and spell iconography. |

---

## Roadmap

In order. Remove an entry once it's built; its design lives in the file named.

1. **Key rebinding menu** ([controls_and_ui.md](docs/design/controls_and_ui.md)).
2. **Editing and copying a placed Ritual** ([rituals.md](docs/design/rituals.md), Construction & deployment UI).
3. **Lane steering and Earth Wall** ([enemies.md](docs/design/enemies.md)).
4. **Party section in the pause menu** ([architecture.md](docs/design/architecture.md), Multiplayer).
5. **Closing the Steam lobby to joins mid-match** ([architecture.md](docs/design/architecture.md), Multiplayer).
6. **Ally revive** ([characters.md](docs/design/characters.md)) — `Sentinel.Respawn()` is the hook.
7. **The missing reactions** ([reactions.md](docs/design/reactions.md)) — Blister and Scalding Zone.
8. **Design the Elementalist.**

## Backlog

Designed or noted but not scheduled. Each is described where it lives.

- **Characters** — Soldier's Harvest talent/mod hooks; Tank's Immolate target visual.
- **Controls & UI** — stick sensitivity and invert, aim assist, rumble, button-icon art, the affix popup on a controller, an on-screen keyboard; local split-screen.
- **Rituals** — Rituals hitting harder with no weapon equipped (in [items.md](docs/design/items.md)).
- **Spells & runes** — planned spells; a wall-explodes-on-break rune; the rune ideas in [runes.md](docs/design/runes.md).
- **Reactions** — tenacity (Layer 2 of crowd-control limits), only if a CC chain needs it.
- **Enemies** — wave-end safety net; navmesh re-baking on Ritual placement.
- **Items** — stock-gating Legendaries by progression, at the vendor and on drops.
- **Multiplayer** — syncing a Ritual's aim once a model has a turning part.
- **Hub** — a real art pass; thumbnails, stats and descriptions in the selection panels.
