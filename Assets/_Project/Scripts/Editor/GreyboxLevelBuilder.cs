using System.Collections.Generic;
using System.IO;
using Game.Audio;
using Game.CameraSystem;
using Game.Companions;
using Game.Dialogue;
using Game.Save;
using Game.Combat;
using Game.Core;
using Game.Player;
using Game.UI;
using Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Generates the Intake greybox scene used to test movement, gating and hazards.
    /// Re-run it any time; it overwrites the scene file.
    ///
    /// Layout (left → right, floor at y = 0):
    ///   A  Holding    x -2..20   crate (jump), tall block (ledge climb)
    ///   B  Maintenance x 20..40  ladder → catwalk with Thermal Lining, steam vent, Level 2 keycard
    ///   C  Security   x 40..56   behind a Level 2 door; optional grid-combat encounter
    /// </summary>
    public static partial class GreyboxLevelBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Greybox_Intake.unity";
        private const string MaterialDir = "Assets/_Project/Art/Materials/Greybox";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string DialogueDir = "Assets/_Project/Dialogue";
        private const float Depth = 4f; // z thickness of level geometry

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        /// <summary>Builds the title scene and the greybox level, and sets the build order (title first).</summary>
        [MenuItem("Null Record/Build All Scenes", priority = 0)]
        public static void BuildAll()
        {
            CharacterArt.Setup();
            MainMenuBuilder.Build();
            Build();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(GameFlow.TitleScene, true),
                new EditorBuildSettingsScene(GameFlow.GameScene, true),
            };
            Debug.Log("[Null Record] Built all scenes. Build order: MainMenu, Greybox_Intake.");
        }

        [MenuItem("Null Record/Build Greybox Test Level", priority = 1)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Materials.Clear();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupLighting();
            Transform level = new GameObject("Level").transform;
            BuildShell(level);
            BuildRoomA(level);
            BuildRoomB(level);
            BuildRoomC(level);
            BuildRoomD(level);
            BuildRoomE(level);
            BuildDressing(level);
            BuildCameraZones(level);

            GameObject player = BuildPlayer(new Vector3(1f, 0.05f, 0f));
            Camera cam = BuildCamera(player.transform);
            BuildLighting(level, cam, player.transform);
            BuildHud(player.GetComponent<PlayerInteractor>());
            new GameObject("DialogueUI").AddComponent<DialogueUI>();
            new GameObject("SaveManager").AddComponent<SaveManager>();
            new GameObject("PauseMenu").AddComponent<PauseMenu>();
            new GameObject("TouchControls").AddComponent<TouchControls>();
            new GameObject("AudioManager").AddComponent<AudioManager>();

            // The listener rides with the player (the camera is 16 m back), so world sounds pan and fade sensibly.
            var listener = new GameObject("AudioListener");
            listener.transform.position = player.transform.position + Vector3.up;
            listener.AddComponent<AudioListener>();
            listener.AddComponent<FollowOffset>().Configure(player.transform, Vector3.up);
            BuildCompanion(level);
            new GameObject("Bootstrap").AddComponent<Bootstrap>();

            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log($"[Null Record] Built greybox level at {ScenePath}. Press Play to test.");
        }

        [MenuItem("Null Record/Delete Save", priority = 20)]
        public static void DeleteSave()
        {
            SaveSystem.Delete();
            Debug.Log($"[Null Record] Deleted save at {SaveSystem.FilePath}");
        }

        [MenuItem("Null Record/Show Save File", priority = 21)]
        public static void ShowSave()
        {
            if (SaveSystem.Exists) EditorUtility.RevealInFinder(SaveSystem.FilePath);
            else Debug.Log($"[Null Record] No save yet ({SaveSystem.FilePath}).");
        }

        // ------------------------------------------------------------------ level

        private static void BuildShell(Transform level)
        {
            Transform shell = Group(level, "Shell");
            Material concrete = Mat("Concrete", new Color(0.32f, 0.33f, 0.35f));
            Material backWall = Mat("BackWall", new Color(0.18f, 0.19f, 0.21f));

            // Shell spans x -2.5 .. SliceEndX + 0.5.
            float cx = (SliceEndX - 2f) * 0.5f, w = SliceEndX + 3f;
            Box(shell, "Floor", new Vector3(cx, -0.5f, 0f), new Vector3(w, 1f, Depth), concrete);
            Box(shell, "Ceiling", new Vector3(cx, 8.25f, 0f), new Vector3(w, 0.5f, Depth), concrete);
            Box(shell, "WallLeft", new Vector3(-2.25f, 4f, 0f), new Vector3(0.5f, 8f, Depth), concrete);
            Box(shell, "WallRight", new Vector3(SliceEndX + 0.25f, 4f, 0f), new Vector3(0.5f, 8f, Depth), concrete);
            Box(shell, "BackWall", new Vector3(cx, 4f, Depth * 0.5f + 0.25f), new Vector3(w, 9f, 0.5f), backWall, collider: false);

            // Pillars between rooms for visual separation (background only).
            Material pillar = Mat("Pillar", new Color(0.24f, 0.25f, 0.27f));
            foreach (float x in new[] { 20f, 40f, 56f, 72f })
            {
                Box(shell, "Pillar", new Vector3(x, 4f, Depth * 0.5f - 0.2f), new Vector3(0.8f, 8f, 0.4f), pillar, collider: false);
            }
        }

        private static void BuildRoomA(Transform level)
        {
            Transform room = Group(level, "A_Holding");
            Material crate = Mat("Crate", new Color(0.45f, 0.4f, 0.32f));
            Material block = Mat("Block", new Color(0.38f, 0.4f, 0.43f));

            Box(room, "Crate_Jump", new Vector3(5.75f, 0.5f, 0f), new Vector3(1.5f, 1f, 2f), crate);
            Box(room, "Block_LedgeClimb", new Vector3(16f, 1.2f, 0f), new Vector3(8f, 2.4f, Depth), block);
            Box(room, "CellBars", new Vector3(-0.5f, 3f, 1.6f), new Vector3(0.1f, 6f, 0.1f), Mat("Metal", new Color(0.5f, 0.52f, 0.55f), metallic: 0.8f), collider: false);

            Label(room, "HOLDING 03", new Vector3(2f, 5.5f, 1.9f), new Color(0.75f, 0.8f, 0.85f));
            Label(room, "jump [Space]", new Vector3(5.75f, 2.2f, 1.9f), new Color(0.5f, 0.55f, 0.6f), 0.5f);
            Label(room, "grab the ledge: hold toward it while falling", new Vector3(11f, 3.6f, 1.9f), new Color(0.5f, 0.55f, 0.6f), 0.4f);
        }

        private static void BuildRoomB(Transform level)
        {
            Transform room = Group(level, "B_Maintenance");
            Material metal = Mat("Metal", new Color(0.5f, 0.52f, 0.55f), metallic: 0.8f);
            Material grate = Mat("SteamGrate", new Color(0.25f, 0.12f, 0.05f), emission: new Color(1f, 0.35f, 0.05f) * 2f);

            Checkpoint(room, "Checkpoint_B", new Vector3(21f, 0f, 0f));

            // Ladder up to the catwalk.
            GameObject ladderGo = new GameObject("Ladder");
            ladderGo.transform.SetParent(room, false);
            ladderGo.transform.position = new Vector3(22.5f, 0f, 0f);
            ladderGo.AddComponent<Ladder>().Configure(4f, 1);
            for (int i = -1; i <= 1; i += 2)
            {
                Box(ladderGo.transform, "Rail", new Vector3(22.5f + i * 0.3f, 2.1f, 0.6f), new Vector3(0.08f, 4.2f, 0.08f), metal, collider: false);
            }
            for (float y = 0.3f; y < 4f; y += 0.4f)
            {
                Box(ladderGo.transform, "Rung", new Vector3(22.5f, y, 0.6f), new Vector3(0.6f, 0.05f, 0.05f), metal, collider: false);
            }

            Box(room, "Catwalk", new Vector3(26f, 3.85f, 0f), new Vector3(6f, 0.3f, 2f), metal);
            Box(room, "Catwalk_Rail", new Vector3(26f, 4.6f, -0.9f), new Vector3(6f, 0.05f, 0.05f), metal, collider: false);

            GameObject lining = Pickup(room, "Pickup_ThermalLining", new Vector3(27.5f, 4.6f, 0f), Mat("Pickup_Gear", new Color(1f, 0.5f, 0.1f), emission: new Color(1f, 0.45f, 0.1f)));
            lining.GetComponent<ItemPickup>().ConfigureProtection(HazardType.Heat);
            lining.GetComponent<ItemPickup>().SetSaveId("pickup_thermal_lining");

            // Steam vent spanning floor to ceiling; impassable without Thermal Lining.
            Box(room, "SteamGrate", new Vector3(31.5f, 0.02f, 0f), new Vector3(3f, 0.05f, 2.5f), grate, collider: false);
            GameObject hazard = new GameObject("Hazard_Steam");
            hazard.transform.SetParent(room, false);
            hazard.transform.position = new Vector3(31.5f, 4f, 0f);
            BoxCollider hz = hazard.AddComponent<BoxCollider>();
            hz.isTrigger = true;
            hz.size = new Vector3(2.6f, 8f, Depth);
            hazard.AddComponent<HazardVolume>().Configure(HazardType.Heat, "Scalded by venting steam.");
            AddSteamParticles(hazard.transform);
            hazard.AddComponent<AmbientEmitter>().Configure(SoundId.SteamLoop, 12f);
            PointLight(room, "Light_Steam", new Vector3(31.5f, 1f, -1f), new Color(1f, 0.45f, 0.1f), 5f, 8f, shadows: false);

            GameObject card = Pickup(room, "Pickup_Keycard_L2", new Vector3(36f, 1f, 0f), Mat("Pickup_Keycard", new Color(0.2f, 0.6f, 1f), emission: new Color(0.2f, 0.6f, 1f) * 1.5f));
            card.GetComponent<ItemPickup>().ConfigureKeycard(2);
            card.GetComponent<ItemPickup>().SetSaveId("pickup_keycard_l2");

            Label(room, "MAINTENANCE", new Vector3(24f, 6.5f, 1.9f), new Color(1f, 0.7f, 0.3f));
            Label(room, "climb [W]", new Vector3(22.5f, 1.8f, 1.9f), new Color(0.5f, 0.55f, 0.6f), 0.4f);
        }

        private static void BuildRoomC(Transform level)
        {
            Transform room = Group(level, "C_Security");
            Material concrete = Mat("Concrete", new Color(0.32f, 0.33f, 0.35f));
            Material doorMat = Mat("Door", new Color(0.55f, 0.57f, 0.6f), metallic: 0.6f);
            Material status = Mat("StatusLight", Color.white, emission: Color.white);
            Material encounter = Mat("EncounterMarker", new Color(0.3f, 0.02f, 0.02f), emission: new Color(1f, 0.05f, 0.05f) * 0.45f);

            // Wall above the doorway; the panel slides up into it.
            Box(room, "Door_Header", new Vector3(40f, 5.5f, 0f), new Vector3(0.6f, 5f, Depth), concrete);

            GameObject doorGo = new GameObject("Door_L2");
            doorGo.transform.SetParent(room, false);
            doorGo.transform.position = new Vector3(40f, 0f, 0f);
            GameObject panel = Box(doorGo.transform, "Panel", new Vector3(40f, 1.5f, 0f), new Vector3(0.4f, 3f, Depth), doorMat);
            GameObject light = Box(doorGo.transform, "StatusLight", new Vector3(39.55f, 3.3f, -0.8f), new Vector3(0.3f, 0.15f, 0.1f), status, collider: false);
            Door door = doorGo.AddComponent<Door>();
            door.Configure(2, panel.transform, light.GetComponent<Renderer>(), 3f);
            door.SetInteractionPoint(new Vector3(-0.6f, 1.2f, 0f), 1.5f);
            door.SetSaveId("door_security_l2");

            Checkpoint(room, "Checkpoint_C", new Vector3(41.5f, 0f, 0f));

            // Props sit on the 1 m combat lattice (tile edges at whole numbers from x = 42).
            GameObject zoneMarker = Box(room, "EncounterZone_Marker", new Vector3(49f, 0.02f, 0f), new Vector3(14f, 0.04f, 2.5f), encounter, collider: false);
            Box(room, "Cover_Low", new Vector3(47.5f, 0.5f, 0f), new Vector3(1f, 1f, 2f), Mat("Crate", new Color(0.45f, 0.4f, 0.32f)));
            Box(room, "Platform_High", new Vector3(51.5f, 2f, 0f), new Vector3(3f, 0.3f, 2f), Mat("Metal", new Color(0.5f, 0.52f, 0.55f), metallic: 0.8f));
            // Arcing floor grate under the platform's edge: live only during the fight (see EncounterController.SetHazards).
            // No collider, so the combat grid still sees the floor under it as one ordinary tile.
            Box(room, "Hazard_ArcGrate", new Vector3(50.5f, 0.03f, 0f), new Vector3(0.9f, 0.05f, 2f),
                Mat("ArcGrate", new Color(0.12f, 0.14f, 0.05f), emission: new Color(0.7f, 1f, 0.15f) * 1.5f), collider: false);

            GameObject engageSign = Label(room, "[ optional encounter: E to engage ]", new Vector3(49f, 4.2f, 1.9f), new Color(1f, 0.4f, 0.35f), 0.4f);
            GameObject encGo = new GameObject("Encounter_Security");
            encGo.transform.SetParent(room, false);
            encGo.transform.position = new Vector3(44.5f, 0f, 0f);
            EncounterController enc = encGo.AddComponent<EncounterController>();
            enc.SetInteractionPoint(new Vector3(0f, 0.9f, 0f), 1.2f);
            enc.SetHazards(new Vector2(50.5f, 0f));
            enc.SetUnitModels(
                new EncounterController.UnitModel { unit = "Enforcer", prefab = CharacterArt.Prefab("Enforcer") },
                new EncounterController.UnitModel { unit = "Sentry", prefab = CharacterArt.Prefab("Sentry") });
            enc.Configure(
                new Rect(42f, -0.2f, 14f, 6.2f),
                new[]
                {
                    new EncounterController.EnemySpawn { unit = "Enforcer", position = new Vector2(49.5f, 0f) },
                    new EncounterController.EnemySpawn { unit = "Sentry", position = new Vector2(51.5f, 2.2f) },
                    new EncounterController.EnemySpawn { unit = "Sentry", position = new Vector2(54.5f, 0f) },
                },
                30,
                "encounter_security_cleared",
                "Recovered: security incident report (fragment).",
                TransparentMat("GridTile", new Color(1f, 1f, 1f, 0.3f)),
                Mat("Enemy", new Color(0.5f, 0.16f, 0.14f)),
                Mat("EnemyVisor", new Color(1f, 0.25f, 0.2f), emission: new Color(1f, 0.2f, 0.15f) * 2f),
                zoneMarker.GetComponent<Renderer>(),
                engageSign);

            Label(room, "SECURITY", new Vector3(44f, 6.5f, 1.9f), new Color(1f, 0.35f, 0.3f));
        }

        private static void BuildCameraZones(Transform level)
        {
            Transform zones = Group(level, "CameraZones");
            CameraZoneAt(zones, "Zone_A", new Vector3(9f, 4f, 0f), new Vector2(22f, 8.5f), "Holding");
            CameraZoneAt(zones, "Zone_B", new Vector3(30f, 4f, 0f), new Vector2(20f, 8.5f), "Maintenance");
            CameraZoneAt(zones, "Zone_C", new Vector3(48f, 4f, 0f), new Vector2(16f, 8.5f), "Security");
            CameraZoneAt(zones, "Zone_D", new Vector3(64f, 4f, 0f), new Vector2(16f, 8.5f), "Records");
            CameraZoneAt(zones, "Zone_E", new Vector3(81f, 4f, 0f), new Vector2(18f, 8.5f), "Server Hall");
        }

        // ------------------------------------------------------------------ actors

        private static GameObject BuildPlayer(Vector3 position)
        {
            GameObject player = new GameObject("Player");
            player.transform.position = position;
            player.tag = "Player";

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.radius = 0.35f;
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.03f;

            // Character model (Kenney CC0 base, our skin); the motor turns local +Z toward the facing direction.
            GameObject body = CharacterBody(player.transform, "Subject", new Color(0.85f, 0.85f, 0.8f), new Color(0.1f, 0.8f, 0.9f));

            PlayerInputReader input = player.AddComponent<PlayerInputReader>();
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions != null) SetRef(input, "actions", actions);
            else Debug.LogWarning($"[Null Record] {InputActionsPath} not found; falling back to project-wide actions.");

            PlayerMotor motor = player.AddComponent<PlayerMotor>();
            SetRef(motor, "body", body.transform);
            player.AddComponent<PlayerInventory>();
            player.AddComponent<PlayerInteractor>();
            player.AddComponent<PlayerRespawn>();
            player.AddComponent<PlayerAudio>();
            return player;
        }

        /// <summary>
        /// Adds the character prefab as "Body" (pivot at the feet). Falls back to the old capsule and visor
        /// if the prefab hasn't been built.
        /// </summary>
        private static GameObject CharacterBody(Transform parent, string character, Color capsule, Color visorColor)
        {
            GameObject prefab = CharacterArt.Prefab(character);
            if (prefab != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                model.name = "Body";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                return model;
            }

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(parent, false);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.GetComponent<Renderer>().sharedMaterial = Mat(character, capsule);
            GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "Visor";
            Object.DestroyImmediate(visor.GetComponent<Collider>());
            visor.transform.SetParent(parent, false);
            visor.transform.localPosition = new Vector3(0f, 1.5f, 0.3f);
            visor.transform.localScale = new Vector3(0.4f, 0.12f, 0.12f);
            visor.GetComponent<Renderer>().sharedMaterial = Mat(character + "Visor", visorColor, emission: visorColor * 2f);
            return body;
        }

        private static void BuildCompanion(Transform level)
        {
            Transform group = Group(level, "Companions");
            TextAsset recruit = AssetDatabase.LoadAssetAtPath<TextAsset>($"{DialogueDir}/dace_recruit.json");
            TextAsset banter = AssetDatabase.LoadAssetAtPath<TextAsset>($"{DialogueDir}/dace_banter.json");
            if (recruit == null || banter == null) Debug.LogWarning("[Null Record] Dialogue JSON not found; Dace won't talk.");

            // Dace: Companion 1 (frontline). Waits in Maintenance by the keycard they dropped.
            GameObject dace = new GameObject("Dace");
            dace.transform.SetParent(group, false);
            dace.transform.position = new Vector3(37.6f, 0f, 0f);
            dace.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            CharacterBody(dace.transform, "Dace", new Color(0.32f, 0.42f, 0.55f), new Color(1f, 0.75f, 0.3f));

            dace.AddComponent<CompanionFollower>();
            CompanionNPC npc = dace.AddComponent<CompanionNPC>();
            npc.Configure("Dace", recruit, "met_dace", "companion_guard");
            npc.SetInteractionPoint(new Vector3(0f, 0.9f, 0f), 1.3f);

            // Remarks, once each, while Dace is with you.
            Banter(group, "Banter_SecurityDoor", new Rect(38f, -0.5f, 1.6f, 4.5f), banter, "security_door", "companion_guard");
            Banter(group, "Banter_SecurityRoom", new Rect(41f, -0.5f, 3f, 4.5f), banter, "security_room", "companion_guard");
            Banter(group, "Banter_SecurityCleared", new Rect(42f, -0.5f, 14f, 7f), banter, "security_cleared", "encounter_security_cleared");
        }

        private static void Banter(Transform parent, string name, Rect area, TextAsset lines, string node, string requireFlag)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(area.center.x, area.center.y, 0f);
            go.AddComponent<BanterTrigger>().Configure(area, lines, node, requireFlag);
        }

        private static Camera BuildCamera(Transform target)
        {
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            camGo.transform.position = target.position + new Vector3(0f, 1.5f, -16f);

            SideCameraRig rig = camGo.AddComponent<SideCameraRig>();
            SetRef(rig, "target", target);
            return cam;
        }

        private static void BuildHud(PlayerInteractor player)
        {
            GreyboxHUD hud = new GameObject("HUD").AddComponent<GreyboxHUD>();
            SetRef(hud, "player", player);
        }

        // ------------------------------------------------------------------ helpers

        private static void SetupLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.08f, 0.1f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            GameObject sun = new GameObject("Directional Light (fill)");
            Light l = sun.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = new Color(0.5f, 0.6f, 0.82f);
            l.intensity = 0.32f;
            l.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(32f, 18f, 0f); // front-top fill: lights faces toward the camera, shadows fall on the back wall
        }

        private static Transform Group(Transform parent, string name)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool collider = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject Pickup(Transform parent, string name, Vector3 position, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.4f, 0.25f, 0.05f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            ItemPickup pickup = go.AddComponent<ItemPickup>();
            pickup.SetInteractionPoint(Vector3.zero, 1.4f);
            PointLight(go.transform, "Glow", position + new Vector3(0f, 0f, -0.5f), mat.GetColor("_BaseColor"), 1.5f, 2.5f, shadows: false);
            return go;
        }

        private static void Checkpoint(Transform parent, string name, Vector3 position)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position + new Vector3(0f, 0.05f, 0f);
            BoxCollider c = go.AddComponent<BoxCollider>();
            c.isTrigger = true;
            c.center = new Vector3(0f, 4f, 0f);
            c.size = new Vector3(1f, 8f, Depth);
            go.AddComponent<Game.World.Checkpoint>();
        }

        private static void CameraZoneAt(Transform parent, string name, Vector3 center, Vector2 size, string roomName)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.AddComponent<CameraZone>().Configure(size, roomName);
        }

        private static void PointLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range, bool shadows = true)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        private static GameObject Label(Transform parent, string text, Vector3 position, Color color, float size = 1f)
        {
            GameObject go = new GameObject("Label_" + text);
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.1f * size;
            tm.fontSize = 64;
            tm.color = color;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            return go;
        }

        private static void AddSteamParticles(Transform parent)
        {
            GameObject go = new GameObject("Steam");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(parent.position.x, 0.1f, 0f);
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = 1.6f;
            main.startSpeed = 5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startColor = new Color(1f, 0.92f, 0.85f, 0.25f);
            main.maxParticles = 400;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 120f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2.6f, 1.5f, 0.1f);
            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMat("Steam", new Color(1f, 0.95f, 0.9f, 0.3f));
        }

        private static Material Mat(string name, Color color, float metallic = 0f, Color? emission = null)
        {
            if (Materials.TryGetValue(name, out Material cached)) return cached;

            string path = $"{MaterialDir}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                EnsureFolder(MaterialDir);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", metallic > 0f ? 0.6f : 0.25f);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            Materials[name] = mat;
            return mat;
        }

        private static Material ParticleMat(string name, Color color)
        {
            string path = $"{MaterialDir}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
                mat = new Material(shader);
                EnsureFolder(MaterialDir);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", color);
            // URP transparent, alpha-blended.
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material TransparentMat(string name, Color color)
        {
            string path = $"{MaterialDir}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                EnsureFolder(MaterialDir);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolder(string assetPath)
        {
            string[] parts = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void SetRef(Object target, string field, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[Null Record] {target.GetType().Name} has no serialized field '{field}'.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
