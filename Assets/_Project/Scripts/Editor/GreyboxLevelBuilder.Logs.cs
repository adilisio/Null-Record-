using Game.World;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Readable logs (the main lore channel) and the figure waiting past the freight shutter.
    /// Log text lives in Dialogue/logs.json; every line is PLACEHOLDER until the owner rewrites it.
    /// </summary>
    public static partial class GreyboxLevelBuilder
    {
        /// <summary>(log id = start node in logs.json, title, feet position). Kept clear of fights and other interactables.</summary>
        public static readonly (string id, string title, Vector3 feet)[] LogSpots =
        {
            ("holding_shift", "shift note", new Vector3(8.0f, 0f, 0f)),
            ("maint_scratched", "scratched panel", new Vector3(19.0f, 0f, 0f)),
            ("records_purge", "purge queue", new Vector3(66.5f, 0f, 0f)),
            ("labs_counting", "specimen tag", new Vector3(110.5f, R2Y, 0f)),
            ("labs_incident", "incident 41", new Vector3(132.0f, R2Y, 0f)),
        };

        private static void BuildLogs(Transform level)
        {
            Transform root = Group(level, "Logs");
            TextAsset logs = AssetDatabase.LoadAssetAtPath<TextAsset>($"{DialogueDir}/logs.json");
            if (logs == null) Debug.LogWarning("[Null Record] Dialogue/logs.json not found.");
            Material body = Mat("LogTerminal", new Color(0.3f, 0.32f, 0.36f), metallic: 0.6f);
            Material glow = Mat("LogScreen", new Color(0.35f, 0.75f, 1f), emission: new Color(0.35f, 0.75f, 1f) * 1.5f);
            foreach ((string id, string title, Vector3 feet) in LogSpots)
            {
                var go = new GameObject("Log_" + id);
                go.transform.SetParent(root, false);
                go.transform.position = feet;
                Box(go.transform, "Stand", feet + new Vector3(0f, 0.55f, 1.25f), new Vector3(0.5f, 1.1f, 0.4f), body, collider: false);
                GameObject screen = Box(go.transform, "Screen", feet + new Vector3(0f, 1.25f, 1.1f), new Vector3(0.6f, 0.4f, 0.06f), glow, collider: false);
                screen.transform.rotation = Quaternion.Euler(-15f, 0f, 0f);
                PointLight(go.transform, "Glow", feet + new Vector3(0f, 1.3f, 0.6f), new Color(0.35f, 0.75f, 1f), 0.8f, 2.5f, shadows: false);
                LogTerminal term = go.AddComponent<LogTerminal>();
                term.Configure(logs, id, title, screen.GetComponent<Renderer>());
                term.SetInteractionPoint(new Vector3(0f, 1.0f, 0f), 1.2f);
            }
        }

        /// <summary>
        /// Someone waiting past the shutter: a dark silhouette, backlit. Who it is is the owner's call, so it has no
        /// face, name or interaction yet.
        /// </summary>
        private static void Figure(Transform room, Vector3 feet)
        {
            var go = new GameObject("WaitingFigure");
            go.transform.SetParent(room, false);
            go.transform.position = feet;
            go.transform.rotation = Quaternion.Euler(0f, -90f, 0f); // facing back toward the shutter
            GameObject model = CharacterBody(go.transform, "Subject", new Color(0.03f, 0.03f, 0.035f), new Color(0.8f, 0.9f, 1f));
            Material shadow = Mat("Silhouette", new Color(0.02f, 0.02f, 0.025f));
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = shadow;
                r.sharedMaterials = mats;
            }
            PointLight(go.transform, "Backlight", feet + new Vector3(0.6f, 2.2f, 1.2f), new Color(0.7f, 0.8f, 1f), 3f, 4f, shadows: false);
        }
    }
}
