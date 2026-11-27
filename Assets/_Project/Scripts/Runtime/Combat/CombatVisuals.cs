using UnityEngine;

namespace Game.Combat
{
    /// <summary>Small helpers for world-space combat visuals. Everything is world-space so it shows up in captures.</summary>
    public static class CombatVisuals
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static MaterialPropertyBlock _block;

        public static void Tint(Renderer r, Color c, bool emissive = false)
        {
            if (r == null) return;
            _block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_block);
            _block.SetColor(BaseColor, c);
            if (emissive) _block.SetColor(EmissionColor, c * 0.5f);
            r.SetPropertyBlock(_block);
        }

        public static void ClearTint(Renderer r)
        {
            if (r != null) r.SetPropertyBlock(null);
        }

        public static TextMesh Text(Transform parent, string name, Vector3 localPos, float size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.UpperLeft ? TextAlignment.Left : TextAlignment.Center;
            tm.characterSize = 0.1f * size;
            tm.fontSize = 64;
            tm.color = color;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            return tm;
        }

        public static GameObject Quad(Transform parent, string name, Material mat, Vector3 pos, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.localScale = scale;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>A collider-free capsule figure with a visor, matching the player's greybox look.</summary>
        public static GameObject Figure(string name, Material body, Material visor, Vector3 feet, int facing, GameObject model = null)
        {
            var root = new GameObject(name);
            root.transform.position = feet;
            root.transform.rotation = Quaternion.Euler(0f, facing >= 0 ? 90f : -90f, 0f);
            if (model != null)
            {
                GameObject m = Object.Instantiate(model, root.transform, false);
                m.name = "Body";
                return root;
            }

            GameObject b = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            b.name = "Body";
            Object.Destroy(b.GetComponent<Collider>());
            b.transform.SetParent(root.transform, false);
            b.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            b.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            b.GetComponent<Renderer>().sharedMaterial = body;

            GameObject v = GameObject.CreatePrimitive(PrimitiveType.Cube);
            v.name = "Visor";
            Object.Destroy(v.GetComponent<Collider>());
            v.transform.SetParent(root.transform, false);
            v.transform.localPosition = new Vector3(0f, 1.5f, 0.3f);
            v.transform.localScale = new Vector3(0.4f, 0.12f, 0.12f);
            v.GetComponent<Renderer>().sharedMaterial = visor;
            return root;
        }
    }
}
