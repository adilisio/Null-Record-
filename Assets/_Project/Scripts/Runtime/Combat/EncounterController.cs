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
        /// <summary>Player-side bodies knocked down this fight, with their upright rotation (restored when it ends).</summary>
        private readonly List<(Transform body, Quaternion upright)> _downed = new List<(Transform, Quaternion)>();

        private PlayerInteractor _player;
        private CompanionFollower _companion;
        private Vector3 _engagePosition;
        private GridView _grid;
        private GameObject _hud;
        private UnityEngine.UI.Text _banner;
        private UnityEngine.UI.Text _help;
        private UnityEngine.UI.Text _logText;
        private RectTransform _helpPanel;
        private bool _animating;
        private bool _finishing;
        private int _inputBlockedUntilFrame;
        private bool _moveHeld;
        private Vector2 _lastMouse;
        private bool _intentsDirty;
        private UnityEngine.UI.Text _next;
        private UnityEngine.UI.Text _tip;
        private string _feedback = "";
        private float _feedbackUntil;
        private float _lastTouchTime = -10f;
        private readonly List<(UnityEngine.UI.Image bg, UnityEngine.UI.Text label, int mode)> _skillButtons = new List<(UnityEngine.UI.Image, UnityEngine.UI.Text, int)>();

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

        public override string GetPrompt(PlayerInteractor player) => $"Fight (optional): {enemies.Length} hostiles";

        /// <summary>The fight can be started from anywhere inside its area, not just at the entrance point.</summary>
        public override bool InReach(Vector3 probe, float sqrDistance) =>
            base.InReach(probe, sqrDistance) || area.Contains(new Vector2(probe.x, probe.y));

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
            // Engaged from inside the arena: take position at the entrance edge rather than next to (or on) a hostile.
            Vector3 pp = player.transform.position;
            bool inside = area.Contains(new Vector2(pp.x, pp.y + 0.5f)) && Mathf.Abs(pp.x - transform.position.x) > 1.5f;
            Tile playerTile = Nearest(grid, inside ? transform.position : pp);
            Unit subject = State.AddUnit(CombatPresets.Subject, Team.Player, playerTile, leader: true, bonusHp: bonusHp);
            motor?.SetExternalControl(true);
            player.transform.position = playerTile.World;
            _views.Add(new UnitView(subject, player.transform, owned: false, barMaterial: tileMaterial));

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
                    _views.Add(new UnitView(c, _companion.transform, owned: false, barMaterial: tileMaterial));
                }
            }

            // Enemies take over their idle figures.
            for (int i = 0; i < enemies.Length; i++)
            {
                UnitDef def = CombatPresets.Find(enemies[i].unit);
                if (def == null) continue;
                Tile t = Nearest(grid, GroundAt(enemies[i].position));
                if (State.Occupied(t))
                {
                    Destroy(_idleEnemies[i]); // no free tile: don't leave the idle figure standing
                    continue;
                }
                Unit e = State.AddUnit(def, Team.Enemy, t);
                GameObject fig = _idleEnemies[i];
                fig.transform.position = t.World;
                _views.Add(new UnitView(e, fig.transform, owned: true, barMaterial: tileMaterial));
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
            GameAudio.SetCombat(true);
        }

        /// <summary>Give up the fight: everything resets to just before engaging, like a loss but without the sting.</summary>
        public bool Retreat()
        {
            if (Status != EncounterStatus.Active || _finishing) return false;
            StopAllCoroutines();
            _animating = false;
            State?.Events.Clear();
            StartCoroutine(Finish(false, retreated: true));
            return true;
        }

        private IEnumerator Finish(bool victory, bool retreated = false)
        {
            _finishing = true;
            if (!retreated) GameAudio.Play(victory ? SoundId.Victory : SoundId.Defeat);
            GameAudio.SetCombat(false);
            yield return new WaitForSeconds(retreated ? 0.2f : 1.2f);

            // Downed player-side bodies get back up.
            foreach ((Transform body, Quaternion upright) in _downed)
            {
                if (body == null) continue;
                body.rotation = upright;
                body.GetComponentInChildren<CharacterAnimator>()?.ResetPose();
            }
            _downed.Clear();
            foreach (UnitView v in _views) v.Destroy();
            _views.Clear();
            _grid?.Destroy();
            _grid = null;
            if (_hud != null) Destroy(_hud);
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
                GameMessages.Show(retreated ? "You pulled back. The hostiles reset." : "Defeated. The encounter has reset.");
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
            Unit there = State.UnitAt(Cursor);
            if (Mode < 0)
            {
                if (there != null && there != Selected && there.Team == Team.Player && !there.Done)
                {
                    SelectUnit(there);
                    return true;
                }
                if (there != null && there.Team == Team.Enemy)
                {
                    // Confirming on a hostile in MOVE mode picks the skill that can hit it; confirm again to use it.
                    int skill = SkillFor(Selected, Cursor);
                    if (skill >= 0)
                    {
                        _feedbackUntil = 0f;
                        Mode = skill;
                        return true;
                    }
                    Feedback(Selected.Acted ? $"{Selected.Name} has already attacked this turn."
                        : Selected.Moved ? "Out of reach this turn. Try another target, or END TURN."
                        : "Out of reach: move next to it first (blue tiles).");
                    return false;
                }
                ok = State.TryMove(Selected, Cursor);
                if (!ok && there != Selected) Feedback(Selected.Moved ? $"{Selected.Name} has already moved this turn." : "Can't move there. Pick a blue tile.");
            }
            else
            {
                ok = State.TryUseSkill(Selected, Mode, Cursor);
                if (ok) Mode = -1;
                else
                {
                    int other = there != null && there.Team == Team.Enemy ? SkillFor(Selected, Cursor) : -1;
                    if (other >= 0) { Mode = other; _feedbackUntil = 0f; return true; }
                    Feedback($"{Selected.Def.Skills[Mode].Name} can't reach there. Pick a red target, or choose MOVE.");
                }
            }
            if (ok) _feedbackUntil = 0f;
            if (ok && Selected.Done) SelectNext();
            return ok;
        }

        /// <summary>The first ready attack of <paramref name="u"/> that can hit whoever stands on <paramref name="t"/> right now, or -1.</summary>
        public int SkillFor(Unit u, Tile t)
        {
            if (u == null || t == null || u.Acted) return -1;
            for (int i = 0; i < u.Def.Skills.Length; i++)
            {
                if (u.SkillReady(i) && !u.Def.Skills[i].TargetsAllies && State.TargetOptions(u, i).Contains(t)) return i;
            }
            return -1;
        }

        /// <summary>True on the player's turn once every living party member has moved and acted.</summary>
        public bool AllActed
        {
            get
            {
                if (State == null || State.Phase != Phase.Player || IsBusy) return false;
                foreach (Unit u in State.Alive(Team.Player)) if (!u.Done) return false;
                return true;
            }
        }

        /// <summary>The reason an action was refused, shown on the next-step line for a moment.</summary>
        public string Feedback(string text)
        {
            _feedback = text;
            _feedbackUntil = Time.unscaledTime + 2.5f;
            GameAudio.Play(SoundId.UiMove);
            return text;
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

            // Phones can echo a tap as a mouse click as well: ignore the mouse right after any touch.
            Touchscreen ts0 = Touchscreen.current;
            if (ts0 != null && ts0.primaryTouch.press.isPressed) _lastTouchTime = Time.unscaledTime;
            bool mouseOk = Time.unscaledTime - _lastTouchTime > 1f;
            bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            Mouse mouse = Mouse.current;
            if (mouse != null && mouseOk)
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
                    else
                    {
                        Cursor = tapped;
                        // The first tap previews the obvious action: an attack on a hostile, a move to an empty tile.
                        Unit u = State.UnitAt(tapped);
                        if (u != null && u.Team == Team.Enemy)
                        {
                            int sk = SkillFor(Selected, tapped);
                            if (sk >= 0) Mode = sk;
                        }
                        else if (u == null && Mode >= 0 && !Selected.Moved) Mode = -1;
                    }
                }
            }

            int skills = Selected.Def.Skills.Length;
            if (Pressed(_aNext)) CycleMode(1, skills);
            if (Pressed(_aPrev)) CycleMode(-1, skills);
            if (Pressed(_aCancel)) Mode = -1;
            if (Pressed(_aUnit)) CycleUnit();
            bool click = Pressed(_aClick) && mouseOk && !pointerOverUi;
            if (Pressed(_aConfirm) || click) Confirm();
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
                v.DisplayHp = v.Unit.Hp; // idle: the playback has caught up
                if (v.Unit.Alive) v.Refresh(playerTurn && v.Unit == Selected);
            }
            UpdateHud();
        }

        /// <summary>The turn banner as shown (tests).</summary>
        public string BannerText => _banner != null ? _banner.text : "";
        /// <summary>The unit/skill/controls bar as shown (tests).</summary>
        public string HelpText => (_next != null ? _next.text + "\n" : "") + (_help != null ? _help.text : "");
        /// <summary>Just the next-step line (tests).</summary>
        public string NextStepText => _next != null ? _next.text : "";

        /// <summary>Banner, unit/skill bar and log on a screen canvas, so they stay readable at any size (phones included).</summary>
        private void BuildHud()
        {
            Canvas canvas = Game.UI.MenuWidgets.Canvas(null, "CombatHUD", 55, Camera.main);
            _hud = canvas.gameObject;
            Transform r = canvas.transform;

            _banner = Game.UI.MenuWidgets.Label(r, "Banner", "", 30, new Color(1f, 0.85f, 0.6f), TextAnchor.UpperCenter);
            Game.UI.MenuWidgets.Anchor(_banner.rectTransform, new Vector2(0.2f, 1f), new Vector2(0.8f, 1f), new Vector2(0f, -66f), new Vector2(0f, -22f));
            _banner.gameObject.AddComponent<UnityEngine.UI.Shadow>().effectDistance = new Vector2(2f, -2f);

            UnityEngine.UI.Image panel = Game.UI.MenuWidgets.Panel(r, "HelpPanel", new Color(0.03f, 0.04f, 0.05f, 0.78f));
            _helpPanel = panel.rectTransform;
            Game.UI.MenuWidgets.Anchor(_helpPanel, new Vector2(0.1f, 1f), new Vector2(0.9f, 1f), new Vector2(0f, -150f), new Vector2(0f, -70f));
            // What to do next, in plain words (big), then the device's controls and a tip (small).
            _next = Game.UI.MenuWidgets.Label(panel.transform, "NextStep", "", 26, new Color(1f, 0.93f, 0.75f), TextAnchor.MiddleCenter);
            Game.UI.MenuWidgets.Anchor(_next.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -40f), new Vector2(-14f, -4f));
            _help = Game.UI.MenuWidgets.Label(panel.transform, "Help", "", 16, new Color(0.75f, 0.8f, 0.86f), TextAnchor.MiddleCenter);
            Game.UI.MenuWidgets.Anchor(_help.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -60f), new Vector2(-14f, -40f));
            _tip = Game.UI.MenuWidgets.Label(panel.transform, "Tip", "", 15, new Color(1f, 0.55f, 0.6f), TextAnchor.MiddleCenter);
            Game.UI.MenuWidgets.Anchor(_tip.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -78f), new Vector2(-14f, -60f));

            // Action bar, bottom centre: MOVE plus the selected unit's skills, tap or click to choose.
            Game.UI.MenuWidgets.EnsureEventSystem();
            _skillButtons.Clear();
            for (int i = -1; i < 3; i++)
            {
                int mode = i;
                UnityEngine.UI.Image bg = Game.UI.MenuWidgets.Panel(r, $"Action_{i + 1}", new Color(1f, 1f, 1f, 0.1f));
                RectTransform rt = bg.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(150f, 64f);
                rt.anchoredPosition = new Vector2(-50f + i * 160f, 22f);
                var b = bg.gameObject.AddComponent<UnityEngine.UI.Button>();
                b.targetGraphic = bg;
                b.transition = UnityEngine.UI.Selectable.Transition.None;
                b.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
                b.onClick.AddListener(() => ChooseAction(mode));
                UnityEngine.UI.Text t = Game.UI.MenuWidgets.Label(bg.transform, "Label", "", 18, Color.white, TextAnchor.MiddleCenter);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                Game.UI.MenuWidgets.Stretch(t.rectTransform, new Vector2(4f, 2f), new Vector2(-4f, -2f));
                _skillButtons.Add((bg, t, mode));
            }

            _logText = Game.UI.MenuWidgets.Label(r, "Log", "", 14, new Color(0.85f, 0.85f, 0.8f), TextAnchor.UpperRight);
            Game.UI.MenuWidgets.Anchor(_logText.rectTransform, new Vector2(0.76f, 1f), new Vector2(1f, 1f), new Vector2(0f, -250f), new Vector2(-16f, -150f));
            _logText.gameObject.AddComponent<UnityEngine.UI.Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
        }

        /// <summary>The action bar: MOVE (-1) or a skill index. Refused choices say why.</summary>
        public void ChooseAction(int mode)
        {
            if (State == null || State.Phase != Phase.Player || IsBusy || Selected == null) return;
            if (mode >= Selected.Def.Skills.Length) return;
            if (mode < 0)
            {
                if (Selected.Moved) Feedback($"{Selected.Name} has already moved this turn.");
                Mode = -1;
                return;
            }
            if (Selected.Acted) { Feedback($"{Selected.Name} has already attacked this turn."); return; }
            if (!Selected.SkillReady(mode)) { Feedback($"{Selected.Def.Skills[mode].Name} is recharging ({Selected.Cooldowns[mode]} more turn{(Selected.Cooldowns[mode] == 1 ? "" : "s")})."); return; }
            Mode = mode;
            _feedbackUntil = 0f;
            GameAudio.Play(SoundId.UiConfirm);
        }

        private static string SkillSummary(SkillDef s)
        {
            if (s.TargetsAllies) return $"heal {s.Heal}";
            string r = s.Ranged ? $"range {s.MinRange}-{s.MaxRange}" : "melee";
            return $"{s.Damage} dmg{(s.Knockback > 0 ? " +push" : "")}, {r}";
        }

        /// <summary>One plain sentence: what the player should do now.</summary>
        private string NextStep()
        {
            bool touch = Game.UI.InputHints.Touch;
            string pick = touch ? "Tap" : "Select";
            string again = touch ? "tap it again" : "press Space";
            string end = touch ? "tap END TURN" : "press E to end the turn";
            if (Selected == null) return "";
            bool allDone = true;
            foreach (Unit u in State.Alive(Team.Player)) allDone &= u.Done;
            if (allDone) return $"Everyone has acted: {end}.";
            if (Selected.Done) return $"{Selected.Name} is done. {end.Substring(0, 1).ToUpperInvariant() + end.Substring(1)}, or switch unit.";

            if (Mode >= 0)
            {
                SkillDef s = Selected.Def.Skills[Mode];
                Unit victim = State.UnitAt(Cursor);
                if (victim != null && State.TargetOptions(Selected, Mode).Contains(Cursor))
                {
                    if (s.TargetsAllies) return $"{s.Name} {victim.Name}: +{Mathf.Min(s.Heal, victim.MaxHp - victim.Hp)} HP. To confirm, {again}.";
                    int dmg = State.PreviewDamage(Selected.Tile, s, Cursor, victim);
                    string ko = dmg >= victim.Hp ? "  KNOCKOUT" : $" ({victim.Hp - dmg} HP left)";
                    return $"{s.Name} the {victim.Name}: {dmg} damage{ko}. To confirm, {again}.";
                }
                if (State.TargetOptions(Selected, Mode).Count == 0)
                    return Selected.Moved ? $"{s.Name}: nothing in reach. {end.Substring(0, 1).ToUpperInvariant() + end.Substring(1)}." : $"{s.Name}: nothing in reach. Choose MOVE and get closer.";
                return $"{s.Name}: {pick.ToLowerInvariant()} a red {(s.TargetsAllies ? "ally" : "target")}.";
            }

            bool canHit = false;
            for (int i = 0; i < Selected.Def.Skills.Length; i++) canHit |= !Selected.Acted && Selected.SkillReady(i) && !Selected.Def.Skills[i].TargetsAllies && State.TargetOptions(Selected, i).Count > 0;
            if (!Selected.Moved && canHit) return $"{Selected.Name}: {pick.ToLowerInvariant()} an enemy next to you to attack, or a blue tile to move.";
            if (!Selected.Moved) return $"{Selected.Name}: {pick.ToLowerInvariant()} a blue tile to move{(Selected.Acted ? "" : " toward an enemy")}.";
            if (canHit) return $"{Selected.Name}: {pick.ToLowerInvariant()} an enemy to attack it.";
            return $"{Selected.Name}: no enemy in reach. {end.Substring(0, 1).ToUpperInvariant() + end.Substring(1)}.";
        }

        private void UpdateActionBar()
        {
            bool show = State.Phase == Phase.Player && Selected != null && !IsBusy;
            for (int k = 0; k < _skillButtons.Count; k++)
            {
                (UnityEngine.UI.Image bg, UnityEngine.UI.Text label, int mode) = _skillButtons[k];
                bool exists = show && mode < Selected.Def.Skills.Length;
                bg.gameObject.SetActive(exists);
                if (!exists) continue;
                bool usable;
                if (mode < 0)
                {
                    usable = !Selected.Moved;
                    label.text = Selected.Moved ? "MOVE\n<size=13>done</size>" : "MOVE\n<size=13>up to " + Selected.Def.Move + " tiles</size>";
                }
                else
                {
                    SkillDef s = Selected.Def.Skills[mode];
                    usable = !Selected.Acted && Selected.SkillReady(mode);
                    string sub = Selected.Cooldowns[mode] > 0 ? $"recharging ({Selected.Cooldowns[mode]})" : SkillSummary(s);
                    label.text = $"{s.Name.ToUpperInvariant()}\n<size=13>{sub}</size>";
                }
                bool current = Mode == mode;
                bg.color = current ? new Color(1f, 0.74f, 0.45f, 0.55f) : usable ? new Color(1f, 1f, 1f, 0.16f) : new Color(1f, 1f, 1f, 0.05f);
                label.color = usable || current ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            }
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
                bool fresh = Time.unscaledTime < _feedbackUntil;
                _next.text = fresh ? _feedback : NextStep();
                _next.color = fresh ? new Color(1f, 0.55f, 0.45f) : new Color(1f, 0.93f, 0.75f);
                _help.text = Game.UI.InputHints.CombatControls;
                _tip.text = Cursor != null && Cursor.Hazard > 0 ? $"Arcing floor: {Cursor.Hazard} damage to anyone who stops here."
                    : State.Turn <= 2 ? "Red ! tiles show where enemies will hit next turn. Step off them." : "";
            }
            else
            {
                _next.text = "";
                _help.text = "";
                _tip.text = "";
            }

            _helpPanel.gameObject.SetActive(_help.text.Length > 0);
            UpdateActionBar();

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
                    case CombatEventKind.Heal:
                        yield return AnimateHeal(ev);
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

        private IEnumerator AnimateHeal(CombatEvent ev)
        {
            UnitView healer = ViewOf(ev.Actor);
            UnitView v = ViewOf(ev.Target);
            if (healer != null)
            {
                if (ev.To.Col != ev.From.Col) healer.SetFacing(ev.To.Col > ev.From.Col ? 1 : -1);
                healer.Root.GetComponentInChildren<CharacterAnimator>()?.Play(ev.Skill.Anim, 0.5f);
            }
            GameAudio.Play(SoundId.Pickup, (v != null ? v.Root.position : ev.To.World) + Vector3.up);
            yield return new WaitForSeconds(0.2f);
            if (v == null) yield break;
            v.DisplayHp = ev.HpAfter;
            v.Refresh(false);
            TextMesh pop = CombatVisuals.Text(null, "HealPop", v.Root.position + new Vector3(0.4f, 2f, -0.8f), 0.5f, new Color(0.35f, 1f, 0.5f));
            pop.text = $"+{ev.Amount}";
            StartCoroutine(FloatAway(pop));
            foreach (Renderer r in v.Renderers) CombatVisuals.Tint(r, new Color(0.4f, 1f, 0.55f), true);
            yield return new WaitForSeconds(0.2f);
            foreach (Renderer r in v.Renderers)
            {
                if (r != null) CombatVisuals.ClearTint(r);
            }
        }

        private IEnumerator AnimateDamage(CombatEvent ev)
        {
            UnitView v = ViewOf(ev.Target);
            if (v == null) yield break;
            v.DisplayHp = ev.HpAfter;
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
            CharacterAnimator anim = v.Root.GetComponentInChildren<CharacterAnimator>();
            if (!v.Owned)
            {
                // A companion at 0 HP goes down where they stand and gets up after the fight. (The Subject going down
                // is a defeat, which resets the encounter, so the leader stays standing.)
                if (anim == null || v.Unit.IsLeader) yield break;
                v.HideLabels();
                anim.Play("Down", 5f);
                Quaternion up = v.Root.rotation;
                _downed.Add((v.Root, up));
                Quaternion down = up * Quaternion.Euler(FallPitch(v.Root), 0f, 0f);
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
                {
                    v.Root.rotation = Quaternion.Slerp(up, down, t * t);
                    yield return null;
                }
                v.Root.rotation = down;
                GameAudio.Play(SoundId.Land, v.Root.position + Vector3.up * 0.3f, 0.8f);
                yield break;
            }
            if (anim != null)
            {
                // Knocked out: arms fly up, the whole body tips over from the feet (backwards, or forwards if a wall
                // is right behind), lies still, then sinks away.
                v.HideLabels();
                anim.Play("Down", 5f);
                yield return new WaitForSeconds(0.1f);
                Quaternion upright = v.Root.rotation;
                Quaternion flat = upright * Quaternion.Euler(FallPitch(v.Root), 0f, 0f);
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

        /// <summary>
        /// Pitch for tipping a body over: backwards (away from what hit it), unless a wall is within a body length
        /// behind, in which case forwards, so it doesn't sink into the wall.
        /// </summary>
        private float FallPitch(Transform body)
        {
            Vector3 chest = body.position + Vector3.up * 0.9f;
            foreach (RaycastHit h in Physics.RaycastAll(chest, -body.forward, 1.8f, environment, QueryTriggerInteraction.Ignore))
            {
                if (!(h.collider is CharacterController) && !h.transform.IsChildOf(body)) return 85f;
            }
            return -85f;
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
