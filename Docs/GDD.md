# Null Record: Game Design Document (working draft)

_Last updated: 2026-11-25_

## 1. One-liner
A grounded, near-future, side-on (2.5D) Metroidvania. You play a silent protagonist who wakes inside a corporate black site that has erased every record of them. You hack your way through the facility to get out and find out why. Fights are optional and play out as turn-based tactics on a grid, right where you stand in the level.

## 2. Title
**Chosen: Null Record.** The other candidates, kept for reference:

| Title | Angle |
|---|---|
| **Redacted** | The protagonist's own file has been erased |
| **Null Record** | The protagonist is a deleted entry |
| **Black Site** | Blunt and genre-clear |
| **Clean Room** | Lab term, with a double meaning: "wiped clean" |
| **Sublevel Zero** | Vertical facility, descending into the truth |

## 3. Pillars
1. **Access is progress.** The world opens through hacking tools and environmental protection, not jump upgrades.
2. **Fights are a choice.** Every combat encounter is optional and clearly marked. Engaging pays off in tools, scrap, story and XP. Skipping them is never a dead end on the main path.
3. **Grounded and quiet.** Low-poly forms under harsh, practical lighting, an industrial soundscape and no superheroics. The player is a person in a building, not a weapon.
4. **Story carries the run.** It's story-heavy within a 1–3 hour game: text boxes with portraits, three companions with arcs, and plenty of logs.

## 4. Core loops
**Moment to moment (exploration):** run, jump, climb, interact → find a locked door, terminal or hazard → use or acquire a tool → new route.

**Encounter (opt-in):** find a marked encounter → choose to engage → the local level space snaps into a grid → turn-based tactics with protagonist + one companion → rewards → the level returns to free movement.

**Macro:** explore a region → meet a companion and recruit them → gain a hacking tool or hazard protection → open the next region → story beat → escape.

## 5. Player and companions
- **Protagonist:** silent, no name on record (the game refers to them only by a subject ID). Their personality comes through dialogue choices and companion reactions.
- **Companions:** 3 in total, **one active at a time**. Swap at save terminals or safe rooms. Each is tied to one region and one combat role:

| # | Region | Combat role (draft) | Story hook (draft) |
|---|---|---|---|
| 1 | Intake / Holding | Frontline: armor, shove, guard | **Dace** (working name), a guard who stopped following orders |
| 2 | Research Wing | Support: heal, buff, reveal enemy intent | A researcher with a guilty conscience |
| 3 | Security Core | Ranged: overwatch, line attacks | A former test subject, like you |

The active companion also follows you during exploration and chimes in with dialogue and lore. They don't add traversal abilities, so gating stays in the player's tools.

## 6. Exploration and gating
Two kinds of gate, with no movement upgrades:

**Hacking / access tools**: earned from story beats and optional fights.
- Keycard tiers (colour-coded doors)
- Signal spoofer (fools sensors, cameras and laser grids)
- Remote override (operates lifts, bridges and freight shutters from a distance)
- Drone link (pilot a small drone through vents to reach far-side switches)

**Environmental protection:**
- Thermal lining (hot zones: reactor and steam tunnels)
- Filter mask (gas and contaminated areas)
- Insulated gloves (live electrical panels and flooded floors with power running through them)

Base movement is fixed from the start: walk, run, jump, crouch, ledge-climb, ladders and interact.

## 7. Combat: in-place grid tactics
- **Trigger:** only at **optional, marked encounters** (side rooms, bounties, sealed labs). Main-path progress never requires a fight.
- **Space:** the 2.5D level plane itself becomes the grid. It's a 2D grid on the side-on plane: X = horizontal position, Y = floor or platform level. Tiles come from the actual level geometry, and ladders and ledges become vertical moves between tiles.
- **Turns:** player phase (protagonist, then companion, in any order) → enemy phase. Enemy intents are shown in advance.
- **Actions:** move + one action per unit per turn. There's a small skill set per unit, cooldown-based, with no mana.
- **Hacking is not used in combat.** Combat skills are a separate set, so exploration tools and combat stay distinct.
- **Terrain:** cover (half and full), height advantage, hazards (steam vents, live wires, drop-offs), and knockback into hazards.
- **Fail state:** losing restarts the encounter from just before it. Autosave makes this low-cost.
- **Rewards (all four):**
  - **Hacking tools:** some optional tools come only from fights (they shorten routes or open shortcuts and secret areas)
  - **Scrap:** spent at workbenches on upgrades for combat skills and equipment
  - **Story info:** logs, alternate companion dialogue, reveals about the black site
  - **XP:** levels raise HP and unlock skill choices for the protagonist and each companion

### Prototype decisions (2026-11-26)
These are first-pass calls made to get the prototype playable. Revisit them after hands-on play.
- **Tiles:** 1 m columns. A tile is any surface in a column with standing headroom, so multi-floor encounters work: a platform and the floor under it are separate tiles. Level props in encounter rooms should sit on whole-metre edges.
- **Moving between tiles:** walking to a neighbouring tile costs 1, and so does dropping down (up to 4.5 m). Climbing a ledge up to 2.5 m costs 2, which matches the player's ledge climb.
- **Enemy intents target a tile, not a unit.** At the start of each player phase every enemy shows where it will move and which tile it will hit, with the expected damage. Stepping off that tile dodges the attack, and an enemy standing on it gets hit instead.
- **Damage:**
  - Height advantage (attacking from 0.9 m or more above) adds +1.
  - Half cover halves ranged damage.
  - Armour subtracts flat damage, but a hit always deals at least 1.
  - Knockback into a wall or another unit deals 1 to each; knockback off a drop of 1.5 m or more deals 2.
  - Melee reaches the adjacent column with a height difference of up to 1.2 m.
- **Outcome:** losing the protagonist loses the fight, but losing the companion doesn't. On defeat, the encounter resets to the moment before you engaged.
- **No randomness.** Results are fully predictable, which keeps the shown intents honest.
- **Prototype roster:**
  - Subject: HP 8, Move 4. Strike 2; Shove 1 + push 2, cooldown 2.
  - Guard (companion stand-in): HP 12, Move 3, Armour 1. Baton 3; Shield Bash 1 + push 2.
  - Enforcer (melee): HP 6. Stun Prod 3.
  - Sentry (ranged 2–5): HP 4. Taser Shot 2.
- **Controls:** the existing Player actions, so keyboard, mouse and gamepad all work.
  - Move moves the cursor; Jump or Attack confirms; Crouch cancels.
  - Previous and Next change the action; Sprint switches unit; Interact ends the turn.
- **Not in yet:**
  - XP
  - Skill upgrades
  - Hazards inside encounters (steam, wires)
  - Full cover
  - Turning units and skills into ScriptableObjects
  - Combat audio

## 8. Save, death and flow
- **Frequent autosave:** on room transitions, after dialogue and before and after encounters.
  - **Prototype:** one JSON save slot in the user's persistent data folder, written to a temp file first and then swapped in.
  - **When it saves:**
    - entering a room
    - any change to inventory or story flags (pickups, doors, recruiting, dialogue choices, banter)
    - right before an encounter starts
    - after an encounter is won
  - Saves wait until the player is standing in normal control, so never mid-jump, in dialogue, in combat or while respawning. They're spaced at least 1 s apart, and a "SAVING..." note shows top-right.
  - **What's saved:** position, facing, respawn point, keycard level, protections, scrap, tools, all flags and the companion in the party.
  - **How world objects persist:** each one records itself as a flag, such as `taken_<id>`, `opened_<id>` or the encounter's cleared flag, and checks that flag on load.
  - The game loads the save automatically when the scene starts; there's no title or Continue screen yet. A broken save file is set aside as `.corrupt`, and the game starts fresh.
  - Editor menu: **Null Record → Delete Save** and **Show Save File**.
- Dying outside combat (falls, hazards) respawns you at the last room entry: the first spot you stood on after entering the room, unless a checkpoint in that room set a different one.
- Manual save terminals double as companion swap points and workbenches.

## 9. Narrative delivery
- Text boxes with character portraits and a light branching choice structure (tone choices, plus a few that matter).
- Readable terminals and logs, which are the main lore channel.
- Companion banter is triggered by location.
- **Tooling (prototype):** each conversation is a JSON file in `Assets/_Project/Dialogue`, made of nodes with a speaker, a line, a `next` node or choices, and flags it sets. Choices can depend on flags (`requireFlag`, `hideIfFlag`), and EditMode tests check every file for broken links. That's enough to write in for now; Ink is still an option if branching outgrows it.
- **Presentation (prototype):** a bottom text box with a speaker portrait (a placeholder colour block with an initial until there's art), name, line and up to 4 choices. Banter shows as a small strip at the top that doesn't stop play.

### Companion 1: Dace (prototype)
- Dace waits in Maintenance next to the Level 2 keycard they dropped. Talking to them runs a short recruit conversation with one tone choice (ask who, ask what happened, or stay silent), then an offer to come along. Saying "Not yet" leaves them there, and they'll ask again next time.
- Once recruited, Dace follows a step behind. When the player climbs to another floor or gets far ahead, Dace catches up by appearing behind them; there's no pathfinding yet.
- Dace joins optional encounters as the frontline unit. Without them, the Subject fights alone.
- **Banter:** three one-off remarks while Dace is with you: at the Security door, on entering Security, and after the fight is cleared.
- **All of Dace's writing is first-draft placeholder.** The name, voice and lines are meant to be rewritten.

## 10. Art and audio
- **Art:** low-poly models, flat or minimal-texture materials, and **strong URP lighting**: volumetric shafts, emissive signage, hard shadows and colour-coded zones (cold white labs, amber maintenance, red security). Characters are simple, readable silhouettes.
- **Camera:** an orthographic or narrow-FOV perspective side view with a little depth for parallax layers. During combat it pulls back slightly and shows a grid overlay.
- **Audio:** industrial and percussive: machine hum, clanks, pneumatic hiss and rhythmic hits that build tension in combat. Each region has an ambient bed.

## 11. Platform and controls
- PC (Steam). Keyboard and mouse plus controller, through the Input System (`InputSystem_Actions`).
- Exploration: move, jump, crouch, interact and use tool (with a tool wheel).
- Combat: cursor or tile selection works with mouse and with stick or d-pad.

## 12. Scope
- **Target length:** 1–3 hours. Three regions plus a short finale.
- **Team:** solo.

### Milestone 1: vertical slice
One region (Intake / Holding), polished enough to show:
- [x] Side-on character controller (run, jump, crouch, ledge-climb, ladders, interact)
- [ ] Camera rig with follow, room bounds and parallax
- [ ] 1 hacking tool (keycard tier 1 → tier 2) + 1 environmental hazard (steam / thermal lining)
- [~] Companion 1: recruit scene, follow AI, banter (prototype: Dace; writing is placeholder, no art)
- [~] 1 optional encounter: level → grid transition, 2–3 enemy types, win and lose flow, rewards (playable prototype in Security; scrap and log rewards so far, no tool or XP yet)
- [~] Dialogue system with portraits and choices (JSON, placeholder portraits)
- [~] Autosave and room transitions (autosave and room-entry respawn done; no title/Continue menu or save slots yet)
- [ ] Lighting pass and audio ambience for the region

### Later milestones (rough)
2. Greybox all three regions and the gating map
3. Companions 2 and 3, the full skill sets and the XP/scrap economy
4. Content, writing and polish
5. Steam build pipeline, settings and accessibility

## 13. Technical plan (initial)
| System | Approach |
|---|---|
| Player controller | Kinematic `CharacterController` or Rigidbody, locked to Z = 0 |
| World grid | Grid generated at runtime from tagged level geometry inside an encounter volume |
| Combat | State machine (Explore ↔ Encounter); turn manager; data-driven units and skills as ScriptableObjects |
| Gating | `IAccessRequirement` on doors and hazards, checked against the player's inventory and flags |
| Dialogue | ScriptableObject graph, or Ink (to be decided) |
| Save | JSON save of flags, inventory, position and companion state |
| Scenes | One scene per region plus additive UI and systems scenes |

## 14. Open questions
- Should the black site go **down** (escape by descending to the truth) or **up** (climbing toward the surface)?
- How many endings? (Suggestion: 2, shaped by which companions you kept close and whether you expose the site.)
- Dialogue tooling: JSON for now (see §9). Move to Ink if branching gets heavy?
- ~~Grid tile size and multi-floor encounters~~ (prototype: 1 m tiles, multi-floor allowed; see §7)
- What the protagonist's subject ID is and why they were erased (the central reveal)
