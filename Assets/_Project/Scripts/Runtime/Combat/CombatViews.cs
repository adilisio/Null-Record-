using System.Collections.Generic;
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
        public static GameObject Figure(string name, Material body, Material visor, Vector3 feet, int facing)
        {
            var root = new GameObject(name);
            root.transform.position = feet;
            root.transform.rotation = Quaternion.Euler(0f, facing >= 0 ? 90f : -90f, 0f);

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

    /// <summary>Presentation for one combat unit: its transform, a name/HP label and (for enemies) an intent label.</summary>
    public sealed class UnitView
    {
        public Unit Unit;
        public Transform Root;
        public TextMesh Label;
        public TextMesh IntentLabel;
        public Renderer[] Renderers;
        /// <summary>Spawned for this encounter (destroyed afterwards); false for the player's own body.</summary>
        public bool Owned;
        private readonly Transform _labelRoot;

        public UnitView(Unit unit, Transform root, bool owned)
        {
            Unit = unit;
            Root = root;
            Owned = owned;
            Renderers = root.GetComponentsInChildren<Renderer>();
            // Labels live on an unrotated child so they read correctly whichever way the unit faces.
            _labelRoot = new GameObject("CombatLabels").transform;
            _labelRoot.SetParent(root, false);
            Label = CombatVisuals.Text(_labelRoot, "Label", new Vector3(0f, 2.35f, -0.6f), 0.28f, Color.white);
            if (unit.Team == Team.Enemy)
            {
                IntentLabel = CombatVisuals.Text(_labelRoot, "Intent", new Vector3(0f, 2.85f, -0.6f), 0.24f, new Color(1f, 0.45f, 0.4f));
            }
            unit.View = this;
            Refresh(false);
        }

        public void Refresh(bool selected)
        {
            _labelRoot.rotation = Quaternion.identity;
            Color team = Unit.Team == Team.Player ? new Color(0.6f, 0.9f, 1f) : new Color(1f, 0.6f, 0.55f);
            Label.color = selected ? Color.white : team;
            Label.text = (selected ? "> " : "") + Unit.Name + "\n" + $"{Unit.Hp}/{Unit.Def.MaxHp}";
            if (IntentLabel != null)
            {
                Intent i = Unit.Intent;
                IntentLabel.text = !Unit.Alive || i == null ? "" : i.Skill == null ? "advance" : $"{i.Skill.Name} ({i.ExpectedDamage})";
            }
        }

        public void SetFacing(int dir)
        {
            Root.rotation = Quaternion.Euler(0f, dir >= 0 ? 90f : -90f, 0f);
            _labelRoot.rotation = Quaternion.identity;
        }

        public void Destroy()
        {
            if (_labelRoot != null) Object.Destroy(_labelRoot.gameObject);
            if (Owned && Root != null) Object.Destroy(Root.gameObject);
        }
    }

    /// <summary>Tile quads, cursor and enemy-intent markers.</summary>
    public sealed class GridView
    {
        public static readonly Color Idle = new Color(0.75f, 0.8f, 0.9f, 0.12f);
        public static readonly Color Reach = new Color(0.2f, 0.75f, 1f, 0.38f);
        public static readonly Color InRange = new Color(1f, 0.6f, 0.15f, 0.28f);
        public static readonly Color Target = new Color(1f, 0.2f, 0.15f, 0.6f);
        public static readonly Color Danger = new Color(1f, 0.1f, 0.35f, 0.5f);
        public static readonly Color CursorColor = new Color(1f, 1f, 1f, 0.85f);

        private readonly Transform _root;
        private readonly Dictionary<Tile, Renderer> _tiles = new Dictionary<Tile, Renderer>();
        private readonly Renderer _cursor;
        private readonly Material _mat;
        private readonly List<GameObject> _intentMarkers = new List<GameObject>();

        public GridView(CombatGrid grid, Material mat)
        {
            _mat = mat;
            _root = new GameObject("CombatGrid").transform;
            float s = grid.TileSize;
            foreach (Tile t in grid.Tiles)
            {
                GameObject q = CombatVisuals.Quad(_root, $"Tile_{t.Col}_{t.Y:0.0}", mat, t.World + new Vector3(0f, 0.07f, 0f), new Vector3(s * 0.9f, 0.02f, 1.6f));
                _tiles[t] = q.GetComponent<Renderer>();
            }
            _cursor = CombatVisuals.Quad(_root, "Cursor", mat, Vector3.zero, new Vector3(s * 0.98f, 0.03f, 1.8f)).GetComponent<Renderer>();
            CombatVisuals.Tint(_cursor, CursorColor);
        }

        public void Paint(Tile t, Color c)
        {
            if (t != null && _tiles.TryGetValue(t, out Renderer r)) CombatVisuals.Tint(r, c);
        }

        public void PaintAll(Color c)
        {
            foreach (Renderer r in _tiles.Values) CombatVisuals.Tint(r, c);
        }

        public void SetCursor(Tile t)
        {
            _cursor.gameObject.SetActive(t != null);
            if (t != null) _cursor.transform.position = t.World + new Vector3(0f, 0.085f, 0f);
        }

        public void ShowIntents(IEnumerable<Unit> enemies)
        {
            foreach (GameObject g in _intentMarkers) Object.Destroy(g);
            _intentMarkers.Clear();
            foreach (Unit e in enemies)
            {
                Intent i = e.Intent;
                if (!e.Alive || i == null || i.Skill == null || i.Target == null) continue;
                GameObject m = CombatVisuals.Quad(_root, $"Intent_{e.Name}", _mat, i.Target.World + new Vector3(0f, 0.1f, 0f), new Vector3(0.6f, 0.03f, 1.2f));
                CombatVisuals.Tint(m.GetComponent<Renderer>(), Danger);
                TextMesh tm = CombatVisuals.Text(m.transform, "Dmg", Vector3.zero, 0.3f, new Color(1f, 0.35f, 0.4f));
                tm.transform.SetParent(_root, true);
                tm.transform.position = i.Target.World + new Vector3(0f, 0.35f, -0.9f);
                tm.transform.localScale = Vector3.one;
                tm.text = $"! {i.ExpectedDamage}";
                _intentMarkers.Add(m);
                _intentMarkers.Add(tm.gameObject);
            }
        }

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
        }
    }
}
