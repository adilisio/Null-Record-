using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Authors the combat clips the Kenney pack lacks (Strike, Shove, Swing, Shoot, Hit) as keyframed AnimationClips.
    /// Each clip holds the full idle pose on every bone (so unanimated bones don't fall back to the T-pose) and keys
    /// rotation offsets on the arms and spine. Axes were picked from a rendered rig probe, with the model facing +X:
    ///   RightArm      -Z swings forward / up, +Z back       RightForeArm  -Z bends the elbow
    ///   LeftArm       +X swings forward,      -X back       Spine         +X leans forward, -X leans back
    /// </summary>
    public static class CharacterActions
    {
        public static readonly string[] Names = { "Strike", "Shove", "Swing", "Shoot", "Hit" };

        private struct Key
        {
            public float T;
            public Vector3 RightArm;     // euler-ish offsets, applied one axis at a time (see Offset)
            public float RightFore;
            public float LeftArm;
            public float Spine;
        }

        private static Key K(float t, float ra = 0f, float fore = 0f, float la = 0f, float spine = 0f, float raBack = 0f) =>
            new Key { T = t, RightArm = new Vector3(0f, 0f, ra - raBack), RightFore = fore, LeftArm = la, Spine = spine };

        /// <summary>Key lists per clip. ra: right arm forward (deg), fore: elbow bend, la: left arm forward, spine: forward lean.</summary>
        private static Dictionary<string, Key[]> Moves() => new Dictionary<string, Key[]>
        {
            // Wind back, punch through, recover.
            ["Strike"] = new[] { K(0f), K(0.1f, raBack: 35f, fore: 100f, spine: -5f), K(0.2f, ra: 85f, fore: 5f, spine: 12f), K(0.45f) },
            // Both hands pull in, then shove forward.
            ["Shove"] = new[] { K(0f), K(0.12f, raBack: 20f, fore: 90f, la: -20f, spine: -5f), K(0.25f, ra: 75f, la: 75f, spine: 15f), K(0.5f) },
            // Overhead chop (baton, stun prod, maul).
            ["Swing"] = new[] { K(0f), K(0.15f, ra: 175f, fore: 70f, spine: -12f), K(0.28f, ra: 40f, fore: 5f, spine: 20f), K(0.55f) },
            // Two-handed aim, small recoil, lower.
            ["Shoot"] = new[] { K(0f), K(0.1f, ra: 88f, la: 80f), K(0.2f, ra: 95f, la: 86f, spine: -6f), K(0.35f, ra: 88f, la: 80f), K(0.5f) },
            // Flinch back.
            ["Hit"] = new[] { K(0f), K(0.08f, raBack: 25f, la: -25f, spine: -22f), K(0.35f) },
        };

        /// <summary>Builds (or rebuilds) the clips from the idle pose and returns them by name.</summary>
        public static Dictionary<string, AnimationClip> Build(GameObject characterPrefab, AnimationClip idle, string dir)
        {
            Directory.CreateDirectory(dir);
            var result = new Dictionary<string, AnimationClip>();
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab);
            Transform root = inst.transform.Find("Model/Root") ?? inst.transform.Find("Root");
            idle.SampleAnimation(root.gameObject, 0.25f);
            var bones = new List<Transform>(root.GetComponentsInChildren<Transform>());
            var basePos = new Dictionary<Transform, Vector3>();
            var baseRot = new Dictionary<Transform, Quaternion>();
            foreach (Transform b in bones)
            {
                basePos[b] = b.localPosition;
                baseRot[b] = b.localRotation;
            }

            foreach (KeyValuePair<string, Key[]> move in Moves())
            {
                Key[] keys = move.Value;
                float length = keys[keys.Length - 1].T;
                var clip = new AnimationClip { name = move.Key, frameRate = 30f };
                foreach (Transform b in bones)
                {
                    string path = AnimationUtility.CalculateTransformPath(b, root);
                    // Hold the idle position on every bone.
                    Vector3 p = basePos[b];
                    SetConst(clip, path, "m_LocalPosition.x", p.x, length);
                    SetConst(clip, path, "m_LocalPosition.y", p.y, length);
                    SetConst(clip, path, "m_LocalPosition.z", p.z, length);

                    var rot = new List<(float t, Quaternion q)>();
                    foreach (Key k in keys) rot.Add((k.T, baseRot[b] * Offset(b.name, k)));
                    SetRotation(clip, path, rot);
                }
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);

                string assetPath = $"{dir}/{move.Key}.anim";
                AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(clip, assetPath);
                    result[move.Key] = clip;
                }
                else
                {
                    EditorUtility.CopySerialized(clip, existing);
                    result[move.Key] = existing;
                }
            }
            Object.DestroyImmediate(inst);
            AssetDatabase.SaveAssets();
            return result;
        }

        private static Quaternion Offset(string bone, Key k)
        {
            switch (bone)
            {
                case "RightArm": return Quaternion.AngleAxis(-k.RightArm.z, Vector3.forward);
                case "RightForeArm": return Quaternion.AngleAxis(-k.RightFore, Vector3.forward);
                case "LeftArm": return Quaternion.AngleAxis(k.LeftArm, Vector3.right);
                case "Spine": return Quaternion.AngleAxis(k.Spine, Vector3.right);
                default: return Quaternion.identity;
            }
        }

        private static void SetConst(AnimationClip clip, string path, string prop, float value, float length)
        {
            var curve = new AnimationCurve(new Keyframe(0f, value), new Keyframe(length, value));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), prop), curve);
        }

        private static void SetRotation(AnimationClip clip, string path, List<(float t, Quaternion q)> keys)
        {
            var cx = new AnimationCurve();
            var cy = new AnimationCurve();
            var cz = new AnimationCurve();
            var cw = new AnimationCurve();
            Quaternion prev = keys[0].q;
            foreach ((float t, Quaternion q0) in keys)
            {
                Quaternion q = q0;
                if (Quaternion.Dot(prev, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w); // keep the shortest path
                prev = q;
                cx.AddKey(t, q.x);
                cy.AddKey(t, q.y);
                cz.AddKey(t, q.z);
                cw.AddKey(t, q.w);
            }
            for (int i = 0; i < cx.length; i++)
            {
                cx.SmoothTangents(i, 0f);
                cy.SmoothTangents(i, 0f);
                cz.SmoothTangents(i, 0f);
                cw.SmoothTangents(i, 0f);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.x"), cx);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.y"), cy);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.z"), cz);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation.w"), cw);
        }
    }
}
