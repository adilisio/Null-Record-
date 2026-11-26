using Game.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.EditorTools
{
    /// <summary>
    /// Lighting pass for the greybox: post-processing, fog, and a distinct mood per room.
    ///   Holding      cold fluorescent strips, one failing
    ///   Maintenance  warm sodium cage lamps over the orange steam glow
    ///   Security     dim cold work light under a rotating red alarm beacon
    /// Only spot lights cast shadows (points are expensive in the shadow atlas).
    /// </summary>
    public static partial class GreyboxLevelBuilder
    {
        private const string LightingDir = "Assets/_Project/Art/Lighting";
        private const string VolumeProfilePath = LightingDir + "/Greybox_PostFX.asset";

        private static readonly Color FogColor = new Color(0.028f, 0.032f, 0.042f);
        private static readonly Color Cool = new Color(0.78f, 0.88f, 1f);
        private static readonly Color Sodium = new Color(1f, 0.6f, 0.28f);
        private static readonly Color Alarm = new Color(1f, 0.08f, 0.05f);

        private static void BuildLighting(Transform level, Camera cam, Transform player)
        {
            Transform lights = Group(level, "Lighting");

            // Soft key on the camera side of the player so they read against dark walls.
            var key = new GameObject("PlayerKeyLight");
            key.transform.SetParent(lights, false);
            key.transform.position = player.position + new Vector3(0f, 1.4f, -1.6f);
            Light kl = key.AddComponent<Light>();
            kl.type = LightType.Point;
            kl.color = new Color(0.85f, 0.9f, 1f);
            kl.intensity = 1.6f;
            kl.range = 3.5f;
            kl.shadows = LightShadows.None;
            key.AddComponent<FollowOffset>().Configure(player, new Vector3(0f, 1.4f, -1.6f));

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.075f, 0.09f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.018f;
            RenderSettings.fogColor = FogColor;
            cam.backgroundColor = FogColor;

            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            var volumeGo = new GameObject("PostFX");
            volumeGo.transform.SetParent(lights, false);
            Volume volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = PostFxProfile();

            HoldingLights(lights);
            MaintenanceLights(lights);
            SecurityLights(lights);
            ExpansionLights(lights);
            ResearchLights(lights);
        }

        // ---------------------------------------------------------------- rooms

        private static void HoldingLights(Transform parent)
        {
            Transform room = Group(parent, "Holding");
            Fixture(room, "Fixture_Cell", new Vector3(2.5f, 7.85f, 0f), 2.2f, Cool, 48f, "Fixture_Cool");
            (Light flicker, Renderer strip) = Fixture(room, "Fixture_Failing", new Vector3(9.5f, 7.85f, 0f), 2.2f, Cool, 48f, "Fixture_Cool");
            flicker.gameObject.AddComponent<LightFlicker>().Configure(flicker, strip, Cool * 4f);
            Fixture(room, "Fixture_Block", new Vector3(16f, 7.85f, 0f), 2.2f, Cool, 44f, "Fixture_Cool");

            // Faint blue rim behind the cell bars so the start reads as a cell.
            Point(room, "Rim_Cell", new Vector3(-0.8f, 3f, 1.2f), new Color(0.35f, 0.5f, 1f), 3f, 5f);
        }

        private static void MaintenanceLights(Transform parent)
        {
            Transform room = Group(parent, "Maintenance");
            Material glass = Mat("Lamp_Sodium", Sodium, emission: Sodium * 2.5f);
            Material metal = Mat("Housing", new Color(0.12f, 0.12f, 0.13f), metallic: 0.7f);

            foreach (Vector3 p in new[] { new Vector3(24.5f, 6.6f, 0f), new Vector3(29.5f, 6.4f, 0f), new Vector3(36f, 6.2f, 0f) })
            {
                // Cable, cage and bulb.
                Box(room, "Cable", p + new Vector3(0f, (8f - p.y) * 0.5f + 0.1f, 0f), new Vector3(0.03f, 8f - p.y, 0.03f), metal, collider: false);
                Box(room, "Cage", p + new Vector3(0f, 0.12f, 0f), new Vector3(0.42f, 0.08f, 0.42f), metal, collider: false);
                GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = "Bulb";
                Object.DestroyImmediate(bulb.GetComponent<Collider>());
                bulb.transform.SetParent(room, true);
                bulb.transform.position = p;
                bulb.transform.localScale = Vector3.one * 0.26f;
                bulb.GetComponent<Renderer>().sharedMaterial = glass;
                Point(room, "Lamp", p + new Vector3(0f, -0.3f, -0.4f), Sodium, 16f, 10f);
            }

            // One shadowed key light so the catwalk and ladder throw shadows on the back wall.
            Spot(room, "Key_Catwalk", new Vector3(25.5f, 7.8f, -1.6f), Quaternion.Euler(70f, 0f, 0f), Sodium, 45f, 14f, 85f);
        }

        private static void SecurityLights(Transform parent)
        {
            Transform room = Group(parent, "Security");
            Fixture(room, "Fixture_Work", new Vector3(44f, 7.85f, 0f), 2f, Cool, 42f, "Fixture_Cool");
            Fixture(room, "Fixture_Far", new Vector3(53f, 7.85f, 0f), 2f, Cool, 32f, "Fixture_Cool");

            // Emergency strip along the ceiling.
            Material red = Mat("Strip_Alarm", Alarm, emission: Alarm * 2f);
            Box(room, "AlarmStrip", new Vector3(50.5f, 7.95f, -0.9f), new Vector3(9f, 0.06f, 0.08f), red, collider: false);
            Point(room, "AlarmFill", new Vector3(50f, 6.5f, -1.5f), Alarm, 6f, 12f);

            // Rotating beacon: two opposed red spots on a spinning pivot.
            Material metal = Mat("Housing", new Color(0.12f, 0.12f, 0.13f), metallic: 0.7f);
            Material lens = Mat("Beacon_Lens", Alarm, emission: Alarm * 2.5f);
            Vector3 b = new Vector3(48.5f, 7.6f, -0.3f);
            Box(room, "BeaconBase", b + new Vector3(0f, 0.25f, 0f), new Vector3(0.3f, 0.12f, 0.3f), metal, collider: false);
            GameObject lensGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lensGo.name = "BeaconLens";
            Object.DestroyImmediate(lensGo.GetComponent<Collider>());
            lensGo.transform.SetParent(room, true);
            lensGo.transform.position = b;
            lensGo.transform.localScale = new Vector3(0.24f, 0.14f, 0.24f);
            lensGo.GetComponent<Renderer>().sharedMaterial = lens;

            var pivot = new GameObject("BeaconPivot");
            pivot.transform.SetParent(room, true);
            pivot.transform.position = b;
            pivot.AddComponent<Rotator>().Configure(Vector3.up, 150f);
            for (int i = 0; i < 2; i++)
            {
                Light s = Spot(pivot.transform, $"Beam{i}", b, Quaternion.Euler(35f, i * 180f, 0f), Alarm, 60f, 16f, 45f);
                s.transform.SetParent(pivot.transform, true);
            }

            // Cold spill through the doorway.
            Point(room, "DoorSpill", new Vector3(41f, 3.5f, -1.4f), Cool, 4f, 6f);
        }

        /// <summary>
        /// Renders each room of the saved greybox scene to Screenshots/lighting_*.png (edit mode, no Play needed).
        /// Batch: -executeMethod Game.EditorTools.GreyboxLevelBuilder.CaptureLightingShots
        /// </summary>
        [MenuItem("Null Record/Capture Lighting Shots", priority = 30)]
        public static void CaptureLightingShots()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
            Camera cam = Camera.main;
            var rig = cam.GetComponent<Game.CameraSystem.SideCameraRig>();
            if (rig != null) rig.enabled = false;
            string dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "Screenshots");
            System.IO.Directory.CreateDirectory(dir);

            var shots = new (string name, Vector2 focus)[]
            {
                ("lighting_holding", new Vector2(8f, 4f)),
                ("lighting_maintenance", new Vector2(29f, 4f)),
                ("lighting_security", new Vector2(48f, 4f)),
                ("lighting_records", new Vector2(64f, 4f)),
                ("lighting_servers", new Vector2(81f, 4f)),
            };
            const int w = 1280, h = 720;
            foreach ((string name, Vector2 focus) in shots)
            {
                cam.transform.SetPositionAndRotation(new Vector3(focus.x, focus.y, -16f), Quaternion.identity);
                RenderTexture rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            Debug.Log($"[Null Record] Lighting shots written to {dir}");
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>Ceiling strip light: housing, emissive tube, and a downward shadowed spot.</summary>
        private static (Light, Renderer) Fixture(Transform parent, string name, Vector3 pos, float length, Color color, float intensity, string matName)
        {
            Material housing = Mat("Housing", new Color(0.12f, 0.12f, 0.13f), metallic: 0.7f);
            Material tube = Mat(matName, color, emission: color * 4f);
            Box(parent, name + "_Housing", pos + new Vector3(0f, 0.05f, 0f), new Vector3(length + 0.15f, 0.08f, 0.4f), housing, collider: false);
            GameObject t = Box(parent, name + "_Tube", pos - new Vector3(0f, 0.03f, 0f), new Vector3(length, 0.05f, 0.22f), tube, collider: false);
            Renderer r = t.GetComponent<Renderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            // Angled a little toward the back wall so each fixture paints a pool of light on it.
            Light l = Spot(parent, name + "_Light", pos + new Vector3(0f, -0.1f, -0.8f), Quaternion.Euler(72f, 0f, 0f), color, intensity, 16f, 120f);
            return (l, r);
        }

        private static Light Spot(Transform parent, string name, Vector3 pos, Quaternion rot, Color color, float intensity, float range, float angle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, rot);
            Light l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.55f;
            l.shadows = LightShadows.Soft;
            l.shadowBias = 0.03f;
            l.shadowNormalBias = 0.3f;
            return l;
        }

        private static void Point(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
        }

        /// <summary>Creates or updates the post-processing profile in place, so its GUID stays stable.</summary>
        private static VolumeProfile PostFxProfile()
        {
            EnsureFolder(LightingDir);
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }

            Tonemapping tone = Get<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.ACES);

            Bloom bloom = Get<Bloom>(profile);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.72f);
            bloom.highQualityFiltering.Override(true);

            Vignette vig = Get<Vignette>(profile);
            vig.intensity.Override(0.36f);
            vig.smoothness.Override(0.45f);
            vig.color.Override(Color.black);

            ColorAdjustments grade = Get<ColorAdjustments>(profile);
            grade.postExposure.Override(0.35f);
            grade.contrast.Override(14f);
            grade.saturation.Override(-12f);

            FilmGrain grain = Get<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.16f);
            grain.response.Override(0.8f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing)) return existing;
            T c = profile.Add<T>(true);
            c.name = typeof(T).Name;
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }
    }
}
