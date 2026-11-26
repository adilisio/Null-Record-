using System.IO;
using System.Linq;
using Game.World;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Turns Kenney's Animated Characters pack (CC0) into Null Record's character prefabs:
    /// sets the import options, builds one Animator Controller (Idle / Run / Jump), a material per skin
    /// (skins painted by Tools/Art/make_skins.py) and a prefab per character. Run by Build All Scenes.
    /// </summary>
    public static class CharacterArt
    {
        public const string KenneyDir = "Assets/_Project/ThirdParty/Kenney/AnimatedCharacters";
        public const string Dir = "Assets/_Project/Art/Characters";
        /// <summary>Scales the imported model to about 1.6 m (its T-pose bounds overstate its height).</summary>
        public const float ModelScale = 0.62f;
        /// <summary>Normalised time in the jump clip used as the crouch pose (the wind-up before take-off).</summary>
        public const float CrouchPoseTime = 0.12f;
        public static readonly string[] Roster = { "Subject", "Dace", "Researcher", "Enforcer", "Sentry", "Warden" };

        private const string ModelPath = KenneyDir + "/Model/characterMedium.fbx";

        [MenuItem("Null Record/Build Character Prefabs", priority = 5)]
        public static void Setup()
        {
            ConfigureModel();
            AnimationClip idle = ConfigureClip("idle", "Idle", true);
            AnimationClip run = ConfigureClip("run", "Run", true);
            AnimationClip jump = ConfigureClip("jump", "Jump", false);
            var actions = CharacterActions.Build(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), idle, $"{Dir}/Actions");
            AnimatorController controller = BuildController(idle, run, jump, actions);
            Directory.CreateDirectory($"{Dir}/Materials");
            foreach (string name in Roster) BuildPrefab(name, controller);
            AssetDatabase.SaveAssets();
            CapturePortraits(idle);
            Debug.Log("[Null Record] Character prefabs built: " + string.Join(", ", Roster));
        }

        /// <summary>Speaker key -> character prefab. Portraits land in Resources/Portraits/&lt;key&gt;.png.</summary>
        private static readonly (string key, string character)[] PortraitSubjects = { ("dace", "Dace"), ("researcher", "Researcher"), ("subject", "Subject") };
        private const string PortraitDir = "Assets/_Project/Resources/Portraits";

        /// <summary>
        /// Renders a head-and-shoulders portrait of each speaking character from its model (idle pose, three-quarter view)
        /// into a sprite. Runs in a scratch scene; Build All then replaces the open scene anyway.
        /// </summary>
        private static void CapturePortraits(AnimationClip idle)
        {
            Directory.CreateDirectory(PortraitDir);
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);

            var camGo = new GameObject("PortraitCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.1f, 0.12f);
            cam.orthographic = true;
            var key = new GameObject("Key").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.3f;
            key.transform.rotation = Quaternion.Euler(25f, 25f, 0f);   // from the camera side, upper left
            var rim = new GameObject("Rim").AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.intensity = 0.7f;
            rim.color = new Color(0.5f, 0.75f, 1f);
            rim.transform.rotation = Quaternion.Euler(10f, 200f, 0f);  // from behind
            RenderSettings.ambientLight = new Color(0.35f, 0.36f, 0.4f);

            var rt = new RenderTexture(256, 256, 24);
            foreach ((string speaker, string character) in PortraitSubjects)
            {
                GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(character));
                inst.transform.rotation = Quaternion.Euler(0f, 160f, 0f); // face the camera, turned slightly
                Transform bones = inst.transform.Find("Model/Root");
                if (idle != null && bones != null) idle.SampleAnimation(bones.gameObject, 0.25f);

                Bounds b = new Bounds();
                bool first = true;
                foreach (Renderer r in inst.GetComponentsInChildren<Renderer>())
                {
                    if (first) { b = r.bounds; first = false; }
                    else b.Encapsulate(r.bounds);
                }
                float h = b.size.y;
                cam.orthographicSize = h * 0.21f;
                cam.transform.position = new Vector3(0f, b.max.y - h * 0.22f, -5f);
                cam.transform.rotation = Quaternion.identity;
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(256, 256, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                string path = $"{PortraitDir}/{speaker}.png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(inst);

                AssetDatabase.ImportAsset(path);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                if (ti.textureType != TextureImporterType.Sprite)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.mipmapEnabled = false;
                    ti.SaveAndReimport();
                }
            }
            cam.targetTexture = null;
            rt.Release();
        }

        /// <summary>
        /// Builds the character's weapon from primitives and parents it to the right hand, so the action clips swing it.
        /// It is laid along the forearm-to-hand direction in the idle pose (down at rest, forward when aiming).
        /// The Subject is unarmed.
        /// </summary>
        private static void AttachWeapon(string character, Transform bones)
        {
            AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath($"{KenneyDir}/Animations/idle.fbx").OfType<AnimationClip>().First(c => c.name == "Idle");
            idle.SampleAnimation(bones.gameObject, 0.25f);
            Transform hand = bones.GetComponentsInChildren<Transform>().First(t => t.name == "RightHand");
            Transform fore = hand.parent;
            Vector3 dir = (hand.position - fore.position).normalized;

            Material metal = WeaponMaterial("Weapon_Metal", new Color(0.18f, 0.19f, 0.21f), Color.black, 0.7f);
            Material grip = WeaponMaterial("Weapon_Grip", new Color(0.08f, 0.08f, 0.09f), Color.black, 0.1f);
            Material glow = WeaponMaterial("Weapon_Glow", new Color(0.4f, 0.8f, 1f), new Color(0.4f, 0.8f, 1f) * 3f, 0.3f);
            Material hazard = WeaponMaterial("Weapon_Hazard", new Color(0.9f, 0.7f, 0.12f), Color.black, 0.3f);

            var weapon = new GameObject($"Weapon_{character}");
            weapon.transform.position = hand.position;
            weapon.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
            switch (character)
            {
                case "Dace": // baton
                    Part(weapon, PrimitiveType.Cylinder, grip, new Vector3(0f, 0.05f, 0f), new Vector3(0.05f, 0.08f, 0.05f));
                    Part(weapon, PrimitiveType.Cylinder, metal, new Vector3(0f, 0.32f, 0f), new Vector3(0.04f, 0.2f, 0.04f));
                    break;
                case "Enforcer": // stun prod
                    Part(weapon, PrimitiveType.Cylinder, grip, new Vector3(0f, 0.08f, 0f), new Vector3(0.05f, 0.1f, 0.05f));
                    Part(weapon, PrimitiveType.Cylinder, metal, new Vector3(0f, 0.4f, 0f), new Vector3(0.035f, 0.24f, 0.035f));
                    Part(weapon, PrimitiveType.Sphere, glow, new Vector3(0f, 0.66f, 0f), new Vector3(0.07f, 0.07f, 0.07f));
                    break;
                case "Warden": // maul
                    Part(weapon, PrimitiveType.Cylinder, grip, new Vector3(0f, 0.3f, 0f), new Vector3(0.05f, 0.36f, 0.05f));
                    Part(weapon, PrimitiveType.Cube, metal, new Vector3(0f, 0.68f, 0f), new Vector3(0.32f, 0.16f, 0.16f));
                    Part(weapon, PrimitiveType.Cube, hazard, new Vector3(0f, 0.68f, 0f), new Vector3(0.33f, 0.05f, 0.17f));
                    break;
                case "Researcher": // hand scanner
                    Part(weapon, PrimitiveType.Cube, grip, new Vector3(0f, 0.05f, 0f), new Vector3(0.05f, 0.1f, 0.05f));
                    Part(weapon, PrimitiveType.Cube, metal, new Vector3(0f, 0.13f, 0f), new Vector3(0.12f, 0.08f, 0.03f));
                    Part(weapon, PrimitiveType.Cube, glow, new Vector3(0f, 0.13f, -0.017f), new Vector3(0.09f, 0.055f, 0.005f));
                    break;
                case "Sentry": // taser pistol, barrel along the arm
                    Part(weapon, PrimitiveType.Cube, grip, new Vector3(0f, 0.04f, 0f), new Vector3(0.06f, 0.1f, 0.09f));
                    Part(weapon, PrimitiveType.Cube, metal, new Vector3(0f, 0.14f, 0f), new Vector3(0.07f, 0.16f, 0.06f));
                    Part(weapon, PrimitiveType.Cube, glow, new Vector3(0f, 0.23f, 0f), new Vector3(0.04f, 0.02f, 0.04f));
                    break;
                default:
                    Object.DestroyImmediate(weapon);
                    return;
            }
            weapon.transform.SetParent(hand, true);
        }

        private static void Part(GameObject parent, PrimitiveType type, Material mat, Vector3 localPos, Vector3 size)
        {
            GameObject p = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(p.GetComponent<Collider>());
            p.name = type.ToString();
            p.transform.SetParent(parent.transform, false);
            p.transform.localPosition = localPos;
            p.transform.localScale = size;
            p.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static Material WeaponMaterial(string name, Color color, Color emission, float smoothness)
        {
            string path = $"{Dir}/Materials/{name}.mat";
            var fresh = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            fresh.SetColor("_BaseColor", color);
            fresh.SetFloat("_Smoothness", smoothness);
            fresh.SetFloat("_Metallic", smoothness > 0.5f ? 0.8f : 0f);
            if (emission.maxColorComponent > 0f)
            {
                fresh.EnableKeyword("_EMISSION");
                fresh.SetColor("_EmissionColor", emission);
            }
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }
            existing.shader = fresh.shader;
            existing.CopyPropertiesFromMaterial(fresh);
            if (emission.maxColorComponent > 0f) existing.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(existing);
            return existing;
        }

        public static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/{name}.prefab");

        private static void ConfigureModel()
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            if (mi.animationType == ModelImporterAnimationType.Generic && !mi.importAnimation) return;
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.importAnimation = false;
            mi.SaveAndReimport();
        }

        private static AnimationClip ConfigureClip(string file, string clipName, bool loop)
        {
            string path = $"{KenneyDir}/Animations/{file}.fbx";
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            ModelImporterClipAnimation[] current = mi.clipAnimations;
            bool ok = current.Length == 1 && current[0].name == clipName && current[0].loopTime == loop;
            if (!ok)
            {
                mi.animationType = ModelImporterAnimationType.Generic;
                ModelImporterClipAnimation take = mi.defaultClipAnimations.First(c => c.takeName.EndsWith(clipName));
                take.name = clipName;
                take.loopTime = loop;
                take.loopPose = loop;
                mi.clipAnimations = new[] { take };
                mi.SaveAndReimport();
            }
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => c.name == clipName);
        }

        private static AnimatorController BuildController(AnimationClip idle, AnimationClip run, AnimationClip jump,
            System.Collections.Generic.Dictionary<string, AnimationClip> actions)
        {
            Directory.CreateDirectory(Dir);
            string path = $"{Dir}/Character.controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController c = AnimatorController.CreateAnimatorControllerAtPath(path);
            c.AddParameter("Speed", AnimatorControllerParameterType.Float);
            c.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
            c.AddParameter("Crouch", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine sm = c.layers[0].stateMachine;
            AnimatorState sIdle = sm.AddState("Idle");
            sIdle.motion = idle;
            AnimatorState sRun = sm.AddState("Run");
            sRun.motion = run;
            AnimatorState sJump = sm.AddState("Jump");
            sJump.motion = jump;
            // The pack has no crouch clip: hold the jump's wind-up (knees bent) as a crouch pose.
            AnimatorState sCrouch = sm.AddState("Crouch");
            sCrouch.motion = jump;
            sCrouch.speed = 0f;
            sCrouch.cycleOffset = CrouchPoseTime;
            sm.defaultState = sIdle;
            Link(sIdle, sCrouch, 0.1f).AddCondition(AnimatorConditionMode.If, 0f, "Crouch");
            Link(sRun, sCrouch, 0.1f).AddCondition(AnimatorConditionMode.If, 0f, "Crouch");
            Link(sCrouch, sIdle, 0.12f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Crouch");

            // Combat actions: fired by trigger from any state, then back to Idle when the clip ends.
            foreach (string action in CharacterActions.Names)
            {
                c.AddParameter(action, AnimatorControllerParameterType.Trigger);
                AnimatorState s = sm.AddState(action);
                s.motion = actions[action];
                AnimatorStateTransition into = sm.AddAnyStateTransition(s);
                into.hasExitTime = false;
                into.duration = 0.05f;
                into.canTransitionToSelf = true;
                into.AddCondition(AnimatorConditionMode.If, 0f, action);
                if (CharacterActions.Holds(action)) continue;
                AnimatorStateTransition back = s.AddTransition(sIdle);
                back.hasExitTime = true;
                back.exitTime = 0.95f;
                back.duration = 0.1f;
            }

            Link(sIdle, sRun, 0.12f).AddCondition(AnimatorConditionMode.Greater, 0.4f, "Speed");
            Link(sRun, sIdle, 0.15f).AddCondition(AnimatorConditionMode.Less, 0.4f, "Speed");
            AnimatorStateTransition toJump = sm.AddAnyStateTransition(sJump);
            toJump.hasExitTime = false;
            toJump.duration = 0.08f;
            toJump.canTransitionToSelf = false;
            toJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            Link(sJump, sIdle, 0.1f).AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            return c;
        }

        private static AnimatorStateTransition Link(AnimatorState from, AnimatorState to, float duration)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            return t;
        }

        private static Material SkinMaterial(string name)
        {
            string path = $"{Dir}/Materials/{name}.mat";
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/Skins/{name}.png");
            var fresh = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            fresh.SetTexture("_BaseMap", tex);
            fresh.SetColor("_BaseColor", Color.white);
            fresh.SetFloat("_Smoothness", 0.2f);
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }
            existing.shader = fresh.shader;
            existing.CopyPropertiesFromMaterial(fresh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void BuildPrefab(string name, AnimatorController controller)
        {
            var root = new GameObject(name);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = Vector3.one * ModelScale * (name == "Warden" ? 1.15f : 1f); // the Warden is bulkier

            Material mat = SkinMaterial(name);
            foreach (SkinnedMeshRenderer r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                r.sharedMaterial = mat;
                r.updateWhenOffscreen = true;
            }
            foreach (Animator stray in model.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(stray);

            // The clips' curve paths start below the "Root" bone, so the Animator lives there.
            Transform bones = model.transform.Find("Root");
            Animator animator = bones.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            root.AddComponent<CharacterAnimator>().Configure(animator);
            AttachWeapon(name, bones);
            PrefabUtility.SaveAsPrefabAsset(root, $"{Dir}/{name}.prefab");
            Object.DestroyImmediate(root);
        }
    }
}
