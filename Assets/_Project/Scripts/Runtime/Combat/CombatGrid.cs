using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    public enum EdgeKind
    {
        Walk,
        Climb,
        Drop,
    }

    public enum LineOfSight
    {
        Clear,
        HalfCover,
        Blocked,
    }

    /// <summary>A standable spot: one column of the side-on plane at one surface height.</summary>
    public sealed class Tile
    {
        public int Id;
        public int Col;
        /// <summary>Surface height (world Y).</summary>
        public float Y;
        public Vector3 World;
        public readonly List<Edge> Edges = new List<Edge>();

        public override string ToString() => $"Tile(c{Col}, y{Y:0.##})";
    }

    public struct Edge
    {
        public Tile To;
        public int Cost;
        public EdgeKind Kind;
    }

    /// <summary>
    /// The 2D combat grid on the side-on plane: X = column, Y = whatever surface heights exist
    /// in that column. Plain C# so the rules can be tested without a scene.
    /// </summary>
    public sealed class CombatGrid
    {
        /// <summary>Height difference that still counts as level ground.</summary>
        public const float StepHeight = 0.35f;
        /// <summary>Highest ledge a unit can climb in one move (matches the player's ledge climb).</summary>
        public const float MaxClimb = 2.5f;
        /// <summary>Largest drop a unit will take voluntarily.</summary>
        public const float MaxDrop = 4.5f;
        public const int ClimbCost = 2;

        private readonly List<Tile> _tiles = new List<Tile>();
        private readonly Dictionary<int, List<Tile>> _byCol = new Dictionary<int, List<Tile>>();

        public IReadOnlyList<Tile> Tiles => _tiles;
        public float TileSize { get; }
        public float OriginX { get; }
        public float LaneZ { get; }

        /// <summary>Line-of-sight test between two tiles. Defaults to always clear; <see cref="Build"/> wires physics.</summary>
        public Func<Tile, Tile, LineOfSight> Sight = (a, b) => LineOfSight.Clear;

        public CombatGrid(float tileSize = 1f, float originX = 0f, float laneZ = 0f)
        {
            TileSize = tileSize;
            OriginX = originX;
            LaneZ = laneZ;
        }

        public float ColumnCenterX(int col) => OriginX + (col + 0.5f) * TileSize;

        public Tile AddTile(int col, float y)
        {
            var t = new Tile { Id = _tiles.Count, Col = col, Y = y, World = new Vector3(ColumnCenterX(col), y, LaneZ) };
            _tiles.Add(t);
            if (!_byCol.TryGetValue(col, out List<Tile> list)) _byCol[col] = list = new List<Tile>();
            list.Add(t);
            list.Sort((a, b) => a.Y.CompareTo(b.Y));
            return t;
        }

        public IReadOnlyList<Tile> Column(int col) => _byCol.TryGetValue(col, out List<Tile> l) ? l : (IReadOnlyList<Tile>)Array.Empty<Tile>();

        public Tile Find(int col, float y, float tolerance = 0.3f)
        {
            foreach (Tile t in Column(col))
            {
                if (Mathf.Abs(t.Y - y) <= tolerance) return t;
            }
            return null;
        }

        /// <summary>Connects neighbouring columns: walk on level ground, climb up ledges, drop down.</summary>
        public void LinkNeighbours()
        {
            foreach (Tile t in _tiles) t.Edges.Clear();
            foreach (Tile a in _tiles)
            {
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    foreach (Tile b in Column(a.Col + dir))
                    {
                        float dy = b.Y - a.Y;
                        if (Mathf.Abs(dy) <= StepHeight) a.Edges.Add(new Edge { To = b, Cost = 1, Kind = EdgeKind.Walk });
                        else if (dy > 0f && dy <= MaxClimb) a.Edges.Add(new Edge { To = b, Cost = ClimbCost, Kind = EdgeKind.Climb });
                        else if (dy < 0f && -dy <= MaxDrop) a.Edges.Add(new Edge { To = b, Cost = 1, Kind = EdgeKind.Drop });
                    }
                }
            }
        }

        /// <summary>Movement cost to every tile reachable within <paramref name="budget"/>. Blocked tiles can't be entered or passed.</summary>
        public Dictionary<Tile, int> Reachable(Tile from, int budget, Func<Tile, bool> blocked)
        {
            var cost = new Dictionary<Tile, int> { [from] = 0 };
            var open = new List<Tile> { from };
            while (open.Count > 0)
            {
                int bi = 0;
                for (int i = 1; i < open.Count; i++)
                {
                    if (cost[open[i]] < cost[open[bi]]) bi = i;
                }
                Tile cur = open[bi];
                open.RemoveAt(bi);
                foreach (Edge e in cur.Edges)
                {
                    int c = cost[cur] + e.Cost;
                    if (c > budget || (blocked != null && blocked(e.To))) continue;
                    if (cost.TryGetValue(e.To, out int old) && old <= c) continue;
                    cost[e.To] = c;
                    if (!open.Contains(e.To)) open.Add(e.To);
                }
            }
            return cost;
        }

        /// <summary>Cheapest path (excluding <paramref name="from"/>), or null if unreachable within budget.</summary>
        public List<Tile> Path(Tile from, Tile to, int budget, Func<Tile, bool> blocked)
        {
            var cost = new Dictionary<Tile, int> { [from] = 0 };
            var prev = new Dictionary<Tile, Tile>();
            var open = new List<Tile> { from };
            while (open.Count > 0)
            {
                int bi = 0;
                for (int i = 1; i < open.Count; i++)
                {
                    if (cost[open[i]] < cost[open[bi]]) bi = i;
                }
                Tile cur = open[bi];
                open.RemoveAt(bi);
                if (cur == to) break;
                foreach (Edge e in cur.Edges)
                {
                    int c = cost[cur] + e.Cost;
                    if (c > budget || (blocked != null && blocked(e.To))) continue;
                    if (cost.TryGetValue(e.To, out int old) && old <= c) continue;
                    cost[e.To] = c;
                    prev[e.To] = cur;
                    if (!open.Contains(e.To)) open.Add(e.To);
                }
            }
            if (!prev.ContainsKey(to)) return to == from ? new List<Tile>() : null;
            var path = new List<Tile>();
            for (Tile t = to; t != from; t = prev[t]) path.Add(t);
            path.Reverse();
            return path;
        }

        /// <summary>Where a unit pushed one column in <paramref name="dir"/> ends up: level ground or a drop. Null = wall/ledge.</summary>
        public Tile PushStep(Tile from, int dir, out EdgeKind kind)
        {
            Tile walk = null, drop = null;
            foreach (Edge e in from.Edges)
            {
                if (e.To.Col != from.Col + dir) continue;
                if (e.Kind == EdgeKind.Walk) walk = e.To;
                else if (e.Kind == EdgeKind.Drop && (drop == null || e.To.Y > drop.Y)) drop = e.To;
            }
            kind = walk != null ? EdgeKind.Walk : EdgeKind.Drop;
            return walk ?? drop;
        }

        // ---------------------------------------------------------------- building from level geometry

        /// <summary>
        /// Scans level colliders inside <paramref name="area"/> (side-on plane) and builds tiles on every
        /// surface with headroom for a standing unit. Triggers and CharacterControllers are ignored.
        /// </summary>
        public static CombatGrid Build(Rect area, float laneZ, float tileSize, float unitHeight, LayerMask mask)
        {
            int cols = Mathf.FloorToInt(area.width / tileSize);
            var grid = new CombatGrid(tileSize, area.xMin, laneZ);
            var overlap = new Collider[16];

            for (int c = 0; c < cols; c++)
            {
                float x = grid.ColumnCenterX(c);
                Vector3 top = new Vector3(x, area.yMax, laneZ);
                RaycastHit[] hits = Physics.RaycastAll(top, Vector3.down, area.height + 0.5f, mask, QueryTriggerInteraction.Ignore);
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (RaycastHit h in hits)
                {
                    if (h.collider is CharacterController || h.normal.y < 0.7f) continue;
                    float y = h.point.y;
                    if (y < area.yMin - 0.1f) continue;

                    // Headroom: nothing solid in a standing capsule above the surface.
                    const float r = 0.3f;
                    Vector3 p0 = new Vector3(x, y + r + 0.05f, laneZ);
                    Vector3 p1 = new Vector3(x, y + unitHeight - r, laneZ);
                    int n = Physics.OverlapCapsuleNonAlloc(p0, p1, r, overlap, mask, QueryTriggerInteraction.Ignore);
                    bool clear = true;
                    for (int i = 0; i < n; i++)
                    {
                        if (!(overlap[i] is CharacterController)) clear = false;
                    }
                    if (clear && grid.Find(c, y, 0.05f) == null) grid.AddTile(c, y);
                }
            }

            grid.LinkNeighbours();
            grid.Sight = (a, b) => PhysicsSight(a, b, mask);
            return grid;
        }

        /// <summary>
        /// Chest-height ray blocked = no line of sight. Only a low ray blocked near the target = half cover.
        /// </summary>
        public static LineOfSight PhysicsSight(Tile a, Tile b, LayerMask mask)
        {
            const float chest = 1.3f;
            const float low = 0.5f;
            if (Blocked(a.World + Vector3.up * chest, b.World + Vector3.up * chest, mask, out _)) return LineOfSight.Blocked;
            if (Blocked(a.World + Vector3.up * chest, b.World + Vector3.up * low, mask, out float hitDistFromTarget) && hitDistFromTarget <= 1.6f)
            {
                return LineOfSight.HalfCover;
            }
            return LineOfSight.Clear;
        }

        private static bool Blocked(Vector3 from, Vector3 to, LayerMask mask, out float distFromTarget)
        {
            distFromTarget = 0f;
            Vector3 d = to - from;
            float len = d.magnitude;
            RaycastHit[] hits = Physics.RaycastAll(from, d / len, len, mask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (RaycastHit h in hits)
            {
                if (h.collider is CharacterController) continue;
                best = Mathf.Min(best, len - h.distance);
            }
            if (best == float.MaxValue) return false;
            distFromTarget = best;
            return true;
        }
    }
}
