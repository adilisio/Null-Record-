# Null Record

Unity 6000.6.7f1, URP 3D template, new Input System (`Assets/InputSystem_Actions.inputactions`).

## Conventions
- Own code lives in `Assets/_Project/Scripts/Runtime` (asmdef `Game.Runtime`, namespace `Game`); editor tools go in `Scripts/Editor` (`Game.Editor`).
- C#: 4-space indent, Allman braces, `[SerializeField] private` fields over public fields, PascalCase for public members and `_camelCase` for private fields.
- Use the Input System (`UnityEngine.InputSystem`), not the legacy `Input` class.
- Every asset needs its `.meta` file. When you create files outside the editor, let Unity generate the meta files, and never hand-write GUIDs.
- Don't edit `.unity` or `.prefab` YAML by hand unless it's unavoidable. Prefer code or editor scripts.
- Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/` or the generated `.csproj` and `.sln` files.

## Tooling notes
- Licensing (2026-11-25): the Personal license is entitlement-based, so the editor needs Unity Hub running to get its access token. The Microsoft Store Hub package was stuck "currently updating" and would not launch, so a copy of its `app` folder runs unpackaged from `%LOCALAPPDATA%\UnityHubPortable\Unity Hub.exe`. Start that first. `%LOCALAPPDATA%\UnityHubPortable\unity.exe auth status` / `license` confirm sign-in. If Unity can't run, `Tools/CompileCheck` still checks the scripts compile.

## Design
- The game design lives in `Docs/GDD.md`. Read it before building features: it covers a 2.5D sci-fi Metroidvania with opt-in, in-place grid tactics combat and hacking/hazard gating.

## Compile check (no editor needed)
- Run `sh Tools/CompileCheck/check.sh` after every script change. It builds Game.Runtime and Game.Editor with dotnet against the 6000.6.7f1 editor DLLs. Package DLLs come from this project's `Library/ScriptAssemblies`, or from `C:\Projects\caromancer` until this project has been imported once.
- The check covers compilation only. Behaviour still has to be tested in Play mode.

## Greybox
- **Null Record → Build Greybox Test Level** regenerates `Assets/_Project/Scenes/Greybox_Intake.unity`, overwriting it. Change the layout in `GreyboxLevelBuilder.cs`, not by editing the scene.
