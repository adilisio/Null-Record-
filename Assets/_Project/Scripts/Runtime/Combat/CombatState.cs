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

        public Unit AddUnit(UnitDef def, Team team, Tile tile, bool leader = false)
        {
            var u = new Unit
            {
                Def = def,
                Team = team,
                Hp = def.MaxHp,
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
            if (from.Y - target.Y >= CombatRules.HeightAdvantage) dmg += 1;
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
                if (other.Alive && other.Team != u.Team && InRange(u.Tile, s, other.Tile)) result.Add(other.Tile);
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
            return true;
        }

        public bool TryUseSkill(Unit u, int skillIndex, Tile target)
        {
            if (Phase != Phase.Player || u == null || u.Team != Team.Player || !u.Alive || u.Acted || !u.SkillReady(skillIndex)) return false;
            Unit victim = UnitAt(target);
            if (victim == null || victim.Team == u.Team) return false;
            SkillDef s = u.Def.Skills[skillIndex];
            if (!InRange(u.Tile, s, target)) return false;

            Resolve(u, skillIndex, target);
            u.Acted = true;
            CheckEnd();
            return true;
        }

        public void EndPlayerTurn()
        {
            if (Phase != Phase.Player) return;
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
        }

        private void Resolve(Unit attacker, int skillIndex, Tile target)
        {
            SkillDef s = attacker.Def.Skills[skillIndex];
            attacker.Cooldowns[skillIndex] = s.Cooldown;
            if (target.Col != attacker.Tile.Col) attacker.Facing = target.Col > attacker.Tile.Col ? 1 : -1;

            Unit victim = UnitAt(target);
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
            Emit(new CombatEvent { Kind = CombatEventKind.Damage, Target = victim, Amount = amount, Text = why });
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
                    return;
                }
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
                }

                if (intent.Skill != null)
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
                for (int i = 0; i < u.Cooldowns.Length; i++) u.Cooldowns[i] = Mathf.Max(0, u.Cooldowns[i] - 1);
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

        private Intent Plan(Unit e, HashSet<Tile> reserved)
        {
            var players = new List<Unit>(Alive(Team.Player));
            if (players.Count == 0) return null;

            var options = new List<KeyValuePair<Tile, int>>(
                Grid.Reachable(e.Tile, e.Def.Move, t => Occupied(t, e) || reserved.Contains(t)));
            options.Sort((a, b) => a.Value != b.Value ? a.Value.CompareTo(b.Value) : a.Key.Id.CompareTo(b.Key.Id));

            Intent best = null;
            int bestScore = int.MinValue;
            foreach (KeyValuePair<Tile, int> opt in options)
            {
                for (int si = 0; si < e.Def.Skills.Length; si++)
                {
                    if (!e.SkillReady(si)) continue;
                    SkillDef s = e.Def.Skills[si];
                    foreach (Unit p in players)
                    {
                        if (!InRange(opt.Key, s, p.Tile)) continue;
                        int dmg = PreviewDamage(opt.Key, s, p.Tile, p);
                        int score = dmg * 100 - opt.Value * 2
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

            // Nothing in reach: close the distance to the nearest player unit.
            Tile approach = e.Tile;
            float bestDist = float.MaxValue;
            foreach (KeyValuePair<Tile, int> opt in options)
            {
                foreach (Unit p in players)
                {
                    float d = Mathf.Abs(p.Tile.Col - opt.Key.Col) + Mathf.Abs(p.Tile.Y - opt.Key.Y) * 0.5f;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        approach = opt.Key;
                    }
                }
            }
            return new Intent { MoveTo = approach };
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
