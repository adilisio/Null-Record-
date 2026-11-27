# Null Record: handoff

_Last updated 2026-11-27, end of a long session. A new session should read this first, then `CLAUDE.md` (conventions) and `Docs/GDD.md` (design, and every first-pass decision with its reasons)._

- **Play it:** https://adilisio.github.io/Null-Record-/ (desktop browser, or Android Chrome with touch controls). It's live and current as of commit `624b961` (weapons and zoom).
- **Repo:** https://github.com/adilisio/Null-Record- (public). `main` holds the source and everything is pushed; `gh-pages` holds only the web build.
- **Status:** 135 automated tests passing (70 EditMode, 65 PlayMode). The working tree is clean.

---

## 1. How the owner works

- **Remote and hands-off.**
  - They're almost always on Claude Code's Remote Control from another device and can't click anything on the PC. Admin (UAC) prompts, computer-use approvals and sign-in windows all stall, so never plan work that needs one.
  - They may not see the app's next-step box or buttons. End every turn with a plain-text summary, with any question in the message itself as numbered choices.
- **Autonomy.**
  - Their words: "don't ask permission", "handle it yourself". Make reasonable calls, record them in `Docs/GDD.md` marked first-pass or placeholder, and keep going.
  - Ask only when the choice is really theirs: story and characters, publishing somewhere new, or spending money.
  - They gave blanket OK to download free CC0 assets ("Don't ask for permission"); Kenney packs came in that way.
  - Safety blocks still apply. A scripted fetch of Kenney's download API was blocked; using the site's own Download button in the built-in browser worked, so don't route around blocks.
- **Git:**
  - Work and commit **straight on `main`**, with no feature branches ("Just work from main").
  - Commit after each feature or slice. Pushing finished work and republishing the web build is expected.
  - End commit messages with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **They follow along through files.** Send screenshots (`Screenshots/*.png`) and WAVs with SendUserFile, and give short progress notes during long jobs. **Always put a time limit on Unity batch runs**; once a run hung for hours.
- **The end-of-chunk rhythm:** they pick the next feature from 2 or 3 numbered options. They also like a second opinion: "Ask fable to advise" meant spawning an Agent with `model: "fable"` to review the project. That review drove the last polish pass (section 6).

## 2. Machine and environment

- **Machine and editor:** Windows 11. The repo is at `C:\Projects\new-unity-game`. Unity is **6000.6.7f1**, at `C:\Program Files\Unity\Hub\Editor\6000.6.7f1` (read-only without admin).
- **Licensing:** the Store Unity Hub is broken. **Start the unpackaged Hub before any Unity batch run:** `%LOCALAPPDATA%\UnityHubPortable\Unity Hub.exe`. It may already be running; check sign-in with `%LOCALAPPDATA%\UnityHubPortable\unity.exe auth status`. The "Access token is unavailable" line in logs is harmless noise.
- **Build support:**
  - WebGL and Windows only.
  - Android needs an admin install at the PC, and the owner chose web-only for now.
  - A spare 15 GB editor copy at `%LOCALAPPDATA%\UnityEditors\6000.6.7f1-android` can be deleted.
- **Tools:**
  - The `gh` CLI is signed in as `adilisio`.
  - Python 3 with PIL is available (used by `Tools/Art/make_skins.py`).
  - Git LFS handles binaries (FBX, PNG, OGG).

## 3. Workflow

| Task | Command | Notes |
|---|---|---|
| Compile check (fast, no Unity) | `sh Tools/CompileCheck/check.sh` | After every script change. Covers Runtime and Editor, **not test assemblies** |
| Full verification | `powershell -ExecutionPolicy Bypass -File Tools/unity-batch.ps1 test` | About 8 min. Rebuilds the scenes, then runs the lighting shots, WAV export, and EditMode and PlayMode tests. Results go to `Logs/batch/{edit,play}.xml`; screenshots go to `Screenshots/` |
| One suite only | `Unity.exe -batchmode -projectPath … -runTests -testPlatform PlayMode -testResults Logs/batch/play.xml` | Wrap in `Start-Process` + `WaitForExit(540000)`. Fine when only runtime code or tests changed, since the scenes don't need a rebuild |
| Publish to the web | `unity-batch.ps1 web`, then `sh Tools/publish-web.sh` | About 2–4 min. Then wait for `gh api repos/adilisio/Null-Record-/pages/builds/latest --jq .status` to say `built`, and check the live URL in the built-in browser (Enter on the title starts New Game; clicks don't always register) |

**Hard-won rules:**
- **Scenes are generated.**
  - **Null Record → Build All Scenes** (run by both batch jobs) rebuilds `MainMenu.unity` and `Greybox_Intake.unity`.
  - It also rebuilds the character prefabs, skins and portraits (`CharacterArt`) and the sound gains (`SoundTuning`).
  - Edit the builders (`GreyboxLevelBuilder*.cs`, `MainMenuBuilder.cs`), never the `.unity` files. Rerun `python Tools/Art/make_skins.py` first if skins change.
- **Commit the scenes after a test run** that changed a builder.
- **After a `web` run, discard the churn with `git checkout -- Assets`.** The rebuilt scenes and assets only reshuffle IDs then.
- **Don't edit live files during a batch run.** Each step (scenes, shots, wav, edit, play) is a separate Unity launch that recompiles, so mid-run edits leak in. Draft in the scratchpad and apply afterwards.
- **Apply big edits with Python patch files written via the Write tool.** Long inline heredocs in the Bash tool sometimes fail with "unexpected EOF". The patch style used all session is assert-the-anchor-exists, then replace once, keeping each file's CRLF or LF.
- **Waiting on runs:** run the batch job in the background, and use a Monitor `until grep …` loop on its output to be woken when it finishes.
- **Test gotchas:**
  - Tests run at 60 fps; use unscaled time for waits. `WaitForEndOfFrame` never fires in batch mode.
  - Check audio by counters, not output. Tests use a scratch save folder.
  - **Find scene objects by name**, because the level has several encounters, companions and gates: `SecurityEncounter`, `DaceNpc` and `IdleHostilesBetween` in `GreyboxTestBase`.
  - After a test teleport, give the camera about 1.5 s before taking a screenshot.
  - `TouchControls.ForceVisible` shows the touch UI. A simulated touch must call `TouchControls.ResetTouchSeen()` afterwards.
- **Editor-mode lighting shots** (`lighting_*.png`) show the Subject and the panel edges in odd palette colours. PlayMode captures and the game render correctly, so judge looks from PlayMode shots.

## 4. What exists

Everything below is greybox plus CC0 placeholder art. "Placeholder" means it's meant to be replaced.

**The route (one scene).**

| Room (x range) | What happens |
|---|---|
| A Holding (−2–20) | Spawn. Jump crate, ledge-climb block |
| B Maintenance (20–40) | Ladder to a catwalk (Thermal Lining), the steam vent it protects against, the L2 keycard, **Dace** (companion 1) |
| C Security (40–56) | Behind the L2 door. **Fight 1:** Enforcer and 2 Sentries, a platform, crate cover, one arc grate |
| D Records (56–72) | A ladder to the loft holds the **signal spoofer**. Optional scrap on the archive block. A laser grid the spoofer opens |
| E Server Hall (72–90) | **Fight 2:** Enforcer, Sentry, **Warden**. Mezzanine, two arc grates. The freight lift goes down to region 2 |
| F Lift Lobby (100–112, y −30) | Arrival. **Companion terminal**: one companion at a time, the other waits here |
| G Labs (112–132) | The **remote override** on a lab bench. A 7 m pit (unjumpable, checked by test) whose bridge you work from across the gap. A ladder out of the pit |
| H Specimen Hall (132–156) | **Fight 3:** Enforcer, Sentry, and the Warden on a raised block. **"Researcher"** (companion 2, support) waits past it. A freight shutter (override) marks the **end of the slice** |

**Systems.**

| Area | State |
|---|---|
| Player | Run, sprint, jump (coyote time and buffer), crouch (pose), ladders, ledge grab and climb, interact |
| Combat | Optional, in-place grid tactics (`Combat/`). Deterministic. Telegraphed intents target a tile. Height bonus (none from low cover), half cover, armour, knockback with impact and fall damage, hazard tiles. Move plus one action, cooldowns (enemies' tick like players'), heal skills. Wins give scrap and party XP (+2 max HP per 50 XP). The camera frames the whole fight. Units have HP bars |
| Enemies | Enforcer (melee), Sentry (ranged 2–5, holds high ground, avoids melee reach), Warden (armour 2; beaten with shoves, hazards and falls) |
| Companions | Dace (frontline: Baton, Shield Bash) and Researcher (support: Static Dart, Patch heal), both with working names and placeholder writing. Followers, banter (Dace only, only while following), the swap terminal, downed pose at 0 HP, fully healed between fights |
| Tools and gates | Keycard L2 (door), Thermal Lining (steam), signal spoofer (`LaserGrid`), remote override (`RemoteConsole`: long-range bridge and shutter). Freight lift (`FreightLift`, fades in) |
| Saving | Autosave on room entry, progress, and before and after fights. Room-entry respawn (fades in). One slot. Saves who follows and who waits at the terminal. PlayerPrefs on the web |
| UI | Title, pause, settings (master, effects, ambience, music, fullscreen, debug HUD). Screen-canvas HUD (prompt pill, messages), combat HUD (banner, skill bar, log), dialogue box with portraits rendered from the models. Device-aware hints (`InputHints`, `HintLabel`) |
| Touch | On-screen stick and buttons (round), driving a virtual gamepad. Combat buttons in a bottom-centre strip. A screen tap no longer counts as "confirm" (it double-fired) |
| Art | Kenney CC0 characters with our skins, keyframed actions (Strike, Shove, Swing, Shoot, Hit, Down; `CharacterActions`) and primitive weapons. Space Station Kit dressing over greybox colliders; solid blocks get a panel face. See `CREDITS.md` |
| Audio | 28 sound ids. 10 are recorded Kenney clips (`Resources/Sounds`, loudness-matched by `gains.json`); the rest, plus ambience, the combat loop and the music bed, are synthesised (`Audio/Synth.cs`) |
| Platforms | Windows editor and WebGL (GitHub Pages). No Android APK |

**Code map:**
- **Runtime:** `Assets/_Project/Scripts/Runtime/{Player, World, Combat, Companions, Dialogue, Save, Audio, UI, Core, CameraSystem}`.
- **Level builders:**
  - `Scripts/Editor/GreyboxLevelBuilder.cs`: rooms A–C, shell, camera zones, actors.
  - `.Expansion.cs`: D, E.
  - `.Research.cs`: region 2.
  - `.Lighting.cs`, `.Props.cs`: dressing.
- **Other editor tools:** `CharacterArt.cs` (prefabs, controller, portraits), `CharacterActions.cs` (authored clips), `SoundTuning.cs`.
- **Tests:** `Assets/_Project/Tests/{EditMode,PlayMode}`.

## 5. Open questions (the owner's call)

- **Story and characters:** don't invent them in code.
  - Dace's real name, voice and lines.
  - Companion 2 ("Researcher"): name, voice, look and lines. The owner said "keep placeholder" for now.
  - The protagonist's subject ID and why they were erased.
  - Who the figure waiting past the shutter is. The escape goes **up** (decided 2026-11-26).
  - The endings.
- **Hand-tested once:** the owner played on a phone once (2026-11-26), before real-time combat. Real-time combat, weapons and the wider camera haven't been played by hand yet. Next, ask how fights and switching weapons feel on the phone.
- **Story wording:** the opening card, objective lines and end card are placeholders (`StoryBeats.cs`). They avoid naming anyone, and say nothing about the facility's direction or why the Subject was erased.

## 6. This session, in order

All commits are on `main`.

| Step | What changed |
|---|---|
| Combat tuning | Hazard tiles, smarter ranged AI, party XP and levels |
| Music | A synthesised 32 s music bed, plus a Music slider |
| Real assets | Kenney characters, kit dressing, recorded sounds, portraits, `CREDITS.md` |
| Slice expansion | Records, laser grid and spoofer, Server Hall, the Warden, combat camera framing |
| Art polish | Kit meshes over the greybox, crouch pose, loudness matching |
| Animation | Keyframed attack, hit and knockout clips, hand weapons, filmstrip timing test |
| Region 2 | Research Wing, freight lift, remote override and bridge, companion 2 with heals, swap terminal, save roster |
| Fable polish review | Touch tap fix, device hints, screen HUDs, HP bars, downed companions, cover height rule, enemy cooldown fix, fuller framing, solid blocks, fades |
| Phone polish (`b7d44dd`) | After the owner's first phone play ("crouch barely moves", "couldn't figure out combat"): next-step combat line, action bar, tap-an-enemy-to-attack, engage anywhere in the arena, END TURN pulse, Retreat; crouch 3.2 and a toggle on touch, auto-run on touch, crawl space in Holding; closer touch camera, dimmer back wall; opening card, objective line, end card (`StoryBeats`, placeholder text). See GDD "Phone polish" |
| Story pass (`ef2eac6`) | Owner's answers: the escape goes **up**, the opening uses the **intercom** (Site PA barks), logs come from **staff and earlier subjects** (5 `LogTerminal`s, text in `Dialogue/logs.json`), and **someone is waiting** past the shutter (a silhouette; end card "Someone was waiting"). Names stay placeholder. See GDD "Story decisions" |
| Real-time combat (`c27d93e`) | The owner didn't like the grid tactics, so fights are now real-time side-on action: strike, HP, knockback, telegraphed Enforcer/Sentry/Warden, `EnemyRoom` rewards, shock grates, simple companion assists, ATTACK on touch. The grid code is deleted; the last grid commit is `05a8af5`. Tests set `Enemy.Passive = true` (in `GreyboxTestBase`); combat tests turn it off. See GDD §7 |
| Weapons and zoom (owner, 2026-11-27) | Findable weapons: Pipe (Maintenance, 2 damage, long and slow) and Shock Baton (Records, gets through armour and stuns). Picking one up equips it; switch with 1 / 2, RB / d-pad, or the touch weapon button (`Weapons.cs`). Camera pulled back: distance 17.5, touch ×0.9 |

**Decisions and lessons worth keeping:**
- **Cover stays a tile.** In a one-lane side view, removing it split the grid and stalemated melee, so the fix was "no height bonus from cover" instead.
- **The pit gap is sized from the motor.** The best sprint jump is about 5.6 m, so the gap is 7 m, and the pit is 4.5 m deep so the far wall can't be climbed.
- **Weapons follow the arm.** They're laid along the forearm in the idle pose, so they point forward when aiming.
- **Clip axes come from a rendered probe.** Rig axes for authored clips were found by rendering a contact sheet. In a batch-mode editor, `SkinnedMeshRenderer.forceMatrixRecalculationPerRender = true` is needed to see poses.

## 7. Known rough edges

- **Dace:** has no collision (they pass through walls); a benched companion pops straight to the terminal when another is recruited.
- **Input:** the pause key is defined in code, not the input asset.
- **WebGL lighting:** WebGL uses Forward instead of Forward+, so lights can differ slightly.
- **Art:** weapons are primitives; a body falling right next to a wall can clip slightly.
- **Not built:** per-unit XP and level-up skill choices, tool rewards from fights, more hazard kinds, ScriptableObject data, per-region music, bespoke art.

## 8. Suggested next steps (offer 2–3; the owner picks)

1. **Combat feel pass** after the owner's phone play: hit-stop, screen shake, damage and speed tuning, zoom, real death animations, better companion AI.
2. **More weapons:** a third, rarer one (Labs) or a secret one off the main path; a sub-weapon (owner's call). Add it to `Weapons.All`, a prop case in `CharacterArt.AttachWeapon`, and a pickup in the builder.
3. **Expand further:** the third region (Security Core) past the freight shutter, the drone link tool, companion 3 (ranged), who the figure past the shutter is (owner's call).
4. **Phone pass 2:** fix whatever the owner hits when they replay the phone polish. Also still open: crouch-walk animation (the pose is a frozen jump frame).
5. **Rewrite the companions** once the owner gives names and voices.
