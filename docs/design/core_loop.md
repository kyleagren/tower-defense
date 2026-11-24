# Core Loop & Economy

Fixed-path maps, multiple lanes converging on one or more defendable objectives. Lane count is per-map.

**Four phases**, all implemented:

- **Planning** — the level opens with the player controlling their Sentinel, free to walk around with no time pressure. Ends on a ready check (below). Exists so a future Prep-phase timer has a deliberate player action to start counting from instead of beginning the instant the level loads.
- **Build (Prep)** — the player stays their Sentinel and places towers within a fixed capacity budget, allocates runes, and pre-crafts up to 3 tokens. Weapons and abilities still work.
- **Combat (Wave)** — the player controls a Sentinel with their own health. Tokens deploy reactively; towers otherwise can't be built or repositioned. Enemies advance toward the nearest objective and attack it until it falls or the wave ends.
- **PostGame** — after clearing every wave, the player walks over and collects what dropped. Returning to the hub is another ready check. A loss skips PostGame entirely and returns to the hub after a short delay.

**Every "move on" is a ready check** — leaving Planning, starting a wave from Build, and leaving PostGame. `start_wave` (G / d-pad right) readies you up and pressing it again un-readies you. The check passes once every player in the match is ready, or when a 90-second countdown runs out; the countdown starts when the host readies and is cancelled if the host un-readies, so a player who crashed or left can't hold everyone else in place forever. In single-player you're the whole match, so one press moves on immediately.

`MatchState` (`LevelContext.Match`, null in the hub) is the one owner of the phase, the ready check and whether the match is over. The match ends exactly once however many things try to end it, and anything spawned late reads the current phase rather than waiting on an event it missed. `WaveController` runs waves from `_PhysicsProcess`, so pausing the tree pauses them.

**Objectives.** The primary one is the mage's own shell — enemies attack it directly and it falling ends the run. Harder maps add **Energy Harvesters**, which function identically minus having a mage inside; this is how one mage covers more than one objective.

Tower capacity is a fixed ceiling for the whole match. Pacing comes from the economy, not capacity growth.

**Difficulty** has two vectors — campaign progression (map to map) and difficulty setting (replaying at Easy/Medium/Hard). Both resolve through enemy composition and timing, never flat stat multipliers, to avoid higher difficulty just feeling like bullet sponges.

**Two currencies.** Match-only **mana** buys towers and tokens, accrues passively, and can be spent to skip the death-respawn wait. Persistent **gold** is earned per mission and spent at the hub's crafting table and vendor ([items.md](items.md)).

**Income tower.** Fires a "spirit" that an enemy absorbs; the player must personally kill that carrier in time to collect bonus income, and the carrier is immune to tower damage. One spirit in flight at a time. It also doubles as the loot-goblin for the loot-timing direction below.

**Loot timing — built, though still a tentative design call.** Most loot never drops in real time.

- **The payout.** `ItemDropManager` rolls each kill's drop chances immediately but holds the results, then sprays the whole pile out of the primary objective's position (`DefendableObjective.IsPrimary`, the mage's shell) at PostGame, ordered common-to-legendary and placed on a golden-angle spiral so items separate without needing physics. The objective is freed as the spray starts — the shield defended all level becomes the reward.
- **A loss never fires the victory signal**, so queued loot simply never spawns, extending "nothing to loot after a loss" to everything.
- **Why.** This sidesteps the mid-combat "risk it or lose it" tension, the ground-clutter problem, and a future Build-timer squeeze on evaluating finds — at the cost of the moment-to-moment ARPG texture during combat, which is why it is worth revisiting if it does not feel good in play. The 40-slot inventory cap becomes a real triage decision at the payout moment rather than a slow background nuisance.
- **Not built:** the two intended live-drop exceptions — elite enemies and whichever enemy carries the income spirit — which would keep full lose-it-on-death stakes. Today every drop is queued.

**Hardcore (considered, not built).** A per-difficulty toggle rather than a difficulty of its own. A Sentinel that dies in hardcore stays dead for the rest of the wave, which makes ally revive the mode's lifeline in co-op. Hardcore also adds the Build-phase timer; the regular game has none. Loot timing follows the timer: hardcore keeps loot queued for the PostGame spray so nothing needs evaluating under time pressure, while the regular game would go back to live drops at the enemy's feet, with anything picked up kept even if the match is lost. Hardcore probably pays slightly better rewards, mechanism open. The name is open too — ARPG players read "hardcore" as permanent character death.

Minimap shows monster positions, which lane they came from, and disabled-tower status.

Co-op shares capacity and currency during a match; at the end every player gets the full gold earned, since a co-op match doesn't spawn more enemies than a solo one. It makes them tougher instead: each player past the first adds 50% to every enemy's health (`GameRules.EnemyHealthPerExtraPlayer`), so four players face 250% — otherwise extra Sentinels kill everything too fast to leave much to do. An enemy's multiplier is fixed when it spawns, so someone leaving mid-match only affects later spawns. Pause is single-player only: every player can open their own pause menu, but it only freezes the game when nobody else is in the session ([controls_and_ui.md](controls_and_ui.md), Pause menu).

**Loot is instanced per player in co-op.** Each player sees only the drops rolled for them, each rolled against their own Sentinel pick; an item becomes visible to everyone only once its owner drops it on the ground from their inventory, which is also how items are traded ([architecture.md](architecture.md), Multiplayer).
