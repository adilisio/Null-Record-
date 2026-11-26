using Game.Combat;
using Game.Companions;
using Game.World;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Region 2, the Research Wing (2026-11-26, first pass). It sits in the same scene as region 1, offset to
    /// x 100..156 and y = <see cref="R2Y"/>, and the freight lift in the Server Hall carries the player between them.
    /// (The GDD plans one scene per region; one scene is simpler while the slice is small.)
    ///   F  Lift Lobby     x 100..112  arrival lift, checkpoint, companion terminal (swap who comes along)
    ///   G  Labs           x 112..132  remote override on a lab bench; a 7 m pit whose bridge only the override
    ///                                 extends (console on the far side); a ladder out of the pit
    ///   H  Specimen Hall  x 132..152  third optional encounter (Enforcer, Sentry, Warden on a raised block you can
    ///                                 shove it off); companion 2 waits past it; a freight shutter at the end,
    ///                                 also opened by the override, marks the end of the slice
    /// Room names follow the GDD's region list; the writing and companion 2's identity are placeholders.
    /// </summary>
    public static partial class GreyboxLevelBuilder
    {
        public const float R2Y = -30f;
        public const float R2Start = 100f;
        public const float R2End = 156f;
        public const float PitStart = 122f;
        public const float PitEnd = 129f;
        public const float PitDepth = 4.5f;

        private static void BuildResearchWing(Transform level)
        {
            Transform region = Group(level, "R2_ResearchWing");
            BuildR2Shell(region);
            BuildRoomF(region);
            BuildRoomG(region);
            BuildRoomH(region);
        }

        private static void BuildR2Shell(Transform region)
        {
            Transform shell = Group(region, "R2_Shell");
            Material concrete = Mat("LabConcrete", new Color(0.36f, 0.38f, 0.4f));
            Material backWall = Mat("LabBackWall", new Color(0.2f, 0.22f, 0.24f));
            float y = R2Y;
            // Floors are 5 m deep so their ends form the pit walls.
            float westW = PitStart - (R2Start - 0.5f), eastW = (R2End + 0.5f) - PitEnd;
            Box(shell, "R2_Floor_West", new Vector3(R2Start - 0.5f + westW * 0.5f, y - 2.5f, 0f), new Vector3(westW, 5f, Depth), concrete);
            Box(shell, "R2_Floor_East", new Vector3(PitEnd + eastW * 0.5f, y - 2.5f, 0f), new Vector3(eastW, 5f, Depth), concrete);
            Box(shell, "R2_Pit_Floor", new Vector3((PitStart + PitEnd) * 0.5f, y - PitDepth - 0.5f, 0f), new Vector3(PitEnd - PitStart, 1f, Depth), concrete);
            float cx = (R2Start + R2End) * 0.5f, w = R2End - R2Start + 1f;
            Box(shell, "R2_Ceiling", new Vector3(cx, y + 8.25f, 0f), new Vector3(w, 0.5f, Depth), concrete);
            Box(shell, "R2_WallLeft", new Vector3(R2Start - 0.75f, y + 4f, 0f), new Vector3(0.5f, 8f, Depth), concrete);
            Box(shell, "R2_WallRight", new Vector3(R2End + 0.75f, y + 4f, 0f), new Vector3(0.5f, 8f, Depth), concrete);
            Box(shell, "R2_BackWall", new Vector3(cx, y + 3.5f, Depth * 0.5f + 0.25f), new Vector3(w, 14f, 0.5f), backWall, collider: false);
            Material pillar = Mat("Pillar", new Color(0.24f, 0.25f, 0.27f));
            foreach (float x in new[] { 112f, 132f, 152f })
            {
                Box(shell, "R2_Pillar", new Vector3(x, y + 4f, Depth * 0.5f - 0.2f), new Vector3(0.8f, 8f, 0.4f), pillar, collider: false);
            }
        }

        private static void BuildRoomF(Transform region)
        {
            Transform room = Group(region, "F_LiftLobby");
            float y = R2Y;
            Checkpoint(room, "Checkpoint_F", new Vector3(103f, y, 0f));

            GameObject lift = new GameObject("FreightLift_Up");
            lift.transform.SetParent(room, false);
            lift.transform.position = new Vector3(100.6f, y, 0f);
            lift.AddComponent<FreightLift>().Configure(new Vector3(88.2f, 0.05f, 0f), -1, "Take the freight lift (Server Hall)", "Server Hall.");
            lift.GetComponent<FreightLift>().SetInteractionPoint(new Vector3(0.4f, 1.2f, 0f), 1.5f);
            Box(room, "Lift_Doors_Lobby", new Vector3(100.1f, y + 1.6f, 1.2f), new Vector3(0.6f, 3.2f, 1.4f), Mat("Door", new Color(0.55f, 0.57f, 0.6f), metallic: 0.6f), collider: false);
            Label(room, "FREIGHT LIFT", new Vector3(101.5f, y + 4f, 1.9f), new Color(1f, 0.7f, 0.3f), 0.5f);

            // Companion terminal: the one who isn't with you waits here.
            GameObject term = new GameObject("CompanionTerminal");
            term.transform.SetParent(room, false);
            term.transform.position = new Vector3(106.5f, y, 0f);
            term.AddComponent<CompanionTerminal>().SetInteractionPoint(new Vector3(0f, 1.2f, 0f), 1.4f);
            Box(term.transform, "Console", new Vector3(106.5f, y + 0.6f, 1.2f), new Vector3(0.9f, 1.2f, 0.6f), Mat("Metal", new Color(0.5f, 0.52f, 0.55f), metallic: 0.8f), collider: false);
            Box(term.transform, "Screen", new Vector3(106.5f, y + 1.35f, 0.95f), new Vector3(0.7f, 0.4f, 0.05f),
                Mat("TerminalScreen", new Color(0.2f, 0.9f, 0.7f), emission: new Color(0.2f, 0.9f, 0.7f) * 2f), collider: false);
            Label(room, "companion terminal", new Vector3(106.5f, y + 2.3f, 1.9f), new Color(0.5f, 0.9f, 0.8f), 0.35f);
            Label(room, "LIFT LOBBY", new Vector3(104f, y + 6.5f, 1.9f), new Color(0.8f, 0.9f, 1f));
        }

        private static void BuildRoomG(Transform region)
        {
            Transform room = Group(region, "G_Labs");
            float y = R2Y;
            Material metal = Mat("Metal", new Color(0.5f, 0.52f, 0.55f), metallic: 0.8f);
            Checkpoint(room, "Checkpoint_G", new Vector3(113.5f, y, 0f));

            // Lab bench (a jump up) with the remote override on top.
            Box(room, "Lab_Bench", new Vector3(115.5f, y + 0.6f, 0f), new Vector3(3f, 1.2f, 2f), Mat("LabBench", new Color(0.75f, 0.78f, 0.8f)));
            GameObject tool = Pickup(room, "Pickup_Override", new Vector3(115.5f, y + 1.9f, 0f),
                Mat("Pickup_Override", new Color(1f, 0.75f, 0.2f), emission: new Color(1f, 0.7f, 0.15f) * 1.5f));
            tool.GetComponent<ItemPickup>().ConfigureTool(RemoteConsole.OverrideTool, "Remote Override");
            tool.GetComponent<ItemPickup>().SetSaveId("pickup_override");

            // The pit and its bridge. Retracted, the bridge sits inside the far floor; the console slides it out.
            var bridgeGo = new GameObject("Bridge_Labs");
            bridgeGo.transform.SetParent(room, false);
            bridgeGo.transform.position = new Vector3((PitStart + PitEnd) * 0.5f + (PitEnd - PitStart), y - 0.15f, 0f);
            GameObject deck = Box(bridgeGo.transform, "Deck", bridgeGo.transform.position, new Vector3(PitEnd - PitStart, 0.3f, 2f), metal);
            deck.transform.SetParent(bridgeGo.transform, true);

            var consoleGo = new GameObject("Console_Bridge");
            consoleGo.transform.SetParent(room, false);
            consoleGo.transform.position = new Vector3(PitEnd + 1.2f, y, 0f);
            Box(consoleGo.transform, "Panel", new Vector3(PitEnd + 1.2f, y + 0.7f, 1.0f), new Vector3(0.6f, 1.4f, 0.5f), metal, collider: false);
            GameObject light = Box(consoleGo.transform, "StatusLight", new Vector3(PitEnd + 1.2f, y + 1.55f, 0.7f), new Vector3(0.3f, 0.15f, 0.1f),
                Mat("StatusLight", Color.white, emission: Color.white), collider: false);
            RemoteConsole bc = consoleGo.AddComponent<RemoteConsole>();
            bc.Configure("bridge controls", bridgeGo.transform, Vector3.left * (PitEnd - PitStart), light.GetComponent<Renderer>(), "bridge_labs");
            bc.SetInteractionPoint(new Vector3(0f, 1.2f, 0f), 10f);

            // Ladder out of the pit, against the near wall.
            GameObject ladderGo = new GameObject("Ladder_Pit");
            ladderGo.transform.SetParent(room, false);
            ladderGo.transform.position = new Vector3(PitStart + 0.5f, y - PitDepth, 0f);
            ladderGo.AddComponent<Ladder>().Configure(PitDepth, -1);
            for (int i = -1; i <= 1; i += 2)
            {
                Box(ladderGo.transform, "Rail", new Vector3(PitStart + 0.5f + i * 0.3f, y - PitDepth * 0.5f + 0.1f, 0.6f), new Vector3(0.08f, PitDepth + 0.2f, 0.08f), metal, collider: false);
            }
            for (float ry = 0.3f; ry < PitDepth; ry += 0.4f)
            {
                Box(ladderGo.transform, "Rung", new Vector3(PitStart + 0.5f, y - PitDepth + ry, 0.6f), new Vector3(0.6f, 0.05f, 0.05f), metal, collider: false);
            }
            Material hazardStripe = Mat("HazardStripe", new Color(0.9f, 0.7f, 0.1f));
            Box(room, "PitEdge_Near", new Vector3(PitStart - 0.15f, y + 0.01f, 0f), new Vector3(0.3f, 0.02f, 2.2f), hazardStripe, collider: false);
            Box(room, "PitEdge_Far", new Vector3(PitEnd + 0.15f, y + 0.01f, 0f), new Vector3(0.3f, 0.02f, 2.2f), hazardStripe, collider: false);

            Label(room, "LABS", new Vector3(116f, y + 6.5f, 1.9f), new Color(0.85f, 0.95f, 1f));
        }

        private static void BuildRoomH(Transform region)
        {
            Transform room = Group(region, "H_SpecimenHall");
            float y = R2Y;
            Material metal = Mat("Metal", new Color(0.5f, 0.52f, 0.55f), metallic: 0.8f);
            Material encounter = Mat("EncounterMarker", new Color(0.3f, 0.02f, 0.02f), emission: new Color(1f, 0.05f, 0.05f) * 0.45f);
            Material grate = Mat("ArcGrate", new Color(0.12f, 0.14f, 0.05f), emission: new Color(0.7f, 1f, 0.15f) * 1.5f);
            Checkpoint(room, "Checkpoint_H", new Vector3(133.2f, y, 0f));

            // Encounter on the 1 m lattice from x = 134.
            GameObject zoneMarker = Box(room, "EncounterZone_Marker_Specimens", new Vector3(141f, y + 0.02f, 0f), new Vector3(14f, 0.04f, 2.5f), encounter, collider: false);
            Box(room, "Cover_Tank", new Vector3(137.5f, y + 0.5f, 0f), new Vector3(1f, 1f, 2f), Mat("Crate", new Color(0.45f, 0.4f, 0.32f)));
            Box(room, "Specimen_Platform", new Vector3(141.5f, y + 2f, 0f), new Vector3(3f, 0.3f, 2f), metal);
            Box(room, "Raised_Block", new Vector3(146.5f, y + 0.8f, 0f), new Vector3(3f, 1.6f, 2.4f), Mat("ArchiveBlock", new Color(0.28f, 0.3f, 0.34f)));
            Box(room, "Hazard_ArcGrate_C", new Vector3(138.5f, y + 0.03f, 0f), new Vector3(0.9f, 0.05f, 2f), grate, collider: false);

            GameObject engageSign = Hint(Label(room, "[ optional encounter: E to engage ]", new Vector3(141f, y + 4.2f, 1.9f), new Color(1f, 0.4f, 0.35f), 0.4f), "[ optional encounter: USE to engage ]");
            var encGo = new GameObject("Encounter_Specimens");
            encGo.transform.SetParent(room, false);
            encGo.transform.position = new Vector3(135.5f, y, 0f);
            EncounterController enc = encGo.AddComponent<EncounterController>();
            enc.SetInteractionPoint(new Vector3(0f, 0.9f, 0f), 1.2f);
            enc.Configure(
                new Rect(134f, y - 0.2f, 14f, 6.2f),
                new[]
                {
                    new EncounterController.EnemySpawn { unit = "Enforcer", position = new Vector2(139.5f, y) },
                    new EncounterController.EnemySpawn { unit = "Sentry", position = new Vector2(141.5f, y + 2.2f) },
                    new EncounterController.EnemySpawn { unit = "Warden", position = new Vector2(146.5f, y + 1.6f) },
                },
                50,
                "encounter_specimens_cleared",
                "Recovered: specimen intake log (fragment).",
                TransparentMat("GridTile", new Color(1f, 1f, 1f, 0.3f)),
                Mat("Enemy", new Color(0.5f, 0.16f, 0.14f)),
                Mat("EnemyVisor", new Color(1f, 0.25f, 0.2f), emission: new Color(1f, 0.2f, 0.15f) * 2f),
                zoneMarker.GetComponent<Renderer>(),
                engageSign);
            enc.SetHazards(new Vector2(138.5f, y));
            enc.SetUnitModels(
                new EncounterController.UnitModel { unit = "Enforcer", prefab = CharacterArt.Prefab("Enforcer") },
                new EncounterController.UnitModel { unit = "Sentry", prefab = CharacterArt.Prefab("Sentry") },
                new EncounterController.UnitModel { unit = "Warden", prefab = CharacterArt.Prefab("Warden") });

            // Companion 2 waits past the fight (you can walk past idle hostiles without engaging).
            BuildResearcher(room, new Vector3(150f, y, 0f));

            // Freight shutter at the end: also opened by the override. Beyond it, the end of the slice.
            var shutterGo = new GameObject("Shutter_Freight");
            shutterGo.transform.SetParent(room, false);
            shutterGo.transform.position = new Vector3(152.5f, y + 2f, 0f);
            GameObject slab = Box(shutterGo.transform, "Slab", shutterGo.transform.position, new Vector3(0.5f, 4f, Depth), Mat("Shutter", new Color(0.42f, 0.4f, 0.32f), metallic: 0.4f));
            slab.transform.SetParent(shutterGo.transform, true);
            Box(room, "Shutter_Header", new Vector3(152.5f, y + 6.1f, 0f), new Vector3(0.6f, 4.2f, Depth), Mat("LabConcrete", new Color(0.36f, 0.38f, 0.4f)));
            var sc = new GameObject("Console_Shutter");
            sc.transform.SetParent(room, false);
            sc.transform.position = new Vector3(155f, y, 0f);
            GameObject sLight = Box(sc.transform, "StatusLight", new Vector3(152.1f, y + 4.2f, -0.8f), new Vector3(0.3f, 0.15f, 0.1f),
                Mat("StatusLight", Color.white, emission: Color.white), collider: false);
            RemoteConsole shutter = sc.AddComponent<RemoteConsole>();
            shutter.Configure("freight shutter", shutterGo.transform, Vector3.up * 4.2f, sLight.GetComponent<Renderer>(), "shutter_freight");
            shutter.SetInteractionPoint(new Vector3(0f, 1.2f, 0f), 10f);
            Label(room, "end of the slice", new Vector3(154.5f, y + 2.6f, 1.9f), new Color(0.5f, 0.55f, 0.6f), 0.35f);
            Label(room, "SPECIMEN HALL", new Vector3(137f, y + 6.5f, 1.9f), new Color(0.5f, 1f, 0.7f));
        }

        private static void BuildResearcher(Transform room, Vector3 feet)
        {
            TextAsset recruit = AssetDatabase.LoadAssetAtPath<TextAsset>($"{DialogueDir}/researcher_recruit.json");
            GameObject npcGo = new GameObject("Researcher");
            npcGo.transform.SetParent(room, false);
            npcGo.transform.position = feet;
            npcGo.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            CharacterBody(npcGo.transform, "Researcher", new Color(0.85f, 0.88f, 0.9f), new Color(0.3f, 1f, 0.7f));
            CompanionFollower follower = npcGo.AddComponent<CompanionFollower>();
            follower.SetCombatUnit("Researcher");
            CompanionNPC npc = npcGo.AddComponent<CompanionNPC>();
            npc.Configure("Researcher", recruit, "met_researcher", "companion_researcher");
            npc.SetInteractionPoint(new Vector3(0f, 0.9f, 0f), 1.3f);
        }

        private static void ResearchLights(Transform parent)
        {
            Transform r = Group(parent, "ResearchWing");
            float y = R2Y;
            Color lab = new Color(0.9f, 0.96f, 1f);
            Color tank = new Color(0.3f, 1f, 0.6f);
            Fixture(r, "Fixture_Lobby", new Vector3(105f, y + 7.85f, 0f), 2f, Cool, 34f, "Fixture_Cool");
            Fixture(r, "Fixture_Labs_A", new Vector3(116f, y + 7.85f, 0f), 2.4f, lab, 46f, "Fixture_Cool");
            Fixture(r, "Fixture_Labs_B", new Vector3(126f, y + 7.85f, 0f), 2.4f, lab, 40f, "Fixture_Cool");
            Spot(r, "Key_Pit", new Vector3(125.5f, y + 7.8f, -1.6f), Quaternion.Euler(75f, 0f, 0f), lab, 30f, 14f, 70f);
            Fixture(r, "Fixture_Hall", new Vector3(141f, y + 7.85f, 0f), 2f, Cool, 28f, "Fixture_Cool");
            Point(r, "TankGlow_A", new Vector3(136f, y + 2f, 1.3f), tank, 3f, 6f);
            Point(r, "TankGlow_B", new Vector3(146f, y + 3f, 1.3f), tank, 3f, 6f);
            Point(r, "TankGlow_C", new Vector3(150f, y + 1.5f, 1.3f), tank, 2.5f, 5f);
        }

        private static void ResearchZones(Transform zones)
        {
            float y = R2Y;
            CameraZoneAt(zones, "Zone_F", new Vector3(106f, y + 4f, 0f), new Vector2(14f, 8.5f), "Lift Lobby");
            CameraZoneAt(zones, "Zone_G", new Vector3(122f, y + 1.5f, 0f), new Vector2(20f, 15f), "Labs"); // reaches down into the pit
            CameraZoneAt(zones, "Zone_H", new Vector3(144f, y + 4f, 0f), new Vector2(24f, 8.5f), "Specimen Hall");
        }

        private static void ResearchProps(Transform root)
        {
            Transform r = Group(root, "ResearchWing");
            float y = R2Y;
            BackWallPanels(Group(r, "R2_BackWallPanels"), (int)R2Start - 1, (int)R2End + 1, y);
            FloorTiles(Group(r, "R2_FloorTiles"), (int)R2Start - 1, (int)R2End + 1, y, PitStart, PitEnd, "R2_Floor_West", "R2_Floor_East");
            Prop(r, "computer-system", new Vector3(108.6f, y, 1.45f), 180f, 1.4f);
            Prop(r, "table-display", new Vector3(119.5f, y, 1.4f), 180f, 1.3f);
            Prop(r, "computer-screen", new Vector3(115.5f, y + 1.2f, 0.6f), 180f, 1.2f);
            Prop(r, "container-tall", new Vector3(131.2f, y, 1.45f), 0f, 1.4f);
            Prop(r, "table-display-planet", new Vector3(149.2f, y, 1.4f), 180f, 1.3f);
            Prop(r, "computer-wide", new Vector3(141.5f, y + 2.15f, 1.45f), 180f, 1.2f);
            Reskin("Lab_Bench", "table-large");
            Reskin("Cover_Tank", "container");
            Reskin("Specimen_Platform", "floor-panel");
            Reskin("Raised_Block", "structure");
        }
    }
}
