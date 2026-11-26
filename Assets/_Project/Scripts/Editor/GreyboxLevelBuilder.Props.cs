using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Set dressing from Kenney's Space Station Kit (CC0): back-wall panelling and props.
    /// Purely visual: no colliders, and kept off the play lane (z between -1 and 1), so movement,
    /// the combat grid and line of sight still come from the greybox geometry alone.
    /// </summary>
    public static partial class GreyboxLevelBuilder
    {
        private const string KitDir = "Assets/_Project/ThirdParty/Kenney/SpaceStationKit/Models";
        /// <summary>Front face of the back wall is z = 2; panels sit just in front of it, behind the wall labels (z 1.9).</summary>
        private const float PanelZ = 2.1f;

        private static void BuildDressing(Transform level)
        {
            Transform root = Group(level, "Dressing");
            BackWallPanels(Group(root, "BackWall"));
            HoldingProps(Group(root, "Holding"));
            MaintenanceProps(Group(root, "Maintenance"));
            SecurityProps(Group(root, "Security"));
            ExpansionProps(root);
        }

        private static void BackWallPanels(Transform parent)
        {
            // Three rows of 1 m panels along the whole level, with a little deterministic variety.
            for (int x = -2; x < (int)SliceEndX; x++)
            {
                for (int row = 0; row < 3; row++)
                {
                    string piece = row == 1 && x % 7 == 3 ? "wall-window" : "wall";
                    if (row == 2 && x % 9 == 4) piece = "wall-banner";
                    Prop(parent, piece, new Vector3(x + 0.5f, row, PanelZ), 0f);
                }
            }
        }

        private static void HoldingProps(Transform parent)
        {
            Prop(parent, "bed-single", new Vector3(1.6f, 0f, 1.35f), 90f);
            Prop(parent, "container-tall", new Vector3(9.2f, 0f, 1.45f), 0f, 1.4f);
            Prop(parent, "container", new Vector3(10.1f, 0f, 1.45f), 20f, 1.4f);
            Prop(parent, "wall-switch", new Vector3(3.5f, 1.2f, 1.95f), 180f);
            Reskin("Crate_Jump", "container-wide");
        }

        private static void MaintenanceProps(Transform parent)
        {
            Prop(parent, "computer-system", new Vector3(21.6f, 0f, 1.45f), 180f, 1.4f);
            Prop(parent, "display-wall", new Vector3(24.5f, 1.6f, 1.95f), 180f, 1.6f);
            Prop(parent, "container-flat", new Vector3(34.5f, 0f, 1.45f), 0f, 1.6f);
            Prop(parent, "container-tall", new Vector3(37.9f, 0f, 1.45f), 0f, 1.4f);
            for (int i = 0; i < 6; i++) Prop(parent, "pipe", new Vector3(29f + i * 0.5f, 6.2f, 1.7f), 0f, 1.5f).transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        }

        private static void SecurityProps(Transform parent)
        {
            Prop(parent, "computer-wide", new Vector3(42.6f, 0f, 1.45f), 180f, 1.5f);
            Prop(parent, "display-wall-wide", new Vector3(45.5f, 1.8f, 1.95f), 180f, 1.6f);
            Prop(parent, "container-tall", new Vector3(54.9f, 0f, 1.45f), 0f, 1.4f);
            Reskin("Cover_Low", "container");
        }

        /// <summary>Places a kit piece with its pivot (bottom centre) at <paramref name="feet"/>.</summary>
        private static GameObject Prop(Transform parent, string piece, Vector3 feet, float yaw, float scale = 1f)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{KitDir}/{piece}.fbx");
            if (prefab == null)
            {
                Debug.LogWarning($"[Null Record] Kit piece '{piece}' not found.");
                return new GameObject($"Missing_{piece}");
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = feet;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>
        /// Hides a greybox box's renderer (its collider stays for gameplay) and fits a kit piece to the same bounds.
        /// </summary>
        private static void Reskin(string boxName, string piece)
        {
            GameObject box = GameObject.Find(boxName);
            if (box == null) return;
            Bounds target = box.GetComponent<Renderer>().bounds;
            GameObject prop = Prop(box.transform.parent, piece, new Vector3(target.center.x, target.min.y, target.center.z), 0f);
            Bounds b = new Bounds(prop.transform.position, Vector3.zero);
            bool first = true;
            foreach (Renderer r in prop.GetComponentsInChildren<Renderer>())
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            if (first) return;
            prop.transform.localScale = new Vector3(target.size.x / b.size.x, target.size.y / b.size.y, target.size.z / b.size.z);
            prop.name = boxName + "_Model";
            box.GetComponent<Renderer>().enabled = false;
        }
    }
}
