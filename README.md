# Runewarder

_(Working title.)_

Very much a work in progress, but Level 2 is a relatively playable level.

A 3D, co-op tower defense game where you fight alongside your defenses instead of just building them. Inspired by **Dungeon Defenders** and ARPGs like **Path of Exile**: you play a hero (called a **Sentinel**) with your own weapon and abilities, and your towers (called **Rituals**) each repeatedly cast a Spell modified by multiple Runes (similar to Path of Exile's support gems).

The two biggest features of interest in the game are hopefully the many different Rune interactions as well as the elemental status system. You can combine different elements to create various reactions to make the gameplay more interesting.

The graphics are not even WIP, they are awful. I envision a stylized art style similar to Dungeon Defenders and have a few mood boards, but I am not an artist and didn't plan to improve the graphics until I had some more money under my belt or some more game to show off.

## Features

- **Active defense** - play as a Sentinel (currently Soldier or Tank) with a full ability kit, while placing and repairing Rituals.
- **Build-crafting Rituals** - combine Spells with Runes (Chain, Fork, Pierce, Nova, elemental reactions and more) to shape how each Ritual behaves.
- **Elements and reactions** - Fire, Cold, Lightning, Water and Wind statuses that combine into reactions for extra effects.
- **Loot and gear** - items with randomly rolled mods, a crafting table, vendor and stash in the hub.
- **Online co-op** - up to 4 players through friends-only Steam invites. WIP.
- **Keyboard & mouse or controller** - both will be fully supported. Currently controller support is decent but not perfect.

## How to play

### The Hub

You start in a sandbox that I've tentatively named "The Hub".

- **Room A** has the stations. Walk up to one and press **Interact** to use it:
  - **Sentinel select** swaps your hero. You can pick the Soldier or the Tank.
  - **Level select** starts a match. Level 2 is the one to play. Test level also works, but Level 1 is not set up.
  - **Crafting table**, **vendor** and **stash** are for your gear.
- **Room B** has training dummies and is where you can try out the different Spells and abilities.

Multiplayer won't work when running it locally because it uses Steam.

### A match

A match has four phases. Each one ends when every player presses **Ready**.

1. **Planning.** You can just look around and see where the lanes run and where they meet at the objective.
2. **Build.** Open the build menu and place Rituals. You're limited by mana and by the level's capacity (at current tuning you are limited only by mana). Repairs are free in this phase, and destroying a Ritual during this phase is a full refund.
3. **Combat.** Waves of enemies walk the lanes toward the objective. Kills and cleared waves earn mana. Repairs have a cost and destrying one gives a partial refund, both relative to the amount of damage the Ritual has taken. After each wave you return to Build.
4. **PostGame.** After the last wave, a pile of loot will drop. Press **Ready** to return to the hub with the spoils.

If the objective breaks, you lose, and no loot drops. This may change later. If you die, you respawn after a short wait. Press **Interact** to pay mana and skip the wait. Every death makes the wait longer and the skip more expensive.

### Building a Ritual

Press **Build menu** to open a radial menu. Pick by pointing the mouse or the stick, and confirm with a click or Controller button.

1. **Choose an element**, then a **spell**.
2. **Socket up to 4 Runes** around the spell. There are a lot of types of runes. Some enable your Ritual to cause reactions, others affect how the spell is delivered (more projectiles, chains, etc.), how it aims, how often it fires, how big the area is, and more. This should be where you get the chance to customize your playstyle the most.
3. **Place it.** Aim the ghost where you want it and confirm.

From the same menu you can **save the build as a favorite** to place it again in a few flicks. You can also **save it as a token**, one of up to 3 builds you can drop during Combat, when the build menu isn't available.

### Looking after Rituals

Look at a Ritual to get its prompts:

- **Hold Repair** to fix a damaged Ritual. During Combat this costs mana.
- **Hold Destroy** to tear it down and get mana back. The more damaged it is, the less you get back during Combat.
- **Hold Details** to see its range and stats, such as damage dealt, kills and damage per mana. Damage per mana is maybe not a very useful stat because it rewards the Rituals that have been placed the longest, but it's easy enough to leave in there. .
- **Interact** fires a Ritual with the Manual Activation rune.

A broken Ritual goes dark and stops firing until it's repaired. The **Sapper** is the enemy that goes after Rituals, so deal with it early.

### Fighting

- **Soldier:** a rifle with headshot crits. His abilities pull enemies together or push them apart, reveal weak points, link two enemies so they share damage, and take elemental Status off enemies. That last one charges Overdrive, a burst of piercing shots.
- **Tank:** a sword and a shield that can block. Getting hit builds up heat. Heat adds fire damage to his swings but lowers his armor. His abilities taunt, slam, vent heat, and finally Immolate, which makes every burning enemy nearby explode.

Elements matter. Fire, Cold, Lightning and Water leave a Status on enemies. Hitting an enemy with a second element sets off a reaction, so pair up Rituals that work together.

### Controls

| Action                         | Keyboard & mouse | Controller         |
| ------------------------------ | ---------------- | ------------------ |
| Move / look                    | WASD / mouse     | Left / right stick |
| Jump                           | Space            | Cross              |
| Sprint                         | Shift            | L3                 |
| Dodge roll                     | Ctrl             | Circle             |
| Attack / confirm placement     | Left click       | R2                 |
| Aim, block / cancel placement  | Right click      | L2                 |
| Abilities 1–4                  | 1–4              | L1 + face buttons  |
| Build menu (Build and hub)     | Q                | Triangle           |
| Tokens (Combat)                | Q, then 1–3      | Hold R1 + d-pad    |
| Interact / pick up             | E                | Square             |
| Repair / destroy Ritual (hold) | X / Z            | D-pad up / down    |
| Ritual details (hold)          | Alt              | R3                 |
| Ready up                       | G                | D-pad right        |
| Inventory / character sheet    | I / C            | Touchpad / Share   |
| Pause                          | Escape           | Options            |

## Built with

Godot 4.7 (.NET), C#, and [GodotSteam](https://godotsteam.com/) for Steam lobbies and networking.

## Documentation

- [`design_reference.md`](design_reference.md) - the full design document. Mostly written as working context for AI coding agents, with my own entries mixed in.
- [`ideas.md`](ideas.md) - spell, rune and character ideas that aren't built yet.

All rights reserved.
