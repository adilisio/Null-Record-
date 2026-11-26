# Null Record: handoff

_Last updated 2026-11-26. Read this first in a new session, then `CLAUDE.md` (conventions) and `Docs/GDD.md` (design)._

**Play it:** https://adilisio.github.io/Null-Record-/ (desktop browser, or Android Chrome with touch controls).
**Repo:** https://github.com/adilisio/Null-Record- (public). `main` holds the source; `gh-pages` holds only the web build.

---

## 1. How the owner works: remote, hands-off

- **They're almost always remote,** using Claude Code's Remote Control from another device. They usually **can't touch the PC**.
  - Any Windows prompt that needs a click on the PC will stall: admin (UAC) prompts, computer-use approval dialogs, sign-in windows.
  - Don't plan work that needs one. If something truly does, say so plainly and offer an alternative.
- **They may not see the app's next-step box or buttons.** So end every turn with a plain-text summary. Any question goes in the message itself, with numbered choices.
- **They want autonomy.** Their words: "don't ask permission in this session", "handle it yourself", "find a way to make it work".
  - Make reasonable calls, record them in `Docs/GDD.md` (marked as first-pass or placeholder), and keep going.
  - Ask only when the choice is really theirs: story and characters, publishing somewhere new, or spending money.
  - Safety limits still apply. A tool once blocked an attempt to skip a Windows admin prompt; that's correct, so don't route around such blocks.
- **They follow along through screenshots and files.**
  - Send progress images and builds with SendUserFile: test screenshots from `Screenshots/`, WAVs from `Screenshots/audio/`.
  - Give short progress notes during long jobs. They once asked "Test running 9 hours?" after a run hung. **Always put a time limit on Unity batch runs** (the scripts below do).
- They pick the next feature from 2 or 3 options at the end of each chunk of work. Commit after each feature; push when they ask (they've asked before, so pushing finished work to `main` is expected).

## 2. Machine and environment quirks

- **Windows 11.** The repo is at `C:\Projects\new-unity-game`. Unity is **6000.6.7f1**, at `C:\Program Files\Unity\Hub\Editor\6000.6.7f1` (read-only without admin).
- **Licensing:**
  - The Microsoft Store Unity Hub is stuck "currently updating" and won't launch.
  - A copy of it runs unpackaged from `%LOCALAPPDATA%\UnityHubPortable\Unity Hub.exe` and carries the owner's sign-in (Personal license).
  - **Start it before any Unity batch run.** It may already be running.
  - Check sign-in with `%LOCALAPPDATA%\UnityHubPortable\unity.exe auth status`.
- **Unity CLI:** `%LOCALAPPDATA%\UnityHubPortable\unity.exe` handles editors, modules and auth. `install`/`install-modules` need admin for the editor in Program Files.
- **Build support:**
  - Installed: **WebGL** and Windows.
  - **Android isn't installed**; adding it needs an admin prompt at the PC.
  - A 15 GB copy of the editor, made while trying, sits at `%LOCALAPPDATA%\UnityEditors\6000.6.7f1-android`. It has no Android yet and can be deleted.
  - The owner chose "web version is enough" for now. If they later want an APK: install "Android Build Support" for 6000.6.7f1 via Unity Hub at the PC, then run **Null Record → Build → Android APK** (`Game.EditorTools.Builds.Android`).
- **GitHub:** the `gh` CLI is signed in as `adilisio` with repo scope. Commits use the noreply email.

## 3. Daily workflow

- **Compile check (fast, no Unity):** `sh Tools/CompileCheck/check.sh`. Run it after every script change.
- **Full verification** (about 6–8 min; builds scenes, lighting shots, WAVs, then EditMode and PlayMode tests):
  `powershell -ExecutionPolicy Bypass -File Tools/unity-batch.ps1 test`
  - Results go to `Logs/batch/{edit,play}.xml`; screenshots go to `Screenshots/`.
  - Current status: **137 tests passing** (91 EditMode, 46 PlayMode).
- **Publish the web build:** run `powershell -ExecutionPolicy Bypass -File Tools/unity-batch.ps1 web` (about 2 min after the first build), then `sh Tools/publish-web.sh`. Then check the live URL in the built-in browser.
- **Scenes are generated.** Build All also rebuilds the character prefabs, skin materials and portraits (`CharacterArt`); rerun `python Tools/Art/make_skins.py` first if you change the skins.
- **Scenes (detail):** `MainMenu.unity` and `Greybox_Intake.unity` are rebuilt by **Null Record → Build All Scenes**, so edit `MainMenuBuilder.cs` and `GreyboxLevelBuilder*.cs`, never the `.unity` files.
- **Menus, HUD, touch controls and the dialogue box** are built in code on Screen Space - Camera canvases, so they appear in test screenshots.
- **Headless test gotchas:**
  - Tests run at 60 fps (the editor runs at thousands of fps otherwise).
  - Use unscaled time for waits.
  - `WaitForEndOfFrame` never fires in batch mode.
  - Check for audio by counters, not by output.
  - Tests use a scratch save folder.

## 4. What exists

All of it is greybox. "Placeholder" means it's meant to be replaced.

| Area | State |
|---|---|
| Player | Run, sprint, jump (coyote time and buffer), crouch, ladders, ledge grab/climb, interact; footsteps and other sounds |
| Level | 5 rooms: Holding (crate, ledge), Maintenance (ladder, catwalk, Thermal Lining, steam hazard, L2 keycard, Dace), Security (L2 door, optional fight), Records (ladder to a loft with the signal spoofer, optional scrap), Server Hall (behind a laser grid the spoofer opens; second optional fight with the armoured Warden; freight lift marks the end of the slice) |
| Combat | Optional grid tactics in Security. 1 m tiles, multi-floor, move plus one action, cooldowns, height bonus, cover, armour, knockback, telegraphed enemy attacks that target a tile, no randomness; win and lose flow. A hazard tile (arc grate) you can shove enemies onto. Ranged AI holds high ground and avoids melee reach. Wins give scrap and party XP (50 XP per level, +2 max HP per level) |
| Companion | **Dace** (working name, placeholder writing): recruit conversation with a tone choice, follows you (catches up across floors), joins fights, 3 banter lines |
| Dialogue | JSON in `Assets/_Project/Dialogue`, flags and conditions, text box with portraits rendered from the character models, barks |
| Saving | Autosave on room entry, any progress, and before and after fights; room-entry respawn; one slot; PlayerPrefs on the web |
| Menus | Title (Continue with save summary, New Game confirmation, Settings, Quit), pause menu, settings (master, effects and ambience volume, fullscreen, debug HUD) |
| Look | URP post-processing, fog, mood lighting per room (flicker, rotating alarm beacon), player key light |
| Sound | 28 sounds. Recorded Kenney CC0 clips cover 10 of them (footsteps, hits, ladder, door, zap and others) from `Resources/Sounds/<SoundId>_<variant>.ogg`. The rest are synthesised in code (`Game.Audio.Synth`), including the room ambience, the combat loop and a 32 s exploration music bed with its own volume slider |
| Art | Kenney CC0 packs (see `CREDITS.md`): animated character models with our own skins (`Tools/Art/make_skins.py`), Space Station Kit dressing (back wall, floor, and meshes over the walkways, blocks, crates and the Security door; the greybox keeps the colliders), a crouch pose, keyframed combat actions (Strike, Shove, Swing, Shoot, Hit, Down; `CharacterActions`), hand-held weapon props, loudness-matched recorded sounds (`SoundTuning`, `gains.json`), and portraits rendered from the models. All are placeholder picks |
| Platforms | Windows editor, WebGL (live on GitHub Pages), touch controls; no Android APK |

Code is under `Assets/_Project/Scripts/Runtime/{Player,World,Combat,Companions,Dialogue,Save,Audio,UI,Core,CameraSystem}`, with editor tools in `Scripts/Editor`.

## 5. Open questions and known gaps

- **Story** is the owner's call; don't invent it in code. Still open:
  - Dace's real name, voice and lines
  - The protagonist's subject ID and why they were erased
  - Whether the facility goes up or down
  - The endings
- **Untested by hand:** nobody has played a build with real hands yet. Feel, balance, mouse control in combat, touch on a real phone and web performance on phones are all unknown.
- **Known rough edges:**
  - Dace has no collision, so they pass through walls and doors.
  - Companion HP doesn't carry over between fights.
  - The pause key is defined in code, not in the input asset.
  - URP Forward+ falls back to Forward on WebGL, so lights can look slightly different on the web.
- **Not built yet:**
  - Per-unit XP, and skill choices on level-up
  - Tool rewards from fights (wait for the hacking-tool design)
  - More hazard kinds (timed steam, switchable wires)
  - Units and skills as data assets (ScriptableObjects)
  - Composed or per-region music
  - Bespoke art (models, animations beyond idle/run/jump, a crouch animation), composed music
  - Room transitions beyond camera zones
  - More rooms or regions

## 6. Suggested next steps (the owner chooses)

1. **Tune combat further:** balance after a hands-on play, skill choices on level-up, a tool reward once hacking tools exist.
2. **Rewrite Dace** once the owner gives the name and voice; add the rest of the companion arc.
3. **Expand further:** a second region past the freight lift, the remote override or drone link tools, and companion 2.
4. **More art:** modelled weapons instead of primitives, a downed pose for Dace (companions just stay standing at 0 HP), a ceiling and side-wall pass, and checking the mix by ear.
5. **Phone polish** once someone tries the web build on a real Android phone.
