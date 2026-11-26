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
- **Third enemy, the Warden (2026-11-26, first pass):** HP 9, Move 2, Armour 2. Its only skill is Maul (3 damage, push 1). Armour lowers normal hits to 1 (Strike 2 → 1, Dace's Baton 3 → 1), but knockback impacts, falls and hazards ignore armour. So it's a puzzle enemy: shove it into walls, onto arc grates or off the mezzanine.
- **Hazard tiles (2026-11-26, first pass):** a hazard tile deals 2 damage, ignoring armour, to any unit that **ends a move on it or is knocked onto it**. Walking through it is safe. It's a tactical tool: shove enemies onto it. The Security fight has one, an arcing floor grate under the platform's edge that's live only during the fight. It's drawn in lime on the grid, and the help line warns when the cursor is on it. Enemies avoid ending on hazards unless it's their only attack.
- **Enemy positioning (2026-11-26, first pass):**
  - Ranged enemies keep high ground while nobody is in range, rather than dropping down to close in.
  - They prefer firing spots the player's melee units can't reach next turn.
  - Every enemy weighs where it stands against the damage it can deal (100 points = 1 damage): a hazard costs 150, each threatening melee unit 40, and each column outside its range band 30.
- **XP (2026-11-26, first pass):** one **shared party XP pool** for now (the full design wants per-unit XP). Each level is 50 XP, up to level 5. Each level gives the Subject and the companion +2 max HP in later fights. The Security encounter awards 50 XP, so the first win reaches level 2. XP is saved and shows on the debug HUD. Skill choices on level-up come later.
- **Controls:** the existing Player actions, so keyboard, mouse and gamepad all work.
  - Move moves the cursor; Jump or Attack confirms; Crouch cancels.
  - Previous and Next change the action; Sprint switches unit; Interact ends the turn.
- **Not in yet:**
  - Per-unit XP and skill choices on level-up
  - Skill upgrades
  - More hazard kinds (steam timing, wires that switch on and off)
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
  - **Title screen** (`MainMenu`): Continue (shows the room and how long ago you saved), New Game (asks before erasing an existing save, and Cancel is the default), Settings and Quit. Opening the game scene directly in the editor still auto-loads the save.
  - **Pause menu:** Esc or gamepad Start freezes the game and turns off player input. It shows the current room and offers Resume, Settings, Quit to Title (saves first if you're somewhere safe) and Quit Game.
  - **Settings stub**, stored in PlayerPrefs rather than the save: master volume, fullscreen and the debug HUD.
  - A broken save file is set aside as `.corrupt`, and the game starts fresh.
  - Editor menu: **Null Record → Delete Save** and **Show Save File**.
- Dying outside combat (falls, hazards) respawns you at the last room entry: the first spot you stood on after entering the room, unless a checkpoint in that room set a different one.
- Manual save terminals double as companion swap points and workbenches.

## 9. Narrative delivery
- Text boxes with character portraits and a light branching choice structure (tone choices, plus a few that matter).
- Readable terminals and logs, which are the main lore channel.
- Companion banter is triggered by location.
- **Tooling (prototype):** each conversation is a JSON file in `Assets/_Project/Dialogue`, made of nodes with a speaker, a line, a `next` node or choices, and flags it sets. Choices can depend on flags (`requireFlag`, `hideIfFlag`), and EditMode tests check every file for broken links. That's enough to write in for now; Ink is still an option if branching outgrows it.
- **Presentation (prototype):** a bottom text box with a speaker portrait (rendered from the speaker's character model at build time; speakers without a model, like the PA, keep a colour block with an initial), name, line and up to 4 choices. Banter shows as a small strip at the top that doesn't stop play.

### Companion 1: Dace (prototype)
- Dace waits in Maintenance next to the Level 2 keycard they dropped. Talking to them runs a short recruit conversation with one tone choice (ask who, ask what happened, or stay silent), then an offer to come along. Saying "Not yet" leaves them there, and they'll ask again next time.
- Once recruited, Dace follows a step behind. When the player climbs to another floor or gets far ahead, Dace catches up by appearing behind them; there's no pathfinding yet.
- Dace joins optional encounters as the frontline unit. Without them, the Subject fights alone.
- **Banter:** three one-off remarks while Dace is with you: at the Security door, on entering Security, and after the fight is cleared.
- **All of Dace's writing is first-draft placeholder.** The name, voice and lines are meant to be rewritten.

## 10. Art and audio
- **Art:** low-poly models, flat or minimal-texture materials, and **strong URP lighting**: volumetric shafts, emissive signage, hard shadows and colour-coded zones (cold white labs, amber maintenance, red security). Characters are simple, readable silhouettes.
- **Lighting (greybox pass, 2026-11-26):** generated by `GreyboxLevelBuilder.Lighting.cs`, so tune it there and rebuild.
  - **Global:**
    - Post-processing: ACES tonemapping, bloom (threshold 1), vignette, a slightly desaturated high-contrast grade and light film grain.
    - Dark exponential fog and a dim, cool ambient.
    - A front-top fill light that throws character shadows onto the back wall.
    - A soft key light that follows the player so they read against dark walls.
  - **Holding:** cold fluorescent strips that paint pools of light on the back wall; one of them flickers like a failing tube.
  - **Maintenance:** hanging sodium cage lamps, a shadowed key light on the catwalk, and the orange glow from the steam vent.
  - **Security:** dim cold work lights, a red emergency strip, and a rotating red alarm beacon.
  - **Light budget:** only spot lights cast shadows (point-light shadows are expensive).
  - **Not in yet:** volumetric light shafts, emissive signage art, and baked lighting.
- **Third-party art, first pass (2026-11-26):** Kenney CC0 packs; see `CREDITS.md`.
  - **Characters:** one rigged Kenney mesh with idle, run and jump animations, scaled to about 1.6 m. Each character has its own skin, painted by `Tools/Art/make_skins.py`. These looks are placeholder picks, so the character calls are still the owner's:
    - **Subject:** pale jumpsuit with cyan piping and a blank ID tag.
    - **Dace:** navy security uniform with an orange armband.
    - **Enforcer and Sentry:** full helmets with red visors, so no faces. The Enforcer wears dark red armour; the Sentry is gunmetal with a red band.
  - `CharacterAnimator` drives the animation from actual movement, so it works for the player, the follower and combat units alike. The pack has no crouch clip, so crouching holds the jump's knees-bent wind-up as a pose. Combat actions are our own keyframed clips (`CharacterActions`, built on the Kenney rig), mapped by each skill's `Anim` field. Each one holds the idle pose and animates the arms and spine:
    - **Strike:** a punch (Subject's Strike).
    - **Shove:** a two-handed push (Shove, Shield Bash).
    - **Swing:** an overhead chop (Baton, Stun Prod, Maul).
    - **Shoot:** a two-handed aim with recoil (Taser Shot).
    - **Hit:** a flinch, played on whoever takes damage.

    - **Down:** knocked out. The arms fly up, the whole body tips over backwards from the feet, lies still, then sinks away. Labels hide as it falls.

    The combat lunge waits for the wind-up so the hit lands on the swing. Full-speed filmstrips (`Screenshots/filmstrip_*.png` after a test run) confirm the damage lands on the down-stroke.
  - **Weapons:** simple primitive props in the right hand, so the action clips swing them. They're laid along the forearm, pointing down at rest and forward when aiming. Dace has a baton, the Enforcer a stun prod with a glowing tip, the Warden a maul with a hazard stripe, and the Sentry a taser pistol. The Subject is unarmed.
  - **Set dressing:** Space Station Kit panels along the back wall, kit floor tiles over the whole floor, kit meshes over the walkways (catwalk, loft, mezzanine, Security platform), the climb blocks, the crates and cover, and the Security door panel, which slides with the door. It's all visual: the greybox boxes keep their colliders with their renderers hidden, so collision and the combat grid are unchanged.
  - **Portraits:** each speaking character's head is rendered from their model into `Resources/Portraits/<speaker>.png` when the scenes are built.
  - **Recorded sounds:** Kenney impact and sci-fi sounds replace the synth for footsteps, ladder clanks, hits, hurt, knockback, unit down, landing, door open and the zap. Every other sound, and the music, is still synthesised. **Mix:** when the scenes are built, the editor measures each recording's loudness against the synth sound it replaces and saves a per-clip gain to `Resources/Sounds/gains.json`. The game applies it, so the tuned mix levels still hold. It's only been checked by measurement, not by ear.
  - **Lighting screenshots:** **Null Record → Capture Lighting Shots** renders each room to `Screenshots/lighting_*.png` without entering Play mode.
- **Camera:** an orthographic or narrow-FOV perspective side view with a little depth for parallax layers. During combat it pulls back slightly and shows a grid overlay.
- **Audio:** industrial and percussive: machine hum, clanks, pneumatic hiss and rhythmic hits that build tension in combat. Each region has an ambient bed.
- **Audio (placeholder pass, 2026-11-26):** every sound is synthesised in code (`Game.Audio.Synth`) from noise, oscillators and envelopes. There are no audio files yet; each `SoundId` can be mapped to a recorded clip later.
  - **Player:** footsteps (4 variants, quieter while crouching), jump, landing (louder the harder you land), ladder and ledge clanks, and a hurt sound on death.
  - **World:**
    - Door: pneumatic hiss, motor whir and a clunk when it opens; a two-beep buzzer when access is denied.
    - Pickup: a two-tone chime.
    - Steam vent: a positional hiss loop.
  - **Ambience per room**, crossfaded as you move: fluorescent hum in Holding, machine rumble with a pump throb and distant clanks in Maintenance, and a low drone with soft alarm beeps in Security. The title screen uses the Security drone.
  - **Combat:**
    - A 120 BPM percussive loop that ducks the room ambience.
    - Swing and taser-zap attacks, hits, shoves, units going down and footsteps between tiles.
    - Short stingers for the player phase, enemy phase, victory and defeat.
  - **UI:** menu move and confirm clicks (these still play while paused), and dialogue blips for each line and bark.
  - **Mixing:** master × effects or ambience volume (both in Settings) × a mix level per sound. The audio listener follows the player rather than sitting with the camera 16 m back. Pausing freezes world audio.
  - **Listening outside Unity:** **Null Record → Export Placeholder Sounds** writes every sound to `Screenshots/audio/*.wav`.
  - **Music (2026-11-26, first pass):** one exploration bed, synthesised in code like the effects (`SoundId.MusicExplore`). It's a 32 s D-minor loop with four 8 s chords (Dm, Bb, Gm, Am): a detuned pad, a soft bass swell every 2 s, a sub drone and a sparse bell motif. It plays on the title and in game. It ducks to a quarter under the combat loop and to 40% in the pause menu, and it has its own Music slider in Settings. It's meant to be replaced or joined by composed tracks per region later.
  - **Not in yet:** real recordings, per-region music, reverb per room, footstep surface types, and an AudioMixer with snapshots.

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
- [~] Companion 1: recruit scene, follow AI, banter (prototype: Dace; writing is placeholder; Kenney-based model with a placeholder skin)
- [~] 1 optional encounter: level → grid transition, 2–3 enemy types, win and lose flow, rewards (playable prototype in Security; scrap, log and XP rewards plus a hazard tile; no tool reward yet)
- [~] Dialogue system with portraits and choices (JSON; portraits rendered from the models)
- [~] Autosave and room transitions (autosave, room-entry respawn, title/Continue and pause menus done; single save slot)
- [~] Lighting pass and audio ambience for the region (lighting and placeholder audio done; a synthesised music bed done; real sound assets still to come)

### Slice expansion (2026-11-26, first pass)
- **Records (x 56–72):**
  - A ladder up to an archive loft holds the **signal spoofer**, the first hacking tool. The main path reaches it without a fight.
  - An optional 15 scrap sits on top of the archive block, reached by a ledge climb.
- **Laser grid (x 72):** a solid barrier until you use the spoofer on it. It stays open after loading (flag `opened_grid_servers`).
- **Server Hall (x 72–90):**
  - **Second optional fight:** the Enforcer, the Sentry and the Warden, with a mezzanine (Sentry perch), low cover and two arc grates.
  - **Rewards:** 40 scrap, 50 XP and a log fragment.
  - An out-of-service freight lift marks the end of the slice.
- **Combat camera:** during a fight the camera frames the whole encounter area, pulling back if needed, rather than following the player.
- Room names, the lift and the log fragments are placeholders until the story is set.

### Region 2: the Research Wing (2026-11-26, first pass)
- **Getting there:** the freight lift at the end of the Server Hall rides to the Research Wing and back. It's in the same scene for now, offset to x 100–156, y −30. The GDD plans one scene per region, so this is a first-pass simplification. The lift text is direction-neutral, because up or down is still a story question.
- **Lift Lobby:** the arrival lift, a checkpoint, and the **companion terminal**. One companion is active at a time; the others wait beside the terminal, and using it cycles who comes along. Recruiting a new companion sends the current one to wait there. Saves remember who follows and who waits.
- **Labs:** the **remote override**, the second hacking tool, sits on a lab bench. A 7 m pit can't be jumped: the best sprint jump with coyote time and ledge reach is about 5.6 m. It's 4.5 m deep, too deep to ledge-climb out of the far side, and a ladder gets you back to the near side. The bridge controls are on the far side; with the override you operate them from across the gap, up to 10 m away, and the bridge slides out. Without it they're locked out. The bridge stays out after loading (`opened_bridge_labs`).
- **Specimen Hall:**
  - **Third optional fight:** the Enforcer, the Sentry on a platform, and the Warden on a 1.6 m raised block. Shoving it off deals fall damage, which ignores its armour. There's one arc grate.
  - **Rewards:** 50 scrap, 50 XP and a log fragment.
  - **Companion 2:** waits past the fight. You can walk past the idle hostiles.
  - **The end:** a freight shutter, also opened by the override, marks the end of the slice.
- **Companion 2:** the support role from the table in section 5, labelled **"Researcher"** for now; the name, voice, look and writing are the owner's call. The recruit lines are marked placeholder.
  - **Skills:** Static Dart (ranged 2–4, 1 damage) and **Patch**, which heals an ally (not self) for 3, range 1–3, cooldown 2. The enemy AI never uses support skills.
  - **Placeholder look:** a lab coat skin and a hand scanner.

### Polish pass (2026-11-26, from a Fable review)
- **Touch:**
  - A screen tap is no longer bound to Attack: it was confirming combat moves and double-advancing dialogue. Dialogue advances on a tap anywhere that isn't an on-screen button.
  - The controls are round, and the combat buttons sit in a bottom-centre strip.
  - With touch controls showing, the combat camera sits lower so the grid clears them.
- **Device-aware hints:** prompts, combat help, dialogue hints and the world signs read E, WASD and Space on keyboard, and USE, tap and JUMP on touch (`InputHints`, `HintLabel`).
- **HUDs:**
  - Exploration prompts and messages are on a screen canvas, with the prompt as a pill.
  - The combat banner, unit and skill bar and the log are on a canvas under the banner.
  - Units have small HP bars, and the shown HP counts down with the hits rather than jumping ahead.
- **Downed bodies:** a companion at 0 HP goes down and gets up when the fight ends. Bodies tip forwards instead of backwards when a wall is within a body length behind.
- **Cover:** standing on low cover (crates, barrels, tanks; `LowCover`) no longer gives a height advantage. Its top stays a tile. Removing it was tried, but in a one-lane side view there's no way around the cover, so it split the grid and stalemated melee.
- **Enemy cooldowns:** they now tick at the end of the player phase, so they last as long as the players' do. The enemy AI plans with that in mind.
- **Framing:** wall panels go up to the ceiling, the floor is deeper, and the camera is a little higher, so there's less black void.
- **Solid blocks:** they keep their box and get a face of kit panels; the open-frame "structure" mesh read as a table.
- **Transitions:** lift rides and respawns fade in from black.
- **Companion HP:** companions are fully healed between fights. That's a deliberate first-pass call, still to revisit after hands-on play.

### Phone polish (2026-11-26, after the owner's first phone play; first pass)
The owner's feedback was "Crouch barely moves" and "couldn't figure out combat". A Fable review produced the plan below.
- **Starting a fight:** USE works anywhere inside the arena, not only at the entry point. The prompt reads "Fight (optional): N hostiles". Engaging from deep inside puts the Subject at the entrance edge.
- **Combat clarity:**
  - **Next-step line:** one large line says what to do now ("tap a blue tile to move", "Strike the Enforcer: 2 damage (KO). To confirm, tap it again.", "Everyone has acted: tap END TURN").
  - **Refusals explain themselves:** a refused action says why on that same line ("Out of reach: move next to it first").
  - **Smaller lines:** the controls line and a turn-1/2 tip about the red `!` tiles sit underneath.
  - **Action bar:** a bottom-centre bar shows MOVE plus the selected unit's skills, with damage, range and cooldown on each button. Tap or click it. It replaces the `<` `>` buttons.
  - **Confirming on a hostile:** in MOVE mode this picks the first attack that can reach it. That's a preview; confirm again to use it. On touch, the first tap on a hostile previews the attack. A stray tap never spends an action.
  - **END TURN:** the touch button pulses once the whole party has acted. Auto end-turn was left out on purpose: it surprises players and collides with tests.
  - **Bigger labels:** unit labels and enemy intents are bigger. Intents read "next: Taser Shot 2" or "moving in". Idle grid tiles are more visible.
  - **Mouse echo:** on phones, mouse input is ignored for 1 s after a touch, in case the browser echoes taps as clicks.
  - **Retreat:** the pause menu offers "Retreat from Fight" during a fight. It resets the fight like a loss.
- **Crouch and running on touch:**
  - `crouchSpeed` went from 2 to 3.2.
  - A thumbstick past 0.35 counts as a full push.
  - On touch, CROUCH toggles; jumping or leaving player control stands you up. Jumping from a crouch stands up first on every device.
  - On touch, holding the stick fully for 0.5 s breaks into a run. Sprint was unreachable on a phone. Sprint speed and jump height are unchanged, because the Labs pit is sized from them.
- **A reason to crouch:** a 1.2 m-high crawl space under the near end of Holding's ledge block holds 10 optional scrap. Crouched height is 1.0 m and standing height is 1.8 m.
- **Visuals:**
  - The camera explores at 0.78× distance on touch.
  - Back-wall panels use darker, matte copies of the kit materials, so their grooves no longer out-shine the characters.
  - World hint text is at least size 0.55.
  - Touch buttons are more opaque, with larger labels, and the pause button is bigger.
- **Story structure:** all wording is PLACEHOLDER until the owner decides (see the open questions).
  - A non-blocking opening card on a new game: "HOLDING 03 / Subject record: no entry found. / Find a way out."
  - A top-left objective line driven by progress: keycard → laser grid → freight lift → Labs pit → freight shutter.
  - An end card past the freight shutter, with fights won, level and scrap, and Back to Title / Keep Exploring (`StoryBeats`).

### Story decisions (owner, 2026-11-26)
- **Direction: up.** The escape climbs toward the surface. The freight lift rides *up* from the Server Hall to the Research Wing. The Research Wing still sits at y −30 in the shared scene; that's only layout, and the lift fades to black.
- **Opening tone: the intercom.** On a new game, the "HOLDING 03 / Find a way up and out." card shows while the Site PA (`pa`) speaks two barks: a door-fault notice, then a "correction" saying Holding 03 is unassigned. The PA barks once more on reaching the Research Wing.
- **Logs: staff and earlier subjects both.** There are five `LogTerminal`s, all with text in `Dialogue/logs.json` (start node = log id):
  - Holding: staff shift note.
  - Maintenance: subject scratching.
  - Records: staff purge queue.
  - Labs: a subject's specimen tag.
  - Labs, east side: staff incident 41.

  Reading one sets `log_<id>`. Unread screens pulse. The end card counts logs read.
- **Behind the shutter: someone waiting.** A dark, backlit silhouette (the Subject model, blacked out) stands past the freight shutter. The end card reads "SOMEONE WAS WAITING". Who it is stays open: it has no name, face or interaction.
- **Names:** Dace and "Researcher" stay placeholders.
- **All the new wording is PLACEHOLDER** (PA lines, log text, end card) and meant to be rewritten.

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
- ~~Down or up?~~ **Up**: the escape climbs toward the surface (owner, 2026-11-26).
- How many endings? (Suggestion: 2, shaped by which companions you kept close and whether you expose the site.)
- Dialogue tooling: JSON for now (see §9). Move to Ink if branching gets heavy?
- ~~Grid tile size and multi-floor encounters~~ (prototype: 1 m tiles, multi-floor allowed; see §7)
- What the protagonist's subject ID is and why they were erased (the central reveal)
