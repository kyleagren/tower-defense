# Spell ideas (all names WIP)

Some of these may be better suited to the kits of possible future characters. Crystallize for example might be a better fit on a character.
The more I think about it the more I'm fairly confident that enemies will need to be able to step over eachother / not collide. Might REALLY benefit strategies that try to group enemies up though. Perhaps some kind of square root scaling above X targets could become necessarily for some spells, similar to how WoW does it.

## Fire

- **Meteor** - calls a flaming meteor from the sky that picks a location and then falls at that location after a delay.
- **Magma Floes** - summons magma in an area at the feet of enemies, causing enemies who walk through it to be slowed and take low fire damage while they remain in it.
- **Fire Burst** - shotguns a bunch of fire pellets. Has fairly short range.
- **Flame Aura** - burns away the rituals own life force to summon a phoenix that deals a percentage of its health to enemies around it each second. Once it becomes disabled, it will automatically revive itself after a time, also reviving the phoenix tied to its own health pool.
- **Flamethrower** - shoots a gout of flame ahead in a direction.

## Cold

- **Ice Spear** - fires a longer projectile that has piercing behavior by default.
- **Frozen Orb** - Fires a slow moving ball that pulses at set intervals. It ignores collision, but is duration based. It travels faster until it collides with an enemy, and then it slows down (but keeps moving in that direction).
- **Crystallize** - encase an enemy in ice, causing it to be frozen solid. Its friends will attack it and it will shatter on death for a portion of its max health. the target can unfreeze if not killed in time. long cooldown.
- **Black Ice** - a field of ice on the ground that every second, runs a random number against each enemy on its surface. If they fail the check, they would fall over and lay on the ground for a couple of seconds before getting up.

## Lightning

- **Lightning Rail** - Shoots a medium range bar of lightnig that hits all enemies in its path
- **Force Field** - protects any rituals in a set area, causing enemies that attack those rituals to be knocked back and take lightning damage.
- **Thunderbolt** - a heavy damage strike from above targeting a single enemy.
- **Tesla Coil** - Every so often, the ritual "activates" arcing damage out from itself to enemies in the vicinity. Long cooldown.
- A spell that calls down a lightning strike on every enemy in an area that is already affected by an electric status.

## Water

- **Protective Shell** - searches for nearby sentinels and casts a water shield on them, which either absorbs some amount of damage or grants some amount of damage reduction
- **Healing Rain** - Can pick a location for the turret to rain soothing water down, healing for a small amount any entity that sets foot inside it. Would tick a small amount of healing per interval.
- **Tsunami** - A big wave of water that travels down the lane, pushing back enemies below a certain mass and dealing damage to them
- **Rain** - A 0 damage spell that just applies water status periodically to enemies in its area.
- **Hydro Blade** - two quick water slashes in an x-like shape that deal 2 instances of damage to a single target in medium range. If both of them consume a status, the damage is tripled or something maybe? don't need that piece but could be interesting.
- **Drown** - Picks up an enemy off the ground and surrounds them in a bubble or cube of water locking them in place and causing them to take damage over time for the duration.
- **Whirlpool** - pulls enemies towards its wide center and deals low damage every tick.
- **Water Cannon** - Shoots a beam of water at a row of enemies. Slightly pushes them all back 3 times over the duration of the cast and deals damage 3 times. could be similarly tick based.

## Wind - I would like instances of these to be a specific air color, but be able to change color based on the first element they pick up and have their damage type change to that element they first encounter. Much like Anemo in Genshin.

- **Gust** - Shoots a twister in a line, small and doesn't pick any enemies up. Passes through enemies but causes them to flare up with the swirl-equivalent reaction.
- **Tornado** - A stronger tornado that would travel a shorter distance but would pick enemies up and carry them with it to the end of its path. Genshin Traveler q has a similar vibe.
- **Tempest** - A very strong tornado that gets summoned in one spot occasionally, picking up all the enemies in that spot and swirling them around in roughly the same spot, and then dumping them back on the ground. Genshin Venti has a similar functionality, but not visual.
- **Wind Blast** - A strong burst of wind that pushes enemies away from it.
- **Wind Blade** - A line of wind that cuts through enemies. Probably a wide projectile that pierces a few enemies by default.
- **Squall** - a field that slowly pushes against enemies in the area, slowing their movement speed. We'd have to figure out a way to set the "push" direction. maybe something similar to ice wall, just in the sense that there's an area placement and then a ritual placement. Would only actually swirl a couple times over its duration.

## Physical

- **Earth Wall** - Similar to Ice wall, but is constantly there and cannot be destroyed.
- **Earthquake** - A wide area on the ground shakes, knocking down all enemies in the area. Long cooldown.

## Multi-Element or element unknown

- **Cataclysm** - Rains fire, hail, lightning, and water over an area. Very long cooldown. Smaller meteors, ice crystals, vertical lightning bolts, and rain.
- **Spirit tower** - Posesses an enemy with a spirit, rendering them immune to tower damage but granting extra drops.

# Rune ideas

## Targeting Runes

- A rune that targets the highest health monster it can reach (maybe a current health and max health variation)
- A rune that tries to get a killing blow on a low health enemy.
- A rune that targets a flying enemy
- A rune that targets the closest enemy to the ritual
- A rune that targets the furthest enemy from the ritual
- A rune that targets the furthest enemy from the nearest objective.
- A rune that targets enemies with a status. Perhaps a per-element variation, or perhaps not.
- A rune that causes a tower to fire in a fixed direction. Incompatible with some types of spells.

## Other Runes

- A rune that causes a broken wall to explode on impact. Perhaps for a percentage of the health it has.
- A rune that does more damage to enemies below half hp
- A manual activation rune. Gives 100 more damage, 100 more cooldown, and stops the ritual from automatically triggering. A player can walk over to it and press E to cause it to automatically cast following it's standard (based on runes / targeting runes) behavior. Could let you put a big "cataclysm" ritual somewhere and not use its long cooldown until you really need it. the spirit carrier ritual could use the same "press e to activate" mechanism to spawn the spirit.

## Tradeoff Runes

Concentrated effect-style runes that give a good chunk of damage but come at other tradeoffs.

- Concentrated effect where the area of the tower shrinks but it gains damage.
- Similar one that shrinks the range of the tower.
- Similar one for duration.
- Similar one for cooldown recovery
- Splinter burst - if this tower kills a frozen enemy, that enemy explodes in a burst of AoE

## Reaction Runes

| Consumer ↓ / Status → | Fire          | Water       | Cold    | Lightning   | Wind |
| --------------------- | ------------- | ----------- | ------- | ----------- | ---- |
| **Fire**              | —             | Blister     | Melt    | Ignition    | N/A  |
| **Water**             | Scalding Zone | —           | Freeze  | Electrocute | N/A  |
| **Cold**              | Frostburn     | Freeze      | —       | Shatter     | N/A  |
| **Lightning**         | Ignition      | Electrocute | Shatter | —           | N/A  |
| **Wind**              | Swirl         | Swirl       | Swirl   | Swirl       | —    |

- **Ignition** - Fire and Lightning combust into a Burn DoT. Bidirectional.
- **Blister / Scalding Zone** - undecided, possibly both. Blister: a single-use mark consumed by the next non-Fire/non-Water hit for bonus damage. Scalding Zone: a lingering ground patch that slows.
- **Shatter** - executes an enemy left below a health threshold. Bidirectional for now.
- **Swirl** - baked into every Wind spell rather than a rune. Bonus damage matching the tagged element, spreading that tag to nearby enemies.
- **Melt** - Deals %max hp damage. Less effective against bigger enemies. per-enemy cooldown.
- **Frostburn** - Deals Cold damage over time to the enemy, slowing them slightly. Less damage than ignition but with a slight slow.
- **Freeze** - Freeze the enemy solid, rendering them unable to move or attack for a duration. per-enemy cooldown.
- **Electrocute** - Finds nearby enemies with the opposite (of water/lightning) status effect on them and apply electrocute to them. They "convulse" from electricity and are rendered unable to move or attack for a duration. Not as long as being frozen as a trade-off for spreading a bit more easily. per-enemy cooldown.

# Character ideas

## Chronomancer

- **Stasis** - crowd-controls enemies in an area.
- **Haste / Slow** - tap speeds up enemies in an area, hold slows them down.
- **Accelerate** - an area where Rituals inside have their cast speed sped up by a lot.
- **Shorten Lifespan** - enemies in an area have their max HP clamped by a certain amount.

Notes:

- Speeding enemies up needs a payoff. One option: speed up the back of a pack so it catches the front and bunches up for AoE.
- Shorten Lifespan pairs with Shatter's execute threshold and anything that scales off percent health. Bosses would need a rule.
- Accelerate overlaps with the Haste rune and the planned Buff Totem, so it needs to stack differently or feel distinct.

## Monk / Martial artist

- Right click could enter a "projectile catching state" and a certain number of those projectiles could float around him and then holding right click and left clicking would throw one of those projectiles back at an enemy. or it could function almost like a parry where you have a short window to actually throw it back. if we did that we'd probably want a short window where it would cool down between uses.
