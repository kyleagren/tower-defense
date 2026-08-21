# [Working Title] — Game Design Document

This is a reference document: what the design currently is. For the reasoning behind each decision and a record of rejected alternatives, see the companion design journal (README.md).

## 1. High Concept

A 3D, low-poly stylized tower defense game combining:

* Dungeon Defenders: the player has direct combat presence in the fight, cannot solo-carry the defense, and picks from a roster of fixed-kit characters.
* Path of Exile: towers fire skills built from a base skill plus stacked support "runes" — composition over inheritance, deep build expression.

Single-player is built first. Invite-a-friend co-op is a hard requirement of the finished game, architected for from day one.

## 2. Core Gameplay Loop

Between waves (prep):

* The player, as "the architect" (a mage-styled, non-combat role), places towers within a fixed capacity budget and allocates gem/rune modifiers to them.
* The architect pre-crafts up to 3 specific "inscribed tokens" — fully-defined towers ready to deploy — anticipating likely weak points in the upcoming wave. Tokens can be recrafted between waves as the player learns more.

During a wave (combat):

* The player controls a separate fighting character with their own health, dealing direct damage and reacting to threats.
* The player can reactively place any banked tokens wherever needed — a universal capability, available to any fighting character.
* Enemies advance down multiple lanes toward a shared crystal, which they physically attack and can destroy over time if left unchecked.

## 3. Match Structure

* Map: fixed-path (not open-grid pathfinding), with multiple lanes converging on one or more shared objectives.
* Loss condition: the defended objective is the mage himself, sheltering in a force field/shell — enemies that reach it attack him directly, and the level ends when his health hits zero. (Flavor leaning resolved away from a separate DD-style crystal prop; see the design journal Section 1a for the reasoning — it also explains why the Architect isn't part of the fighting roster, Section 4.) Losing the shell ends the run. Enemies aren't assigned to a specific objective — they threaten whichever is closest. Harder maps may feature multiple objective points, spreading the same fixed capacity across more simultaneous points of failure; how one mage covers more than one point at once on those maps is still undecided (see Section 13).
* Wave count: varies by map, increasing on later/harder maps.
* Within-match escalation: enemy complexity increases over the course of a match (simple ground threats early, more varied/aerial threats later). Tower capacity is a fixed ceiling for the whole match — it does not grow over waves. Pacing comes from the economy: the player could technically afford full capacity immediately, but won't have earned enough currency yet.
* Two difficulty vectors:
  1. Campaign progression (map-to-map) — resolved via map/composition changes: richer enemy mixes, multiple crystals, more complex layouts as the player advances.
  2. Difficulty setting (replaying the same map at Easy/Medium/Hard) — still being worked out. See Section 13.
* Player awareness: a minimap shows monster positions and which lane/spawn point enemies are coming from.

## 3a. Player-Character / Meta Progression (difficulty-setting scaling)

To address difficulty-setting scaling without reintroducing per-tower power growth, the architect (mage) gains persistent XP that unlocks a global buff slot — a passive modifier applied to all towers.

* Guardrail: this progression must never let the player's overall power outstrip the towers' — towers stay the dominant source of the defense's strength regardless of meta-progression.
* Anti-grinding safeguard: meaningful XP gains are tied to difficulty cleared, not matches played — mirrors the gem-tier system's difficulty-gated access. Prevents farming easy content into disproportionate power.
* Buff variety, not a flat stat: multiple distinct buff types to choose from (tower range, cooldown recovery, attack speed, etc.) rather than one universal "+power" number — different types suit different builds (attack speed for combo-heavy defenses, range for spread-out layouts, cooldown recovery for CC-wall setups), making the choice a real build-identity decision. No single type should be objectively best regardless of build.
* Mechanism: talisman drops with a randomized, tier-equal stat budget. Each difficulty level drops talismans with the same total stat value for that tier, spread across stats — e.g. Level 3: +30 to all stats plus a randomized large bonus (~90) to one specific stat; Level 4: +40 to all, ~100 to one stat. Which stat gets the bonus varies per drop, so no talisman within a tier is objectively best — only best for a given build. The real decision is across tiers: a well-rolled lower-tier talisman can legitimately outclass an unrolled higher-tier one for a specific need. One talisman is equipped at a time.
* Open: exact number of equippable slots (leaning one or two).
* Rewards scale with performance, not just clearing: how well a difficulty was cleared (crystal health remaining, waves survived cleanly, time taken) affects reward quality/quantity, not just whether it was cleared at all. Exact conditions/magnitudes TBD.

## 4. Player & Characters

* The player deals direct damage and has their own health, but cannot single-handedly hold the defense — towers are the backbone.
* Death: temporary respawn with a cooldown. An ally can revive faster than waiting. If not revived in time, the player can spend match-currency to skip the wait — one good confirmed sink for excess currency (Section 8), though not the only one needed.
* The Architect (non-combat): a distinct role, not part of the fighting roster. Handles all tower placement and rune ("inscribing") between waves, and pre-crafts reactive tokens. Does not fight — in fiction, this is because he's sustaining the shell that is the level's loss condition (Section 3), not idle between waves.
* Fighting character roster (initial sketch):
  * Tank — can taunt/aggro enemies away from weak points in the defense.
  * Assassin — stealths in, delivers burst damage to tankier enemies.
  * Sniper — ranged, sustained damage (differentiation from Assassin still being worked out).
* Mid-wave token placement is universal across all fighting characters — no character owns separate defenses (deliberately not the Dungeon Defenders model).

## 5. Tower & Modifier ("Gem") System

* A base skill (e.g. Fireball) produces a data definition (damage, speed, projectile count, on-hit behavior). Modifiers ("runes") transform that definition and/or hook into its events.
* Tag system: skills carry tags (Projectile, AoE, Duration, Trigger, elemental types, etc.); runes can require or add tags. This is the backbone that lets a rune apply generically ("anything with tag X") instead of needing bespoke logic per skill.
  * Elemental reactions (Genshin "swirl"-style): a rune can check for an existing elemental tag on a target and trigger a bonus effect when a second element is applied.
  * Trigger-chaining (PoE "Cast on X"-style): one skill's event (on-activate, on-break) can fire a second, separate skill — supports both a formal explicit link and placement-based emergent combos.
* Gems/runes: available permanently at a low tier; higher tiers are found as drops and allocated across a limited number of slots — a real "which tower gets my best gem" decision. Towers do not level and do not scale in power within a match.
* Targeting: base skills have a natural default target priority (a fireball leans toward ground targets; a dedicated anti-air skill exists). A rune can override this for a specific tower. Player intervention is not relied on as a targeting fallback, since multiple lanes mean the player can only be in one place at a time.
* Base skill roster (initial sketch): Fireball, Lightning Bolt, Meteor, a wind-based skill (elemental-reaction potential), Ice (leans crowd-control over raw damage). Example CC rune: a brief (~0.5s) immobilize on hit.
* Player-buff towers: a non-damage base skill category (e.g. a "Buff Totem") that buffs the player character instead of attacking enemies. Lets players choose to lean into a "main character" playstyle by allocating capacity toward personal buffs instead of pure damage — a build choice, not a fixed role. Doesn't violate the "player must never outstrip towers" rule (Section 3a) since it costs the same finite capacity as any other tower, not a source of power on top of it. Moddable by runes like any other tower. Open: whether the buff is range-based (must stay near the tower) or global.

## 6. Battlefield Shaping / Crowd Control

* No separate destructible "blockade" objects — CC spells (e.g. an ice wall) cover the "corral monsters" role via temporary duration-based effects rather than persistent HP objects.
* Ice wall's exact mechanic is still being finalized between three directions (may ship more than one): a wall that regrows after being broken (sustained pressure), a proximity trap that spawns under the triggering enemy (single-target punish), or a trigger-chain version designed to cluster enemies for a second, delayed spell to hit.
* Cooldown model for wall-type spells: resets on break/trigger, or after a max duration — whichever comes first.

## 7. Towers Can Be Destroyed

* The common case when a tower takes enough damage is disabled, not destroyed — implying a repair mechanic (specifics TBD).
* In the rare case of true destruction, attached runes return to the player's inventory rather than being lost — this protects the player's scarce, precious gem investments.
* A specific enemy archetype (the "Sapper," see Section 9) is the one that meaningfully threatens towers, rather than any enemy incidentally being able to — the player can see this threat coming and choose to prioritize it.

## 8. Economy

* Two separate currencies:
  1. Match-only build resource ("mana," working name) — spent on placing towers/tokens, resets each match, accrues passively over time (not via active pickup, to avoid visual clutter during large multi-kill moments). Also spendable to skip the player-death respawn timer (Section 4) — one confirmed sink, but more are still needed once capacity is full (see Section 13).
  2. Progression currency (gold) — earned from completing missions, persists between matches, used for meta-progression (e.g. unlocking access to higher gem tiers). **Decided: gold is also spent at a shopkeeper/"spell table" crafting station — put an item in, randomly remove a mod or add a mod. This is a smaller-scope, non-deterministic mechanic, distinct from the deterministic PoE-style orb crafting vetoed in Section 10 — it's the adopted answer to "items should be interesting because the player can act on them," and it also serves as a second, always-available currency sink alongside the death-respawn skip.**
* Income tower: fires a "spirit" that an enemy absorbs; the player must personally kill that specific enemy within a time limit to cash in bonus income. The carrying enemy is immune to tower damage. Self-limiting via player attention, not a hard cap.
* Co-op: capacity and match-currency are both shared between players during a match; any leftover currency is split evenly at the end.

## 9. Enemy Design

Philosophy: enemies should create trade-offs, not gates. Prefer soft pressure (tanky/slow vs. fast/fragile, grouping behavior, timed bursts, partial resistances) over hard immunities or required counters. No elemental resistances on enemies — elemental typing exists purely as a player-facing build-crafting axis.

Roster (initial sketch): Fodder, Ranged Attacker, Tank, Flyer, Sapper (threatens towers specifically), Assassin-type.

Difficulty tiers should differentiate primarily via enemy composition and behavior (which enemy types/combinations appear, wave timing) rather than flat stat multipliers, to avoid higher difficulty simply feeling like slower, tankier versions of the same fights.

## 10. Itemization & Meta-Progression

* Avoid flat "+X% tower damage" stat-stick itemization.
* Directions: capacity-affecting items, extra gem-slot items, character-specific kit-growing unlocks, playstyle-tied set bonuses. Tiered gem drops are the core progression loop.
* Multiple difficulties, difficulty-gated unlockable items, possible time limits on harder difficulties, multiple maps.
* Run structure: mission-select and return-to-hub (not roguelike). A roguelike mode is a possible future alternate mode, not part of the initial build. A survival/endless mode is similarly kept in reserve — likely cheap to add later by extending the existing wave-generation system indefinitely.
* Considering ARPG-style loot (talismans, tower-specific uniques) — not yet finalized. Deliberately avoiding PoE-style *deterministic* crafting (named orbs, precise effects) as too much scope for a solo project — this does not rule out item modification generally; see the decided spell-table crafting mechanic under Section 8.
* Tower-specific unique items (changing a tower's fundamental behavior, e.g. an extra projectile beyond what a rune grants) are being considered, paired with a per-tower-type capacity cap (with items able to raise it) to prevent any single powerful unique from letting a player build their entire defense around one tower — preserves build diversity regardless of how strong an individual unique turns out to be.

## 11. Multiplayer (Co-op)

* Hard requirement. Invite-a-friend only — no public matchmaking.
* Built after single-player, but architected for from day one: simulation/state logic kept separate from rendering/input, a serializable/replicable-friendly data model for the skill/modifier system, and authority questions (e.g. who "owns" a tower) decided as placeholders even before multiplayer is implemented.
* Prep phase: both players act as independent architects simultaneously ("two mages"), building/prepping within the shared capacity and currency pool. Each runs to a physical console to lock in their fighting character when ready — duplicate character picks are allowed.
* Pause: available in single-player, not in co-op.

## 12. Art & Technical Direction

* Art: 3D, low-poly, stylized — not realistic, not voxel (voxel rejected: immature tooling, and better suited to mutable terrain this game doesn't need).
* Camera: Dungeon Defenders-style action camera (behind/above the character), not a fixed top-down view.
* Engine: Godot (final). Chosen over Unity primarily on trust grounds (Unity's 2023 pricing controversy), a deliberate tradeoff accepting Godot's less-mature 3D/multiplayer tooling for a 3D, co-op-required project. See design journal Section 7a for the full comparison and reasoning.

## 13. Open Design Questions

These are genuine unresolved gaps, not yet answered:

* Buff slot count (Section 3a): how many meta-progression buff slots can be active at once — one/two leaning, not finalized.
* Player-buff tower range: range-based or global buff application (Section 5) — leaning range-based, not finalized.
* Excess match-currency sink: death-respawn spending (Section 4) plus the spell-table crafting sink (Section 8, decided) likely resolve this — worth confirming with playtesting rather than treating as fully closed.
* Ice wall's final mechanic (Section 6) — which direction(s) ship.
* Sniper vs. Assassin differentiation (Section 4).
* Repair mechanic specifics for disabled towers (Section 7) — trigger, cost, duration.
* Full scope of the elemental tag system — which elements, how conversion works.
* Exact lane count and its interaction with the economy.
* Specific low-poly art style/visual identity.
* How a single mage covers more than one objective point on multi-crystal/multi-shell maps — one idea floated (spectral projection to multiple locations at once), not decided.

For the reasoning behind every decision above, and a record of what was tried and rejected along the way, see the design journal (README.md).
