# Runewarder — Design Reference

*(Working title — open to change.)*

Current state of the design, plus just enough "why" to avoid re-litigating settled decisions. Not a full history — see git log for that. Update this doc as decisions change; it's meant to stay short enough to actually read.

## 1. High Concept

3D, low-poly stylized tower defense combining:

* **Dungeon Defenders** — the player has direct combat presence, can't solo-carry the defense, and picks from a roster of fixed-kit characters.
* **Path of Exile** — towers cast a base skill ("Spell") plus stacked support "Runes." Composition over inheritance, deep build expression.

Single-player is built first. Invite-a-friend co-op is a hard requirement of the finished game, architected for from day one (simulation/rendering kept separate, data model kept serializable) rather than bolted on later.

**Engine:** Godot 4, C#. Chosen over Unity (trust concerns after Unity's 2023 pricing controversy) and UE5 (heavier engine, steeper learning curve for a lightweight stylized project). C# over GDScript for the simulation-heavy load (many enemies/projectiles/modifiers updating every frame).

## 2. Match Structure & Core Loop

* Fixed-path maps (not open-grid pathfinding), multiple lanes converging on one or more defendable objectives. Lane count is per-map, not a fixed game-wide number — same as most TDs.
* **Prep phase:** the Architect (mage, non-combat role) places towers within a fixed capacity budget and allocates runes. He also pre-crafts up to 3 "inscribed tokens" — fully-defined towers ready to deploy reactively — recraftable between waves.
* **Wave phase:** the player controls a fighting character with their own health. Tokens can be placed reactively mid-wave; towers otherwise can't be built or repositioned mid-wave (see Section 4 on repositioning). Enemies advance down lanes toward whichever defendable objective is closest and attack it continuously until it falls or the wave ends.
* **Objectives:** the primary one is the mage's own shell/force field — enemies attack it directly, and it falling ends the run. Harder maps add secondary objectives, **Energy Harvesters**, which siphon energy from slain enemies (mage-powered thematically, but function identically to the main shell, minus having a mage inside). This is how one mage "covers" more than one objective — the harvesters don't need his direct presence.
* Tower capacity is a fixed ceiling for the whole match — it doesn't grow over waves. Pacing comes from the economy (currency accrues over time), not capacity growth.
* Difficulty has two vectors: campaign progression (map-to-map, resolved via composition — richer enemy mixes, more objectives, more complex layouts) and difficulty *setting* (replaying a map at Easy/Medium/Hard, handled by enemy composition/timing — see Section 8 — not flat stat multipliers).
* Minimap shows monster positions, which lane/spawn point they're from, and disabled-tower status.

## 3. Player-Character & Meta-Progression

* The Architect gains persistent XP, tied to difficulty cleared (not matches played, to avoid grinding easy content into disproportionate power). XP unlocks a talisman slot — a passive, global buff applied to all towers. **One slot for now.**
* **Spell unlocks (decided):** new base Spells unlock as the Architect levels up on the same persistent XP track, rather than via per-map completion — keeps spell pacing tied to overall progression instead of needing its own separate per-map tracking system.
* Talismans drop with a randomized, tier-equal stat budget (e.g. Level 3: +30 to all stats plus a random ~90 bonus to one stat) — no talisman within a tier is objectively best, only best for a given build.
* Guardrail: this must never let the player's overall power outstrip the towers' — towers stay the dominant source of defensive strength regardless of meta-progression.
* Rewards scale with performance (objective health remaining, waves survived cleanly, time taken), not just clearing — exact magnitudes TBD.

## 4. Player & Characters

* The player deals direct damage and has their own health, but can't solo-carry the defense.
* Death: temporary respawn with a cooldown; an ally can revive faster, or the player can spend match-currency to skip the wait.
* Fixed-kit characters, not gem-built — keeps the "too much freedom → one optimal build" problem contained to towers.
* **Roster:** implementing **Soldier** first, to get a vertical slice working end to end. Tank, Assassin, and Sniper come later (Sniper/Assassin differentiation not yet designed — will exist, just undecided how).
* The player can **repair** towers mid-match (funnel mana into a tower to restore it) but cannot **reposition** them once placed — the token system covers "I need a tower somewhere new, right now" instead.
* The Architect is a distinct, non-combat role — places/preps towers and pre-crafts tokens, then isn't part of the fighting roster (in-fiction: he's sustaining the shell).
* The player's own power system (health/damage/progression) is completely separate from the tower Rune system — not yet designed, but decided to share no mechanics with Runes.
* Character set bonuses: a planned but far-future itemization idea.

## 5. Tower & Rune System

* A base skill ("Spell," e.g. Fireball) is an immutable data definition (damage, speed, tags). Modifiers ("Runes") transform that definition and/or hook into its events. Composition, not subclassing.
* Tags (Projectile, AoE, Duration, Trigger, elemental types) let a Rune apply generically to "anything with tag X" instead of bespoke per-skill logic. Elemental tags exist for build-crafting depth, not enemy resistances — enemies have none, by design (Section 9).
* Elements confirmed so far: **Fire, Lightning, Cold, Wind.** More may be added later.
* Elemental reactions (Genshin "swirl"-style — bonus effect when a second element hits an already-elemented target) and trigger-chaining (PoE "Cast on X"-style — one skill's on-activate/on-break event fires a second skill) are both planned, reusing the same event-hook pattern.
* Runes are permanently available at low tier; higher tiers drop and are allocated across limited slots per tower. Towers don't level or scale in power within a match. Duplicate rune types on one Spell are disallowed (enforced in code).
* Targeting: base skills have a natural default target priority (a Fireball leans toward ground targets); a dedicated targeting rune can override it. Player presence is never a targeting fallback — multiple lanes mean the player can only be in one place at a time.
* **Player interaction model (decided):** tower placement and aiming use a center-screen reticle + raycast into the world, not a screen-space mouse cursor. Keeps mouse and controller input unified under one system (no separate cursor-based UI to build for controller players) — full controller support is a requirement, not an afterthought.
* Some towers will eventually need line-of-sight raycasting — not implemented yet (currently pure distance checks, so a tower can shoot through walls).
* Player-buff towers (e.g. a "Buff Totem") buff the player instead of attacking — **range-based** (player must stand near it to benefit), costs capacity like any other tower.

## 6. Battlefield Shaping / Crowd Control

* **Ice wall (decided):** a destructible blockade — physically blocks a lane, and enemies that reach it attack it directly (it has HP) rather than routing around. Not a *persistent* blockade though: it also expires passively after a duration cap if never broken, so it can't wall off a lane forever if nothing engages it. Either way (broken by enemies or expired), a regrow cooldown starts, after which the wall reforms.
* A rune that makes the wall explode on break/trigger is planned.

## 7. Towers: Damage & Repair

* Common case when a tower takes damage: disabled, not destroyed. The active fighting character repairs it by funneling mana into it.
* Rare case (true destruction): attached runes return to the player's inventory, never lost.
* The Sapper enemy archetype is the one that meaningfully threatens towers — a visible, prioritizable threat, not universal collateral risk.
* Disabled towers show on the minimap.
* **Repair/Destroy interaction (decided):** both reuse the same reticle + raycast targeting as tower placement (Section 5), capped to roughly the same interaction range — not usable from across the map. When a tower is in view, a contextual world-space prompt appears near it ("Repair" / "Destroy"). Repair is a single press — frequent and low-stakes, matching its role as the common case above. Destroy is hold-to-confirm (~1s) with a progress indicator on that same prompt, rather than a separate confirmation dialog, so it doesn't break gameplay flow — cancels if released early or if the raycast loses the tower. Chosen over a Dungeon-Defenders-style persistent "mode" toggle specifically to avoid mode-confusion risk on the harder-to-reverse action.

## 8. Economy

* Two currencies: match-only **mana** (spent on towers/tokens, accrues passively over time, also spends to skip the death-respawn wait) and persistent **gold** (earned per mission, spent on meta-progression and at a spell-table crafting station — put an item in, randomly add/remove a mod).
* Income tower: fires a "spirit" that an enemy absorbs; the player must personally kill that enemy in time to cash in bonus income (the carrier is immune to tower damage). Self-limiting via player attention. **One spirit in flight at a time.**
* Co-op: capacity and currency both shared during a match; leftover currency splits evenly at the end.

## 9. Enemy Design

Philosophy: trade-offs, not gates. Soft pressure (tanky/slow vs. fast/fragile, grouping behavior, timed bursts, partial resistances) over hard immunities. No elemental resistances on enemies — elemental typing is a player-facing build axis only.

Roster (initial sketch): Fodder, Ranged Attacker, Tank, Flyer, Sapper (threatens towers), Assassin-type.

Difficulty tiers differentiate via composition and timing, not flat stat multipliers — avoids the "bullet-sponge" problem of higher difficulty just feeling slower.

* **Leash range (decided):** if the player character comes within a small leash range of an enemy, it breaks off its lane advance to chase and attack the player instead. If the player gets far enough away, the enemy gives up and returns to its lane route. Same "break from default movement, act, resume" shape as the ice wall (Section 6) — worth one shared behavior pattern rather than bespoke logic per case. Movement-architecture implication: chasing/returning covers arbitrary positions off the lane, which is the one case that still needs real pathfinding even if lane-following itself moves to fixed waypoints — see Section 15.

## 10. Itemization & Equipment

* Character equipment: 6 slots (Weapon, Helmet, Chest, Legs, Boots, Gloves), each holding an Item with tiered Modifiers (T1 strongest/rarest → T5 weakest/commonest, gated by item rarity Mythic→Common).
* The Architect and fighting characters have **separate** equipment/mod pools.
* Item mods are a fully separate system from Runes (no shared class/slot mechanics) — but reuse the same `Apply(context)` shape internally, so the same infrastructure covers both.
* Damage resolves as `Base * (1 + IncreasedPercent) * MoreMultiplier` (PoE-shaped) specifically so equipment mods have a clean bucket to plug into later without another pipeline rework.
* Avoid flat "+X% damage" as the *only* kind of mod — items should let players "break the rules" (e.g. a unique that collapses a spell's projectiles into one, damage summed), not just tweak numbers.
* Not yet designed: mod pool contents, weighting, drop sourcing.

## 11. Multiplayer (Co-op)

* Hard requirement, invite-a-friend only (no public matchmaking). Built after single-player but architected for from day one — see Section 14 for the concrete implications.
* Prep phase: both players act as independent architects simultaneously, sharing one capacity/currency pool.
* Pause allowed in single-player only.

## 12. Development Notes

* The Rune/Tag/Spell composition system was spike-tested before anything else was built, since it was the personal go/no-go condition for the project — passed (composes cleanly, proven in a working test scene). Production is underway.
* Content (skills, runes, enemies, items) is data-driven via Godot `Resource` assets, not hardcoded, so content iteration doesn't slow down as the roster grows.

## 13. Class/Object Architecture

**Cast pipeline, at a glance:**

1. **`Spell`** (e.g. `FireballSpell`) — the immutable definition. Base stats and tags for one skill. Lives once, shared by every tower using it.
2. **`SpellInstance`** — one per placed tower. Wraps a reference to a shared `Spell` plus that tower's own attached `Rune` list and cast/cooldown state. This is "the tower's loadout," not a projectile.
3. **`SpellInstance.Resolve()`** — on cast, applies every attached `Rune` on top of the `Spell`'s base values and produces a **`SpellCastContext`** (or the `ProjectileCastContext` subtype) — a plain data snapshot of the final resolved stats for this one cast. Transient; discarded after the cast reads it.
4. **`ProjectileCastEffect.Cast(context, spawnPoint)`** — reads the resolved context and spawns world objects to match it (e.g. `context.ProjectileCount` separate projectiles for a Nova cast).
5. **`Projectile`** — the actual flying object. Fully independent once spawned: its own damage, radius, pierce count, hit-tracking. Doesn't reference the `SpellInstance` or its siblings.
6. **`ProjectileHitEffect`** (e.g. `ExplodeOnHitEffect`) — reacts when a `Projectile` hits something. Also the right extension point for future on-hit behavior (e.g. splitting into new projectiles), since each `Projectile` already carries its own independent state by this point.

`Spell` → `SpellInstance` (per tower) → resolve into a `Context` (per cast) → `CastEffect` spawns `Projectile`s (per world object) → `HitEffect` reacts (per hit). Each arrow is a hand-off to something new and independent — nothing downstream reaches back upstream.

**Class notes:**

* **Spell** — the immutable definition (Resource). Since many towers reference the same Spell asset in memory, no mutable state (like attached runes) lives on Spell itself — that's what SpellInstance is for. Rule for where a stat's base value lives: if every spell needs it regardless of delivery type (Range, Cooldown, Damage, KnockbackForce), declare it once on the abstract Spell base; if it only makes sense for one delivery type (ProjectileCount, Radius, PierceCount), it stays on the concrete subclass. The resolved-context fields follow the same split.
* **CastEffect family** — `ProjectileEffect` today; `ZoneEffect`, `WallEffect`/`TrapEffect`, possibly `BeamEffect`/`AuraEffect` later. A Spell's tags determine which CastEffect resolves it.
* **Rune** — its own class: tier, required/granted tags, the data transformation it applies, any event hooks it registers.
* **TriggerBinding** — pairs a trigger condition (on-activate, on-break, on-hit) with a Spell to cast in response, making trigger-chaining inspectable data instead of hardcoded per-effect logic.
* **StatusEffect** — temporary state living on an Enemy (elemental tags for swirl reactions, CC immobilize, income-spirit-carrier flag). An Enemy holds a list of these rather than special-casing each into the Enemy class.
* **Tower** — HAS-A SpellInstance (composition, not inheritance). Tower owns position, capacity cost, HP/disabled state; SpellInstance owns the attached runes. Keeps "runes return to inventory on destruction" clean, and lets the Spell definition be shared safely across many towers.
* **PlayableCharacter** — thin shared base (position, input); fighting characters get a `CombatComponent`, the Architect gets a `BuildComponent` — composition over a deep inheritance chain.
* **Enemy** — mostly one generic, data-driven class (stats from a data asset) rather than a subclass per type, since most of the roster differs only by stats/flags. Reserve real subclassing for enemies with genuinely unique logic (Sapper, income-spirit carrier).
* **DefendableObjective** — generic name for the shell/harvester loss-condition object, so flavor can change without renaming code. Both the main shell and Energy Harvesters (Section 2) are this same type.
* **Item** (unifies Talisman and Unique) — one equippable-to-character class covering both meta-progression buffs and tower-behavior-changing uniques, rather than two parallel systems. Holds a list of **Modifier**s (one class with a `SlotType` enum — Prefix/Suffix/Unique — rather than three subclasses, since prefix/suffix are behaviorally identical at runtime). Item mods deliberately don't inherit Rune's constraints (e.g. Rune's one-per-type rejection is wrong for stackable itemization).
* **WaveDefinition, MapDefinition, Lane, EconomyManager** — sensible as initially sketched, no changes.

## 14. Multiplayer Architecture Implications

* **EconomyManager must be host-authoritative from day one.** Both mages spend from one shared pool simultaneously (Section 11); if clients each mutated a local copy, simultaneous spends would desync. Pattern: one source of truth on the host; a client's spend is a request, the host validates and broadcasts the confirmed result.
* Every state-changing action needs the same request/validate/broadcast pattern — placing a Tower, attaching a Rune, casting a Spell, triggering a token, repairing a tower. Build this in from each system's first implementation rather than retrofitting later.
* Enemy simulation is host-authoritative and single-sourced — clients receive state and interpolate, rather than each running their own simulation (which would drift out of sync from timing/floating-point differences).
* Spell/Rune data syncs as small IDs referencing a static data table, not fully serialized objects — both clients already have the transformation logic locally.
* Player death/revive needs its own explicit synced state (respawn timer, "being revived" status).
* Godot 4's high-level multiplayer API (`@rpc` annotations, multiplayer authority settings) covers these patterns directly — this is applying existing engine tools to the classes above, not building plumbing from scratch.

## 15. Open Questions

* Sniper vs. Assassin differentiation — both will ship, exact difference not yet designed.
* Full scope of the elemental tag system — more elements beyond Fire/Lightning/Cold/Wind, and how conversion runes work.
* Equipment mod pool: contents, weighting, drop sourcing.
* Specific low-poly art style/visual identity — not urgent yet, will matter for silhouette readability once designs are more concrete.
* Wave-end safety net: a wave should auto-end after a timeout even if enemies remain (e.g. one stuck on the navmesh) — not implemented yet.
* Non-lethal knockback: `KnockbackForce` currently only applies via the on-death ragdoll launch. A hit that knocks back *without* killing needs a different mechanism (a living enemy is `CharacterBody3D`-driven, not free ragdoll physics) — not designed yet.
* Ritual line of sight and non-horizontal projectile aiming: both confirmed as needed eventually (Section 5), neither implemented yet.
* Enemy movement architecture: currently full `NavigationAgent3D`/navmesh per enemy. Fixed waypoints (matching the "fixed-path maps" decision in Section 2) would be cheaper for plain lane-following, but leash-chase/return (Section 9) needs real pathfinding for the arbitrary off-lane positions a chase can end at. Leading candidate is a hybrid — waypoints by default, a one-off path query only for enemies actively returning from a leash — but not committed to yet.
* **Rune/spell iconography — undecided between two directions:**
  * *Procedural sigil generation:* 30 anchor points evenly spaced on a circle (26 letters + 4, one per rune slot). A spell or rune's name maps its letters to points and connects them in sequence, generating a unique glyph without hand-drawing one per rune. When a rune is socketed, its glyph is anchored through its slot's dedicated marker point and overlaid on the spell's own glyph — so a fully-built tower's sigil is a compound of the spell's glyph plus each attached rune's glyph layered together (chosen over an alternative where each rune reroutes a single shared path, since overlay is order-independent and each rune's glyph is cacheable on its own). Socketing a rune would animate in — a stroke-reveal "growing outward" effect via `Line2D` + `Tween`, segments staggered slightly rather than growing in lockstep, easing into place at the anchor point.
  * *Functional iconography:* instead of (or blended with) letter-based glyphs, hand-draw a small fixed set of primitive strokes tied to mechanical tags/categories (a projectile-pattern primitive, an AoE-radius primitive, a duration/DoT primitive, etc.) and procedurally compose those per rune based on which tags it touches — same overlay/animation system, but the player can learn to read a tower's build at a glance instead of memorizing arbitrary shapes. Trades some of the procedural version's novelty for legibility, without reintroducing a "hand-drawn icon per rune" art bottleneck that would work against Section 12's data-driven/scalable-content principle (primitives are reused across every rune sharing a tag, not authored per rune).
  * Not needed for the vertical slice — revisit once core gameplay is playable.
* Navmesh re-baking on tower placement (surfaced while planning tower placement): placed Rituals now have solid collision, but `NavigationBaker.cs` only bakes once at level load — enemies won't route around anything placed afterward. Idea, not yet built: re-bake once at the end of a build phase, and once whenever a reactive token is used mid-wave, rather than baking on every individual placement (expensive) or never (broken pathing).

## 16. Vertical Slice Roadmap

Order of operations to get a first playable loop working end to end. Update as steps land so this doesn't get lost again.

1. ~~Level~~ / ~~Enemy~~ / ~~Objective~~ / ~~Tower (`Ritual`) targets and casts at nearest enemy~~ — **done.**
2. **Architect movement** — a controllable character in the level during prep, no combat, just walk-around + camera.
3. **Tower placement flow** — Architect selects a tower/spell and places a `Ritual` instance in the world, gated by a hardcoded capacity budget (mana economy comes later). UI flow specced in Section 17.
4. **Minimal economy** — just enough of `EconomyManager` (capacity used/max, maybe mana accrual) to make placement meaningful (Section 8).
5. **Prep → Wave transition** — a "Start Wave" action that hooks into `WaveController`, disables the Architect, hands control to the Soldier.
6. **Soldier character** — basic movement + one attack that damages enemies, closing the "player deals direct damage" requirement (Section 4).
7. **Wave-end handling** — detect wave clear (or objective destroyed) and either loop back to prep for the next wave or end the match.
8. **Win/lose state** — objective falls = loss, waves cleared = win. Bare-minimum feedback (console print or scene reload) is enough for a slice — no UI polish needed yet.

## 17. UI: Tower Construction & Deployment

`Q` drives two distinct flows that behave nothing alike — designed as two separate systems rather than one flexible menu, since they sit under opposite time pressure (prep vs. wave phase, Section 2).

* **Architect, prep phase — construction menu (decided):** `Q` opens a full-screen overlay: tag-filterable grid of available spells → select one → rune socket screen (4 sockets, one per corner, for now) → confirm → placement (reticle + raycast, Section 5). No time pressure, so this can be a rich browsing experience.
  * Socket state persists across backing out and returning to the socket screen for the same spell (an accidental cancel shouldn't lose 3 already-placed runes) — sockets only clear if the underlying spell selection itself changes.
  * A rune already socketed elsewhere on the same spell shows greyed out, sorted to the back of the list, with a short message explaining why — makes the existing one-per-type rule (Section 5) visible instead of silently rejecting the drop.
  * A per-rune "unsocket" action, plus a separate "clear all runes" bind gated behind a confirmation prompt (guards against wiping a near-finished build by accident).
  * Short rune description text (label + one-line effect, e.g. "+8 Proj — projectiles fire in a circle") lives as data on the `Rune` resource itself, not hardcoded per-rune in UI code.
  * Renders as a blocking overlay over the opening player's view. Since Section 11 has both co-op players able to build simultaneously, a true whole-game pause won't work — overlay likely needs to be scoped per-player (split-screen or similar); exact mechanism still open.
* **Any player, mid-wave — token deployment (decided):** `Q` opens an instant radial/hotbar pick among the player's up to 3 pre-crafted tokens (Section 2) — no browsing, no rune sockets, since a token's loadout was already locked in during prep. Optimized for speed under pressure, not depth.
* **Favorites/loadouts vs. tokens — distinct concepts:** a favorite is a saved spell+rune preset that speeds up normal construction (skip re-picking a common combination). A token is the separate, already-decided reactive mid-wave deployable. Same underlying data shape (spell + rune set), different purpose and moment of use — keep the vocabulary distinct so the two don't get conflated in implementation.
* **Navigation (decided):** built gamepad-first, not mouse-first-then-retrofit — discrete grid focus navigation (Godot's `Control`/`GridContainer` focus-neighbor system) rather than hover-based, with tag filters on their own input path (e.g. shoulder buttons cycling filters) so they don't collide with grid navigation.
