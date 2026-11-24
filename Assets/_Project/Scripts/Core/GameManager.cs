using System;
using System.Collections;
using System.Collections.Generic;
using HexaSort.Model;
using HexaSort.View;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace HexaSort.Core
{
    /// <summary>
    /// Game flow: owns the <see cref="GameSession"/> (rules) and drives the views through a small
    /// state machine. UI, audio and haptics listen to its events instead of polling, and call its
    /// public commands (Play, Next, Retry, Revive, Home).
    /// </summary>
    public sealed class GameManager : MonoBehaviour, IDropTarget
    {
        [Header("Scene references")]
        [SerializeField] private LevelManager _levels;
        [SerializeField] private BoardView _boardView;
        [SerializeField] private TrayView _trayView;
        [SerializeField] private MergeSequencePlayer _player;
        [SerializeField] private DragController _drag;
        [SerializeField] private CameraFitter _cameraFitter;

        [Header("Layout")]
        [Tooltip("Gap between the board's bottom edge and the tray row (world units).")]
        [SerializeField] private float _trayGap = 1.1f;
        [Tooltip("Height reserved above board/tray when framing the camera.")]
        [SerializeField] private float _framingHeight = 1f;

        [Header("Rules")]
        [SerializeField] private int _maxRevives = 1;
        [SerializeField] private int _reviveFreeCells = 3;
        [Tooltip("Pause after the final move before the Win/Fail screen appears.")]
        [SerializeField] private float _endScreenDelay = 0.5f;

        [Tooltip("Verify after every move that views mirror the model (editor and development builds).")]
        [SerializeField] private bool _validateViews = true;

        private int _attempt;

        public GameState State { get; private set; } = GameState.Loading;
        public GameSession Session { get; private set; }
        public LevelDefinition Level { get; private set; }
        public ProgressService Progress { get; private set; }
        public BoosterInventory Boosters { get; private set; }

        public bool CanRevive => State == GameState.Fail && Session != null && Session.RevivesUsed < _maxRevives;

        public event Action<GameState> StateChanged;
        public event Action<LevelDefinition> LevelLoaded;
        /// <summary>(cleared tiles, target tiles). Raised as soon as the model knows; the HUD animates towards it.</summary>
        public event Action<int, int> GoalChanged;
        public event Action<PlaceOutcome> MoveResolved;
        /// <summary>A stack touched down on a cell (tray drop or Move booster).</summary>
        public event Action<HexCoord> StackLanded;
        public event Action<HexCoord> CellUnlocked;
        public event Action<BoosterType> BoosterUsed;

        private void Awake()
        {
            // Mobile defaults to 30 FPS; the animations are tuned for 60.
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            var store = new PlayerPrefsStore();
            Progress = new ProgressService(store);
            Boosters = new BoosterInventory(store);
            _drag.Initialize(this);
        }

        private void Start() => LoadLevel(showHome: true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            // Debug shortcuts: R restart, W force win, F force fail screen.
            var keyboard = Keyboard.current;
            if (keyboard == null || State == GameState.Resolving || State == GameState.Loading) return;
            if (keyboard.rKey.wasPressedThisFrame) Retry();
            else if (keyboard.wKey.wasPressedThisFrame && State == GameState.Playing) StartCoroutine(EndLevel(won: true));
            else if (keyboard.fKey.wasPressedThisFrame && State == GameState.Playing) SetState(GameState.Fail);
        }
#endif

        // ---- Commands (UI) ----

        public void Play()
        {
            if (State == GameState.Home) SetState(GameState.Playing);
        }

        public void NextLevel()
        {
            if (State == GameState.Win) LoadLevel(showHome: false);
        }

        public void Retry()
        {
            if (State != GameState.Resolving && State != GameState.Loading) LoadLevel(showHome: false);
        }

        public void GoHome()
        {
            if (State != GameState.Resolving && State != GameState.Loading) LoadLevel(showHome: true);
        }

        public void Revive()
        {
            if (!CanRevive) return;
            // Debug-forced fails (F key) are not real session fails; nothing to revive in the model then.
            var freed = Session.IsFailed ? Session.Revive(_reviveFreeCells) : new List<HexCoord>();
            StartCoroutine(ReviveRoutine(freed));
        }

        // ---- Flow ----

        private void LoadLevel(bool showHome)
        {
            StopAllCoroutines();
            SetState(GameState.Loading);
            _drag.ResetAll();

            _attempt++;
            Level = _levels.GetLevel(Progress.LevelNumber);
            Session = Level.CreateSession(_attempt);

            _boardView.Build(Session.Board);
            var boardBounds = _boardView.CellBounds;
            _trayView.transform.position = new Vector3(boardBounds.center.x, 0f, boardBounds.min.z - _trayGap);
            _trayView.Show(Session.Tray);

            var framing = boardBounds;
            framing.Encapsulate(_trayView.GetBounds(_framingHeight));
            framing.Encapsulate(boardBounds.max + Vector3.up * _framingHeight);
            _cameraFitter.Fit(framing);

            LevelLoaded?.Invoke(Level);
            RaiseGoalChanged();
            SetState(showHome ? GameState.Home : GameState.Playing);
        }

        public bool CanDrop(int slot, HexCoord cell) => State == GameState.Playing && Session.CanPlace(slot, cell);

        public void Drop(int slot, HexCoord cell)
        {
            if (!CanDrop(slot, cell)) return;

            var view = _trayView.Take(slot);
            var outcome = Session.Place(slot, cell);
            SetState(GameState.Resolving);
            RaiseGoalChanged();
            StartCoroutine(PlaceRoutine(view, outcome));
        }

        private IEnumerator PlaceRoutine(StackView placed, PlaceOutcome outcome)
        {
            _boardView.AdoptStack(outcome.Cell, placed);
            yield return TransformTweens.MoveScale(placed.CachedTransform, _boardView.StackRestPosition(outcome.Cell),
                Vector3.one, _boardView.Config.LandDuration);
            Land(placed, outcome.Cell);
            yield return ResolveRoutine(outcome);
        }

        private void Land(StackView view, HexCoord cell)
        {
            view.Punch(0.18f);
            StackLanded?.Invoke(cell);
        }

        private IEnumerator ResolveRoutine(PlaceOutcome outcome)
        {
            yield return _player.Play(outcome.Merge);

            foreach (var coord in outcome.UnlockedCells)
            {
                _boardView.Unlock(coord);
                CellUnlocked?.Invoke(coord);
            }
            if (outcome.TrayRefilled) _trayView.Show(Session.Tray);
            ValidateViews();
            MoveResolved?.Invoke(outcome);

            if (outcome.IsWin) yield return EndLevel(won: true);
            else if (outcome.IsFail) yield return EndLevel(won: false);
            else SetState(GameState.Playing);
        }

        private IEnumerator EndLevel(bool won)
        {
            SetState(GameState.Resolving);
            // Progress is saved the moment the level is won, so quitting on the win screen keeps it.
            if (won) Progress.CompleteLevel();
            yield return new WaitForSeconds(_endScreenDelay);
            SetState(won ? GameState.Win : GameState.Fail);
        }

        private IEnumerator ReviveRoutine(List<HexCoord> freed)
        {
            SetState(GameState.Resolving);
            yield return _player.PlayRemoveStacks(freed);
            ValidateViews();
            SetState(GameState.Playing);
        }

        private void RaiseGoalChanged() =>
            GoalChanged?.Invoke(Mathf.Min(Session.Goal.ClearedTiles, Session.Goal.TargetTiles), Session.Goal.TargetTiles);

        // ---- Boosters ----

        public bool CanUseBooster(BoosterType type) =>
            State == GameState.Playing && !_drag.IsDragging && Boosters.Count(type) > 0;

        public void UseHammer(HexCoord cell)
        {
            if (!CanUseBooster(BoosterType.Hammer) || !Session.CanHammer(cell) || !Boosters.TryConsume(BoosterType.Hammer)) return;
            Session.Hammer(cell);
            SetState(GameState.Resolving);
            BoosterUsed?.Invoke(BoosterType.Hammer);
            StartCoroutine(HammerRoutine(cell));
        }

        private IEnumerator HammerRoutine(HexCoord cell)
        {
            yield return _player.PlaySmash(cell);
            ValidateViews();
            SetState(GameState.Playing);
        }

        public void UseMove(HexCoord from, HexCoord to)
        {
            if (!CanUseBooster(BoosterType.Move) || !Session.CanMove(from, to) || !Boosters.TryConsume(BoosterType.Move)) return;
            var view = _boardView.RemoveStack(from);
            var outcome = Session.Move(from, to);
            SetState(GameState.Resolving);
            BoosterUsed?.Invoke(BoosterType.Move);
            RaiseGoalChanged();
            StartCoroutine(MoveRoutine(view, outcome));
        }

        private IEnumerator MoveRoutine(StackView view, PlaceOutcome outcome)
        {
            _boardView.AdoptStack(outcome.Cell, view);
            yield return TransformTweens.Jump(view.CachedTransform, _boardView.StackRestPosition(outcome.Cell), 1.2f, 0.32f);
            Land(view, outcome.Cell);
            yield return ResolveRoutine(outcome);
        }

        public void UseShuffle()
        {
            if (!CanUseBooster(BoosterType.Shuffle) || !Boosters.TryConsume(BoosterType.Shuffle)) return;
            Session.ShuffleTray();
            _trayView.Show(Session.Tray);
            BoosterUsed?.Invoke(BoosterType.Shuffle);
        }

        private void SetState(GameState state)
        {
            State = state;
            _drag.InputEnabled = state == GameState.Playing;
            StateChanged?.Invoke(state);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ValidateViews()
        {
            if (_validateViews) _boardView.ValidateAgainst(Session.Board);
        }
    }
}
