using System;
using System.Collections.Generic;
using HexaSort.Model;
using HexaSort.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace HexaSort.Core
{
    /// <summary>
    /// Targeting for boosters that need the board. Hammer: tap a stack. Move: tap a stack, then an
    /// empty cell (valid cells are marked). Shuffle needs no target and runs immediately.
    /// While targeting, tray dragging is suspended; tapping the active booster again cancels.
    /// </summary>
    public sealed class BoosterController : MonoBehaviour
    {
        [SerializeField] private GameManager _game;
        [SerializeField] private Camera _camera;
        [SerializeField] private BoardView _board;
        [SerializeField] private DragController _drag;
        [SerializeField] private float _selectedLift = 0.35f;

        private BoosterType? _active;
        private HexCoord? _moveSource;
        private readonly List<HexCoord> _destinations = new List<HexCoord>();
        private PointerEventData _pointerData;
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);

        public BoosterType? Active => _active;

        /// <summary>Targeting mode changed (null = none). The string is the hint to show.</summary>
        public event Action<BoosterType?, string> ModeChanged;

        private void OnEnable() => _game.StateChanged += OnStateChanged;
        private void OnDisable() => _game.StateChanged -= OnStateChanged;

        /// <summary>Booster button pressed: start targeting, run Shuffle, or cancel the active booster.</summary>
        public void Toggle(BoosterType type)
        {
            if (_active == type)
            {
                Cancel();
                return;
            }
            Cancel();
            if (!_game.CanUseBooster(type)) return;

            if (type == BoosterType.Shuffle)
            {
                _game.UseShuffle();
                return;
            }

            _active = type;
            _drag.InputEnabled = false;
            ModeChanged?.Invoke(type, type == BoosterType.Hammer ? "Tap a stack to smash it" : "Tap a stack to move");
        }

        public void Cancel()
        {
            if (!_active.HasValue) return;
            ReleaseSource();
            _active = null;
            if (_game.State == GameState.Playing) _drag.InputEnabled = true;
            ModeChanged?.Invoke(null, null);
        }

        private void OnStateChanged(GameState state)
        {
            if (state != GameState.Playing) Cancel();
        }

        private void Update()
        {
            if (!_active.HasValue) return;
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;

            Vector2 screen = pointer.position.ReadValue();
            if (IsOverUI(screen)) return;

            if (_active == BoosterType.Hammer) HandleHammer(screen);
            else HandleMove(screen);
        }

        private void HandleHammer(Vector2 screen)
        {
            if (!_board.TryPickStack(_camera, screen, out var cell) || !_game.Session.CanHammer(cell)) return;
            Finish();
            _game.UseHammer(cell);
        }

        private void HandleMove(Vector2 screen)
        {
            // A tap on another stack (re)selects it; otherwise a tap on a marked empty cell moves there.
            if (_board.TryPickStack(_camera, screen, out var stack) && stack != _moveSource)
            {
                SelectSource(stack);
                return;
            }
            if (!_moveSource.HasValue || !TryGroundCell(screen, out var target) || !_game.Session.CanMove(_moveSource.Value, target)) return;

            var from = _moveSource.Value;
            Finish();
            _game.UseMove(from, target);
        }

        private void SelectSource(HexCoord cell)
        {
            ReleaseSource();
            _moveSource = cell;
            var view = _board.GetStack(cell);
            if (view != null) view.CachedTransform.position = _board.StackRestPosition(cell) + Vector3.up * _selectedLift;

            _destinations.Clear();
            foreach (var c in _game.Session.Board.Cells)
                if (c.CanPlace) _destinations.Add(c.Coord);
            _board.SetMarked(_destinations);
            ModeChanged?.Invoke(_active, "Now tap an empty cell");
        }

        private void ReleaseSource()
        {
            if (_moveSource.HasValue)
            {
                var view = _board.GetStack(_moveSource.Value);
                if (view != null) view.CachedTransform.position = _board.StackRestPosition(_moveSource.Value);
            }
            _moveSource = null;
            _board.SetMarked(null);
        }

        /// <summary>Leaves targeting before the GameManager takes over (it changes state to Resolving).</summary>
        private void Finish()
        {
            ReleaseSource();
            _active = null;
            ModeChanged?.Invoke(null, null);
        }

        private bool TryGroundCell(Vector2 screen, out HexCoord cell)
        {
            cell = default;
            var ray = _camera.ScreenPointToRay(screen);
            return Ground.Raycast(ray, out float distance) && _board.TryGetCellAt(ray.GetPoint(distance), out cell);
        }

        private bool IsOverUI(Vector2 screen)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            _pointerData ??= new PointerEventData(eventSystem);
            _pointerData.position = screen;
            _uiHits.Clear();
            eventSystem.RaycastAll(_pointerData, _uiHits);
            return _uiHits.Count > 0;
        }
    }
}
