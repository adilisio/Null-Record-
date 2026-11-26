# Null Record

Unity **6000.6.7f1** · Universal Render Pipeline (3D) · Input System

**▶ Play in your browser: https://adilisio.github.io/Null-Record-/**. It works on desktop (keyboard, mouse or gamepad) and on Android phones in Chrome (touch controls; turn the phone sideways). It's an early vertical-slice prototype.

## Getting started
1. Open Unity Hub → **Add** → **Add project from disk** → select this folder.
2. Open it with 6000.6.7f1. The first open imports packages and builds `Library/`, which takes a few minutes.
3. Run **Null Record → Build All Scenes**, then open `Assets/_Project/Scenes/MainMenu.unity`.

## Layout
- `Assets/_Project/` holds our own content (scripts, prefabs, art, audio, UI, scenes).
  - `Scripts/Runtime`: the `Game.Runtime` assembly (namespace `Game`)
  - `Scripts/Editor`: the `Game.Editor` assembly, editor-only
- `Assets/Settings/`: URP pipeline assets and volume profiles from the template
- Third-party assets go in their own top-level folders under `Assets/`.

## Version control
- Git with **LFS** for binary assets (see `.gitattributes`).
- Asset serialization is Force Text and `.meta` files are always committed.
- Optional smart merge: point git at UnityYAMLMerge:
  ```
  git config merge.unityyamlmerge.name "Unity SmartMerge"
  git config merge.unityyamlmerge.driver "\"C:/Program Files/Unity/Hub/Editor/6000.6.7f1/Editor/Data/Tools/UnityYAMLMerge.exe\" merge -p %O %B %A %A"
  ```

## Try the greybox
1. Run the menu command **Null Record → Build All Scenes** (title screen + greybox level).
2. Open `Assets/_Project/Scenes/MainMenu.unity` and press Play (or open `Greybox_Intake` to skip the title). **Esc** pauses. Controls: **A/D** move, **Space** jump, **Shift** sprint, **C** crouch, **W/S** climb ladders and ledges, **E** interact. Gamepad works too.
3. The route: jump the crate → ledge-climb the block → ladder to the catwalk for the Thermal Lining → walk through the steam → take the Level 2 keycard → talk to Dace (optional: they can join you) → open the security door.
4. In Security, press **E** at the red zone to start the optional fight. In combat: **WASD** moves the cursor, **Space** confirms, **1/2** changes the action, **C** cancels, **Shift** switches unit, **E** ends the turn.
5. The game autosaves as you go and reloads where you left off. To start over, use **Null Record → Delete Save**.

## Builds
- **Web:** run **Null Record → Build → WebGL**, which writes to `Builds/WebGL`. It uses the `Assets/WebGLTemplates/Responsive` page: full window, a loading bar, and a rotate hint on portrait phones. To publish, push the contents of `Builds/WebGL` (plus an empty `.nojekyll` file) to the `gh-pages` branch, and GitHub Pages serves it.
- **Android APK:** run **Null Record → Build → Android APK**, which writes to `Builds/Android/NullRecord.apk`. This needs Android Build Support (with the SDK, NDK and OpenJDK) installed for 6000.6.7f1 through Unity Hub.
- On phones, touch controls appear automatically: a stick on the left, context buttons on the right (Jump/Use/Crouch, and OK/Back/End turn plus action and unit buttons in combat), and pause at the top right. In combat you can also tap a tile to aim, then tap it again to confirm.
