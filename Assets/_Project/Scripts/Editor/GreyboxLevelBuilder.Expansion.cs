using Game.Audio;
using Game.Combat;
using Game.World;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Slice expansion east of Security (2026-11-26, first pass):
    ///   D  Records      x 56..72   archive stacks; a ladder to the loft where the signal spoofer sits;
    ///                              optional scrap on top of the archive block (ledge climb)
    ///   E  Server Hall  x 72..90   behind a laser grid that only the spoofer opens; second optional encounter
    ///                              (Enforcer, Sentry and the armoured Warden) with a mezzanine and two arc grates;
    ///                              a freight lift at the far end marks the end of the slice
    /// Room names and the lift sign are placeholders, not story.
    /// </summary>
    public static partial class GreyboxLevelBuilder
    {
        public const float SliceEndX = 90f;

        private static void BuildRoomD(Transform level)
        {
            Transform room = Group(level, "D_Records");
            Material metal = Mat("Metal", new Color(0.5f, 0.52f, 0.55f), metallic: 0.8f);
            Material block = Mat("ArchiveBlock", new Color(0.28f, 0.3f, 0.34f));

            Checkpoint(room, "Checkpoint_D", new Vector3(57.5f, 0f, 0f));

            // Archive stack: a ledge climb with optional scrap on top.
            Box(room, "Block_Archive", new Vector3(60.5f, 1.2f, 0f), new Vector3(3f, 2.4f, 2.4f), block);
            GameObject scrap = Pickup(room, "Pickup_Scrap_Archive", new Vector3(60.5f, 3f, 0f),
                Mat("Pickup_Scrap", new Color(0.8f, 0.75f, 0.6f), emission: new Color(0.8f, 0.7f, 0.4f) * 0.8f));
            scrap.GetComponent<ItemPickup>().ConfigureScrap(15);
            scrap.GetComponent<ItemPickup>().SetSaveId("pickup_scrap_archive");

            // Ladder up to the loft (same build as the Maintenance ladder).
            GameObject ladderGo = new GameObject("Ladder_Records");
            ladderGo.transform.SetParent(room, false);
            ladderGo.transform.position = new Vector3(63f, 0f, 0f);
            ladderGo.AddComponent<Ladder>().Configure(4f, 1);
            for (int i = -1; i <= 1; i += 2)
            {
                Box(ladderGo.transform, "Rail", new Vector3(63f + i * 0.3f, 2.1f, 0.6f), new Vector3(0.08f, 4.2f, 0.08f), metal, collider: false);
            }
            for (float y = 0.3f; y < 4f; y += 0.4f)
            {
                Box(ladderGo.transform, "Rung", new Vector3(63f, y, 0.6f), new Vector3(0.6f, 0.05f, 0.05f), metal, collider: false);
            }
            Box(room, "Loft", new Vector3(66.5f, 3.85f, 0f), new Vector3(6f, 0.3f, 2f), metal);
            Box(room, "Loft_Rail", new Vector3(66.5f, 4.6f, -0.9f), new Vector3(6f, 0.05f, 0.05f), metal, collider: false);

            GameObject spoofer = Pickup(room, "Pickup_Spoofer", new Vector3(68.5f, 4.6f, 0f),
                Mat("Pickup_Tool", new Color(0.3f, 1f, 0.6f), emission: new Color(0.3f, 1f, 0.6f) * 1.5f));
            spoofer.GetComponent<ItemPickup>().ConfigureTool(LaserGrid.SpooferTool, "Signal Spoofer");
            spoofer.GetComponent<ItemPickup>().SetSaveId("pickup_spoofer");

            // Laser grid into the Server Hall, under a header like the Security door.
            Material concrete = Mat("Concrete", new Color(0.32f, 0.33f, 0.35f));
            Box(room, "Grid_Header", new Vector3(72f, 5.5f, 0f), new Vector3(0.6f, 5f, Depth), concrete);
            var gridGo = new GameObject("LaserGrid_Servers");
            gridGo.transform.SetParent(room, false);
            gridGo.transform.position = new Vector3(72f, 0f, 0f);
            GameObject wall = new GameObject("Barrier");
            wall.transform.SetParent(gridGo.transform, false);
            BoxCollider wc = wall.AddComponent<BoxCollider>();
            wc.center = new Vector3(0f, 1.5f, 0f);
            wc.size = new Vector3(0.3f, 3f, Depth);
            var beams = new GameObject("Beams");
            beams.transform.SetParent(gridGo.transform, false);
            Material beam = Mat("LaserBeam", new Color(1f, 0.1f, 0.1f), emission: new Color(1f, 0.08f, 0.05f) * 4f);
            for (float y = 0.35f; y < 3f; y += 0.45f)
            {
                Box(beams.transform, "Beam", new Vector3(72f, y, 0f), new Vector3(0.04f, 0.03f, 2.6f), beam, collider: false);
            }
            PointLight(beams.transform, "BeamGlow", new Vector3(71.6f, 1.5f, -0.8f), new Color(1f, 0.15f, 0.1f), 2f, 4f, shadows: false);
            GameObject status = Box(gridGo.transform, "StatusLight", new Vector3(71.55f, 3.3f, -0.8f), new Vector3(0.3f, 0.15f, 0.1f),
                Mat("StatusLight", Color.white, emission: Color.white), collider: false);
            LaserGrid grid = gridGo.AddComponent<LaserGrid>();
            grid.Configure(LaserGrid.SpooferTool, "signal spoofer", wc, beams, status.GetComponent<Renderer>(), "grid_servers");
            grid.SetInteractionPoint(new Vector3(-0.6f, 1.2f, 0f), 1.5f);

            Label(room, "RECORDS", new Vector3(60f, 6.5f, 1.9f), new Color(0.75f, 0.85f, 1f));
            Hint(Label(room, "climb [W]", new Vector3(63f, 1.8f, 1.9f), new Color(0.5f, 0.55f, 0.6f), 0.4f), "climb [stick up]");
        }

        private static void BuildRoomE(Transform level)
        {
            Transform room = Group(level, "E_ServerHall");
            Material metal = Mat("Metal", new Color(0.5f, 0.52f, 0.55f), metallic: 0.8f);
            Material encounter = Mat("EncounterMarker", new Color(0.3f, 0.02f, 0.02f), emission: new Color(1f, 0.05f, 0.05f) * 0.45f);
            Material grate = Mat("ArcGrate", new Color(0.12f, 0.14f, 0.05f), emission: new Color(0.7f, 1f, 0.15f) * 1.5f);

            Checkpoint(room, "Checkpoint_E", new Vector3(73.5f, 0f, 0f));

            // Encounter geometry on the 1 m lattice (tile edges at whole numbers from x = 74).
            GameObject zoneMarker = Box(room, "EncounterZone_Marker_Servers", new Vector3(81.5f, 0.02f, 0f), new Vector3(15f, 0.04f, 2.5f), encounter, collider: false);
            Box(room, "Cover_Rack", new Vector3(79.5f, 0.5f, 0f), new Vector3(1f, 1f, 2f), Mat("Crate", new Color(0.45f, 0.4f, 0.32f))).AddComponent<LowCover>();
            Box(room, "Mezzanine", new Vector3(84f, 2f, 0f), new Vector3(4f, 0.3f, 2f), metal);
            Box(room, "Hazard_ArcGrate_A", new Vector3(81.5f, 0.03f, 0f), new Vector3(0.9f, 0.05f, 2f), grate, collider: false);
            Box(room, "Hazard_ArcGrate_B", new Vector3(87.5f, 0.03f, 0f), new Vector3(0.9f, 0.05f, 2f), grate, collider: false);

            GameObject engageSign = Hint(Label(room, "[ optional encounter: E to engage ]", new Vector3(81.5f, 4.2f, 1.9f), new Color(1f, 0.4f, 0.35f), 0.4f), "[ optional encounter: USE to engage ]");
            var encGo = new GameObject("Encounter_Servers");
            encGo.transform.SetParent(room, false);
            encGo.transform.position = new Vector3(75.5f, 0f, 0f);
            EncounterController enc = encGo.AddComponent<EncounterController>();
            enc.SetInteractionPoint(new Vector3(0f, 0.9f, 0f), 1.2f);
            enc.Configure(
                new Rect(74f, -0.2f, 15f, 6.2f),
                new[]
                {
                    new EncounterController.EnemySpawn { unit = "Enforcer", position = new Vector2(80.5f, 0f) },
                    new EncounterController.EnemySpawn { unit = "Sentry", position = new Vector2(84.5f, 2.2f) },
                    new EncounterController.EnemySpawn { unit = "Warden", position = new Vector2(86.5f, 0f) },
                },
                40,
                "encounter_servers_cleared",
                "Recovered: server maintenance log (fragment).",
                TransparentMat("GridTile", new Color(1f, 1f, 1f, 0.3f)),
                Mat("Enemy", new Color(0.5f, 0.16f, 0.14f)),
                Mat("EnemyVisor", new Color(1f, 0.25f, 0.2f), emission: new Color(1f, 0.2f, 0.15f) * 2f),
                zoneMarker.GetComponent<Renderer>(),
                engageSign);
            enc.SetHazards(new Vector2(81.5f, 0f), new Vector2(87.5f, 0f));
            enc.SetUnitModels(
                new EncounterController.UnitModel { unit = "Enforcer", prefab = CharacterArt.Prefab("Enforcer") },
                new EncounterController.UnitModel { unit = "Sentry", prefab = CharacterArt.Prefab("Sentry") },
                new EncounterController.UnitModel { unit = "Warden", prefab = CharacterArt.Prefab("Warden") });

            // Freight lift to the Research Wing (region 2).
            Box(room, "Lift_Doors", new Vector3(89.6f, 1.6f, 1.2f), new Vector3(0.6f, 3.2f, 1.4f), Mat("Door", new Color(0.55f, 0.57f, 0.6f), metallic: 0.6f), collider: false);
            Label(room, "FREIGHT LIFT", new Vector3(88.2f, 4f, 1.9f), new Color(1f, 0.7f, 0.3f), 0.5f);
            Label(room, "UP to the Research Wing", new Vector3(88f, 3.4f, 1.9f), new Color(0.5f, 0.55f, 0.6f), 0.35f);
            var liftGo = new GameObject("FreightLift_Down");
            liftGo.transform.SetParent(room, false);
            liftGo.transform.position = new Vector3(89.2f, 0f, 0f);
            FreightLift lift = liftGo.AddComponent<FreightLift>();
            lift.Configure(new Vector3(102.5f, R2Y + 0.05f, 0f), 1, "Ride the freight lift up (Research Wing)", "Research Wing. Closer to the surface.");
            lift.SetInteractionPoint(new Vector3(-0.2f, 1.2f, 0f), 1.5f);
            Label(room, "SERVER HALL", new Vector3(77f, 6.5f, 1.9f), new Color(0.4f, 1f, 0.9f));
        }

        private static void ExpansionLights(Transform parent)
        {
            Transform records = Group(parent, "Records");
            Fixture(records, "Fixture_Archive", new Vector3(60f, 7.85f, 0f), 2f, Cool, 36f, "Fixture_Cool");
            Spot(records, "Key_Loft", new Vector3(66.5f, 7.8f, -1.6f), Quaternion.Euler(70f, 0f, 0f), Sodium, 40f, 12f, 80f);

            Transform servers = Group(parent, "ServerHall");
            Color teal = new Color(0.25f, 0.95f, 0.85f);
            Fixture(servers, "Fixture_Servers", new Vector3(77f, 7.85f, 0f), 2f, Cool, 30f, "Fixture_Cool");
            Fixture(servers, "Fixture_Mezz", new Vector3(85f, 7.85f, 0f), 2f, Cool, 26f, "Fixture_Cool");
            Point(servers, "RackGlow_A", new Vector3(76f, 1.5f, 1.2f), teal, 3f, 6f);
            Point(servers, "RackGlow_B", new Vector3(84f, 4f, 1.2f), teal, 3f, 6f);
        }

        private static void ExpansionProps(Transform parent)
        {
            Transform records = Group(parent, "Records");
            Prop(records, "container-tall", new Vector3(57.8f, 0f, 1.45f), 0f, 1.4f);
            Prop(records, "container", new Vector3(65f, 0f, 1.45f), 15f, 1.4f);
            Prop(records, "container-flat", new Vector3(66.5f, 0f, 1.45f), 0f, 1.6f);
            Prop(records, "computer-screen", new Vector3(69.8f, 4f, 1.4f), 180f, 1.4f);
            Prop(records, "wall-switch", new Vector3(71.4f, 1.2f, 1.95f), 180f);

            Transform servers = Group(parent, "ServerHall");
            for (int i = 0; i < 4; i++) Prop(servers, "computer-system", new Vector3(74.6f + i * 0.75f, 0f, 1.45f), 180f, 1.4f);
            Prop(servers, "computer-wide", new Vector3(84f, 2.15f, 1.45f), 180f, 1.3f);
            Prop(servers, "display-wall-wide", new Vector3(80f, 2.2f, 1.95f), 180f, 1.6f);
            Prop(servers, "container-tall", new Vector3(88.6f, 0f, 1.45f), 0f, 1.4f);
            Reskin("Cover_Rack", "container");
        }
    }
}
