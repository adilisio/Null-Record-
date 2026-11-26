# Null Record

Unity **6000.6.7f1** · Universal Render Pipeline (3D) · Input System

## Getting started
1. Open Unity Hub → **Add** → **Add project from disk** → select this folder.
2. Open it with 6000.6.7f1. The first open imports packages and builds `Library/`, which takes a few minutes.
3. Open `Assets/Scenes/SampleScene.unity`.

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
1. Run the menu command **Null Record → Build Greybox Test Level**.
2. Press Play. Controls: **A/D** move, **Space** jump, **Shift** sprint, **C** crouch, **W/S** climb ladders and ledges, **E** interact. Gamepad works too.
3. The route: jump the crate → ledge-climb the block → ladder to the catwalk for the Thermal Lining → walk through the steam → take the Level 2 keycard → talk to Dace (optional: they can join you) → open the security door.
4. In Security, press **E** at the red zone to start the optional fight. In combat: **WASD** moves the cursor, **Space** confirms, **1/2** changes the action, **C** cancels, **Shift** switches unit, **E** ends the turn.
5. The game autosaves as you go and reloads where you left off. To start over, use **Null Record → Delete Save**.
