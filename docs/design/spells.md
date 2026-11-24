# Spells

## Ice Wall

A destructible blockade that physically blocks a lane — enemies that reach it attack it directly rather than routing around, and it blocks the player too. It also expires after a duration cap if never broken, so it can't wall off a lane forever unattended. Either way, the casting Ritual goes on cooldown before recasting; a wall reappearing later is a fresh `IceWall`, not the original reforming.

Implemented as a real `Spell` (`IceWallSpell`) so it goes through the existing placement/economy/rune pipeline, with `WallCastEffect` as a third `CastEffect` alongside Projectile and InstantHit. Placement is three stages — wall start, wall end, then a linked tower within `Range` of the wall's line. Destroying the tower frees the wall, since the wall is parented under the tower's spawn point. Each prospective segment position is validated against the `Obstacles` layer, and the whole preview flips red if any segment is invalid. The wall auto-segments between its two points (minimum 2) so a token-sized wall can't fully block a lane.

- **Cost: the base covers the shortest wall, then each extra segment adds its own cost** — 15 for two segments, 25 for three, and so on, all scaled by the usual cost multipliers (`IceWall.ChargedSegmentCount`). A wall can't be shorter than two segments, so the shortest one costs exactly the spell's listed base.
- **All of this lives with the spell, not in the generic placement code.** `IceWallSpell.CreatePlacementFlow` returns a `WallPlacementFlow`, which owns the endpoint stages, per-segment validation, the zone the tower must sit in, and the footprint marker. `RitualPlacer` knows nothing about walls. The picked endpoints become `WallPlacementData`, which the tower keeps and which folds into every resolve of its context — the endpoints onto `WallCastData`, the charged segments into its cost — so the cast, the trigger and the cost paid all read the same wall.

- **Health is one shared pool**, not per-segment: `IceWall` owns segment health × count, any segment's damage drains it, and every segment breaks together.
- **`Expired` and `Destroyed` are two separate signals**, kept distinct so a future "explode on destruction" rune can react to one and not the other.
- **Casting is gated by a `CastTrigger` hook** rather than the usual nearest-enemy check. `WallProximityCastTrigger` waits for an enemy within range of _any point along the wall's line_ (read from `context.Wall`), so a long wall spanning lanes still triggers. The `CastTrigger` base is generic, so a future "if an enemy steps here" rune can add its own trigger; this particular one is wall-specific.
- **Cooldown doesn't start until the wall is gone** — a general mechanism, not wall-specific. `CastEffect` has an `IsOngoing` flag, and an ongoing effect calls `Ritual.ConcludeOngoingEffect()` once whatever it left behind is gone; `SpellInstance` has `BeginOngoingEffect()`/`EndOngoingEffect(cooldown)` folded into `CanCast`. `Ritual` only talks to the abstract surface and has no knowledge of `IceWall`.
- **Enemies commit to a wall** within a detection radius, path to whichever point on the wall's line is nearest their own position (not a shared center, so converging enemies don't all aim at one spot), and attack the nearest segment once in range.
- **Do not try rebaking the navmesh around an active wall.** It was tried and fully reverted: any valid detour means pathfinding walks everyone around the wall, defeating its entire purpose, and recomputing shortest paths as walls appear and vanish has no concept of "already committed to this route," so paths oscillate. Walls are pure physical blockers.
- **Known rough edge:** enemies packed densely at a wall's face visibly jitter, from many overlapping `CharacterBody3D`s plus `NavigationAgent3D` avoidance fighting over the same overlaps. Disabling enemy-vs-enemy collision was rejected — it would let enemies overlap freely and make AoE hit far more targets than intended. The likely fix is decoupling simulation position (used for AoE and targeting) from rendered position, smoothed toward it so a hard physics correction doesn't pop.

A rune that makes the wall explode on break is planned.

## Ice Storm

Cold's first damage spell, and the one that makes Chill reachable. The Ritual calls hail down on its current target: damage lands across an area over a short window rather than in one instant, and a patch of `ChilledGround` is left behind that slows anything crossing it long after the hail stops.

- **Two independent things, spawned side by side.** `BombardmentCastEffect` drops a `Bombardment` and a `GroundEffect` at the same point; neither knows the other exists. That's what lets a rune leave chilled ground off something that isn't a bombardment at all, and lets a bombardment exist without leaving anything.
- **Damage is per second, not per cast.** Ticks land on each `TickInterval` mark that falls inside the storm's duration (0.2, 0.4, 0.6 for a 0.75s storm — a partial interval at the end earns nothing), each dealing damage-per-second × interval. So a longer duration means more total damage, the way PoE treats duration on damage over time, while the tick cadence — authored on the bombardment's scene beside the visual it drives — changes the rhythm, never the damage per second.
- **The storm slows nothing.** Being caught in it is worse than crossing the leftover ground because of the damage, not a bigger slow.
- **`ChillRune` stacks with the ground rather than competing.** `ChillAilment` and `ChilledGroundAilment` are separate types, so they take separate slots and multiply: 30% and 25% resolve to a 47.5% slow inside a rune-socketed storm, decaying to 25% once only the frozen ground remains.
- **Targeting is the spell default** (`ClosestToGoal`), which aims at the enemy furthest along the path — the front edge of a pack, and so the fewest enemies. Swarm, the "most enemies nearby" targeting rune, is the natural pairing for any bombardment, and Anchor pins it to a chokepoint.
- **Visual:** pale hail streaks falling down three nested open cylinders over the area (`falling_streaks.gdshader`, shared with Rain), and frost veins over the chilled ground (`frost.gdshader`, laid out in world space so a bigger patch shows more veins rather than stretched ones).

## Water Cannon

Water's first spell. The Ritual fires a short beam of water at its target, a cylinder running out to its range: three ticks 0.3s apart, starting the instant it fires, each damaging every enemy in the beam and pushing it slightly back along it. Every tick is an ordinary Water hit, so it lands Water Status down the whole line.

- **Fire-and-forget, like a bombardment.** The beam lasts 0.6s, well inside the cast time, and the cooldown starts at once — not an ongoing cast like Ice Wall.
- **The aim locks when it fires** and doesn't track. Range sets the length, Area the width (Expand fits), and the beam stops at the first terrain along it.
- **Hits are a shape query per tick, not an `Area3D`** (`Beam`, through `BeamCastEffect`), so the first tick lands the moment the beam appears instead of a physics step later.
- **The push is small enough not to count as crowd control**, so it has no per-enemy cooldown. Several cannons on one lane could stack into a soft stall; worth watching.
- **Visual:** noise streaming along the beam reads as water rushing outward, with white foam at its peaks and brighter edges (`water_beam.gdshader`).

## Gust

Wind's first spell: a small twister that travels along the ground through every enemy in its path, hitting each once and swirling any Status it finds ([reactions.md](reactions.md)).

- **A ground sweep, not a projectile** (`GroundSweep`, through `SweepCastEffect`). It starts at the Ritual's base, heads flat toward the target and follows the terrain up and down; a wall, or a drop deeper than one step, ends it. Not being a projectile keeps Salvo, Nova, Return, Pierce and Chain off it. Other ground-wind spells are meant to reuse it.
- **It lives for a duration, not a distance**, so reach is speed × duration: Velocity (which takes Projectile _or_ the `Traveling` tag) and duration gear both push it further. Expand widens it.
- **Infusion** (`WindInfusion`, declared by the `Infusion` component and shared with Wind Blade). The first enemy it touches carrying a Status hands it that element, locked for the rest of the sweep. Every hit after is 60% Wind, 40% that element (`BaseInfusionShare`) — Wind still leads, so it keeps swirling, and on an enemy with no Status the element's share paints it, so an infused Gust leaves its element down the line. Those hits can't set off reactions: reactions belong to runes, and a Gust carries none that fit.
- **Visual:** a translucent Wind-green cone, tip down, with darker stripes spiralling up its sides and curling in on its top, so it always reads as spinning. Infused, the stripes take the element's color.
- **Damage numbers split for infused Wind only** — a hit that's Wind plus exactly one other element shows one number per color. Every other mix stays one number; the number cap means many small mixed hits would bury the screen.
- **Every machine runs the same movement against the same terrain**, so a copy follows the host's path from its starting point alone; only an infusion is sent on.

## Thunderbolt

A heavy single Lightning strike on a cooldown — enough to one-shot a Grunt. An instant hit whose arc comes down from high above the target (`InstantHitCastData.SkyStrikeHeight`) rather than out of the Ritual; the direction is purely visual. No `Chaining` tag, so Chain and Fork don't fit.

## Lightning Rail

A `Beam` with one instant tick and no push: a thin bar of lightning hitting everything along it once. Carries `Line`, like Water Cannon.

- **Visual:** one big jagged arc struck once down the length of the rail, about a kink a metre, held for 0.25s (`BeamArcs`, `lightning_rail_arc_visual.tscn`). Thicker and brighter than Thunderbolt's and less jittery, so it reads as a rail rather than Lightning Bolt's thin arc. A shader drawing crackling bolts on the beam's cylinder was tried and didn't match the rest of the game's lightning, which strikes once rather than crackling.

## Hydro Blade

Two Water hits on one target a short gap apart, drawn as an X.

- **Its own delivery** (`SlashCastEffect`). Later hits wait on a physics-step timer that pauses with the game, and are dropped if the target is gone by then.
- **Each slash is drawn in front of the body**, pushed out by its capsule radius, from its upper end down, tapered to a point at both ends (`slash.gdshader`). Every slash holds until the last has drawn, so the whole X clears at once.

## Ice Spear

A long projectile that pierces two enemies on its own, stacking with Pierce — the first non-round projectile ([architecture.md](architecture.md), Projectile shapes).

## Fire Burst

A tight 4×4 grid of small fire pellets at very short range, built to dump every pellet into one big target up close.

- **The grid is `ProjectileCastData.Rows`:** each projectile direction fires a column of pellets. Salvo therefore adds columns, and Nova makes a ring of them.
- **Pellets vanish at 3× the range from where they launched** (`ExpiryRangeMultiplier`) — room for a Chain jump, but no stray hits far down the lane.

## Tesla Coil

Arcs lightning from the Ritual to several enemies at once, in a few quick pulses, then a long cooldown.

- **Its own delivery** (`PulseCastEffect`). Targets are re-picked every pulse through `EnemyTargeting.FindTargets` — `FindTarget` run N times, excluding each pick from the next — so a kill hands its share to the next in line. A destroyed Ritual's remaining pulses do nothing.
- **It targets at random by default.** A targeting rune overrides that, and its damage bonus lands on every arc's target, since each was picked by the priority.
- **The arcs bow upward** (`ArcVisual.ArcHeight`), thinner than Lightning Bolt's.

## Lightning Strike

Strikes every enemy carrying Lightning Status within a radius of its target, all at once, each with its own bolt from the sky; enemies without the Status are left alone. It pays off beside anything that marks a crowd with Lightning — Tesla Coil, Lightning Rail, a chaining Lightning Bolt.

- **Its own delivery** (`StatusStrikeCastEffect`), aiming by default at enemies carrying Lightning first, through the same status-first priority the status targeting runes use.
- **It only fires when there's something to strike.** `StatusInRangeCastTrigger` holds the cast until an enemy in range and in sight carries the Status — inside its spot, for an Anchor Ritual. Each strike re-sets Lightning Status, which changes nothing.
- A thinner sky-arc than Thunderbolt's, so Thunderbolt keeps feeling the biggest.

## Meteor

Marks the ground where its target stands at cast time, drops a meteor onto it over a short delay, and lands one big Fire blast on whatever is inside the circle **when it lands**. Hard to land on purpose: Rituals that slow, freeze or wall the pack are what make it pay, and it's fine with hitting the third enemy in line instead.

- **Its own delivery** (`ImpactCastEffect`, placing a `DelayedImpact`). Each machine runs its own countdown, falling rock and landing from one message, which carries the casting Ritual's position so every screen sees the same approach; only the host deals the damage. The blast throws corpses outward and up.
- **The fall.** The rock comes in 35° off vertical, over the Ritual that cast it and heading away from it, so it reads as thrown from there. It starts slow and slams down (eased in), tumbles the way it travels, and carries an orange light that brightens as it nears, so the ground lights up before anyone looks up. Embers and smoke trail behind it in the world and finish on their own after it lands. The meteor is three-quarters the blast's radius and comes to rest on the spot as it lands.
- **The marker** (`meteor_marker.gdshader`) shows the blast's full edge from the start — where — and a fill growing out to meet it, brightest at its front — when — fading in over the first part of the fall.
- **The landing** (`MeteorLanding`): the dissolving explosion, a ring of fire racing out across the ground past the blast's edge with a scorch left behind that fades over 6s (Immolate's `ground_fire.gdshader`), embers and glowing rock debris thrown up, a flash lighting the area, and camera shake for anyone within 35m (`CameraShake`, below).
- **The rock** is a churning lava sphere (`meteor.gdshader`, two scrolling noise reads mapped from dark crust through orange to cracks bright enough to bloom, with a hot rim and a lumpy surface).
- **Camera shake** (`CameraShake`, a child of each Sentinel's camera) moves the view through the camera's offsets rather than moving the camera, so aim — rayed from the camera's position — never shakes. Impacts add trauma that falls off with distance and decays on its own; the shake goes as trauma squared, so small hits stay subtle. Anything heavy (Shield Slam, Immolate, a Brute's stomp) can call `CameraShake.FromImpact`. Off with the Options toggle.

## Rain

Zero damage: an area that sets Water Status on every enemy inside it, the moment one steps in and every second after. It sets the Status directly (`Enemy.SetStatus`, the counterpart to `RemoveStatus`) rather than landing a hit, so nothing reacts to it.

- **Always up by timing alone.** The field lasts exactly its cast time plus cooldown, so each new one lands as the last ends, re-centered on the current target — it follows the pack. Overlapping rain is harmless.
- **Its own delivery** (`FieldCastEffect`): a spell whose whole effect is the ground it leaves. Ice Storm's chilled ground goes through the same spawn helper.
- **Visual:** a faint puddle and a sparse, slow drizzle (`falling_streaks.gdshader` with rain settings).

## Black Ice

A slick dark patch where each enemy on it that isn't already down rolls a chance, on the patch's own one-second tick, to slip and be **knocked down** (`KnockedDownAilment`) — no moving, turning or attacking for a couple of seconds, its model tipped over. No roll on stepping on: the first comes on the patch's next tick.

- **No per-enemy cooldown** — the deliberate exception to hard-CC rule in [reactions.md](reactions.md) — so one enemy might go down repeatedly and another cross untouched. That variance is its identity next to Freeze and Electrocute.
- **Timed and targeted like Rain**, lasting its cast time plus cooldown so it's always up, re-landing on the pack. Anchor pins it to a chokepoint. A broken Ritual stops casting new patches; one already down runs out its time.
- **Visual:** the frost shader in dark, glossy colors.

## Wind Blade

A wide crescent of wind that cuts through three enemies before stopping, infusing like Gust (each blade on its own, so Salvo's blades can pick up different elements) and swirling like any Wind hit.

- **A crosswise projectile** ([architecture.md](architecture.md), Projectile shapes), so Pierce, Salvo, Chain and Return apply, Expand widens it, and Bearing fits.
- **Visual:** a solid crescent built in code (`CrescentBlade`: a lens-shaped cross-section swept across the span, bowed forward in the middle, pinched to points at the tips, built once per shape and shared), translucent green with a brighter rim (`wind_blade.gdshader`). Infusion tints it.

## Wind Blast

A short, wide gust out from the Ritual toward its target — 3.5m long, 4m wide — that hits everything in it once for a little Wind and shoves it straight away along the blast. Placed beside a lane and aimed across it (Bearing fits), it can tip an enemy off a nearby edge, but it slides them rather than throwing them.

- **A one-tick `Beam`**, like Lightning Rail: instant, stopping at the first terrain, with Expand widening it and Extend lengthening it.
- **The push** is flat, 2000 kg·m/s divided by mass like any knockback — about 4m on a Grunt, a little over 1m on a Brute. Meant to beat Shield Slam once a player builds a Ritual around it, without needing one early. Several on one lane could chain into a stall; worth watching.
- **It doesn't infuse**, unlike Gust and Wind Blade, but every hit is Wind, so it swirls.
- **Visual:** a translucent wind-green block the size of the blast, fading out over 0.25s (`wind_blast.gdshader`, fed the blast's age through `Beam.FadeMesh`). Streaks rushing along it were tried and read worse than the plain block.

## Spells that never hit

Rain and Black Ice deal no damage, so `Spell.DealsHits` is false on both and every on-hit rune — Chill and all the reactions — refuses them; they'd otherwise sit socketed and silent.

## Planned spells

Grouped by the system they share, so each new system is paid for once. Worked roughly in this order.

**1. Forced movement.** Enemies whose position something else controls for a while — carried, held aloft, swept. Each may cap how many enemies it holds.

- **Tornado** — a `GroundSweep` that picks enemies up and carries them to the end of its path.
- **Tempest** — summoned in one spot, swirls everything there in the air, then drops it. Very long cooldown.
- **Tsunami** — a `GroundSweep` that pushes back enemies below a mass threshold and damages them.

**2. One-offs.**

- **Flamethrower** — a `Beam` that stays active and ticks longer than Water Cannon, with no push. A `Line` spell.
- **Frozen Orb** — a slow traveler in the `GroundSweep` family rather than a projectile. It pulses damage at intervals, slows down once among enemies, and stops at walls like everything else.
- **Force Field** — protects Rituals in an area; enemies that attack one are knocked back and take Lightning damage. The first spell that reacts to a Ritual being hit. Niche by design: one Force Field against Shroud on four Rituals.
