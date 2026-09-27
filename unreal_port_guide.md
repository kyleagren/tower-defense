# Unreal Port Guide

Handoff for a Claude Code session starting the Unreal version of this game. Written from the Godot project (Godot 4.7, C#) on 2026-09-26.

**Read in this order:**

1. This file — how the Godot project is built, what's been decided, and how it maps onto Unreal.
2. `README.md` (copy it into the Unreal project) — the full game design: what every system _does_. It's the design reference; this file doesn't repeat it.
3. The Godot source itself, when you need an exact formula or number. Everything is text: `.cs` for logic, `.tres` for data (spells, runes), `.tscn` for scenes, `assets/waves/*.json` for waves.

**Status:** the user is evaluating Unreal Engine **5.8.3** with a short test before committing to a port (Section 9). Godot work is paused mid-way through multiplayer. Nothing is decided until that test is done.

---

## 1. Working agreement (carry these over)

These are the user's standing rules from the Godot project's `CLAUDE.md` and Claude's saved feedback. Replace only the Godot-specific parts.

- **Show code, then stop.** For any code file, post the full proposed code and wait for an explicit approval ("yes", "go ahead") in a _separate_ message before writing it. Approval never carries over from an earlier batch. If it's ambiguous whether approval was given, ask.
- **Data/scene/config files:** a plain-language description of the change is enough instead of full contents — but still wait for a go-ahead.
- **Don't build or run commands.** The user builds and runs. Use search/read tools; only use a shell when nothing else can do the job, and ask first.
- **Don't mention commits.** The user manages git; never suggest or remind about committing.
- **Don't route around editor-only steps.** If the idiomatic solution needs an editor action (wiring a reference, a Blueprint default, a Project Setting), say so and give step-by-step instructions rather than substituting a code workaround. Check the official docs for the right way.
- **Comments describe the present, never the past.** Default to none; write one only for a non-obvious why. No "used to be", no references to removed code.
- **README is current-state design only.** No changelogs, implementation logs or post-mortems. Ask before updating it; you don't have to post the text first.
- **Naming:** the Godot project followed Godot's official C# style guide exactly. For Unreal, the equivalent is **Epic's C++ Coding Standard** (A/U/F/E/I/T prefixes, PascalCase, `b` prefix for bools). Confirm with the user that it replaces the Godot rule wholesale.
- **"Tower" is "Ritual"** in code, identifiers and player-facing text. (The README's prose sometimes still says tower.)
- **1 unit = 1 meter** in Godot. Unreal uses centimeters — every distance in the README and the Godot data is in meters and needs ×100.

**Version caution:** Claude's Unreal knowledge is strongest for UE4 and UE 5.0–5.5. 5.8 is newer than what Claude knows in detail — check 5.8 docs/release notes for anything version-sensitive (template contents, networking settings, build setup) rather than trusting memory.

**Keep the project C++-first.** Blueprint assets are binary `.uasset` files Claude can't read or edit. All gameplay logic, abilities, networking and data definitions go in C++; Blueprints only pick meshes/materials/sounds and set values. Same for data: prefer `UDataAsset` classes defined in C++ (values set in the editor) or plain-text sources (JSON/CSV) where the design allows.

---

## 2. What exists in Godot

All built unless marked otherwise. The README has the detail for each.

- **Core loop:** four phases per level — Planning (walk around as Sentinel) → Build (become the Architect, place Rituals) → Combat (Sentinel, waves) → PostGame (loot spray, return to hub). Every "move on" is a ready check with a host-started 90s countdown.
- **Rituals (towers):** Spell + socketed Runes, composed into a per-cast `SpellCastContext`. Spells: Fireball, Lightning Bolt (chaining), Ice Wall (placed wall), Ice Storm (bombardment). ~16 rune types. Elemental Status + reactions (Ignition built; more designed).
- **Sentinels:** Soldier (rifle, ADS, headshots; Converge/Repel, Weak-Point Visor, Health Link, Harvest/Overdrive) and Tank (sword swing, shield block; heat dial; Shield Slam, Retaliation, Vent, Immolate). Dodge roll, sprint latch, death with escalating respawn timer.
- **Architect:** non-combat builder body for Build phase; build menu, Ritual placement (ghost + validity), tokens (pre-crafted Rituals deployable mid-wave by the Sentinel).
- **Enemies:** Grunt (melee), Marksman (ranged, real projectiles), Sapper (targets Rituals), TrainingDummy (hub DPS test). Lane-following on precomputed waypoints, leash/chase/rejoin state machine, knockback, ragdoll on death, taunt.
- **Items:** 11 equipment slots, PoE-style affixes (5 tiers, weighted pool), rarities, armor/shield base types, crafting table, vendor, 40-slot inventory with drag/click/controller support, rich tooltips (advanced view, compare), world drops with nameplate pickup.
- **Economy:** match mana (towers, tokens, respawn skip), persistent gold, capacity budget.
- **Controller support:** first-class, PS4-tested — device detection, prompts, chords (L1+face = abilities, R1+dpad = tokens), UI focus navigation.
- **Hub:** two rooms — character select, level select, crafting, vendor, Architect test zone, training dummies.
- **Visuals:** almost all placeholder (capsules, boxes, procedural meshes). No character animation except a procedural walk on enemies.
- **Multiplayer:** in progress — see Section 5.

---

## 3. Codebase map

Folders are by domain. No C# namespaces. Key classes, in the order you'd port them:

| Folder | Holds | Key classes |
| --- | --- | --- |
| `core/` | Cross-cutting | `LevelContext` (per-level registries + per-player body lookups), `EventBus` (global signals), `EconomyManager`, `NetworkSession`, `PlayerId`, `CharacterId`, `GameRules` (tuning constants), `PhysicsLayers`, `InputDevice`, `InputChords` |
| `characters/` | Player bodies | `PlayerCharacter` (movement, look, camera, activation), `Sentinel` (health/shield/armor, dodge, death, ability input, attack rolls), `SentinelSpawner`, interactors (`LookInteractor`, `StationInteractor`, `ItemInteractor`, `RitualInteractor`), `RitualPlacer`, `TokenDeployer` |
| `characters/abilities/` | `Ability` base | Cooldowns, gear scaling (`ScaledArea/Duration/Damage`), capture/lockout hooks |
| `characters/soldier/`, `characters/tank/` | Each Sentinel + its abilities | `Soldier`, `Tank`, one class per ability |
| `characters/architect/` | `Architect` | |
| `spells/` | Cast pipeline | `Spell`, `SpellInstance`, `SpellCastContext` (+ `cast_contexts/` components), `cast_effects/` (delivery), `effects/` (on-hit), `runes/`, `effect_volumes/` (`EffectVolume`, `GroundEffect`, `Bombardment`), `ChainResolver`, `Tag` |
| `rituals/` | `Ritual` | HP/disabled/repair, target selection, casting |
| `combat/` | Damage model | `Hit`, `HitType`, `DamagePacket`, `Attribution`, `IDamageable`, `debuffs/` + `debuffs/ailments/` |
| `enemies/` | `Enemy` → `WalkingEnemy` | `EnemyProjectile`, `TrainingDummy`, `ProceduralWalkAnimator` |
| `waves/` | Match flow | `WaveController`, `MatchState`, `GameOverHandler`, `EnemySpawner`, `Lane`, `EnemyCatalog`, wave POCOs |
| `items/` | Itemization | `Item` + per-slot subclasses, `affixes/` (`Affix`, `AffixTable`, `RolledAffix`), `LootGenerator`, `CraftingActions`, `CharacterSheet`, `ItemDropManager` |
| `save/` | Persistence | `PlayerSaveData`, `PlayerProfile(s)`, `SaveManager`, loadouts |
| `ui/` | All UI (code-built mostly) | `player_hud.tscn` pieces, `BuildMenu`, inventory, tooltips, `UiPanelStack`, `PauseMenu`, `OptionsMenu` |
| `levels/` | Scenes | `main.tscn` + `LevelLoader`, `hub/`, `test_level/`, `level_systems.tscn` |
| `visual_effects/` | Pure visuals | Grouped `soldier/`, `tank/`, `spells/`; `GroundMesh` helper |
| `assets/` | Data | `spells/*.tres`, `runes/*/*.tres`, `waves/*.json` |

---

## 4. Godot → Unreal concept map

A starting point, not a mandate — confirm the big ones (starred) with the user.

| Godot (this project) | Unreal equivalent |
| --- | --- |
| Scene / Node tree | Actor + Components; Blueprint subclass of a C++ class for content variants |
| `Resource` `.tres` (Spell, Rune — shared definitions) | `UPrimaryDataAsset` subclasses in C++, instances authored in editor. **Same rule: shared, so never cache per-instance state on them.** |
| Autoload (`EventBus`, `SaveManager`, `NetworkSession`, tooltip/drag controllers) | `UGameInstanceSubsystem` (session-wide) |
| `LevelContext` (per-level registries) | `UWorldSubsystem`, or registries on `AGameStateBase` |
| `EventBus` signals | Multicast delegates on a subsystem / GameState |
| `PlayerId` seats P1–P4 | `APlayerController` + `APlayerState` per player; keep a seat index on the PlayerState if the design needs P1–P4 |
| `PlayerProfile` (persistent per-player data) | `USaveGame` + a GameInstance subsystem; remote players' copies replicate via PlayerState or RPC |
| **Per-player match record (planned, Section 6)** | **`APlayerState`** — this is exactly its job |
| `SentinelSpawner` + `MultiplayerSpawner` | `AGameMode` spawning pawns (`RestartPlayer`, `SpawnDefaultPawnFor`) and `Possess` |
| Body swap (Sentinel ↔ Architect) | GameMode spawns the new pawn and the controller possesses it; old pawn destroyed |
| `MatchState` + `WaveController` | `AGameMode` (server-only rules, waves) + `AGameState` (replicated phase, ready set, countdown) |
| `LevelLoader` + persistent Main scene | `ServerTravel` with seamless travel (carries PlayerStates/controllers across maps) |
| `MultiplayerSynchronizer` | Replicated `UPROPERTY`s; `UCharacterMovementComponent` already replicates and predicts movement |
| RPCs | `UFUNCTION(Server/Client/NetMulticast, Reliable/Unreliable)` |
| `NetworkSession` (ENet, seats) | Online Subsystem (Steam) sessions + GameMode `PostLogin`/`Logout` |
| Input Map actions + `InputDevice` + chords | Enhanced Input (Input Actions, Mapping Contexts, chorded actions); CommonUI for input-type switching and prompts |
| `UiPanelStack` + code-built `Control` UI | UMG + **CommonUI** activatable widget stack (handles focus, back, controller) |
| `Area3D` / `EffectVolume` | Actor with a `USphereComponent`/box using overlap events |
| Raycasts, shape queries | `LineTraceSingleByChannel`, `OverlapMultiByChannel` |
| Physics layers (`PhysicsLayers`) | Collision channels + collision profiles |
| `Lane` waypoints + `NavigationAgent3D` avoidance | NavMesh + `AAIController`; keep the precomputed-waypoint approach, use RVO or Detour crowd avoidance |
| Ragdoll on death | Skeletal mesh + Physics Asset, `SetSimulatePhysics` on death |
| `Tween` | Timelines, curves, or small tick-driven interpolation in C++ |
| **`Ability` (Node with state) + `Debuff` list** | **Decision:** port the custom system to C++ as-is, or adopt **Gameplay Ability System (GAS)** — abilities, cooldowns, attributes (health/shield/heat), effects (debuffs/ailments), gameplay tags (elements, status) and replication/prediction are all its job. GAS fits this game's shape closely but has a real learning curve. Raise this with the user before porting abilities. |

---

## 5. Multiplayer — decisions already made

These were decided with the user and apply in any engine:

- **Co-op only, invite-only, up to 4 players.** No public matchmaking.
- **A player hosts (listen server).** No dedicated servers. The host is seat P1; joiners take the next free seat.
- **Trust split:** each player's machine owns its own character (movement, aim, and whether its own shots/swings hit — reported to the host); the host owns everything shared (enemies, economy, Rituals, phases, win/loss). Invite-only makes cheating a non-concern. In Unreal, CharacterMovementComponent handles owner movement; hit reports become Server RPCs.
- **The hub is the lobby.** No single/multiplayer menu choice — single-player is hosting with nobody connected. **Joining is hub-only**; a match in progress can't be joined. A joiner's world becomes a copy of the host's.
- **Only the host starts a level** (level select tells clients "The host chooses the level") and only the host returns everyone to the hub.
- **Clients get "Leave Session"** in the pause menu (back to their own solo hub); the host gets "Return to Hub" in a match.
- **Someone leaving mid-match:** their bodies disappear, the match continues, **Rituals they placed stay**. The host leaving drops everyone to solo.
- **Pause:** each player has their own pause menu (in the HUD); it only freezes the game when nobody else is connected. Someone joining a paused host unpauses the game.
- **Steam is the release transport:** friends-only lobby opened automatically in the hub, "Invite Friends" button in the pause menu, privacy setting in Options. In Unreal: Online Subsystem Steam + Steam Sockets (check 5.8 docs for the current recommended setup).
- **Steam testing:** App ID 480 (Spacewar) is Valve's SDK sample; using it for your own game is a common convention but not Valve-sanctioned. The clean path is the user's own App ID (Steam Direct fee), usable privately pre-release.
- **Dev testing before Steam:** localhost and LAN (the user has a second person/PC for LAN).

**Where the Godot implementation stopped:** phase 1 (sessions, seats, per-seat bodies, position sync) and phase 2a (persistent Main scene, host-driven level switching, Leave Session) are built. Next was the per-player state record and body swaps (Section 6), then host-owned match state, then enemies, economy/Rituals, ability replication, instanced loot. The phase list is the same one worth following in Unreal.

**Enemy replication is the scale risk.** A tower defense can have many enemies alive; replicate lean (position/health/state), consider relevancy and update rates early.

---

## 6. Player state vs. body — built in Godot

In Unreal this is simply **PlayerState vs. Pawn**. The README's Section 6 describes the Godot version (`PlayerLevelState`, `SentinelDefinition`/`SentinelStats`, `PlayerSpawnPoint`).

- **One body per player at a time.** Sentinel in Planning, Combat and PostGame; Architect in Build. The GameMode swaps bodies at each phase change, new body placed where the old one stood.
- **Per-player match state survives swaps:** Tank **heat** (cleared on death), Harvest's **Overdrive gauge** (charge + per-element shares; cleared on death), the **death count** that escalates respawn time and cost.
- **Resets at every swap:** ability **cooldowns** (user's call — Build means cooldowns come back), health/shield (already full-healed at Build), dodge/block/attack timers. Overdrive ends at wave end anyway.
- **Per-seat spawn points** in every level and the hub (replaces the Godot level's hand-placed Architect).
- **Ready checks count connected players, not Sentinels** (in Build there are no Sentinels).
- A build menu open when Combat starts must be cleaned up with the Architect, or it blocks input.

**Decided:** stat math works without a body (Sentinel base values on a data asset), so the character sheet shows your Sentinel during Build; the heat gauge stays visible in Build; a dud Health Link clearing when the Sentinel is swapped out is fine; Health and Shield refill when Build starts; Harvest's gauge survives death and empties only when Overdrive spends it.

---

## 7. Architecture worth keeping

The README explains each; these are the ones that are engine-independent and hard-won:

- **Cast pipeline** (README §7): shared immutable `Spell` → per-Ritual `SpellInstance` → per-cast `SpellCastContext` snapshot → stateless `CastEffect` → independent `Projectile` → `ProjectileHitEffect`. Nothing downstream reaches back upstream.
- **`SpellCastContext` is one class with nullable capability components** (Projectile, Chain, Area, InstantHit, Wall, CastTrigger), not a subclass tree — a spell needing two unrelated capabilities broke the subclass version. Runes can lazily add components.
- **Rune on-hit behavior reaches every damage path** via the hit (`SpellHit` on `Hit`), not just projectiles.
- **`Hit` bundles everything about one hit**; `HitType` (Direct / Unmarked / DamageOverTime / Mirrored) decides whether it sets Status and whether debuffs react. Attribution (which player) follows damage through burns and mirrors.
- **Debuffs:** a list on the enemy, three hooks (`Tick`, `OnHostDamaged`, `SpeedMultiplier`), one general stacking rule (`OccupiesSameSlotAs` + `ResolveAgainst`, strongest-wins; Burn merges). Ailments = debuffs with elements. (If GAS is adopted, these become GameplayEffects — keep the stacking rules.)
- **Stats:** `StatId` vocabulary; `CharacterSheet` holds _bonuses only_, each consumer combines with its own base. Damage = Base × (1 + Increased) × More. Per-element Ritual damage in a bucket runes can't touch. Armor/Shield resolve per item.
- **Every write to player data goes through `PlayerProfile`** — the single place that becomes host-validated in co-op.
- **Waves are JSON** (`assets/waves/*.json`): ordered steps of `spawn` bursts `{lane, enemy, count}` or `wait`. Reusable as-is.
- **Enemy variety is subclassing**, not behavior composition.
- **Abilities:** tap/hold is a slot feature; slot partners never run together; cooldown starts when the ability _ends_; per-ability gear-effectiveness levers; the three input gates for abilities that capture the attack button; "a click an ability took never becomes weapon fire". Wave end (handing to the Architect) ends running abilities — see README §7 class notes.
- **Loot timing:** queued, sprayed from the objective at PostGame (tentative). Loot is instanced per player in co-op (decided, not built).

---

## 8. Lessons learned

**Transfer to Unreal:**

- **Shared data assets are shared.** A value on a Spell/Rune asset or a mesh/shape asset is shared by every user; never write per-instance state into it. Build per-instance collision/meshes at runtime from resolved sizes.
- **Walking enemies steer horizontally and fall under gravity** — steering to a navmesh point's full height floats them above the real floor.
- **Spawn and despawn can arrive in the same frame** on a client. Read anything you need from an outgoing body at the moment the new one is created, not a frame later.
- **Aim at a target's aim point**, not its origin (feet).
- **Knockback is horizontal only** (a vertical launch needs stand-up animations per enemy).
- **Nameplate hit-testing beats raycasting at small pickups**; tooltips pinned to a fixed screen corner beat anchored ones.
- **Controller focus:** a plain click that doesn't drift past a small threshold must fall through to the click path, or controller confirm breaks drag-and-drop.
- **Save format fragility:** Godot's save embedded script paths, so renaming a class broke saves. In Unreal, use a versioned `USaveGame` (or a stable type tag + schema version) from day one.

**Godot-only — don't carry over:** typed exports against inherited-scene nodes, `.tscn` values dropped when the editor re-saves before a rebuild, case-only file renames minting new uids, `CurrentScene` vs. level-root parenting, `MultiplayerSynchronizer` process-mode quirks.

---

## 9. The Unreal test (before committing)

Goal: find out, in a few evenings, whether working in Unreal is clearly better. Build **C++-first** from the **Third Person** template.

1. **Two players over LAN** (and/or two local instances) in a room, each with the template character and its animation, seeing each other move.
2. **One body swap** (e.g. template character ↔ a second pawn class), triggered by one player and seen by both.
3. **Travel together** to a second map and back, host-driven (seamless travel), with both players' state intact.
4. **One ability ported to C++** — Shield Slam is a good pick (120° cone, knockback by mass, damage roll, heat gain, a ground fan visual). Enough to feel what gameplay code, iteration and compile times are like.

Compare against Godot on: how fast iteration feels, how much Claude could do vs. what needed the editor, how much the template/animation saved, and anything that fought back. Then decide.

**If the port goes ahead, suggested order:** project conventions (coding standard, folder layout, collision channels, input) → player body + camera + controller input → phases/GameMode/GameState with the PlayerState/Pawn split → enemies + lanes + waves → Rituals + cast pipeline + runes → damage/debuffs (or GAS) → Sentinel kits → items/affixes/inventory/save → UI (CommonUI) → hub stations → multiplayer phases in the order in Section 5 → Steam.
