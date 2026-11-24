using HexaSort.Model;
using UnityEngine;

namespace HexaSort.View
{
    /// <summary>The offer slots under the board, laid out in a row around this transform.</summary>
    public sealed class TrayView : MonoBehaviour
    {
        [SerializeField] private StackViewPool _stackPool;
        [SerializeField] private float _spacing = 1.45f;
        [Tooltip("Grab radius around a tray stack, in world units.")]
        [SerializeField] private float _pickRadius = 0.7f;
        [SerializeField] private float _spawnDuration = 0.25f;
        [SerializeField] private float _spawnStagger = 0.07f;

        private StackView[] _slots = new StackView[Tray.DefaultSlotCount];
        private Coroutine[] _spawnRoutines = new Coroutine[Tray.DefaultSlotCount];

        public int SlotCount => _slots.Length;

        public Vector3 SlotPosition(int slot)
        {
            float x = (slot - (_slots.Length - 1) * 0.5f) * _spacing;
            return transform.TransformPoint(new Vector3(x, 0f, 0f));
        }

        public StackView Get(int slot) => _slots[slot];

        /// <summary>World box covering all slots up to <paramref name="height"/> (for camera framing).</summary>
        public Bounds GetBounds(float height)
        {
            var bounds = new Bounds(SlotPosition(0), Vector3.zero);
            bounds.Encapsulate(SlotPosition(_slots.Length - 1));
            bounds.Encapsulate(bounds.center + Vector3.up * height);
            bounds.Expand(new Vector3(_pickRadius * 2f, 0f, _pickRadius * 2f));
            return bounds;
        }

        /// <summary>
        /// Returns the slot's view for dragging, cancelling its spawn animation.
        /// The slot keeps ownership until <see cref="Take"/> is called on a successful drop.
        /// </summary>
        public StackView Grab(int slot)
        {
            StopSpawn(slot);
            return _slots[slot];
        }

        /// <summary>Removes the view from the slot; the caller now owns it.</summary>
        public StackView Take(int slot)
        {
            var view = _slots[slot];
            _slots[slot] = null;
            StopSpawn(slot);
            return view;
        }

        /// <summary>Rebuilds every slot from the model tray, popping new stacks in one after another.</summary>
        public void Show(Tray tray)
        {
            Clear();
            if (_slots.Length != tray.SlotCount)
            {
                _slots = new StackView[tray.SlotCount];
                _spawnRoutines = new Coroutine[tray.SlotCount];
            }
            for (int i = 0; i < tray.SlotCount; i++)
            {
                var stack = tray.Get(i);
                if (stack == null) continue;
                var view = _stackPool.Spawn(stack, transform);
                view.CachedTransform.position = SlotPosition(i);
                _slots[i] = view;
                _spawnRoutines[i] = StartCoroutine(TransformTweens.PopIn(view.CachedTransform, _spawnDuration, i * _spawnStagger));
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                StopSpawn(i);
                if (_slots[i] == null) continue;
                _stackPool.Despawn(_slots[i]);
                _slots[i] = null;
            }
        }

        private void StopSpawn(int slot)
        {
            if (_spawnRoutines[slot] == null) return;
            StopCoroutine(_spawnRoutines[slot]);
            _spawnRoutines[slot] = null;
        }

        /// <summary>
        /// Nearest occupied slot to a screen point. Tested in screen space against each stack's
        /// mid-height, so tall stacks are grabbable by their visible body under a tilted camera.
        /// </summary>
        public bool TryPick(Camera camera, Vector2 screen, out int slot)
        {
            slot = -1;
            float best = float.MaxValue;
            for (int i = 0; i < _slots.Length; i++)
            {
                var view = _slots[i];
                if (view == null) continue;

                Vector3 centre = SlotPosition(i) + Vector3.up * (view.WorldTop.y - SlotPosition(i).y) * 0.5f;
                Vector2 c = camera.WorldToScreenPoint(centre);
                Vector2 edge = camera.WorldToScreenPoint(centre + camera.transform.right * _pickRadius);
                float radius = (edge - c).magnitude;
                float sq = (screen - c).sqrMagnitude;
                if (sq <= radius * radius && sq < best)
                {
                    best = sq;
                    slot = i;
                }
            }
            return slot >= 0;
        }
    }
}
