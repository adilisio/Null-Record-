using System;
using System.Collections;
using System.Collections.Generic;
using Game.Companions;
using Game.Core;
using Game.Player;
using Game.Save;
using Game.World;
using Game.Audio;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Combat
{
    public enum EncounterStatus
    {
        Idle,
        Active,
        Cleared,
    }

    /// <summary>
    /// An optional, marked fight. Interact to engage: the room snaps into a grid, the player and the
    /// active companion fight the room's hostiles turn by turn, then free movement resumes.
    /// Losing resets the encounter to how it was before engaging.
    ///
    /// Controls (existing Player actions): Move = cursor, Jump/Attack = confirm, Crouch = cancel,
    /// Previous/Next = change action, Sprint = switch unit, Interact = end turn. Mouse hover/click works too.
    /// </summary>
    public class EncounterController : Interactable
    {
        [Serializable]
        public struct EnemySpawn
        {
            public string unit;
            public Vector2 position;
        }

        /// <summary>Character model for a unit name (roster key); units without one use the capsule figure.</summary>
        [Serializable]
        public struct UnitModel
        {
            public string unit;
            public GameObject prefab;
        }

        [Header("Space")]
        [Tooltip("Side-on area (world X/Y) that becomes the grid.")]
        [SerializeField] private Rect area = new Rect(0f, 0f, 10f, 6f);
        [SerializeField] private float tileSize = 1f;
        [SerializeField] private LayerMask environment = ~0;

        [Header("Roster")]
        [Tooltip("The player's side is the Subject plus whichever companion is in the party (CompanionFollower.Active).")]
        [SerializeField] private EnemySpawn[] enemies = Array.Empty<EnemySpawn>();

        [Header("Rewards")]
        [SerializeField] private int rewardScrap = 30;
        [SerializeField] private int rewardXp = 50;
        [SerializeField] private string clearedFlag = "encounter_cleared";
        [TextArea] [SerializeField] private string rewardLog = "";

        [Header("Hazards")]
        [Tooltip("Floor points (world X/Y) whose tiles hurt whoever ends a move or is knocked onto them. Live only during the fight.")]
        [SerializeField] private Vector2[] hazards = Array.Empty<Vector2>();
        [SerializeField] private int hazardDamage = CombatRules.HazardDamage;

        [Header("Presentation")]
        [SerializeField] private UnitModel[] unitModels = Array.Empty<UnitModel>();
        [SerializeField] private Material tileMaterial;
        [SerializeField] private Material enemyMaterial;
        [SerializeField] private Material visorMaterial;
        [SerializeField] private Renderer marker;
        [Tooltip("Hidden while the fight is on (e.g. the 'engage' sign).")]
        [SerializeField] private GameObject[] hideDuringCombat = Array.Empty<GameObject>();
        [SerializeField] private float stepTime = 0.12f;

        private readonly List<GameObject> _idleEnemies = new List<GameObject>();
        private readonly List<UnitView> _views = new List<UnitView>();
        private readonly List<string> _log = new List<string>();

        private PlayerInteractor _player;
        private CompanionFollower _companion;
        private Vector3 _engagePosition;
        private GridView _grid;
        private TextMesh _banner;
        private TextMesh _help;
        private TextMesh _logText;
        private bool _animating;
        private bool _finishing;
        private int _inputBlockedUntilFrame;
        private bool _moveHeld;
        private Vector2 _lastMouse;
        private bool _intentsDirty;

        private InputAction _aMove, _aConfirm, _aClick, _aCancel, _aNext, _aPrev, _aUnit, _aEnd;

        public EncounterStatus Status { get; private set; } = EncounterStatus.Idle;
        /// <summary>The encounter being fought right now, if any (the level can hold several).</summary>
        public static EncounterController Active { get; private set; }
        /// <summary>Side-on area (world X/Y) that becomes the grid.</summary>
        public Rect Area => area;
        public CombatState State { get; private set; }
        public Unit Selected { get; private set; }
        /// <summary>-1 = move, otherwise the selected unit's skill index.</summary>
        public int Mode { get; private set; } = -1;
        public Tile Cursor { get; private set; }
        /// <summary>True while events are animating; input is ignored. (Wrap-up after the fight is tracked by <see cref="Status"/>.)</summary>
        public bool IsBusy => _animating || (State != null && State.Events.Count > 0);

        public event Action<EncounterStatus> StatusChanged;

        public void Configure(Rect newArea, EnemySpawn[] spawns, int scrap, string flag, string log,
            Material tile, Material enemy, Material visor, Renderer zoneMarker, params GameObject[] hide)
        {
            hideDuringCombat = hide;
            area = newArea;
            enemies = spawns;
            rewardScrap = scrap;
            clearedFlag = flag;
            rewardLog = log;
            tileMaterial = tile;
            enemyMaterial = enemy;
            visorMaterial = visor;
            marker = zoneMarker;
        }

        public void SetUnitModels(params UnitModel[] models) => unitModels = models ?? Array.Empty<UnitModel>();

        private GameObject ModelFor(string unit)
        {
            foreach (UnitModel m in unitModels)
            {
                if (m.unit == unit) return m.prefab;
            }
            return null;
        }

        public void SetHazards(params Vector2[] points) => hazards = points ?? Array.Empty<Vector2>();

        public IReadOnlyList<Vector2> Hazards => hazards;

        // ---------------------------------------------------------------- exploration side

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        private void Start()
        {
            if (tileMaterial == null) tileMaterial = FallbackMaterial(new Color(1f, 1f, 1f, 0.3f), true);
            if (enemyMaterial == null) enemyMaterial = FallbackMaterial(new Color(0.55f, 0.18f, 0.16f), false);
            if (visorMaterial == null) visorMaterial = FallbackMaterial(new Color(1f, 0.25f, 0.2f), false);
            PlayerInventory inv = GameObject.FindWithTag("Player")?.GetComponent<PlayerInventory>();
            if (inv != null && !string.IsNullOrEmpty(clearedFlag) && inv.HasFlag(clearedFlag))
            {
                // Already won in a previous session.
                Status = EncounterStatus.Cleared;
                SetHidden(true);
                if (marker != null) CombatVisuals.Tint(marker, new Color(0.15f, 0.15f, 0.17f), emissive: true);
                return;
            }
            SpawnIdleEnemies();
        }

        public override bool IsAvailable(PlayerInteractor player) => Status == EncounterStatus.Idle && base.IsAvailable(player);

        public override string GetPrompt(PlayerInteractor player) => $"Engage hostiles ({enemies.Length}) - optional";

        public override void Interact(PlayerInteractor player) => Engage(player);

        private void SpawnIdleEnemies()
        {
            foreach (GameObject g in _idleEnemies) Destroy(g);
            _idleEnemies.Clear();
            foreach (EnemySpawn s in enemies)
            {
                Vector3 feet = GroundAt(s.position);
                _idleEnemies.Add(CombatVisuals.Figure($"Hostile_{s.unit}", enemyMaterial, visorMaterial, feet, -1, ModelFor(s.unit)));
            }
        }

        private Vector3 GroundAt(Vector2 p)
        {
            Vector3 origin = new Vector3(p.x, p.y + 1f, 0f);
            foreach (RaycastHit h in Physics.RaycastAll(origin, Vector3.down, 10f, environment, QueryTriggerInteraction.Ignore))
            {
                if (!(h.collider is CharacterController)) return new Vector3(p.x, h.point.y, 0f);
            }
            return new Vector3(p.x, p.y, 0f);
        }

        // ---------------------------------------------------------------- start / finish

        public void Engage(PlayerInteractor player)
        {
            if (Status != EncounterStatus.Idle || player == null) return;
            // Autosave just before the fight (the GDD's "before and after encounters").
            if (SaveManager.Instance != null) SaveManager.Instance.SaveNow("encounter");
            _player = player;
            PlayerMotor motor = player.Motor;
            float laneZ = motor != null ? motor.LaneZ : 0f;
            _engagePosition = player.transform.position;

            CombatGrid grid = CombatGrid.Build(area, laneZ, tileSize, 1.8f, environment);
            if (grid.Tiles.Count == 0)
            {
                Debug.LogError($"[Encounter] {name}: no standable tiles found in {area}.", this);
                return;
            }
            State = new CombatState(grid);
            foreach (Vector2 h in hazards)
            {
                Tile ht = Nearest(grid, GroundAt(h));
                if (ht != null) ht.Hazard = hazardDamage;
            }

            // Player and companion. Party XP raises max HP for both.
            int bonusHp = player.Inventory != null ? Progression.BonusHp(player.Inventory.Xp) : 0;
            Tile playerTile = Nearest(grid, player.transform.position);
            Unit subject = State.AddUnit(CombatPresets.Subject, Team.Player, playerTile, leader: true, bonusHp: bonusHp);
            motor?.SetExternalControl(true);
            player.transform.position = playerTile.World;
            _views.Add(new UnitView(subject, player.transform, owned: false));

            // The companion in the party (if any) fights with their own body.
            _companion = CompanionFollower.Active;
            UnitDef companionDef = _companion != null ? CombatPresets.Find(_companion.CombatUnit) : null;
            if (companionDef != null)
            {
                Tile ct = FreeNeighbour(grid, playerTile, -1) ?? FreeNeighbour(grid, playerTile, 1);
                if (ct != null)
                {
                    Unit c = State.AddUnit(companionDef, Team.Player, ct, bonusHp: bonusHp);
                    _companion.SetPaused(true);
                    _companion.transform.position = ct.World;
                    _views.Add(new UnitView(c, _companion.transform, owned: false));
                }
            }

            // Enemies take over their idle figures.
            for (int i = 0; i < enemies.Length; i++)
            {
                UnitDef def = CombatPresets.Find(enemies[i].unit);
                if (def == null) continue;
                Tile t = Nearest(grid, GroundAt(enemies[i].position));
                if (State.Occupied(t)) continue;
                Unit e = State.AddUnit(def, Team.Enemy, t);
                GameObject fig = _idleEnemies[i];
                fig.transform.position = t.World;
                _views.Add(new UnitView(e, fig.transform, owned: true));
            }
            _idleEnemies.Clear();

            // Dim the zone marker so tile highlights read clearly, and hide the engage sign.
            if (marker != null) CombatVisuals.Tint(marker, new Color(0.08f, 0.05f, 0.06f), emissive: true);
            SetHidden(true);
            _grid = new GridView(grid, tileMaterial);
            _intentsDirty = true;
            BuildHud();
            BindInput();

            Status = EncounterStatus.Active;
            Active = this;
            StatusChanged?.Invoke(Status);
            _log.Clear();
            State.Start();
            Selected = subject;
            Cursor = subject.Tile;
            Mode = -1;
            _inputBlockedUntilFrame = Time.frameCount + 1; // the Interact press that engaged must not end the turn
            GameMessages.Show("Hostiles engaged.");
            GameAudio.SetCombat(true);
        }

        private IEnumerator Finish(bool victory)
        {
            _finishing = true;
            GameAudio.Play(victory ? SoundId.Victory : SoundId.Defeat);
            GameAudio.SetCombat(false);
            yield return new WaitForSeconds(1.2f);

            foreach (UnitView v in _views) v.Destroy();
            _views.Clear();
            _grid?.Destroy();
            _grid = null;
            if (_banner != null) Destroy(_banner.transform.parent.gameObject);
            Selected = null;
            Cursor = null;

            PlayerMotor motor = _player != null ? _player.Motor : null;
            if (_companion != null)
            {
                foreach (Renderer r in _companion.GetComponentsInChildren<Renderer>()) CombatVisuals.ClearTint(r);
                _companion.SetPaused(false);
            }
            if (victory)
            {
                motor?.SetExternalControl(false);
                PlayerInventory inv = _player != null ? _player.Inventory : null;
                int levelBefore = inv != null ? Progression.Level(inv.Xp) : 1;
                if (inv != null)
                {
                    inv.AddScrap(rewardScrap);
                    inv.AddXp(rewardXp);
                    if (!string.IsNullOrEmpty(clearedFlag)) inv.SetFlag(clearedFlag);
                }
                GameMessages.Show($"Encounter cleared. +{rewardScrap} scrap, +{rewardXp} XP");
                if (inv != null && Progression.Level(inv.Xp) > levelBefore)
                {
                    GameMessages.Show($"Level {Progression.Level(inv.Xp)}: max HP +{Progression.HpPerLevel} for the party.");
                }
                if (!string.IsNullOrEmpty(rewardLog)) GameMessages.Show(rewardLog);
                if (marker != null) CombatVisuals.Tint(marker, new Color(0.15f, 0.15f, 0.17f), emissive: true);
                Status = EncounterStatus.Cleared;
            }
            else
            {
                // Back to just before engaging.
                if (motor != null)
                {
                    motor.Teleport(_engagePosition);
                    motor.SetExternalControl(false);
                }
                SpawnIdleEnemies();
                _companion?.WarpBehindPlayer();
                if (marker != null) CombatVisuals.ClearTint(marker);
                SetHidden(false);
                GameMessages.Show("Defeated. The encounter has reset.");
                Status = EncounterStatus.Idle;
            }
            State = null;
            _companion = null;
            _finishing = false;
            if (Active == this) Active = null;
            StatusChanged?.Invoke(Status);
        }

        // ---------------------------------------------------------------- frame loop

        private void Update()
        {
            if (Status != EncounterStatus.Active || State == null || _finishing) return;
            if (_animating) return;

            if (State.Events.Count > 0)
            {
                StartCoroutine(PlayEvents());
                return;
            }

            if (State.IsOver)
            {
                Paint();
                StartCoroutine(Finish(State.Phase == Phase.Victory));
                return;
            }

            if (State.Phase == Phase.Player)
            {
                if (Time.frameCount > _inputBlockedUntilFrame) HandleInput();
                Paint();
            }
        }

        // ---------------------------------------------------------------- commands (also used by tests)

        public void SelectUnit(Unit u)
        {
            if (u == null || u.Team != Team.Player || !u.Alive) return;
            Selected = u;
            Mode = -1;
            Cursor = u.Tile;
        }

        public void SetMode(int mode)
        {
            if (Selected == null) return;
            if (mode >= 0 && (Selected.Acted || !Selected.SkillReady(mode))) return;
            Mode = mode;
        }

        public void SetCursor(Tile t)
        {
            if (t != null) Cursor = t;
        }

        /// <summary>Confirm on the cursor: select a unit, move there, or use the current skill on it.</summary>
        public bool Confirm()
        {
            if (State == null || State.Phase != Phase.Player || IsBusy || Selected == null || Cursor == null) return false;
            bool ok;
            if (Mode < 0)
            {
                Unit there = State.UnitAt(Cursor);
                if (there != null && there != Selected && there.Team == Team.Player && !there.Done)
                {
                    SelectUnit(there);
                    return true;
                }
                ok = State.TryMove(Selected, Cursor);
            }
            else
            {
                ok = State.TryUseSkill(Selected, Mode, Cursor);
                if (ok) Mode = -1;
            }
            if (ok && Selected.Done) SelectNext();
            return ok;
        }

        public void EndTurn()
        {
            if (State == null || State.Phase != Phase.Player || IsBusy) return;
            Mode = -1;
            State.EndPlayerTurn();
        }

        private void SelectNext()
        {
            Unit start = Selected;
            var team = new List<Unit>(State.Alive(Team.Player));
            if (team.Count == 0) return;
            int idx = Mathf.Max(0, team.IndexOf(start));
            for (int k = 1; k <= team.Count; k++)
            {
                Unit u = team[(idx + k) % team.Count];
                if (!u.Done)
                {
                    SelectUnit(u);
                    return;
                }
            }
            // Everyone is done: keep the current selection; the player ends the turn.
            if (Selected == null || !Selected.Alive) SelectUnit(team[0]);
        }

        // ---------------------------------------------------------------- input

        private void BindInput()
        {
            InputActionMap map = _player.GetComponent<PlayerInputReader>()?.Map;
            if (map == null) return;
            _aMove = map.FindAction("Move");
            _aConfirm = map.FindAction("Jump");
            _aClick = map.FindAction("Attack");
            _aCancel = map.FindAction("Crouch");
            _aNext = map.FindAction("Next");
            _aPrev = map.FindAction("Previous");
            _aUnit = map.FindAction("Sprint");
            _aEnd = map.FindAction("Interact");
            _moveHeld = true; // ignore whatever direction was held when the fight began
        }

        private static bool Pressed(InputAction a) => a != null && a.WasPressedThisFrame();

        private void HandleInput()
        {
            if (Selected == null || !Selected.Alive) SelectNext();
            if (Selected == null) return;
            if (Mode >= 0 && (Selected.Acted || !Selected.SkillReady(Mode))) Mode = -1;

            // Cursor: one step per press.
            Vector2 mv = _aMove != null ? _aMove.ReadValue<Vector2>() : Vector2.zero;
            if (mv.magnitude < 0.4f) _moveHeld = false;
            else if (!_moveHeld)
            {
                _moveHeld = true;
                Cursor = Mathf.Abs(mv.x) >= Mathf.Abs(mv.y) ? StepColumn(Cursor, mv.x > 0 ? 1 : -1) : StepLevel(Cursor, mv.y > 0 ? 1 : -1);
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 mp = mouse.position.ReadValue();
                if ((mp - _lastMouse).sqrMagnitude > 1f)
                {
                    _lastMouse = mp;
                    Tile hovered = PickTile(mp);
                    if (hovered != null) Cursor = hovered;
                }
            }

            // Touch: tap a tile to put the cursor there, tap it again to confirm.
            Touchscreen touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            {
                int id = touch.primaryTouch.touchId.ReadValue();
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(id);
                Tile tapped = overUi ? null : PickTile(touch.primaryTouch.position.ReadValue());
                if (tapped != null)
                {
                    if (tapped == Cursor) Confirm();
                    else Cursor = tapped;
                }
            }

            int skills = Selected.Def.Skills.Length;
            if (Pressed(_aNext)) CycleMode(1, skills);
            if (Pressed(_aPrev)) CycleMode(-1, skills);
            if (Pressed(_aCancel)) Mode = -1;
            if (Pressed(_aUnit)) CycleUnit();
            if (Pressed(_aConfirm) || Pressed(_aClick)) Confirm();
            else if (Pressed(_aEnd)) EndTurn();
        }

        private void CycleMode(int dir, int skills)
        {
            for (int k = 1; k <= skills + 1; k++)
            {
                int m = ((Mode + 1 + dir * k) % (skills + 1) + skills + 1) % (skills + 1) - 1;
                if (m < 0 || (!Selected.Acted && Selected.SkillReady(m)))
                {
                    Mode = m;
                    return;
                }
            }
        }

        private void CycleUnit()
        {
            var team = new List<Unit>(State.Alive(Team.Player));
            int idx = team.IndexOf(Selected);
            if (team.Count > 1) SelectUnit(team[(idx + 1) % team.Count]);
        }

        private Tile StepColumn(Tile from, int dir)
        {
            if (from == null) return Selected.Tile;
            for (int c = from.Col + dir, n = 0; n < 4; c += dir, n++)
            {
                Tile best = null;
                foreach (Tile t in State.Grid.Column(c))
                {
                    if (best == null || Mathf.Abs(t.Y - from.Y) < Mathf.Abs(best.Y - from.Y)) best = t;
                }
                if (best != null) return best;
            }
            return from;
        }

        private Tile StepLevel(Tile from, int dir)
        {
            if (from == null) return Selected.Tile;
            IReadOnlyList<Tile> col = State.Grid.Column(from.Col);
            int i = -1;
            for (int k = 0; k < col.Count; k++)
            {
                if (col[k] == from) i = k;
            }
            int j = i + dir;
            return j >= 0 && j < col.Count ? col[j] : from;
        }

        private Tile PickTile(Vector2 screen)
        {
            Camera cam = Camera.main;
            if (cam == null) return null;
            Ray ray = cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.forward, new Vector3(0f, 0f, State.Grid.LaneZ));
            if (!plane.Raycast(ray, out float d)) return null;
            Vector3 p = ray.GetPoint(d);
            Tile best = null;
            float bestScore = float.MaxValue;
            foreach (Tile t in State.Grid.Tiles)
            {
                if (Mathf.Abs(p.x - t.World.x) > State.Grid.TileSize * 0.5f) continue;
                if (p.y < t.Y - 0.4f || p.y > t.Y + 1.9f) continue;
                float s = Mathf.Abs(p.y - (t.Y + 0.9f));
                if (s < bestScore)
                {
                    bestScore = s;
                    best = t;
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- presentation

        private void Paint()
        {
            if (_grid == null) return;
            _grid.PaintAll(GridView.Idle);
            foreach (Tile t in State.Grid.Tiles)
            {
                if (t.Hazard > 0) _grid.Paint(t, GridView.HazardColor);
            }
            bool playerTurn = State.Phase == Phase.Player && !State.IsOver;

            if (playerTurn && Selected != null && Selected.Alive)
            {
                if (Mode < 0 && !Selected.Moved)
                {
                    foreach (KeyValuePair<Tile, int> kv in State.MoveOptions(Selected))
                    {
                        if (kv.Key != Selected.Tile) _grid.Paint(kv.Key, kv.Key.Hazard > 0 ? GridView.HazardColor : GridView.Reach);
                    }
                }
                else if (Mode >= 0)
                {
                    SkillDef s = Selected.Def.Skills[Mode];
                    foreach (Tile t in State.Grid.Tiles)
                    {
                        if (State.InRange(Selected.Tile, s, t)) _grid.Paint(t, GridView.InRange);
                    }
                    foreach (Tile t in State.TargetOptions(Selected, Mode)) _grid.Paint(t, GridView.Target);
                }
            }
            _grid.SetCursor(playerTurn ? Cursor : null);
            if (_intentsDirty)
            {
                _grid.ShowIntents(State.Alive(Team.Enemy));
                _intentsDirty = false;
            }

            foreach (UnitView v in _views)
            {
                if (v.Unit.Alive) v.Refresh(playerTurn && v.Unit == Selected);
            }
            UpdateHud();
        }

        private void BuildHud()
        {
            var root = new GameObject("CombatHUD").transform;
            float cx = area.center.x;
            _banner = CombatVisuals.Text(root, "Banner", new Vector3(cx, area.yMax - 0.35f, 1.4f), 0.55f, new Color(1f, 0.85f, 0.6f));
            _help = CombatVisuals.Text(root, "Help", new Vector3(cx, area.yMax - 0.95f, 1.4f), 0.3f, new Color(0.75f, 0.8f, 0.85f));
            _logText = CombatVisuals.Text(root, "Log", new Vector3(area.xMin + 0.4f, area.yMax - 1.5f, 1.4f), 0.26f, new Color(0.85f, 0.85f, 0.8f), TextAnchor.UpperLeft);
        }

        private void UpdateHud()
        {
            if (_banner == null || State == null) return;
            string phase = State.Phase switch
            {
                Phase.Player => "PLAYER PHASE",
                Phase.Enemy => "ENEMY PHASE",
                Phase.Victory => "VICTORY",
                _ => "DEFEAT",
            };
            _banner.text = $"TURN {State.Turn}  -  {phase}";

            if (State.Phase == Phase.Player && Selected != null)
            {
                string action = Mode < 0 ? (Selected.Moved ? "moved" : "MOVE") : Selected.Def.Skills[Mode].Name.ToUpperInvariant();
                string skills = "";
                for (int i = 0; i < Selected.Def.Skills.Length; i++)
                {
                    SkillDef s = Selected.Def.Skills[i];
                    string cd = Selected.Cooldowns[i] > 0 ? $" ({Selected.Cooldowns[i]})" : "";
                    skills += $"   {s.Name} {s.Damage}{(s.Knockback > 0 ? $"+push{s.Knockback}" : "")}{cd}";
                }
                string hazard = Cursor != null && Cursor.Hazard > 0 ? $"   [cursor: HAZARD, {Cursor.Hazard} damage if you stop here]" : "";
                _help.text = $"{Selected.Name}: {action}{(Selected.Acted ? " / acted" : "")}  |{skills}{hazard}\n" +
                             "WASD cursor   Space confirm   1/2 action   C cancel   Shift unit   E end turn";
            }
            else
            {
                _help.text = "";
            }

            int from = Mathf.Max(0, _log.Count - 3);
            _logText.text = string.Join("\n", _log.GetRange(from, _log.Count - from));
        }

        private IEnumerator PlayEvents()
        {
            _animating = true;
            _grid?.SetCursor(null);
            var batch = new List<CombatEvent>(State.Events);
            State.Events.Clear();

            foreach (CombatEvent ev in batch)
            {
                switch (ev.Kind)
                {
                    case CombatEventKind.Move:
                        yield return AnimateMove(ev);
                        break;
                    case CombatEventKind.Attack:
                        GameAudio.Play(ev.Skill.Ranged ? SoundId.Zap : SoundId.Swing, ev.From.World + Vector3.up);
                        yield return AnimateAttack(ev);
                        break;
                    case CombatEventKind.Damage:
                        if (ViewOf(ev.Target) != null) GameAudio.Play(SoundId.Hit, ViewOf(ev.Target).Root.position + Vector3.up);
                        yield return AnimateDamage(ev);
                        break;
                    case CombatEventKind.Knockback:
                        GameAudio.Play(SoundId.Knockback, ev.To.World + Vector3.up);
                        yield return Slide(ViewOf(ev.Target), ev.To.World, 0.15f);
                        break;
                    case CombatEventKind.Death:
                        if (ev.From != null) GameAudio.Play(SoundId.UnitDown, ev.From.World + Vector3.up);
                        yield return AnimateDeath(ev);
                        break;
                    case CombatEventKind.Phase:
                        if (ev.Text == "Player phase") GameAudio.Play(SoundId.PhasePlayer);
                        else if (ev.Text == "Enemy phase") GameAudio.Play(SoundId.PhaseEnemy);
                        UpdateHud();
                        if (_banner != null) _banner.text = $"TURN {State.Turn}  -  {ev.Text.ToUpperInvariant()}";
                        yield return new WaitForSeconds(0.35f);
                        break;
                    case CombatEventKind.Log:
                        _log.Add(ev.Text);
                        break;
                }
            }

            _animating = false;
            _intentsDirty = true;
            if (State != null && !State.IsOver && State.Phase == Phase.Player && (Selected == null || !Selected.Alive || Selected.Done)) SelectNext();
            if (State != null) Paint();
        }

        private UnitView ViewOf(Unit u) => u?.View as UnitView;

        private IEnumerator AnimateMove(CombatEvent ev)
        {
            UnitView v = ViewOf(ev.Actor);
            if (v == null) yield break;
            Tile prev = ev.From;
            foreach (Tile t in ev.Path)
            {
                if (t.Col != prev.Col) v.SetFacing(t.Col > prev.Col ? 1 : -1);
                float time = Mathf.Abs(t.Y - prev.Y) > CombatGrid.StepHeight ? stepTime * 2f : stepTime;
                GameAudio.Play(SoundId.CombatStep, t.World);
                yield return Slide(v, t.World, time, Mathf.Abs(t.Y - prev.Y) > CombatGrid.StepHeight ? 0.6f : 0f);
                prev = t;
            }
        }

        private IEnumerator AnimateAttack(CombatEvent ev)
        {
            UnitView v = ViewOf(ev.Actor);
            if (v == null) yield break;
            if (ev.To.Col != ev.From.Col) v.SetFacing(ev.To.Col > ev.From.Col ? 1 : -1);
            Vector3 home = v.Root.position;
            // Character action clip; wait out its wind-up so the hit lands on the swing.
            CharacterAnimator anim = v.Root.GetComponentInChildren<CharacterAnimator>();
            if (anim != null)
            {
                anim.Play(ev.Skill.Anim, 0.6f);
                yield return new WaitForSeconds(0.12f);
            }
            if (ev.Skill.Ranged)
            {
                GameObject bolt = CombatVisuals.Quad(null, "Bolt", tileMaterial, home + Vector3.up * 1.3f, new Vector3(0.25f, 0.06f, 0.06f));
                CombatVisuals.Tint(bolt.GetComponent<Renderer>(), new Color(0.6f, 0.9f, 1f, 1f));
                Vector3 to = ev.To.World + Vector3.up * 1.1f;
                Vector3 from = bolt.transform.position;
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.15f)
                {
                    bolt.transform.position = Vector3.Lerp(from, to, t);
                    yield return null;
                }
                Destroy(bolt);
            }
            else
            {
                Vector3 lunge = Vector3.Lerp(home, ev.To.World, 0.35f);
                yield return Slide(v, lunge, 0.08f);
                yield return Slide(v, home, 0.1f);
            }
        }

        private IEnumerator AnimateDamage(CombatEvent ev)
        {
            UnitView v = ViewOf(ev.Target);
            if (v == null) yield break;
            v.Refresh(false);
            v.Root.GetComponentInChildren<CharacterAnimator>()?.Play("Hit", 0.35f);
            TextMesh pop = CombatVisuals.Text(null, "DamagePop", v.Root.position + new Vector3(0.4f, 2f, -0.8f), 0.5f, new Color(1f, 0.3f, 0.25f));
            pop.text = $"-{ev.Amount}";
            StartCoroutine(FloatAway(pop));
            foreach (Renderer r in v.Renderers) CombatVisuals.Tint(r, new Color(1f, 0.25f, 0.2f), true);
            yield return new WaitForSeconds(0.15f);
            foreach (Renderer r in v.Renderers)
            {
                if (r != null) CombatVisuals.ClearTint(r);
            }
            yield return new WaitForSeconds(0.1f);
        }

        private IEnumerator AnimateDeath(CombatEvent ev)
        {
            UnitView v = ViewOf(ev.Target);
            if (v == null) yield break;
            v.Refresh(false);
            if (!v.Owned) yield break; // the player's own body stays; defeat resets the encounter
            CharacterAnimator anim = v.Root.GetComponentInChildren<CharacterAnimator>();
            if (anim != null)
            {
                // Knocked out: arms fly up, the whole body tips over backwards from the feet, lies still, then sinks away.
                v.HideLabels();
                anim.Play("Down", 5f);
                yield return new WaitForSeconds(0.1f);
                Quaternion upright = v.Root.rotation;
                Quaternion flat = upright * Quaternion.Euler(-85f, 0f, 0f);
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
                {
                    v.Root.rotation = Quaternion.Slerp(upright, flat, t * t); // accelerate like a fall
                    yield return null;
                }
                v.Root.rotation = flat;
                GameAudio.Play(SoundId.Land, v.Root.position + Vector3.up * 0.3f, 0.8f);
                yield return new WaitForSeconds(0.45f);
                Vector3 lying = v.Root.position;
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
                {
                    v.Root.position = lying + Vector3.down * (0.5f * t);
                    yield return null;
                }
                v.Root.gameObject.SetActive(false);
                yield break;
            }

            Vector3 s = v.Root.localScale;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
            {
                v.Root.localScale = Vector3.Lerp(s, new Vector3(s.x, 0.05f, s.z), t);
                yield return null;
            }
            v.Root.gameObject.SetActive(false);
        }

        private static IEnumerator FloatAway(TextMesh tm)
        {
            Vector3 start = tm.transform.position;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
            {
                if (tm == null) yield break;
                tm.transform.position = start + Vector3.up * (t * 0.8f);
                yield return null;
            }
            if (tm != null) Destroy(tm.gameObject);
        }

        private static IEnumerator Slide(UnitView v, Vector3 to, float time, float arc = 0f)
        {
            if (v == null || v.Root == null) yield break;
            Vector3 from = v.Root.position;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, time))
            {
                v.Root.position = Vector3.Lerp(from, to, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * arc);
                yield return null;
            }
            v.Root.position = to;
        }

        // ---------------------------------------------------------------- helpers

        private void SetHidden(bool hidden)
        {
            foreach (GameObject g in hideDuringCombat)
            {
                if (g != null) g.SetActive(!hidden);
            }
        }

        private static Tile Nearest(CombatGrid grid, Vector3 p)
        {
            Tile best = null;
            float bestD = float.MaxValue;
            foreach (Tile t in grid.Tiles)
            {
                float d = Mathf.Abs(t.World.x - p.x) + Mathf.Abs(t.Y - p.y) * 1.5f;
                if (d < bestD)
                {
                    bestD = d;
                    best = t;
                }
            }
            return best;
        }

        private Tile FreeNeighbour(CombatGrid grid, Tile from, int dir)
        {
            foreach (Edge e in from.Edges)
            {
                if (e.Kind == EdgeKind.Walk && e.To.Col == from.Col + dir && !State.Occupied(e.To)) return e.To;
            }
            return null;
        }

        private static Material FallbackMaterial(Color c, bool transparent)
        {
            Shader sh = Shader.Find(transparent ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            var m = new Material(sh) { color = c };
            m.SetColor("_BaseColor", c);
            return m;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(new Vector3(area.center.x, area.center.y, 0f), new Vector3(area.width, area.height, 0.1f));
        }
    }
}
