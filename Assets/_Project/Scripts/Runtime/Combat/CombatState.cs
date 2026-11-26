using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    public enum Phase
    {
        Player,
        Enemy,
        Victory,
        Defeat,
    }

    public static class CombatRules
    {
        /// <summary>Melee reaches an adjacent column up to this height difference.</summary>
        public const float MeleeMaxHeight = 1.2f;
        /// <summary>Attacking from at least this much higher adds +1 damage.</summary>
        public const float HeightAdvantage = 0.9f;
        /// <summary>Being knocked off a drop at least this high deals <see cref="FallDamage"/>.</summary>
        public const float FallHeight = 1.5f;
        public const int FallDamage = 2;
        /// <summary>Knocked into a wall or another unit.</summary>
        public const int ImpactDamage = 1;
        /// <summary>Default damage of a hazard tile (arcing floor, steam vent).</summary>
        public const int HazardDamage = 2;
    }

    /// <summary>What an enemy will do on its next phase. Attacks target a tile, not a unit: step off it to dodge.</summary>
    public sealed class Intent
    {
        public Tile MoveTo;
        public SkillDef Skill;
        public Tile Target;
        public int ExpectedDamage;
    }

    public sealed class Unit
    {
        public UnitDef Def;
        public Team Team;
        public int Hp;
        /// <summary>Max HP for this fight: the def's base plus any level bonus.</summary>
        public int MaxHp;
        public Tile Tile;
        public bool IsLeader;
        public bool Moved;
        public bool Acted;
        public int Facing = 1;
        public int[] Cooldowns;
        public Intent Intent;
        /// <summary>Presentation object (set by the Unity layer).</summary>
        public object View;

        public string Name => Def.Name;
        public bool Alive => Hp > 0;
        public bool Done => !Alive || (Moved && Acted);
        public bool SkillReady(int i) => i >= 0 && i < Cooldowns.Length && Cooldowns[i] == 0;
    }

    public enum CombatEventKind
    {
        Move,
        Attack,
        Damage,
        Knockback,
        Death,
        Heal,
        Phase,
        Log,
    }

    /// <summary>Something that happened, in order. The Unity layer plays these back as animation.</summary>
    public sealed class CombatEvent
    {
        public CombatEventKind Kind;
        public Unit Actor;
        public Unit Target;
        public SkillDef Skill;
        public List<Tile> Path;
        public Tile From;
        public Tile To;
        public int Amount;
        /// <summary>The target's HP once this event has happened (Damage and Heal), so the view can count down with the playback.</summary>
        public int HpAfter;
        public string Text;
    }

    /// <summary>
    /// Turn-based tactics rules: player phase (each unit moves once and acts once, any order),
    /// then enemy phase, which carries out the intents shown during the player phase.
    /// Deterministic: no randomness anywhere.
    /// </summary>
    public sealed class CombatState
    {
        public CombatGrid Grid { get; }
        public List<Unit> Units { get; } = new List<Unit>();
        public Phase Phase { get; private set; } = Phase.Player;
        public int Turn { get; private set; }
        /// <summary>Events not yet consumed by the presentation layer.</summary>
        public List<CombatEvent> Events { get; } = new List<CombatEvent>();
        public List<string> Log { get; } = new List<string>();

        public bool IsOver => Phase == Phase.Victory || Phase == Phase.Defeat;

        public CombatState(CombatGrid grid) => Grid = grid;

        public Unit AddUnit(UnitDef def, Team team, Tile tile, bool leader = false, int bonusHp = 0)
        {
            var u = new Unit
            {
                Def = def,
                Team = team,
                MaxHp = def.MaxHp + bonusHp,
                Hp = def.MaxHp + bonusHp,
                Tile = tile,
                IsLeader = leader,
                Cooldowns = new int[def.Skills.Length],
                Facing = team == Team.Player ? 1 : -1,
            };
            Units.Add(u);
            return u;
        }

        public void Start()
        {
            Turn = 1;
            Phase = Phase.Player;
            PlanIntents();
            Emit(new CombatEvent { Kind = CombatEventKind.Phase, Text = "Player phase" });
        }

        // ---------------------------------------------------------------- queries

        public Unit UnitAt(Tile t)
        {
            if (t == null) return null;
            foreach (Unit u in Units)
            {
                if (u.Alive && u.Tile == t) return u;
            }
            return null;
        }

        public bool Occupied(Tile t, Unit except = null)
        {
            Unit u = UnitAt(t);
            return u != null && u != except;
        }

        public IEnumerable<Unit> Alive(Team team)
        {
            foreach (Unit u in Units)
            {
                if (u.Alive && u.Team == team) yield return u;
            }
        }

        /// <summary>Tiles the unit can move to this turn, with cost. Includes its own tile (cost 0).</summary>
        public Dictionary<Tile, int> MoveOptions(Unit u) => Grid.Reachable(u.Tile, u.Def.Move, t => Occupied(t, u));

        public bool InRange(Tile from, SkillDef skill, Tile target)
        {
            if (from == null || target == null || from == target) return false;
            int dcol = Mathf.Abs(target.Col - from.Col);
            if (!skill.Ranged)
            {
                return dcol == 1 && Mathf.Abs(target.Y - from.Y) <= CombatRules.MeleeMaxHeight;
            }
            return dcol >= skill.MinRange && dcol <= skill.MaxRange && Grid.Sight(from, target) != LineOfSight.Blocked;
        }

        /// <summary>Damage the skill would deal from <paramref name="from"/> to a unit standing on <paramref name="target"/>.</summary>
        public int PreviewDamage(Tile from, SkillDef skill, Tile target, Unit victim)
        {
            int dmg = skill.Damage;
            // Real high ground only: standing on a crate or barrel doesn't count.
            if (!from.CoverTop && from.Y - target.Y >= CombatRules.HeightAdvantage) dmg += 1;
            if (skill.Ranged && Grid.Sight(from, target) == LineOfSight.HalfCover) dmg = Mathf.Max(1, dmg / 2);
            if (victim != null) dmg = Mathf.Max(1, dmg - victim.Def.Armor);
            return dmg;
        }

        /// <summary>Tiles holding an enemy of <paramref name="u"/> that the skill can hit right now.</summary>
        public List<Tile> TargetOptions(Unit u, int skillIndex)
        {
            var result = new List<Tile>();
            if (skillIndex < 0 || skillIndex >= u.Def.Skills.Length) return result;
            SkillDef s = u.Def.Skills[skillIndex];
            foreach (Unit other in Units)
            {
                if (!other.Alive || other == u) continue;
                bool wanted = s.TargetsAllies ? other.Team == u.Team : other.Team != u.Team;
                if (wanted && InRange(u.Tile, s, other.Tile)) result.Add(other.Tile);
            }
            return result;
        }

        // ---------------------------------------------------------------- player commands

        public bool TryMove(Unit u, Tile to)
        {
            if (Phase != Phase.Player || u == null || u.Team != Team.Player || !u.Alive || u.Moved || to == null || to == u.Tile) return false;
            List<Tile> path = Grid.Path(u.Tile, to, u.Def.Move, t => Occupied(t, u));
            if (path == null || path.Count == 0) return false;
            ApplyMove(u, path);
            u.Moved = true;
            CheckEnd();
            return true;
        }

        public bool TryUseSkill(Unit u, int skillIndex, Tile target)
        {
            if (Phase != Phase.Player || u == null || u.Team != Team.Player || !u.Alive || u.Acted || !u.SkillReady(skillIndex)) return false;
            Unit victim = UnitAt(target);
            SkillDef s = u.Def.Skills[skillIndex];
            if (victim == null || victim == u) return false;
            if (s.TargetsAllies ? victim.Team != u.Team : victim.Team == u.Team) return false;
            if (!InRange(u.Tile, s, target)) return false;

            Resolve(u, skillIndex, target);
            u.Acted = true;
            CheckEnd();
            return true;
        }

        public void EndPlayerTurn()
        {
            if (Phase != Phase.Player) return;
            // Enemy cooldowns tick here, one full round after they were set (players' tick at the end of the enemy phase).
            foreach (Unit u in Units)
            {
                if (u.Team != Team.Enemy) continue;
                for (int i = 0; i < u.Cooldowns.Length; i++) u.Cooldowns[i] = Mathf.Max(0, u.Cooldowns[i] - 1);
            }
            Phase = Phase.Enemy;
            Emit(new CombatEvent { Kind = CombatEventKind.Phase, Text = "Enemy phase" });
            RunEnemyPhase();
        }

        // ---------------------------------------------------------------- resolution

        private void ApplyMove(Unit u, List<Tile> path)
        {
            Tile from = u.Tile;
            Tile last = path[path.Count - 1];
            if (last.Col != from.Col) u.Facing = last.Col > from.Col ? 1 : -1;
            u.Tile = last;
            Emit(new CombatEvent { Kind = CombatEventKind.Move, Actor = u, From = from, To = last, Path = new List<Tile>(path) });
            Scorch(u);
        }

        /// <summary>Hazard tiles hurt whoever ends a move or a knockback step on them (passing through is safe).</summary>
        private void Scorch(Unit u)
        {
            if (u.Alive && u.Tile != null && u.Tile.Hazard > 0) Damage(u, u.Tile.Hazard, $"{u.Name} is caught by the hazard");
        }

        private void Resolve(Unit attacker, int skillIndex, Tile target)
        {
            SkillDef s = attacker.Def.Skills[skillIndex];
            attacker.Cooldowns[skillIndex] = s.Cooldown;
            if (target.Col != attacker.Tile.Col) attacker.Facing = target.Col > attacker.Tile.Col ? 1 : -1;

            Unit victim = UnitAt(target);
            if (s.TargetsAllies)
            {
                if (victim == null) return;
                int before = victim.Hp;
                victim.Hp = Mathf.Min(victim.MaxHp, victim.Hp + s.Heal);
                Emit(new CombatEvent { Kind = CombatEventKind.Heal, Actor = attacker, Target = victim, Skill = s, Amount = victim.Hp - before, HpAfter = victim.Hp, From = attacker.Tile, To = target });
                Say($"{attacker.Name} uses {s.Name} on {victim.Name}: +{victim.Hp - before} HP.");
                return;
            }
            Emit(new CombatEvent { Kind = CombatEventKind.Attack, Actor = attacker, Target = victim, Skill = s, From = attacker.Tile, To = target });
            if (victim == null)
            {
                Say($"{attacker.Name}'s {s.Name} hits nothing.");
                return;
            }

            int dmg = PreviewDamage(attacker.Tile, s, target, victim);
            Damage(victim, dmg, $"{attacker.Name} uses {s.Name} on {victim.Name}");
            if (victim.Alive && s.Knockback > 0)
            {
                int dir = victim.Tile.Col == attacker.Tile.Col ? attacker.Facing : (victim.Tile.Col > attacker.Tile.Col ? 1 : -1);
                Knock(victim, dir, s.Knockback);
            }
        }

        private void Damage(Unit victim, int amount, string why)
        {
            if (!victim.Alive) return;
            victim.Hp = Mathf.Max(0, victim.Hp - amount);
            Emit(new CombatEvent { Kind = CombatEventKind.Damage, Target = victim, Amount = amount, HpAfter = victim.Hp, Text = why });
            Say($"{why}: {amount} damage.");
            if (!victim.Alive)
            {
                Emit(new CombatEvent { Kind = CombatEventKind.Death, Target = victim, From = victim.Tile });
                Say($"{victim.Name} is down.");
                victim.Tile = null;
                victim.Intent = null;
            }
        }

        private void Knock(Unit victim, int dir, int steps)
        {
            for (int i = 0; i < steps && victim.Alive; i++)
            {
                Tile next = Grid.PushStep(victim.Tile, dir, out EdgeKind kind);
                if (next == null)
                {
                    Damage(victim, CombatRules.ImpactDamage, $"{victim.Name} slams into the wall");
                    return;
                }
                Unit blocker = UnitAt(next);
                if (blocker != null)
                {
                    Damage(victim, CombatRules.ImpactDamage, $"{victim.Name} crashes into {blocker.Name}");
                    Damage(blocker, CombatRules.ImpactDamage, $"{blocker.Name} is hit by {victim.Name}");
                    return;
                }

                Tile from = victim.Tile;
                victim.Tile = next;
                Emit(new CombatEvent { Kind = CombatEventKind.Knockback, Target = victim, From = from, To = next });
                if (kind == EdgeKind.Drop)
                {
                    if (from.Y - next.Y >= CombatRules.FallHeight) Damage(victim, CombatRules.FallDamage, $"{victim.Name} falls");
                    Scorch(victim);
                    return;
                }
                Scorch(victim);
            }
        }

        private void CheckEnd()
        {
            if (IsOver) return;
            foreach (Unit u in Units)
            {
                if (u.IsLeader && !u.Alive)
                {
                    Phase = Phase.Defeat;
                    Emit(new CombatEvent { Kind = CombatEventKind.Phase, Text = "Defeat" });
                    return;
                }
            }
            foreach (Unit _ in Alive(Team.Enemy)) return;
            Phase = Phase.Victory;
            Emit(new CombatEvent { Kind = CombatEventKind.Phase, Text = "Victory" });
        }

        // ---------------------------------------------------------------- enemy AI

        private void RunEnemyPhase()
        {
            foreach (Unit e in new List<Unit>(Units))
            {
                if (IsOver) return;
                if (!e.Alive || e.Team != Team.Enemy || e.Intent == null) continue;
                Intent intent = e.Intent;

                if (intent.MoveTo != null && intent.MoveTo != e.Tile && !Occupied(intent.MoveTo, e))
                {
                    List<Tile> path = Grid.Path(e.Tile, intent.MoveTo, e.Def.Move, t => Occupied(t, e));
                    if (path != null && path.Count > 0) ApplyMove(e, path);
                    CheckEnd();
                    if (IsOver) return;
                }

                if (intent.Skill != null && e.Alive)
                {
                    int si = Array.IndexOf(e.Def.Skills, intent.Skill);
                    if (e.SkillReady(si) && InRange(e.Tile, intent.Skill, intent.Target))
                    {
                        Resolve(e, si, intent.Target);
                        CheckEnd();
                    }
                    else
                    {
                        Say($"{e.Name} loses its angle.");
                    }
                }
            }
            if (IsOver) return;

            foreach (Unit u in Units)
            {
                if (u.Team == Team.Player)
                {
                    for (int i = 0; i < u.Cooldowns.Length; i++) u.Cooldowns[i] = Mathf.Max(0, u.Cooldowns[i] - 1);
                }
                u.Moved = false;
                u.Acted = false;
            }
            Turn++;
            Phase = Phase.Player;
            PlanIntents();
            Emit(new CombatEvent { Kind = CombatEventKind.Phase, Text = "Player phase" });
        }

        /// <summary>Each enemy picks where it will stand and which tile it will hit. Shown to the player in advance.</summary>
        public void PlanIntents()
        {
            var reserved = new HashSet<Tile>();
            foreach (Unit e in Units)
            {
                if (!e.Alive || e.Team != Team.Enemy) continue;
                e.Intent = Plan(e, reserved);
                if (e.Intent?.MoveTo != null) reserved.Add(e.Intent.MoveTo);
            }
        }

        // Scoring weights, on the same scale as damage (100 = one point of damage). First-pass numbers.
        private const int ThreatPenalty = 40;     // a ranged unit ending where a player can melee it next turn
        private const int HazardPenalty = 150;    // ending a move on a hazard tile
        private const int HighGroundHold = 100;   // a ranged unit keeping a height advantage while it waits
        private const int RangeGapPenalty = 30;   // per column outside a ranged unit's band

        private Intent Plan(Unit e, HashSet<Tile> reserved)
        {
            var players = new List<Unit>(Alive(Team.Player));
            if (players.Count == 0) return null;
            bool ranged = IsRanged(e);
            Dictionary<Tile, int> threat = ranged ? MeleeThreat(players, e) : null;

            var options = new List<KeyValuePair<Tile, int>>(
                Grid.Reachable(e.Tile, e.Def.Move, t => Occupied(t, e) || reserved.Contains(t)));
            options.Sort((a, b) => a.Value != b.Value ? a.Value.CompareTo(b.Value) : a.Key.Id.CompareTo(b.Key.Id));

            Intent best = null;
            int bestScore = int.MinValue;
            foreach (KeyValuePair<Tile, int> opt in options)
            {
                int place = PlaceScore(opt.Key, opt.Value, threat);
                for (int si = 0; si < e.Def.Skills.Length; si++)
                {
                    // Ready by the time it acts: its cooldowns tick once more when the player phase ends.
                    if (e.Cooldowns[si] > 1 || e.Def.Skills[si].TargetsAllies) continue;
                    SkillDef s = e.Def.Skills[si];
                    foreach (Unit p in players)
                    {
                        if (!InRange(opt.Key, s, p.Tile)) continue;
                        int dmg = PreviewDamage(opt.Key, s, p.Tile, p);
                        int score = dmg * 100 + place
                            + (p.IsLeader ? 15 : 0)
                            + (dmg >= p.Hp ? 50 : 0)
                            + (s.Ranged ? Mathf.Abs(p.Tile.Col - opt.Key.Col) * 3 : 0);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = new Intent { MoveTo = opt.Key, Skill = s, Target = p.Tile, ExpectedDamage = dmg };
                        }
                    }
                }
            }
            if (best != null) return best;

            // Nothing in reach this turn.
            Tile approach = e.Tile;
            float bestValue = float.MinValue;
            foreach (KeyValuePair<Tile, int> opt in options)
            {
                float value = PlaceScore(opt.Key, opt.Value, threat);
                if (ranged)
                {
                    // Keep (or find) a firing spot: inside the range band, high if possible, out of melee reach.
                    int gap = int.MaxValue;
                    bool above = true;
                    foreach (Unit p in players)
                    {
                        gap = Mathf.Min(gap, BandGap(e, Mathf.Abs(p.Tile.Col - opt.Key.Col)));
                        if (opt.Key.Y - p.Tile.Y < CombatRules.HeightAdvantage) above = false;
                    }
                    value += -gap * RangeGapPenalty + (above ? HighGroundHold : 0);
                }
                else
                {
                    // Close the distance to the nearest player unit.
                    float nearest = float.MaxValue;
                    foreach (Unit p in players)
                    {
                        nearest = Mathf.Min(nearest, Mathf.Abs(p.Tile.Col - opt.Key.Col) + Mathf.Abs(p.Tile.Y - opt.Key.Y) * 0.5f);
                    }
                    value += -nearest * 100f;
                }
                if (value > bestValue)
                {
                    bestValue = value;
                    approach = opt.Key;
                }
            }
            return new Intent { MoveTo = approach };
        }

        /// <summary>What standing on <paramref name="t"/> is worth, apart from the attack: path cost, hazards, melee threat.</summary>
        private static int PlaceScore(Tile t, int cost, Dictionary<Tile, int> threat)
        {
            int score = -cost * 2;
            if (t.Hazard > 0) score -= HazardPenalty;
            if (threat != null && threat.TryGetValue(t, out int n)) score -= n * ThreatPenalty;
            return score;
        }

        private static bool IsRanged(Unit u)
        {
            foreach (SkillDef s in u.Def.Skills)
            {
                if (!s.Ranged) return false;
            }
            return u.Def.Skills.Length > 0;
        }

        /// <summary>How many columns <paramref name="dcol"/> is outside the unit's ranged band (0 = inside).</summary>
        private static int BandGap(Unit u, int dcol)
        {
            int min = int.MaxValue, max = 0;
            foreach (SkillDef s in u.Def.Skills)
            {
                min = Mathf.Min(min, s.MinRange);
                max = Mathf.Max(max, s.MaxRange);
            }
            if (dcol < min) return min - dcol;
            return dcol > max ? dcol - max : 0;
        }

        /// <summary>
        /// For every tile, how many player units could step next to it and hit it in melee next turn.
        /// Ranged enemies use this so they don't park where they'll be swarmed.
        /// </summary>
        public Dictionary<Tile, int> MeleeThreat(List<Unit> players, Unit ignore)
        {
            var result = new Dictionary<Tile, int>();
            foreach (Unit p in players)
            {
                bool melee = false;
                foreach (SkillDef s in p.Def.Skills) melee |= !s.Ranged;
                if (!melee) continue;
                var hit = new HashSet<Tile>();
                foreach (Tile from in Grid.Reachable(p.Tile, p.Def.Move, t => Occupied(t, p) && UnitAt(t) != ignore).Keys)
                {
                    for (int dir = -1; dir <= 1; dir += 2)
                    {
                        foreach (Tile t in Grid.Column(from.Col + dir))
                        {
                            if (Mathf.Abs(t.Y - from.Y) <= CombatRules.MeleeMaxHeight) hit.Add(t);
                        }
                    }
                }
                foreach (Tile t in hit) result[t] = result.TryGetValue(t, out int n) ? n + 1 : 1;
            }
            return result;
        }

        // ---------------------------------------------------------------- helpers

        private void Emit(CombatEvent ev) => Events.Add(ev);

        private void Say(string line)
        {
            Log.Add(line);
            Emit(new CombatEvent { Kind = CombatEventKind.Log, Text = line });
        }
    }
}
