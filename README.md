# Runewarder — Design Reference

_(Working title — open to change.)_

What the game currently is, and what's still undecided. Reasoning appears only where a decision is still live or where the obvious approach is wrong for a non-obvious reason. Not a history — that's what git is for.

---

## 1. High Concept

3D, low-poly stylized tower defense combining:

- **Dungeon Defenders** — the player has direct combat presence, can't solo-carry the defense, and picks from a roster of fixed-kit characters.
- **Path of Exile** — towers cast a base skill ("Spell") plus stacked support "Runes." Composition over inheritance, deep build expression.

**Theme.** The Architect is a time-traveler. Years of hopping between eras left cracks in reality behind him, and those fractures are letting monsters — and some imprisoned boss-tier figures whose bonds have weakened — loose in the periods he damaged. The campaign is him sealing each fracture. The Sentinel roster is drawn from people he met along the way, which is what lets it keep growing without needing a shared origin. The vertical slice stays in a single era; multi-era content (distinct rosters and biomes per period, not reskins) is the long-term vision.

**Engine:** Godot 4, C#. C# over GDScript for the simulation-heavy load (many enemies, projectiles and modifiers updating every frame).

**Co-op is a hard requirement** of the finished game — invite-a-friend only, no public matchmaking. Single-player is built first, but the architecture assumes co-op from day one (see Section 7).

## 2. Core Loop & Economy

Fixed-path maps, multiple lanes converging on one or more defendable objectives. Lane count is per-map.

**Four phases**, all implemented:

- **Planning** — the level opens with the player controlling their Sentinel, free to walk around with no time pressure. Ends on the `start_wave` input. Exists so a future Prep-phase timer has a deliberate player action to start counting from instead of beginning the instant the level loads.
- **Build (Prep)** — the player becomes the Architect and places towers within a fixed capacity budget, allocates runes, and pre-crafts up to 3 tokens.
- **Combat (Wave)** — the player controls a Sentinel with their own health. Tokens deploy reactively; towers otherwise can't be built or repositioned. Enemies advance toward the nearest objective and attack it until it falls or the wave ends.
- **PostGame** — after clearing every wave, control returns to the Sentinel so the player can walk over and collect what dropped. Returning to the hub is the same `start_wave` input. A loss skips PostGame entirely and returns to the hub after a short delay.

**Objectives.** The primary one is the mage's own shell — enemies attack it directly and it falling ends the run. Harder maps add **Energy Harvesters**, which function identically minus having a mage inside; this is how one mage covers more than one objective.

Tower capacity is a fixed ceiling for the whole match. Pacing comes from the economy, not capacity growth.

**Difficulty** has two vectors — campaign progression (map to map) and difficulty setting (replaying at Easy/Medium/Hard). Both resolve through enemy composition and timing, never flat stat multipliers, to avoid higher difficulty just feeling like bullet sponges.

**Two currencies.** Match-only **mana** buys towers and tokens, accrues passively, and can be spent to skip the death-respawn wait. Persistent **gold** is earned per mission and spent at the hub's crafting table and vendor (Section 6).

**Income tower.** Fires a "spirit" that an enemy absorbs; the player must personally kill that carrier in time to collect bonus income, and the carrier is immune to tower damage. One spirit in flight at a time. It also doubles as the loot-goblin for the loot-timing direction below.

**Loot timing — built, though still a tentative design call.** Most loot never drops in real time. `ItemDropManager` rolls each kill's drop chances immediately but holds the results, then sprays the whole pile out of the objective's position at PostGame, ordered common-to-legendary and placed on a golden-angle spiral so items separate without needing physics. The objective is freed as the spray starts — the shield defended all level becomes the reward. A loss never fires the victory signal, so queued loot simply never spawns, extending "nothing to loot after a loss" to everything. This sidesteps the mid-combat "risk it or lose it" tension, the ground-clutter problem, and a future Build-timer squeeze on evaluating finds — at the cost of the moment-to-moment ARPG texture during combat, which is why it is worth revisiting if it does not feel good in play. The 40-slot inventory cap becomes a real triage decision at the payout moment rather than a slow background nuisance. **Not built:** the two intended live-drop exceptions — elite enemies and whichever enemy carries the income spirit — which would keep full lose-it-on-death stakes. Today every drop is queued.

Minimap shows monster positions, which lane they came from, and disabled-tower status.

Co-op shares capacity and currency during a match; leftover currency splits evenly at the end. Pause is single-player only.

## 3. Characters

The player deals direct damage and has their own health, but can't solo-carry the defense. Death is a temporary respawn on a cooldown — an ally can revive faster, or the player can spend mana to skip the wait.

Characters are **fixed-kit, not gem-built**, keeping the "too much freedom collapses into one optimal build" problem contained to towers. The fighting roster collectively goes by **Sentinel** (used in code too, e.g. `CharacterId`).

**Roster:** Soldier and Tank are implemented. Assassin and Templar come later — Assassin's kit isn't designed; Templar has one floated idea (something like PoE's Divine Ire, a channeled ramping-damage release). Sniper is shelved, not cancelled.

**Kit design principles.** Not every character needs the same four-slot template (CC + mobility + buff + panic button) — forcing that shape risks every character being the same abilities reflavored. A kit skipping CC isn't a gap that must be filled per-character; the roster compensates at a wider level. Four abilities isn't a hard requirement either; the goal was "more to do than Dungeon Defenders' two," not a fixed count. A good kit mixes genuinely new mechanics this game's systems make possible (reading and manipulating an enemy's Status) with recognizable genre tropes reskinned to fit.

**Reserved for undesigned characters**, so later kits don't collide with ideas already spoken for: **Detonate** (an elementalist popping all elemental Statuses in an area), **Taunt** (drawing aggro), and **self-duplication/clone** (fits an illusionist better than a disciplined soldier).

**Personal per-ability reactions** are a second axis alongside the shared Reaction system (Section 9). An ability can check an enemy's current Status and do something bespoke if it matches, without needing a slot in the shared combination map — and without following that system's rules at all. The shared system is universal and symmetric; a personal reaction is scoped to one ability.

### Soldier

Deliberately keeps one foot in traditional-shooter appeal — not every character will have a gun, so Soldier is the one giving that audience a reason to pick him. He's implied to be from a future era, which is the in-fiction justification for the tech-flavored parts of his kit.

- **Passive — dual-mode Fire/Cold rifle** with built-in elemental firing modes, plus a Rune socket in the rifle. This does *not* consume an ability slot; the four slots are four *active* abilities, and passives (this, ADS, headshot crits, Tank's block) are ordinary character features coded on the Sentinel rather than `Ability` instances. Soldier having a passive others lack is still a real balance concern, just one handled in the numbers rather than by spending a slot.
- **Headshot crits (built).** A hitscan shot whose line passes through an enemy's head hitbox (`Enemy.HeadCollision`, optional per enemy scene — Grunt/Marksman/Sapper have one, TrainingDummy doesn't) is a guaranteed critical hit. This is its own ray-vs-box test against the head hitbox's volume rather than trusting a raycast's closest-hit result: the head box sits mostly recessed inside the body capsule's silhouette, so it's rarely the physically closest shape even when a shot genuinely passes through it. Because crits land far more often this way, `Gun.BaseCritDamageBonus` is lower than the shared default — 125% total on a crit versus Tank's 150%.
- **Weak-Point Visor (built).** While active, exposes weak points on every enemy at once — chosen over a single-target lock for simplicity. Each enemy lights `WeakPointCount` of its pre-placed body-part markers for the duration: Head always, when that enemy has one wired, so there's a guaranteed high-value target; the rest drawn at random from body/arms/legs. A second press ends it early. Hit detection is the same geometric-proximity test as headshots — how close the shot's line passes to the marker, not a physics hit — and a hit stacks `Soldier.WeakPointDamageMultiplier` on top of whatever the crit roll already produced, so weak points and headshot crits do compound.
    - **Single-player only as written.** The active flag and marker count are static on `Enemy`, which is what lets an enemy spawning mid-duration expose its own markers on `_Ready`. With two Soldiers running the visor at once they share that one flag, so whichever ends first strips the markers for both. Fixing it means refcounting active visors per enemy rather than a single global on/off.
- **Health Link (built).** Activating arms the ability rather than firing it: the next two shots plant spikes instead of bullets, and the two spiked enemies then mirror each other's incoming damage for 7s. Not a shared HP pool — a pair of beefy enemies standing together effectively doubles incoming damage on both. Mirrored damage is **true damage** (`DamagePacket.IsTrueDamage`): it's a copy of damage already mitigated once on the way in, so it must never be mitigated again on the way out. It also can't crit, since no roll happens on the receiving end, and doesn't carry a Status across — the partner wasn't the one actually hit by the element.
    - **Arming captures the attack binding**, which is why `Ability.CapturesAttackInput` and `Sentinel.CanUseWeaponInput` exist. Backing out before the first spike lands is free; once a spike is in, the cast is committed and pays the full cooldown however it ends — a miss, both spikes in the same enemy, the first target dying before the pair closes, or the arming window timing out. Punishing a player for spiking something that was about to die is the point, so a free re-arm can't be allowed to dodge it.
    - **The mirror is a hook on `Ailment`, not ability state** — `OnHostDamaged`, fired from `Enemy.TakeDamage` after the health subtraction but before the death check, so a killing blow still mirrors. The mirrored hit passes `notifyAilments: false`, which is what stops it bouncing back through the partner's own half. A side effect worth knowing: that same guard means **links never chain** — damage reaching an enemy through a link stops there and isn't relayed onward to anything that enemy is separately linked to.
    - **The spike stands upright in a shoulder, not along the shot that planted it.** A spike aligned to the bullet points back at whoever fired it, which foreshortens to nothing from the one angle the shooter is guaranteed to be looking from. It parents to the enemy's `ShoulderAttachment` bone so it rides the walk cycle — **a new enemy type with that export left unwired gets its spike in the middle of its chest**, since the fallback is centre mass. Even upright, picking a marked enemy out of a live wave is hard; see the note on marker readability in Section 9.
    - **Each linked pair is two independent halves**, one on each enemy pointing at the other, rather than one shared object. The pair is symmetric by construction and neither half needs to survive the other's death. Duplicate links on the same pair are refused unless at least as strong as the existing one: equal refreshes the duration, weaker does nothing at all, so two players can't take turns re-casting a weak link to hold a strong one open forever.
- **Elemental-Sapping Nanites (not built).** Harvest Status/Ailments off enemies to fill a gauge; a full gauge unlocks a one-shot lane-clearing raygun blast. Self-limits by gauge-fill rate rather than a cooldown. The trigger (passive drip vs. an active send-out) is undecided and may end up a talent choice.
- **Considered, not slotted:** a rune-configured super-shot whose effect depends on the socketed rune, reading each Rune's own numbers directly rather than routing a gunshot through the spell pipeline. Would need a narrow new `Rune` hook, not a pipeline change.

### Tank

A melee bruiser built as the deliberate opposite of Soldier: higher max health, no ranged option at all. Move speed matches everyone else — a baseline penalty was tried and reverted because the Architect-to-Combat handoff already reads as a sudden slowdown.

- **Sword swing** (`attack`) sweeps the sword in an actual orbit around the body's vertical axis, not a local rotation, alternating direction each swing so chained attacks read as one continuous pendulum. The blade points radially outward to maximize reach and flattens parallel to the ground for the swing. A grace window after each swing — independent of attack speed, so faster attacks don't shrink it — lets a follow-up continue the pendulum instead of snapping back to idle.
- **Shield block** (`alt_action`, held) orbits the shield from its resting position at the character's side to directly in front of the body, so it's genuinely between the attack and the body rather than just reoriented in place. The next hit taken while raised is fully negated and starts a brief total-immunity window (1.5s), after which block goes on cooldown (5s). Blocking and swinging are mutually exclusive; move speed drops while raised but not during the immunity window. Shield color is the state cue — bright during immunity, dull red during cooldown.
- **No impact VFX on a landed swing, deliberately.** Soldier's `ImpactFlash`/tracer are a gun's VFX and Tank never needed a melee equivalent.
- **Known gap:** every number (`AttackDamage`, `AttackSpeed`, block timings) is a first guess, never balanced against real waves.

### Architect

A distinct, non-combat role — places and preps towers, pre-crafts tokens, then isn't part of the fighting roster (in-fiction, he's sustaining the shell). He has **no equipment of his own**: `CharacterId` has no `Architect` value at all, so no item can target him even by mistake, and he has no health value since nothing can attack him during Build phase. Opening the inventory as the Architect shows the current Sentinel's loadout.

The player can **repair** towers mid-match by funneling mana into them, but cannot **reposition** them once placed — tokens cover "I need a tower somewhere else, right now."

**Not designed yet:** the player's own power/progression system, which is decided to share no mechanics with Runes. Character set bonuses are a far-future itemization idea.

**Stretch goal, not committed:** a real Portal-style gun that redirects enemies, not just a player teleport. The rendering is a known Godot technique (`SubViewport` + second camera + oblique near-clip plane); the real cost is that enemies path via `NavigationAgent3D`, which has no concept of falling through a portal and re-pathing from a landing spot. Worth an isolated prototype before ever attempting it for real.

## 4. Towers, Spells & Runes

A base skill (**Spell**, e.g. Fireball) is an immutable data definition. Modifiers (**Runes**) transform that definition or hook into its events. Composition, not subclassing.

**Tags** (Projectile, AoE, Duration, Trigger, elemental types) let a Rune apply generically to "anything with tag X" rather than needing per-spell logic. Elemental tags exist for build-crafting depth only — enemies have no elemental resistances by design (Section 5).

Elements so far: **Fire, Lightning, Cold, Wind, Water.** Runes socket into 4 slots per tower, one per corner. Towers don't level or scale within a match, and duplicate rune types on one Spell are rejected in code.

**Rune roster and naming.** Every rune's display name is one evocative word rather than a mechanical description, the way ARPGs typically name modifiers: **Haste** (cast speed), **Extend** (range), **Velocity** (projectile speed), **Salvo** (multi-projectile), **Expand** (increased area), plus **Chain**, **Fork**, **Pierce**, **Nova**, **Return**, and the **Ignition** reaction rune.

**Mana/capacity cost is multiplicative, PoE-support-style.** Every `Rune` has a `ManaCostMultiplier` applied automatically during resolve, multiplying the tower's resolved `ManaCost` in the same `base * (1 + increased) * more` shape as Damage/Range. Every rune currently uses a flat placeholder 1.2× for testing the mechanism; `MaxCapacity` is a placeholder 1000 to leave room to experiment before real numbers are chosen.

**Targeting.** `TargetPriority` is a flat field on `SpellCastContext`, seeded from `Spell.BaseTargetPriority` and read by `EnemyTargeting.FindTarget` — so a future targeting rune is an ordinary `Rune` writing one field, with no pipeline change. The default is `ClosestToGoal`, ranked by remaining nav-path length, which is comparable across lanes because every lane converges on the same objective. Range is always measured from the ritual; only the choice among in-range enemies varies. Chain jumps, forks and projectile redirects deliberately stay nearest-first, matching what those runes promise. Only `ClosestToGoal`/`ClosestToRitual` exist so far. **Player presence is never a targeting fallback** — multiple lanes mean the player can only be in one place.

**Interaction model.** Placement and aiming use a center-screen reticle plus a raycast into the world, not a screen-space cursor. This keeps mouse and controller unified under one system; full controller support is a requirement, not an afterthought.

Player-buff towers (a "Buff Totem") buff the player instead of attacking, range-based so the player must stand near it, and cost capacity like anything else.

**Not implemented:** line-of-sight raycasting. Towers currently use pure distance checks, so they can shoot through walls.

**Visual direction.** Every tower is fundamentally magical, so the physical shape needn't change per spell. The base is a floating structure with a central energy core; the spell determines the energy color, the central rune's shape, the crystal geometry, the orbiting symbols, the rune circle's behavior and the particles. A fire spell gets angular rapidly-rotating fragments and a turbulent core; an ice spell gets crystalline structures and slow orbiting runes.

### Rune acquisition

Runes carry the same `Rarity` axis Items use. **Common is unlocked for every rune from the start** so a new player always has real options; higher tiers must be found. Storage is one `.tres` per rarity tier per rune type — accepted as authoring overhead rather than re-plumbing `SpellInstance`/`Ritual.RuneSlots`/`BuildMenu`/`RitualPlacer` to carry a tier dimension everywhere a bare `Rune` reference currently flows. Each type gets a subfolder under `assets/runes/`, and `RuneCatalog` scans recursively.

- **Drops** fold into the same weighted roll `LootGenerator.GenerateRandomDrop()` uses for gear: roll a `Rarity` from the shared table, then pick uniformly among that rarity's not-yet-known runes. A rarity with nothing left to teach is excluded from the roll; if everything is known, it falls back to a gear roll rather than wasting itself.
- **A drop is a `RuneTome : Item`**, reusing world-item pickup, nameplates and tooltips entirely. It sits in Inventory until the player right-clicks it (`inventory_use`) to learn it.
- **Cascade-unlock:** learning a higher tier grants every lower not-yet-known tier of the same rune too, so owned tiers are always a contiguous range from Common upward. This is what keeps the tier-picker UI simple — nothing to grey out, only ever list what's owned.
- **Content gap:** only `multi_projectile` (Salvo) has its full five-tier set authored. Every other rune type has just its Common file, and since Common is free, **Salvo is currently the only rune a Rune Tome can ever roll.** Authoring the missing tiers for the other ten types is open.

**Per-copy scarcity (framework only).** Independent of rarity, a known rune can be limited-quantity (`RuneInventoryEntry.Unlimited = false`). `Quantity` is the total ever owned and is never decremented; "available right now" (`RuneCatalog.IsAvailable`) is derived live by scanning currently-placed Rituals' `RuneSlots`. This matters: an earlier spend-and-refund counter was scrapped because any path skipping `Ritual.Destroy` — a crash included — permanently lost a copy. Deriving availability can't desync. Nothing currently grants a limited entry, so this is inert. **Tokens can never use a limited rune**, since a token's loadout locks in during the unpressured Planning phase, exactly when a scarce rune should go on a real tower instead. Favorites may reference one and simply show unavailable when out of copies.

### Ice Wall

A destructible blockade that physically blocks a lane — enemies that reach it attack it directly rather than routing around, and it blocks the player too. It also expires after a duration cap if never broken, so it can't wall off a lane forever unattended. Either way, the casting Ritual goes on cooldown before recasting; a wall reappearing later is a fresh `IceWall`, not the original reforming.

Implemented as a real `Spell` (`IceWallSpell`) so it goes through the existing placement/economy/rune pipeline, with `WallCastEffect` as a third `CastEffect` alongside Projectile and InstantHit. Placement is three stages — wall start, wall end, then a linked tower within `Range` of the wall's line. Destroying the tower frees the wall, since the wall is parented under the tower's spawn point. Each prospective segment position is validated against the `Obstacles` layer, and the whole preview flips red if any segment is invalid. The wall auto-segments between its two points (minimum 2) so a token-sized wall can't fully block a lane.

- **Health is one shared pool**, not per-segment: `IceWall` owns segment health × count, any segment's damage drains it, and every segment breaks together.
- **`Expired` and `Destroyed` are two separate signals**, kept distinct so a future "explode on destruction" rune can react to one and not the other.
- **Casting is gated by a `CastTrigger` hook** rather than the usual nearest-enemy check. `ProximityCastTrigger` waits for an enemy within range of *any point along the wall's line*, so a long wall spanning lanes still triggers. `CastTrigger` is spell-agnostic so a future "if an enemy steps here" rune can reuse it.
- **Cooldown doesn't start until the wall is gone** — a general mechanism, not wall-specific. `CastEffect` has an `IsOngoing` flag and a `Concluded` signal; `SpellInstance` has `BeginOngoingEffect()`/`EndOngoingEffect(cooldown)` folded into `CanCast`. `Ritual` only talks to the abstract surface and has no knowledge of `IceWall`.
- **Enemies commit to a wall** within a detection radius, path to whichever point on the wall's line is nearest their own position (not a shared center, so converging enemies don't all aim at one spot), and attack the nearest segment once in range.
- **Do not try rebaking the navmesh around an active wall.** It was tried and fully reverted: any valid detour means pathfinding walks everyone around the wall, defeating its entire purpose, and recomputing shortest paths as walls appear and vanish has no concept of "already committed to this route," so paths oscillate. Walls are pure physical blockers.
- **Known rough edge:** enemies packed densely at a wall's face visibly jitter, from many overlapping `CharacterBody3D`s plus `NavigationAgent3D` avoidance fighting over the same overlaps. Disabling enemy-vs-enemy collision was rejected — it would let enemies overlap freely and make AoE hit far more targets than intended. The likely fix is decoupling simulation position (used for AoE and targeting) from rendered position, smoothed toward it so a hard physics correction doesn't pop.

A rune that makes the wall explode on break is planned.

### Ice Storm

Cold's first damage spell, and the one that makes Chill reachable. The Ritual calls hail down on its current target: damage lands across an area over a short window rather than in one instant, and a patch of `ChilledGround` is left behind that slows anything crossing it long after the hail stops.

- **Two independent things, spawned side by side.** `BombardmentCastEffect` drops a `Bombardment` and a `GroundEffect` at the same point; neither knows the other exists. That's what lets a rune leave chilled ground off something that isn't a bombardment at all, and lets a bombardment exist without leaving anything.
- **Meteor Shower is the same class with different data.** `Bombardment` applies no ailment of its own and carries no element, so a second spell of this shape costs a `Spell` subclass, a `.tres` and a visual scene — no systems code.
- **One authored damage total, spread across the window.** The tick cadence is authored on the bombardment's scene beside the visual it drives, and the cast effect divides the spell's damage by however many ticks fall inside the window — so changing the cadence changes the rhythm, never the total.
- **The storm slows nothing.** Being caught in it is worse than crossing the leftover ground because of the damage, not a bigger slow. An ailment on the bombardment would be Ice-Storm-specific and a meteor shower would want nothing to do with it.
- **`ChillRune` stacks with the ground rather than competing.** `ChillAilment` and `ChilledGroundAilment` are separate types, so they take separate slots and multiply: 30% and 25% resolve to a 47.5% slow inside a rune-socketed storm, decaying to 25% once only the frozen ground remains.
- **Targeting is the spell default** (`ClosestToGoal`), which aims at the enemy furthest along the path — the front edge of a pack, and so the fewest enemies. A "most enemies nearby" priority is the natural one for any bombardment and is a single enum field on the context; deferred until there are more priorities to add at once.

### Damage & repair

**A Ritual sits on two physics layers, and the split is load-bearing.** `Rituals` means *blocks movement* — it's what enemies and the player collide with. `RitualHitbox` means *everything else a Ritual's body is for*: being shot by enemy projectiles, being raycast by repair and destroy, and blocking placement of another tower on the same spot. They were one layer until the **Shroud** rune needed a tower that enemies walk through but can still be hit and repaired, which is impossible with a single bit — clearing it takes the hits and the repairs with it. Shroud clears `Rituals` alone. Consequence to remember: a mask that wants "is there a tower here" wants `RitualHitbox`, and only movement collision should ever reference `Rituals`.

A damaged tower is normally **disabled, not destroyed**, and the active Sentinel repairs it by funneling mana in. True destruction is rare and returns attached runes to inventory, never losing them. The **Sapper** is the enemy that meaningfully threatens towers — a visible, prioritizable threat rather than universal collateral risk. Disabled towers show on the minimap and their core mesh turns gray.

Repair and destroy both use the same reticle raycast as placement, capped to a similar interaction range, with a contextual world-space prompt near the tower. **Repair is a single press** (frequent, low-stakes) and costs half the tower's original mana cost, shown in the prompt and tinted red when unaffordable. **Destroy is hold-to-confirm (~1s)** with a progress indicator on that same prompt, cancelling if released early or if the raycast loses the tower. Chosen over a persistent mode toggle specifically to avoid mode-confusion on the harder-to-reverse action.

### Construction & deployment UI

`Q` drives two flows that behave nothing alike, built as separate systems because they sit under opposite time pressure.

**Architect, Build phase — construction menu.** `Q` opens a full-screen overlay: tag-filterable spell grid → rune socket screen (4 sockets) → confirm → placement. No time pressure, so this is a rich browsing experience.

- Socket state survives backing out and returning for the same spell, clearing only if the spell selection itself changes.
- A rune already socketed on the same spell shows greyed out and sorted to the back, making the one-per-type rule visible instead of silently rejecting a drop.
- A **Clear All** button sits alongside the per-socket unsocket, gated behind a confirmation dialog so a near-finished build can't be wiped by a stray press.
- **Rarity tier picker.** Rune tiles carry a rarity-colored backdrop showing the player's max owned tier. Pressing an *already-filled* socket opens a small vertical popup — current tier boxed, owned lower rarities listed below in descending order, and a red X for unsocket. Accepting a lower tier commits it; deliberate downgrades are intended, e.g. socketing a Common damage rune to land on an exact breakpoint another tower can finish. Accepting the already-active tier is the escape hatch back to the full grid. An empty socket opens the grid directly.
- Rune description text lives on the `Rune` resource, not hardcoded in UI code.
- Renders as a blocking overlay over the opening player's view. Since co-op has both players building simultaneously, a true game pause won't work — the overlay needs per-player scoping, mechanism still open.

**Favorites.** A saved spell plus up to 4 exact-tier `Rune` references. Created from the socket screen, prompts for a name, auto-suffixes collisions. A Favorites tab sits alongside the spell grid; each tile lists its spell and all 4 rune names. Picking one goes straight to placement — it's a locked stamp, not a pre-filled starting point.

**Tokens.** Up to 3 held unused at once — a "how many can sit ready" cap, not a per-wave cast limit. Crafting and recrafting are Build-phase-only and cost nothing up front; mana and capacity are spent only at deployment, specifically so a token shores up a defense that didn't quite get finished rather than becoming a second economy to budget during prep. The bottom sixth of the picker screen holds 3 slot circles showing each token's spell, runes and cost. Clicking an empty one opens the ordinary picker in "creating token N" mode; clicking a filled one offers New or Edit. A colored border marks both the slot and the picker the whole time, so it's never ambiguous that Confirm writes to a token instead of starting placement.

**Any player, mid-wave — token deployment.** `Q` toggles a lightweight pick mode that remaps `1`/`2`/`3` to the player's tokens. Right-click, `Q`, or Escape all back out. Pressing a mapped number drops into the same ghost/reticle placement a real tower uses; confirming spends live and consumes the token, cancelling doesn't. No browsing, no sockets — optimized for speed under pressure. Tokens are consumable: destroying a tower built from one doesn't give it back.

The field HUD (`AbilityHud`, holding `TokenBar` and `AbilityBar` as independent siblings rather than one combined class) is two permanent rows — 3 token slots colored by spell element and labeled, above 4 ability slots showing each ability's name and a cooldown-fraction fill. Both always display, but only one shows number badges at a time since they share the `1`/`2`/`3` keys: abilities normally, tokens while pick mode is active — each row decides its own badge visibility off the same `TokenPickModeChanged` signal, with no shared state between them.

**Navigation is gamepad-first**, not mouse-first-then-retrofit: discrete grid focus navigation via Godot's focus-neighbor system rather than hover, with tag filters on their own input path so they don't collide with grid movement.

**Future direction, not started:** redo the spell grid and rune picker as a Warframe-style radial menu. Would touch the spell grid, Favorites tab and socket screens alike since they share the same tile-grid shape. Worth its own design pass, not a reskin.

## 5. Enemies

**Philosophy: trade-offs, not gates.** Soft pressure (tanky/slow vs. fast/fragile, grouping, timed bursts, partial resistances) over hard immunities. **No elemental resistances at all** — elemental typing is a player-facing build axis only. Difficulty tiers differentiate through composition and timing, never flat stat multipliers.

Roster sketch: Fodder, Ranged Attacker, Tank, Flyer, Sapper, Assassin-type. **Built so far:** `Grunt` (ordinary walker), **Marksman** (ranged) and **Sapper** (tower-killer).

**Enemy variety is subclassing, not composition.** `Enemy` → `WalkingEnemy`/future `FlyingEnemy`/`BossEnemy` → concrete types. An earlier plan called for swappable `IMovementBehavior`/`IAttackBehavior` objects by analogy to `SpellCastContext`'s components, but that composability only pays off when the same behavior is genuinely reused across many combinations, the way `ChainRune` applies identically to any spell. Enemy specialness doesn't work that way — a flying ranged enemy's attack would be its own bespoke kit, not a `RangedAttackBehavior` reused verbatim from a ground archer. Without real interchangeable reuse there's no combinatorial explosion to protect against, so plain subclassing plus Godot's own scene inheritance for content variants is simpler. **Still open:** whether *movement style* (ground-lane vs. flying) is worth keeping as its own reusable axis — decide when a `FlyingEnemy` actually gets built.

`Enemy` holds only what's universal: health, `TakeDamage`, ailments, elemental-status visuals, registration, a virtual `Die()`. `WalkingEnemy` holds everything lane-following, leash-chasing, wall-engaging and ragdoll-specific. A concrete subclass is only worth creating once an enemy needs code its siblings don't — `Grunt.tscn` just attaches `WalkingEnemy.cs` directly.

**Movement.** A `Lane` per spawner computes its route once via a single static navmesh query and exposes it as ordered waypoints with precomputed remaining-distance-to-goal; enemies walk it by index rather than each running a live query every frame. `RoutePreviewEffect`'s build-phase trail reuses the same waypoints.

`Enemy` runs a `MovementMode` state machine — `FollowingLane`, `ChasingPlayer`, `RejoiningLane`, `ApproachingObjective`, `AttackingWall`. Leash and attack checks run on an interval, not every frame.

- **Leash:** an enemy within a small leash range of the player breaks off its lane advance to chase and attack, giving up and returning to its route if the player gets far enough away. Same "break from default movement, act, resume" shape as wall engagement.
- **Give-up-and-rejoin picks the nearest waypoint across every lane**, not just the one it started on, with one rule: never rejoin behind its own already-covered progress on its own lane. Any point on a lane it never walked is fair game.
- **`NavigationAgent3D` avoidance** is wired with radius pulled from the enemy's own capsule shape rather than duplicated as a separate number. Godot requires `TargetPosition` stay live even during lane-following for avoidance to produce non-zero velocity at all, so this costs one navmesh query per waypoint reached rather than zero — still far cheaper than a live per-frame query per enemy.
- **Known rough edge:** several enemies converging on the same destination still crowd and jostle rather than spreading out. Real level geometry will surface what's worth tuning better than guessing on the test level.

**Marksman** only ever fires a real traveling `EnemyProjectile`, never hitscan, and opportunistically engages a tower it comes into range of without hunting one down (`RitualsOutrankPlayer = false`). `EnemyProjectile` resolves damage against whatever it actually collides with rather than a target locked in at launch, so it's genuinely blockable by anything in the way. It's also the only thing that sets `DamagePacket.Origin = Projectile`; melee defaults to `Melee` for free.

**Sapper** prioritizes towers over both the objective and the player (`RitualsOutrankPlayer = true`), falling back to the objective only once no live tower exists. Ordinary enemies can't damage towers at all — it's an opt-in `WalkingEnemy.CanAttackRituals` flag.

Every enemy attack aims at `IDamageable.AimPoint` rather than a raw `GlobalPosition`, which would be a character's feet or an object's local origin.

**`TrainingDummy : Enemy`** is a dev-only unkillable target for testing Ritual behavior without fighting loot RNG or waiting for a wave. It overrides `Die()` to reset health and never registers a kill, reward or drop, and shows a live rolling DPS readout above its head. Not part of the real roster; lives in the hub (Section 8).

## 6. Items & Progression

**Equipment: 11 slots.** Weapon, 5 Armor (`Helmet`/`Chest`/`Legplates`/`Boots`/`Gloves`), and 5 Accessory (`Belt`, `Amulet`, `LeftRing`/`RightRing`, `Relic`). Accessories are where Ritual-affecting mods live. `Ring` is one class, not two — a dropped ring doesn't know which hand it's for, so the two slots hold the same type.

`Item` is abstract with a real subclass per slot rather than a flat class with an `EquipmentSlot` field. Unlike `SpellCastContext`'s composition case, equipment slots aren't independently-combinable traits: an item is permanently and exclusively one slot, and the set is closed and stable, so this is a genuine is-a hierarchy. `Weapon` is itself abstract with `Gun` and `SwordAndShield` (Tank-only) beneath it — `LootGenerator.CreateItemForSlot` is character-aware so rolling a Weapon for Tank produces a sword. `DefaultItemIcons` is keyed by `System.Type` rather than `EquipmentSlot` because those two share a slot value but need different thumbnails.

**Only one Sentinel is ever instantiated per player**, chosen at level load. `SentinelSpawner` reads `PlayerSentinels.Get(OwnerId)` and instantiates only the matching scene, so there's never a second Sentinel node sitting inert in the tree. `Sentinel.ByOwner`/`Architect.ByOwner` are keyed by `PlayerId` and look each other up for `HandoffFrom`, already correctly shaped for a second player. `Sentinel.Local` is the local player's Sentinel regardless of whether it's currently active; `PlayerCharacter.ActiveCombatant` is separate and exists purely for camera and position purposes.

`PlayerSentinels` maps `PlayerId → CharacterId` and reads/writes straight through to `PlayerSaveData.LastPlayedCharacter`, so the local pick persists across sessions and defaults to whoever you played last — a real hub character-select screen just reads the same field.

### Mods

**A shared definition plus a small per-item roll.** `Affix` is the hand-authored definition — name, description template, `AffixType` (Prefix/Suffix/Unique), the `StatId` it affects, allowed slots, character restriction, and a 5-tier value table. **Tier 1 is weakest, 5 strongest — deliberately the reverse of PoE's convention**, so adding a stronger tier later is just a new highest number instead of renumbering everything. These live hardcoded in `AffixTable` keyed by `StatId`, since there are only a few dozen and nothing but hand-edits touches them. `RolledAffix` is the small `Resource` an item actually carries: just the `StatId`, rolled tier and rolled value, looking everything else up through `Affix`. 20 affixes are authored across the Ritual, Sentinel and Weapon pools.

`AffixType.Unique` and `Rarity.Unique` are orthogonal — the first describes whether one mod is fixed and non-craftable, the second describes the item as a whole. `Affix.AllowedSlots` restricts where a mod can roll or be crafted, kept on the affix because the crafting table needs to answer "which mods are valid for this item" live. `Affix.RestrictedCharacter` is the character half of the same question and is enforced, though no affix sets it yet — it exists for character-signature weapon mods. Note this is *not* what produces a "Templar Only" line on a dropped weapon; that's `Item.EquippableCharacter`, a whole-item scope, and both are needed.

**Loot generation.** `LootGenerator` rolls 3–6 affixes (at least 1 prefix, at least 1 suffix, neither exceeding 3) from a flat weighted pool of every eligible (affix, tier) pair at once — mirroring how PoE actually rolls, one weighted draw across every tier of every mod together rather than picking a mod then its tier. Once an affix is drawn, all its tiers leave the pool. `Rarity` gates which tiers are reachable (`Common` = T1 only through `Legendary` = all five), each step keeping weaker tiers available; `AffixTierRange.Weight` controls likelihood within that. `Rarity.Unique` is never generated — uniques are meant to be hand-authored with their own signature mod.

Damage resolves as `Base * (1 + IncreasedPercent) * MoreMultiplier`, PoE-shaped, so equipment mods have a clean bucket to plug into.

**Design rule:** avoid flat "+X% damage" as the *only* kind of mod. Items should let players break the rules — a unique that collapses a spell's projectiles into one with damage summed — not just tweak numbers.

### How mods reach gameplay

`StatId` is the shared vocabulary mods and consumers both speak. `CharacterSheetCache` resolves and caches, per `CharacterId`, the summed bonus per `StatId` from equipped items, invalidated on equip/unequip. **The sheet holds bonuses only** — base values stay on whatever field already owned them, and each consumer combines its own base with the sheet's bonus, deciding for itself whether that stat is additive or multiplicative.

Ritual-side stats take an ownership-aware path, since they layer alongside per-instance Rune contributions rather than replacing a field read. `Ritual.OwnerId` is a `PlayerId` — deliberately not a `CharacterId`, so two co-op players who pick the same Sentinel still resolve independently. `SpellInstance.Resolve(CharacterSheet ownerSheet = null)` takes the sheet as data rather than reaching for it, keeping `SpellInstance` as ignorant of players and equipment as it already is of what's inside a `Rune`.

`SpellCastContext.ApplyEquipmentBonuses(sheet)` folds simple stats into the same `ScaledStat.Increased` fields Runes use. **Per-element Ritual damage goes into a separate `ElementalIncrease` struct with a private setter specifically so a Rune can never reach that lever** — the same reasoning PoE uses to keep item affixes and support-gem multipliers from cannibalizing each other. Cooldown recovery rate *divides into* cooldown time exactly as cast speed divides into cast time, matching PoE's real mechanic where recovery-rate stats can't push a value to zero; mana and capacity reduction stay linear and floored at 1. Rituals re-resolve live — `EventBus.InventoryChanged` invalidates every listening Ritual's cached context, so re-gearing mid-Build retroactively updates towers already placed.

**Current Sentinel stat surface, all live end to end:** Health, attack damage, attack speed, move speed, Armor/Shield, Crit Chance/Damage, Health Regen (flat and percent), and Cooldown Recovery Rate. `AttackDamage` and `AttackSpeed` live on `Sentinel` itself, not per-subclass.

**Still drafted-only:** a larger per-ability-slot mod pool (Damage/Duration/CDR), which needs abilities carrying per-slot numbers to attach to.

### Armor and Shield

**Base values are item-inherent, not rolled affixes.** Each `Armor` piece gets a `BaseType` (`Hybrid`/`Armor`/`Shield`, rolled at generation) and a `RolledBaseStat[]` referencing the shared `ArmorBaseTypeTable` — deliberately not subclassed, since `BaseType` is rolled per instance like an affix rather than fixed per concrete type the way `Gun`/`SwordAndShield` are. Total scales with `Rarity`: Helmet/Gloves/Boots use 100–500 across Common→Legendary, and Chest/Legplates double that via `Armor.SlotWeight`. Hybrid splits 50/50; a pure type puts it all in one stat.

**Shield is worth half as much per point as Armor**, baked into the stored base value at generation rather than applied at runtime — so a pure-Shield item nets half what a pure-Armor item does from the same budget, and Hybrid's Shield share drops to a quarter while Armor stays at half. **Unique-rarity values are still open**, likely hand-set per unique.

**Resolution is local-per-item, a deliberate divergence from every other stat.** `Armor.ResolvedValue(stat)` is that item's own base × that item's own rolled percentage affixes, with no character-level layer. `CharacterSheetCache` sums each piece's resolved value into `CharacterSheet.GetLocalTotal(stat)`, a pool kept separate from the `GetBonus(stat)` percentage pool. A global multiplier does exist on top — `SentinelTotalArmor`/`SentinelTotalShield`, jewelry-only affixes applying via the ordinary bonus pool: `Total = GetLocalTotal * (1 + GetBonus(totalStat))`.

**Armor mitigates melee; Shield is a separate absorb-then-regen pool for projectiles.** Not a matching mitigation stat, on purpose. Melee damage is reduced by an S-curve (`Armor^p / (Armor^p + K^p)`) rather than a plain `x/(x+K)`, which grew too fast at low investment; it's tuned so trivial gear barely registers and near-max gear approaches ~90%. Shield absorbs projectile hits directly with overflow spilling to Health, and passively regens after a few seconds without a projectile hit. Both reset to full on `HealToFull()`.

### Inventory, drops and stations

**Inventory** is a fixed 40 slots with player-chosen positions, plus all 11 equipment slots in a PoE-style human-silhouette layout. The panel occupies 40% of screen width, works during Build phase without losing mouse control, and stacks correctly with the Build Menu via `UiPanelStack`.

Two interaction styles work on every slot through a small `SlotHandle` abstraction (`GetItem`/`PlaceItem`/`CanAccept`): click-to-pick-then-click-to-place, and a press-drag-release gesture layered on top without altering it. **A plain click that never drifts past a small pixel threshold falls straight through to the click path, which is what keeps this gamepad-safe** — a controller confirm press never emits the mouse motion the drag path watches for. Placing something displaces whatever was there to the earliest open slot rather than losing it; dropping outside the panel, or into a full inventory, sends the item into the world. Rings are placed explicitly, no auto-assignment.

`ItemDragController` is an autoload holding the shared held-item state and cursor icon, so panels other than Inventory can participate in the same gesture. It also supports **sell targets** (a whole panel rect where releasing counts as selling) and **quick-move targets** (`quick_move`, Ctrl by default, offers the item to each registered target in turn, with an inventory fallback always tried last). Both `quick_equip` and `quick_move` are named Input Map actions rather than hardcoded modifier checks, so they're genuinely remappable — including to a controller button.

**World drops.** `ItemDropManager` rolls loot the way PoE does: `Enemy.LootRolls` is a count of *independent* `Enemy.DropChance` rolls per kill, so a kill drops anywhere from 0 to `LootRolls` items. Both are plain per-instance exports, so a bigger enemy just needs a higher number in the Inspector. Slot and rarity are randomized per drop, and a dropped `Weapon` gets a random playable Sentinel as its `EquippableCharacter` rather than always the current one. Each drop spawns a `WorldItem` — a small rarity-colored sphere, jittered so multiple drops don't stack exactly.

**Pickup is nameplate-driven, not raycast-at-a-sphere.** `ItemNameplateController` projects nearby `WorldItem`s into screen space each frame and shows a small rarity-colored nameplate above each; `ItemInteractor.FindTargetAhead()` hit-tests the crosshair against those rects instead of firing a physics ray. Aiming a thin crosshair at a small sphere was genuinely annoying. **Nameplates deliberately never push each other apart** — an earlier distance-sorted version made labels visibly reshuffle as the player approached a cluster, which was worse than the overlap.

Whichever item the crosshair rests on expands into the same rich tooltip Inventory hover uses, **pinned to a fixed screen corner rather than anchored to the item** — anchoring meant the tooltip moved while the player was still trying to hold the crosshair steady to read it. It shifts left only far enough to clear an open panel.

**Crafting table** (hub, Room A) modifies an item's mods after the fact: add or remove a prefix or suffix, or reroll every existing mod's value within its own tier range. `CraftingActions` provides the three actions plus matching gates. Adding draws from `AffixTable` capped by the item's rarity and excludes stats already present; **removal picks uniformly among occupied affixes — the player doesn't choose which one goes**; reroll changes values without touching which mods are present. Each action costs a flat `GameRules.CraftingActionGoldCost` (currently 1), set mostly to give Gold somewhere to go rather than as a tuned price. The panel shows Prefix/Suffix groups of 3 bars each, with empty slots still reading as reserved slots, plus a data-driven action list with live affordability gating. `PlayerSaveData.CraftingSlotItem` persists separately from `Inventory`, since an item on the table isn't in your bag, and saves immediately on every change.

**Vendor** (hub, Room A, mirroring the crafting table) buys, sells and buys back. Stock is 5 items generated fresh via `LootGenerator.GenerateRandomDrop()` each time the hub loads, plus a Buyback list that fills as items are sold. Neither persists — the hub scene fully reloads on every arrival, so this falls out for free. `Item.SaleValue` (mod count) is the one price used in every direction, no markup. Selling is triggered by dragging an item anywhere onto the panel or Ctrl+clicking it.

**Not built:** stock-gating by progression, so Legendaries can't appear at the vendor or drop at level 1. Deferred together with the identical gap on enemy drops rather than solved twice.

### Tooltips

A shared `ItemTooltip` autoload replaces Godot's built-in `Control.TooltipText` everywhere — the built-in one lives in an internal popup window that closes itself on Escape, which read as flickering.

It's **event-driven, not polled**: `Register(control, getItem)` wires the control's real hover signals, subscribes to `ItemDragController.Changed` to catch a slot's contents changing while the mouse never left, and treats the advanced-view modifier as a discrete input event. Only the panel's on-screen position tracks per frame, since that's a genuinely continuous quantity.

- **Hold `advanced_tooltip` (Alt)** for a PoE2-style layout: each mod is an `AffixRowView` row with a right-aligned tier badge, grouped under Prefix/Suffix headers. Rows are single-line — wrapping created a circular width↔height loop with the auto-sizing parent. Hovering a tier badge opens a nested popup with the mod's full name and its `AffixTag[]`.
- **Hold `compare_item` (Ctrl)** to show the equipped item(s) in the same slot alongside, up to 2 panels — 2 rather than 1 specifically for `Ring`'s dual slots, via `EquippedLoadout.GetEquipped(slot)` yielding both rings when asked for either. `compare_item` is a separate action from `quick_move` despite both defaulting to Ctrl, so a controller can bind them differently.
- **Position anchors to the hovered control, not the mouse** — recomputed while hovered, then frozen once the mouse leaves. This is what lets the mouse catch the tooltip and move into it while a modifier holds it open, instead of the tooltip fleeing the cursor. It flips right when there isn't room on the left, checked against the combined extent of the main panel plus the widest visible comparison panel.
- For `Armor`, resolved Armor/Shield values show under the item name in a distinct blue so they read as a different kind of information.
- **Escape closes every open panel at once**, via `UiPanelStack.CloseAll()` — Godot stops delivering an input event once a handler marks it handled, so only the topmost panel used to see it. `BuildMenu` is the deliberate exception: it steps backward through its own wizard screens one at a time when it's the top panel, and is only swept away in full when something else is above it.

**Explicitly decided against:** a player damage-resistance/type system. It would require giving enemies damage types, which isn't needed unless itemization proves it is.

## 7. Architecture

### Cast pipeline

1. **`Spell`** (e.g. `FireballSpell`) — the immutable definition, shared by every tower using it. No mutable state lives here.
2. **`SpellInstance`** — one per placed tower. A reference to the shared `Spell` plus that tower's attached `Rune` list and cast/cooldown state. This is the tower's loadout, not a projectile.
3. **`SpellInstance.Resolve()`** — applies every attached Rune over the Spell's base values, producing a **`SpellCastContext`**: a transient data snapshot of final stats for one cast.
4. **`CastEffect.Cast(context, spawnPoint, caster)`** — each effect declares `CanHandle(context)` by checking for its own component. `Ritual` auto-discovers whichever `CastEffect` children it has and uses whichever says yes. `caster` is passed so an effect can read placement-time state the context doesn't carry, e.g. `WallCastEffect` reading the two wall points.
5. **`Projectile`** — the flying object, fully independent once spawned, with its own damage, radius, pierce count and hit tracking.
6. **`ProjectileHitEffect`** (e.g. `ExplodeOnHitEffect`) — reacts on hit, and the right extension point for future on-hit behavior *authored on the spell*. On-hit behavior granted by a **rune** goes through `OnHitRune` instead, which reaches every damage path rather than just projectiles — `SpellCastContext.OnHitRunes` travels to `Enemy.TakeDamage` as a `SpellHit`, so a projectile, a hitscan strike, a chain jump and an explosion all run the same runes.

Each arrow hands off to something new and independent; nothing downstream reaches back upstream.

**`SpellCastContext` is one class with optional components, not a subclass tree.** It carries universal fields (`Range`, `Damage` as a `DamagePacket`, costs) plus nullable components — `Projectile`, `Chain`, `Area`, `InstantHit`, `Wall`, `CastTrigger` — each null unless needed. A Spell's `CreateBaseContext()` decides which it starts with, and a Rune can lazily create one it didn't (`ChainRune` grants chaining to a spell with no `Chain` component at all).

The original subclass design broke as soon as a spell needed two capabilities with no is-a relationship between them: `FireballSpell` is both Projectile and AoE, neither specializes the other, and C# allows one inheritance path. Patching with `AoeProjectileCastContext`-style classes means a new class per *combination*, and silently assumes every spell sharing a base wants every capability it carries — untrue for a single-target Ice Spear with no use for `Area`. Independently-combinable capabilities live as nullable components checked with `!= null`, borrowing the ECS idea narrowly for this one class rather than adopting it project-wide.

**Where a stat's base value lives:** universal stats (Range, Cooldown, Damage, RagdollForce) are flat fields on the context; capability-specific ones (ProjectileCount, ChainCount, AreaMultiplier) live on the relevant component, which `CreateBaseContext()` only populates if the spell has that capability.

**Swappable hooks over raw fields.** Three cases follow the same shape — an optional `Resource` hook rather than a field on shared data:

- **`ExplosionRadius`** stays on `ExplodeOnHitEffect`, not threaded through `ProjectileCastData`. Those types describe what `Projectile` itself acts on; explosion behavior belongs entirely to the swappable hit-effect strategy, which exists so `Projectile` never needs to know about it. `fireball_spell.tres` embeds its own effect as a local sub-resource, so the radius is authored right there and can't be stomped by another spell.
- **`ChainDamageModifier`** (`GetMultiplier(remainingChains)`) replaced a raw `MoreDamagePerRemainingChain` float. `ChainCastData` is shared by every chaining spell, so a raw field there implied any spell could use it when it only ever described Lightning Bolt's finisher curve.
- **`CastTrigger`** gates whether a cast happens at all, spell-agnostic so a future trigger-rune can reuse it.

**`Projectile.Radius`** (physical size, unrelated to blast radius) *is* threaded through the context, since scaling its own mesh and collision is core `Projectile` behavior. That scaling is uniform on all axes and assumes a roughly round mesh — a future elongated projectile would need its own formula for that one line.

**`ChainableSpell` is reserved for spells where chaining is inherent to identity** (`LightningBoltSpell`), not for projectiles that merely *can* chain if a `ChainRune` is socketed. `FireballSpell` extends `Spell` directly and declares nothing about chaining. `ChainRune` carries its own `FallbackChainRange` applied via `Mathf.Max`, so a rune-granted chain always has reach without ever shrinking a spell's inherent range.

### Class notes

- **Rune** — tier, required/granted tags, the data transformation it applies, any event hooks it registers. A `Rune` is a shared `Resource`: the same `.tres` is referenced by every Ritual socketing it, so a rune can never cache per-Ritual state on itself. Anything resolved per-cast belongs on the `SpellCastContext`, and anything needed at hit time has to arrive through the `OnHitContext`.
- **Ability** — one of a Sentinel's four active slots, and deliberately a **`Node`, not a `Resource`** — the one place the "gameplay values live on a Resource" rule doesn't apply. A Resource can't own scene content, can't call `CreateTween()`, and has no update loop, and abilities need all three (a tether visual between two enemies, a gauge, per-enemy markers). Being a Node also collapses the definition/instance split `Spell`/`SpellInstance` needs: a Node is inherently per-instance, so cooldown and duration state live on it with no shared asset to stomp, and `_PhysicsProcess` is the tick for free. The precedent is `CastEffect` and the `RitualPlacer`/`TokenDeployer`/`LookInteractor` components a Sentinel already carries. The base stays thin — `DisplayName`, `Description`, `BaseCooldown`, `CooldownFraction`, `IsActive`, `CanToggleOff`, `TryActivate()` — with behavior bespoke per subclass. Numbers use the `[Export] public override float BaseCooldown { get; protected set; } = 20f;` pattern `FireballSpell` uses, so the C# initializer is the default and the Inspector is an optional override serialized only if touched.
    - **Cooldown starts when the ability ends, not when it begins**, so a duration-based ability cancelled early recovers from where it actually stopped. `SentinelCooldownRecoveryRate` divides into `BaseCooldown` there, matching `SpellCastContext.ResolvedCooldown`.
    - **The four slots are an explicit exported array, not auto-discovered from child order** — slot order *is* the key binding, so auto-discovery would silently remap keys if nodes were ever reordered.
    - **An ability that targets with the mouse takes the attack binding, through three separate gates.** `CanUseAbilityInput` (activation, and an ability reading its own clicks), `CanUseWeaponInput` (the character's gun or sword), and `ExtraCombatInputGate` underneath both. The capture term lives only in the weapon gate: folding it into `ExtraCombatInputGate` is circular, because `CanUseAbilityInput` is built on top of that, so a capturing ability would gate away the very clicks it's trying to read. The weapon is suppressed by the gate rather than by `SetInputAsHandled()`, since `Soldier`/`Tank` poll `Input.IsActionPressed` in `_PhysicsProcess` and never see the event.
- **Ritual** — HAS-A `SpellInstance`. The Ritual owns position, capacity cost and HP/disabled state; the instance owns attached runes. Keeps "runes return to inventory on destruction" clean and lets one Spell definition be shared safely across many towers.
- **EffectVolume** — an `Area3D` that affects whatever is inside it, on an interval, for a duration, then frees itself. Subclasses decide what "affects" means; it owns only occupancy, timing and cleanup. **An Area3D rather than a per-tick spatial scan**: the broadphase reports entry and exit instead of sampling, so an enemy clipping the edge between ticks can't be missed, and a large enemy brushing the boundary counts by its actual shape rather than by its origin. Named for what it *does* rather than for what its subclasses are — a storm and a patch of frozen ground are not the same kind of thing, and the shared base asserts only that both are volumes that tick.
    - **`GroundEffect`** is the terrain family (`ChilledGround`, later a burning one, maybe a wet one). It knows nothing about what spawned it, so a cast effect, a rune firing off a destruction signal, or a reaction can each drop one in a line. It applies its effect on entry as well as on tick, and the ailment it applies outlives one tick — which is why nothing has to track departures, and why there's no ailment-removal API.
    - **`Bombardment`** is a cast in progress, not terrain, and deliberately *not* a `GroundEffect`. It sits beside that family rather than inside it.
    - **Sizing happens in code, never by scaling the root.** The collision shape is rebuilt fresh per instance from the resolved radius — a shape authored in a `.tscn` is shared by every instance of that scene — and only the footprint mesh scales, since scaling the root would stretch every child visual with it.
- **Ailment** — temporary state in a list on an Enemy rather than special-cased into the Enemy class. Covers elemental markers for reactions, CC like an immobilize, links between two enemies, and flags such as "currently carrying the income spirit." Three hooks: `Tick` for time-based effects, `OnHostDamaged` for anything reacting to its host being hit, and `SpeedMultiplier` for anything modifying movement (folded together multiplicatively by `Enemy.AilmentSpeedMultiplier`, which `WalkingEnemy.Speed` applies over its `BaseSpeed`). An ailment's numbers are resolved and baked in when it's applied (`BurnAilment` takes a total, not a reference to whatever lit it), which is why ailments carry no owner — nothing needs to re-resolve against a caster's live stats later.
    - **Stacking is one general rule, not a policy per ailment.** `OccupiesSameSlotAs` answers "is there already one of me here" — one per type by default, and Health Link widens it to one per *partner*, since links to two different enemies must coexist. When two land in the same slot, `ResolveAgainst` decides, defaulting to strongest-wins on `Magnitude`: an equal application refreshes the duration, a weaker one is discarded outright, so re-casting a weak effect can never keep a strong one alive. `BurnAilment` is the one exception, overriding that to merge remaining damage instead of competing. An ailment meant to stack freely returns false from `OccupiesSameSlotAs` for everything and simply accumulates instances — **a cap on stacks isn't supported**, because the slot check only ever sees one other ailment and can't count the list.
- **TriggerBinding** — pairs a trigger condition with a Spell to cast in response, making trigger-chaining inspectable data instead of hardcoded per-effect logic.
- **DefendableObjective** — generic name for the shell/harvester loss-condition object so flavor can change without renaming code. It's a passive `IDamageable`; enemies attack it through the same melee loop used on the player, gated by an `ApproachRadius` that accounts for its physical bulk.
- **Wave authoring is JSON, not Resources.** A level's waves live in one file (`assets/waves/level_01.json`) — an ordered array of waves, each an ordered array of steps, where a step is either a `spawn` burst of `{lane, enemy, count}` triples or an explicit `wait`. A wave reads top to bottom the way you'd narrate it: "5 of this, wait 10, then 5 of this and 2 of that." Bursts still trickle per-lane on `WaveController.SpawnInterval` so they don't stack-spawn instantly. Deserialized via `System.Text.Json` into plain POCOs, with `EnemyCatalog` resolving string enemy names to `PackedScene` and throwing loudly on a typo. **Accepted trade-off:** enemy references are string-keyed, so a bad name fails at load rather than at edit time. The Resource-array version it replaced needed a fresh sub-resource per spawn entry with no reuse and had a free-text unvalidated spawner id.

### Folder layout

**Organized by domain, not by file type.** A `scripts/` tree used to hold nearly half the project split into `data/` and `systems/`, which is the type-based split [Godot's own guide](https://docs.godotengine.org/en/stable/tutorials/best_practices/project_organization.html) advises against — and the seam leaked, with `CharacterSheet` in `systems/` while `RarityColors` sat in `data/`. It's gone, redistributed into `spells/`, `items/`, `combat/`, `waves/`, `save/`, `core/` and `debug/` alongside the existing feature folders, with `levels/` holding scenes and their scripts together.

**The project declares no C# namespaces at all**, so moving a file is pure relocation — no code changes, only `res://` path rewrites in `.tscn`/`.tres`/`project.godot`.

Content (spells, runes, enemies, items) is data-driven via `Resource` assets so iteration doesn't slow as the roster grows.

### Two real hazards

**Don't use a typed `[Export]` to reference a node from an inherited base scene.** `Sentinel` used to reach `RitualTokenPlacer`/`TokenDeployer` that way, and adding one unrelated export was enough for Godot to silently re-serialize `Soldier.tscn` with both assignments dropped — the cast fails during scene-build, the property reads null, and since only non-default values are written, the next save omits it. **It fails silently.** The tell: exports pointing at built-in types (`SpringArm3D`, `Camera3D`) survived the same save, because those resolve natively without the C# assembly loaded; only the custom-typed ones were lost. Resolve these with `GetNode<T>()` in `_Ready()`, which is what `LookInteractor` already does for the mirror case. Only `Soldier.tscn`/`Tank.tscn` use scene inheritance at all — every other `node_paths` entry points at a node local to its own scene, which is safe.

**The save format is fragile to renaming, moving or deleting a serialized class — a real pre-ship risk.** `SaveManager` uses Godot's raw `ResourceSaver`/`ResourceLoader` on `PlayerSaveData`, which embeds each item's *script path* in the save file. Renaming `Legs.cs` to `Legplates.cs` broke loading an existing save outright, and the only fallback today is to silently start fresh, discarding whatever was there. Fine for dev; not acceptable for a shipped player. Two fixes, neither done: cheap (back up an unparseable save before discarding it) and real (move to a format keyed by a stable type tag plus a schema version, so a rename needs only a migration function). Until then, avoid renaming serialized classes once real saves exist.

### Multiplayer implications

- **`EconomyManager` must be host-authoritative from day one.** Both players spend from one shared pool simultaneously; if clients mutated local copies, simultaneous spends would desync. One source of truth on the host; a client's spend is a request the host validates and broadcasts.
- Every state-changing action needs that same request/validate/broadcast shape — placing a Ritual, attaching a Rune, casting, triggering a token, repairing. Build it in from each system's first implementation rather than retrofitting.
- Enemy simulation is host-authoritative and single-sourced; clients receive state and interpolate rather than each simulating and drifting.
- Spell/Rune data syncs as small IDs into a static table, not serialized objects — both ends already have the transformation logic.
- Player death/revive needs explicit synced state (respawn timer, being-revived status).
- Godot 4's high-level multiplayer API covers these patterns directly; this is applying engine tools to the classes above, not building plumbing.

## 8. Hub & Dev Tools

A deliberately bare-bones staging area — meshes and colored boxes, no art pass — of two rooms connected by a hallway (`res://levels/hub/hub.tscn`, the project's `run/main_scene`).

- **Room A — selection and stations.** `CharacterSelectStation` lists Soldier/Tank and swaps the player's live body on pick. `LevelSelectStation` lists playable levels and loads one via `ChangeSceneToFile`. The crafting table and vendor (Section 6) sit in the remaining corners. All stations share one `StationInteractor`: a station just implements `IInteractable` (`GetPromptText`/`Interact`), so a new station type never needs new interactor wiring.
- **Room B — Architect DPS testing.** `ArchitectSelectStation` toggles the player into the Architect to place Rituals and test builds without a real match. Pressing it again, or walking out of the room (`ArchitectTestZone`), swaps back to the selected Sentinel. Training dummies are placed in groups of **1 / 4 / 8** across three corners, to separate single-target from multi-target and AoE testing.
- **Unlimited economy, hub-only.** `EconomyManager.Unlimited` short-circuits every mana and capacity check. `RitualPlacer.ComputeAffordableSegmentCount` checks the same flag directly, since otherwise an Ice Wall's *drag length* stayed capped even though placing it was free. Chosen over a huge starting pool so mana stays a concept that only exists inside a real match.
- **Dev toggles** (`DebugToggle`): F1 enemy health bars, F2 floating combat text, F3 swap Sentinel, F4 load a throwaway one-enemy test wave and start it immediately. Wired into the hub as well as real levels, since the hub is where they're most useful.
- **Floating combat text** is capped at a fixed count via `ActiveRegistry`, oldest evicted first — a zero-cooldown tower with chain, fork and salvo stacked could otherwise keep hundreds alive and cost over 100fps.
- **Players collide with the `Obstacles` layer.** This is a real gameplay rule, not a hub detail: `IceWallSegment` sits on that layer alone, so before this the player could walk through their own wall despite the design always saying otherwise.

**Not built:** a real environment pass, and any detail beyond a name in the selection panels — no thumbnails, stats or descriptions.

## 9. Open Questions

### Elemental reactions (design in progress)

**Departs from Genshin's automatic pairwise model.** Towers auto-fire constantly, so a reaction that triggers whenever two elements touch the same enemy becomes ambient background noise rather than a deliberate moment. Reactions instead require a **dedicated consumer** — a specific rune built to trigger off an existing element. Incidental crossfire between two vanilla elemental towers does nothing beyond overwriting the marker.

**Three deliberately distinct concepts:**

- **Status** — single-slot, whichever element hit the enemy most recently. Purely a marker for "is there something here a consumer could trigger off." Overwritten by the next elemental hit whether or not anything consumed it.
- **Ailment** — a named, duration-based effect (Burn: damage over 3s) in a list on the Enemy. Multiple stack and coexist independently, since they don't need to react with each other.
- **Reaction** — what happens when an incoming hit's element meets an existing Status. Always opt-in: it only fires because a rune on the *attacking* spell checks for it. The Status stays enemy-owned state; the decision to act on it belongs to the rune.

**Reactions are one kind of hit-time rune, not the only kind.** `OnHitRune` is the general hook — a rune that runs the moment a spell's damage lands — and `ReactionRune` is the subset that *spends* an enemy's Status, which its `OnHit` signals by returning true. `ChillRune` is the counterexample that forced the split: it reads the incoming hit's own element rather than anything already on the enemy, so it must be socketable alongside a reaction rather than competing with it for the same slot. A rune's hook receives an `OnHitContext` carrying the target, the damage packet and the casting spell's own tags.

**Hit-time runes must not mutate state other hit-time runes read.** Nothing in the loop writes `Status` — it's set once afterward from `DamagePacket.DominantElement` — and `OnHit` takes the damage packet by value, so a rune physically cannot alter the hit's damage (that's what `Rune.DamageMultiplier` is for, at resolve time). Together with the one-reaction-per-Ritual cap those make socket order irrelevant. Break the invariant and order becomes load-bearing, and would need a defined sort rather than inheriting socket order by accident.

**Each reaction is its own Rune**, reusing rune socketing rather than inventing a parallel customization layer — gated by `RequiredTags` and costing a socket, so picking one is a real build tradeoff rather than a settings preference. This also lets a single pairing have multiple competing rune options later, competing for the same slot, so a new variant is just one more `.tres`.

**Ruled out:** automatic knockback/CC on incidental double-hits (too disruptive too often given automated fire), and elemental *resistance* (same problem — a passive rule fires on every qualifying hit, not just deliberate ones).

**Wind is consumer-only** and never becomes a Status others can consume, matching Anemo. **Swirl is baked directly into any Wind-dealing spell rather than gated behind a rune** — the one deliberate exception, justified because Wind has no identity outside the reaction system at all, so a Wind spell's entire purpose already *is* Swirl.

A reaction rune with a disruptive component (knockback, stun) needs its own per-enemy internal cooldown (~5s) so a lingering enemy can't be re-triggered every attack cycle. Pure damage-multiplier reactions don't need one. **Overload** is the named candidate here — Lightning consuming a Fire Status for an AoE knockback burst — and it's precisely why that cooldown matters: it's the reaction that would be unbearable firing on every attack cycle, and viable only when rate-limited.

**Goal, not started:** at least one working option for every pairing below.

| Consumer ↓ / Status → | Fire | Water | Cold | Lightning | Wind |
| --- | --- | --- | --- | --- | --- |
| **Fire** | — | **Blister** or **Scalding Zone**, undecided (possibly both) | TBD | **Ignition** | N/A |
| **Water** | TBD | — | TBD | TBD | N/A |
| **Cold** | TBD | TBD | — | TBD | N/A |
| **Lightning** | **Ignition** — detonates latent Fire into a Burn DoT | TBD | TBD | — | N/A |
| **Wind** _(baked into the spell)_ | **Swirl** — bonus damage matching the tagged element, spreading that tag to nearby enemies | Swirl | Swirl | Swirl | — |

Blister would be a single-use mark consumed by the next non-Fire/non-Water hit for bonus damage; Scalding Zone a lingering ground patch that slows.

**`IgnitionRune` is built** and is the only bidirectional cell: it fires on Fire-hits-Lightning-Status *and* the reverse, one rune in two cells, because the two elements combusting reads the same whichever landed second and forcing a different rune per firing order would be busywork. Directionality is per-reaction, not a system rule. **Ignition doesn't spread** — propagation is Swirl's identity, and giving Ignition its own would blur that and hand one rune both a damage reaction and an AoE conversion.

**Per-element damage is `DamagePacket`** (one float per element), not a tag beside a single number — a tag can't express *how much* of a hit was a given element, which a Lightning-Rod-style Ailment redirecting a percentage of only the Lightning portion would need. `Enemy.Status` tracks `DamagePacket.DominantElement`, excluding Physical and Wind from candidacy.

**Chill is built.** `ChillRune` is an `OnHitRune` that applies a `ChillAilment` (30% slow, 3s) whenever a hit carries Cold damage, giving `Tag.Cold` its first mechanical identity beyond Ice Wall's tag-only one. It's deliberately a rune rather than a baseline property of Cold damage: a baseline would be free, always-on, and would **strobe**, since `Status` is single-slot and auto-firing towers overwrite it several times a second — the same ambient-noise problem that made reactions opt-in. Requiring a socket makes it a build decision and sidesteps the strobe entirely, since an Ailment carries its own duration independent of `Status`.

**Open, and decided together:** a hit-time effect's magnitude should derive from *its own element's* damage in that hit, never the total — so Ignition's burn should scale off the Fire and Lightning portions combined (both are participants), not off `.Total`, and its trigger should loosen from "that element is dominant" to "that element is present at all," since a player who spent a socket on a reaction should get the payoff. None of that is implemented; Ignition still gates on `DominantElement` and scales off `.Total`. Separately, for **rate-valued** effects like Chill's slow percent — which have no natural damage scale at all — the lean is that a spell *carrying the matching tag* gets full magnitude regardless of composition, while an off-tag spell dealing that element incidentally gets scaled by its share. That keeps a Blizzard chilling at full strength no matter what gear is bolted onto it, and confines the "adding a foreign element dilutes your own utility" trap to the off-tag case where a reduced payoff is the expected deal. Unreachable until damage conversion or a socket-compatibility override exists, so it's settled in principle only.

**Ground effects are their own family, independent of what spawns them.** A `GroundEffect` is persistent terrain at a location — it knows nothing about spells, so a cast effect, a rune firing off `IceWall.Destroyed`, or a reaction can each drop one in a line. `ChilledGround` is the first. A barrage like Ice Storm is deliberately *not* one: a storm is a cast in progress rather than terrain, and the only thing the two share is "find the enemies inside a circle," which is a query helper rather than a base class.

**Wet Ground (sketch only, not designed).** A puddle where Lightning damage dealt to anything inside also splashes to everything else inside, at a fraction of the value. Appealing because it gives Water an identity as *terrain* without needing the enemy-facing status effect that was rejected, and because the conduction fantasy is instantly legible. Mechanically it would be an ailment the area applies, hooking `OnHostDamaged` — the same shape as Health Link's mirror, including `notifyAilments: false` to stop the splash ping-ponging between wet enemies. Two deliberate differences from Health Link: the splash *should* set Status, since those enemies really were hit by lightning (which lets a puddle set up Ignition across a group), and it should **not** be true damage, since conduction is new damage rather than a copy of something already mitigated. **Open:** the payoff scales with crowd size, and chokepoints are exactly where a tower defense manufactures crowds — a cap on splash targets is the obvious lever if it gets silly. Needs a Water source to exist at all, so it's unreachable today.

**Shroud** is the first rune that changes how the *Ritual* is treated rather than what its spell does — enemies neither see it nor collide with it, and it renders semi-transparent to make that obvious. It's the player-side answer to towers constraining placement: rather than removing the threat, you buy freedom from it with a socket. No required tags, so it fits any spell, and the cost is purely the slot it occupies. It needed the `Rituals`/`RitualHitbox` layer split (Section 4) to exist at all, since a shrouded tower must stay shootable and repairable. **Open:** if it turns out to be an auto-include on every tower, the lever is breaking stealth when the tower casts, which keys its value off cooldown and makes a slow spell like Ice Storm a far better host than a fast one.

### Rune ideas not yet built

Every rune should hook a *tag*, not a spell class, so it applies to any future spell sharing that tag. Worth checking each idea against that explicitly.

- **Prime** — a "Primed" debuff; the next `Tag.Lightning` hit on a Primed enemy sparks a random *nearby* enemy, evaluated fresh off the struck enemy's position so no group tracking is needed. **Must be capped to once per cast** — without that it compounds with `ChainRune` worse than Fireball's Chain+Explode ceiling, which is already bounded to one shot.
- **Targeting rune** — overriding a spell's default priority. Unblocked; `TargetPriority` already exists as one field.
- **Reduced chain falloff** — blocked only by `ChainResolver.DamageFalloffPerChain` being a global const. Moving it to `ChainCastData` is a two-line change on the day such a rune exists.

**Do Reaction Runes participate in rarity tiers at all?** Their identity is enabling an interaction — a tradeoff over which socket to spend, not a number to chase — and since Common is always unlocked, including them wouldn't gate the mechanic behind RNG, only a magnitude bonus. Current lean: each tier makes the reaction ~5% stronger. Not committed, and cheap to change once built.

### Undecided design

- **Sniper vs. Assassin differentiation** — both will ship, the difference isn't designed.
- **Full scope of the elemental tag system** — more elements, and how conversion runes work.
- **Damage roll ranges** (PoE-style min/max per hit) vs. flat deterministic damage. The existing multiplicative stacking already makes exact breakpoints hard to hand-calculate, which weakens the "ranges hurt planning" argument against them.
- **Mana/capacity cost rounding.** Costs stay float through rune resolution and `EconomyManager` converts at the last step, currently rounding *up*, so 10.2 always costs 11 and never rounds down to a free fraction. Ceiling felt safer but isn't committed — revisit once real per-rune multipliers replace the flat test value.
- **Equipment mod pool** — contents, weighting, drop sourcing.
- **PoE-style currency conversion for high-difficulty loot pressure.** Drop fewer raw items on harder content and convert some into stackable crafting currency, so a player buried in duplicates can target-craft instead. Open problem: drops roll per-kill with no signal for "are we in harder content," so this needs an ambient difficulty scalar threaded down to the roll.
- **Item nameplates cluster unreadably at chokepoints.** Overlap avoidance was deliberately dropped because a distance-sorted version made labels reshuffle as the player approached, which was worse. But towers concentrate deaths into a few spots, so plates end up fully stacked and individually untargetable. A stable identity-based ordering rather than a continuously-recomputed one is the likely direction, untried. **Partly defused** by the loot-timing direction — if most drops stop rendering mid-combat, only elite and goblin drops remain to clutter.
- **Per-enemy visual markers don't read at the enemy density levels actually reach.** Both Weak-Point Visor's markers and Health Link's spikes mark individual enemies in a crowd, and picking one marked enemy out of a live wave is genuinely hard — hard enough that verifying the spike placement by eye was a problem in itself. Making the markers bigger or brighter fights the crowd rather than solving it. The likelier direction is CC that thins or slows the crowd so individual enemies are legible at all, plus leaning on cues that read through chaos (a marked enemy moving differently) over cues that need to be visually picked out.
- **Boss-tier enemies.** The time-travel premise motivates a boss archetype, but how a boss fight works mechanically in a lane-based TD isn't started.
- **Where the hub sits in time** — outside time entirely, or anchored to one era. Per-era content scope is entirely TBD and not a slice concern.
- **Multiplayer transport, and what consoles do about it.** The intended shape is Steam peer-to-peer with one player hosting, which fits both the invite-a-friend model and the host-authoritative architecture in Section 7. Consoles have no equivalent of that path, and local split-screen is the obvious answer there — but it's a substantial piece of work rather than a small addition: a second viewport and camera, per-player scoping for every fullscreen panel (the build overlay especially), and input routing per seat. Explicitly not planned right now; worth its own conversation before anything commits to it.
- **Low-poly art style and visual identity** — not urgent, but will matter for silhouette readability.

### Not implemented yet

- **Wave-end safety net** — a wave should auto-end after a timeout even if an enemy is stuck on the navmesh.
- **Navmesh re-baking on tower placement.** Placed Rituals have solid collision, but the navmesh only bakes at level load, so enemies won't route around anything placed after. The idea is to re-bake once at the end of a build phase and once when a token deploys mid-wave — not per placement (expensive) and not never (broken pathing). Note this is distinct from the wall-rebaking dead end in Section 4.
- **Non-lethal knockback.** `KnockbackForce` only applies via the on-death ragdoll launch. Knocking back a *living* enemy needs a different mechanism, since it's `CharacterBody3D`-driven rather than free ragdoll physics.
- **One `ReactionRune` per Ritual, decided but unenforced.** A Status is a single marker and a reaction is the act of spending it, so two reaction runes on one Ritual would both cash in the same Status on the same hit. Capping reactions at one per Ritual avoids that outright, and makes picking a reaction a commitment rather than something you stack. Nothing enforces it yet: rune eligibility is only `Rune.IsCompatibleWith(spellTags)`, which is per-rune and can't see the rest of the loadout. Needs a check wherever runes are attached, mirrored wherever a loadout is validated. Note this caps *reactions* only — ordinary `OnHitRune`s are still meant to combine freely.
- **Ritual line of sight and non-horizontal projectile aiming** — both confirmed needed eventually.

### Rune and spell iconography

Undecided between two directions, and not needed for the slice.

- **Procedural sigils** — anchor points evenly spaced on a circle; a name's letters map to points connected in sequence, generating a glyph per rune without hand-drawing one. Socketing overlays each rune's glyph onto the spell's, so a built tower's sigil is a compound. **A letter-based prototype was built and found lacking:** repeated letters still look cluttered even deduped, and short words often don't self-intersect, reading as a plain polygon. The technique it was based on (chaos-magic word sigils) always involved hand-restyling the plotted skeleton afterward, so raw procedural output was probably never going to look finished.
- **Functional iconography** — a small fixed set of primitive strokes tied to mechanical tags, composed per rune from the tags it touches. The player can learn to read a build at a glance instead of memorizing shapes, and primitives are reused across every rune sharing a tag rather than authored per rune. A tag-based prototype exists (anchor points are `Tag` values, so point count is designer-controlled rather than dictated by English spelling) and renders 4 candidate orderings side by side so a shape can be picked by eye. **Coded but never run or visually evaluated.**
- **Anchor count tradeoff:** vocabulary size should come from counting what's needed, not picked as a round number. More points sit closer together angularly, and past somewhere around 10–16 near-neighbor connections start reading as noise. Vocabulary size and per-glyph clutter are different knobs — a bigger vocabulary means more distinct shapes across the roster, not a busier glyph. Adding a point later reflows every glyph's angles, so reserve a few unused slots before locking this at ship time.

### Deferred cleanups

Looked at and deliberately not done, recorded so they aren't re-discovered from scratch:

- **`Spell.Tags`/`Rune.RequiredTags` allocate a fresh `List<Tag>` on every read** (expression-bodied, and `IsCompatibleWith` reads both). Should be `static readonly Tag[]`. Mechanical, ~20 files, no behavior change — it's UI-time code, not per-frame.
- **`EnemyTargeting`'s local functions capture `fromPosition`**, heap-allocating a closure and delegate per call. Roughly 150 tiny short-lived objects a second at ten rituals, which gen-0 absorbs without noticing. Removing it means an enum switch inside the loop, which reads worse for no measurable gain.
- **`ArcVisual` spawns two nodes plus a timer per segment, per arc**, and a forked chained Lightning Bolt emits a dozen-plus arcs per cast continuously. Real churn, but the whole effect is placeholder VFX — the fix (a `MultiMeshInstance3D` or pooled line renderer) belongs with the art pass.
- **A Sentinel's current health isn't reconciled when gear changes `MaxHealth`**, so equipping +Vitality mid-match leaves the orb reading `100/135`. Left alone on purpose — equipping shouldn't double as a heal. Health regen closes the gap on its own.

## 10. Roadmap

The vertical slice is complete: place towers, survive waves, win or lose, with gold, items, equipment mods, a working elemental reaction, tower disable/repair, tokens, a ranged enemy, and the hub's crafting table and vendor all real.

Next, in order:

1. **Sentinel ability kits.** The `Ability` framework, four-slot array, input actions, cooldown resolution, and the `AbilityBar` HUD row all exist, with Weak-Point Visor and Health Link built on top. Remaining: Elemental-Sapping Nanites, a Tank kit, refcounting the Weak-Point Visor so two Soldiers can run it at once, and a crosshair cue for an ability that has captured the attack binding — the ability bar's depleting sweep is a placeholder for that. Prerequisite for the per-ability-slot mod pool (Section 6).
2. **New spells.** Ice Storm is built (Section 4), which gave Cold a damage spell and made Chill reachable. A second bombardment — a meteor shower — is now pure content, since `Bombardment` carries no element of its own.
3. **Hard CC — Frozen and Electrocuted.** Both are Water pairings, both need `PreventsAction` threaded through `WalkingEnemy`'s target state machine, and both need the per-enemy internal cooldown (Section 9) — on the **enemy**, keyed by ailment type, not on the rune, or five towers each carrying their own timer permafreeze a lane. The lean is that the ailment *is* the cooldown: a hard-CC ailment stays in its slot in an inert immunity phase after its active duration ends, so nothing new has to track "recently frozen." They should differ on an axis a player can plan around — Frozen trading damage for duration, Electrocuted the reverse — or the second one is strictly worse than the first. **Open:** a stopped enemy is an obstacle, and what nav-agent avoidance does when the ones behind it pile up is unknown until it's tried.
4. **New runes** — worth pairing with authoring the missing Uncommon–Legendary tiers for the ten single-tier rune types (Section 4), since both touch the same authoring surface.
5. **New enemies** — a Flyer is the leading candidate, which is also when the "is movement style its own reusable axis" question (Section 5) needs a real answer.
6. **A new level.**
7. **Meta-progression — someday, not near-term.** Persistent Architect XP, tied to difficulty cleared rather than matches played, so grinding easy content can't outpace real progress. `PlayerSaveData` already carries `Level` and `Xp` fields and nothing reads them yet. The track would unlock new base Spells, and match rewards would scale with performance — objective health remaining, waves survived cleanly, time taken — instead of today's flat victory bonus. **Guardrail:** none of it may ever let player power outstrip the towers'; they stay the dominant source of defensive strength. Note this unlocks spells and progression only, never gear — the Architect has no equipment of his own (Section 3), so the old "XP unlocks an item slot for him" idea is dead.

Worth doing alongside these rather than blocking them, since more enemies and levels make both more likely to get hit: the wave-end safety-net timeout, and navmesh re-baking on tower placement.
