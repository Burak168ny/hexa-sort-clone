using System;
using System.Collections;
using System.Collections.Generic;
using HexaSort.Model;
using HexaSort.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace HexaSort.Core
{
    /// <summary>Receives drop requests from <see cref="DragController"/>.</summary>
    public interface IDropTarget
    {
        bool CanDrop(int slot, HexCoord cell);
        void Drop(int slot, HexCoord cell);
    }

    /// <summary>
    /// Drag-and-drop of tray stacks with the Input System pointer (mouse or primary touch).
    /// The dragged stack floats above and slightly ahead of the finger so it stays visible;
    /// the cell under the stack (not under the finger) is the drop candidate.
    /// </summary>
    public sealed class DragController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private BoardView _board;
        [SerializeField] private TrayView _tray;
        [SerializeField] private DropPreview _preview;

        [Header("Feel")]
        [SerializeField] private float _liftHeight = 0.55f;
        [Tooltip("World offset along +Z (towards the top of the screen) so the finger does not hide the stack.")]
        [SerializeField] private float _forwardOffset = 0.9f;
        [SerializeField] private float _dragScale = 1.1f;
        [Tooltip("Higher = the stack follows the pointer more tightly.")]
        [SerializeField] private float _followSharpness = 28f;
        [SerializeField] private float _returnDuration = 0.18f;

        private static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);

        private IDropTarget _target;
        private bool _inputEnabled;
        private int _slot = -1;
        private StackView _dragged;
        private Vector3 _goal;
        private HexCoord? _hover;
        private readonly Coroutine[] _returns = new Coroutine[Tray.DefaultSlotCount];
        private PointerEventData _pointerData;
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();

        public bool IsDragging => _dragged != null;

        public event Action<int> DragStarted;
        /// <summary>True when the stack was dropped on the board, false when it returned to the tray.</summary>
        public event Action<bool> DragEnded;
        public event Action<HexCoord?> HoverChanged;

        public void Initialize(IDropTarget target) => _target = target;

        public bool InputEnabled
        {
            get => _inputEnabled;
            set
            {
                _inputEnabled = value;
                if (!value && IsDragging) CancelDrag();
            }
        }

        /// <summary>Drops any drag and pending return animation, e.g. before the tray is rebuilt.</summary>
        public void ResetAll()
        {
            if (IsDragging) ClearDragState(snapPreview: true);
            for (int i = 0; i < _returns.Length; i++) StopReturn(i);
        }

        private void Update()
        {
            if (!_inputEnabled || _target == null) return;
            var pointer = Pointer.current;
            if (pointer == null) return;

            Vector2 screen = pointer.position.ReadValue();
            var press = pointer.press;

            if (press.wasPressedThisFrame && !IsDragging) TryBeginDrag(screen);
            if (!IsDragging) return;

            if (press.isPressed) UpdateDrag(screen);
            else EndDrag();
        }

        private void TryBeginDrag(Vector2 screen)
        {
            if (IsOverUI(screen) || !_tray.TryPick(_camera, screen, out int slot)) return;

            StopReturn(slot);
            _slot = slot;
            _dragged = _tray.Grab(slot);
            UpdateDrag(screen, snap: false);
            DragStarted?.Invoke(slot);
        }

        private void UpdateDrag(Vector2 screen, bool snap = false)
        {
            if (TryGetGroundPoint(screen, out var ground))
                _goal = ground + new Vector3(0f, _liftHeight, _forwardOffset);

            var t = _dragged.CachedTransform;
            float k = snap ? 1f : 1f - Mathf.Exp(-_followSharpness * Time.deltaTime);
            t.position = Vector3.Lerp(t.position, _goal, k);
            t.localScale = Vector3.Lerp(t.localScale, Vector3.one * _dragScale, k);

            HexCoord? hover = null;
            if (_board.TryGetCellAt(_goal, out var cell) && _target.CanDrop(_slot, cell)) hover = cell;
            if (hover != _hover)
            {
                _hover = hover;
                _board.SetHighlight(hover);
                _board.SetMergePreview(hover, _dragged.TopColor);
                if (hover.HasValue) _preview.ShowGhost(_board.StackRestPosition(hover.Value), _dragged.Count, _dragged.TopColor);
                else _preview.HideGhost();
                HoverChanged?.Invoke(hover);
            }
            _preview.ShowShadow(t.position, t.position.y);
        }

        private void EndDrag()
        {
            var hover = _hover;
            int slot = _slot;
            bool drop = hover.HasValue && _target.CanDrop(slot, hover.Value);
            ClearDragState(snapPreview: drop);

            if (drop) _target.Drop(slot, hover.Value);
            else ReturnToSlot(slot);
            DragEnded?.Invoke(drop);
        }

        private void CancelDrag()
        {
            int slot = _slot;
            ClearDragState(snapPreview: true);
            ReturnToSlot(slot);
            DragEnded?.Invoke(false);
        }

        /// <param name="snapPreview">Put lifted neighbours back instantly (merge animations start right away).</param>
        private void ClearDragState(bool snapPreview = true)
        {
            _preview.Hide();
            if (snapPreview) _board.ClearMergePreviewImmediate();
            else _board.SetMergePreview(null, -1);
            _board.SetHighlight(null);
            _hover = null;
            _dragged = null;
            _slot = -1;
        }

        private void ReturnToSlot(int slot)
        {
            var view = _tray.Get(slot);
            if (view == null) return;
            StopReturn(slot);
            _returns[slot] = StartCoroutine(Return(slot, view));
        }

        private IEnumerator Return(int slot, StackView view)
        {
            yield return TransformTweens.MoveScale(view.CachedTransform, _tray.SlotPosition(slot), Vector3.one, _returnDuration);
            _returns[slot] = null;
        }

        private void StopReturn(int slot)
        {
            if (_returns[slot] == null) return;
            StopCoroutine(_returns[slot]);
            _returns[slot] = null;
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

        private bool TryGetGroundPoint(Vector2 screen, out Vector3 point)
        {
            var ray = _camera.ScreenPointToRay(screen);
            if (Ground.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }
            point = default;
            return false;
        }
    }
}
