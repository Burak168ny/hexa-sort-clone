using System.Collections.Generic;
using HexaSort.Model;
using UnityEngine;

namespace HexaSort.View
{
    /// <summary>
    /// Owns the cell and stack views of the board and converts between hex and world space.
    /// It only mirrors the model; gameplay changes arrive through <see cref="MergeSequencePlayer"/>.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private VisualConfig _config;
        [SerializeField] private HexCellView _cellPrefab;
        [SerializeField] private StackViewPool _stackPool;

        private readonly Dictionary<HexCoord, HexCellView> _cells = new Dictionary<HexCoord, HexCellView>();
        private readonly Dictionary<HexCoord, StackView> _stacks = new Dictionary<HexCoord, StackView>();
        private readonly List<HexCellView> _cellPool = new List<HexCellView>();
        private HexCellView _highlighted;
        private readonly HashSet<HexCellView> _marked = new HashSet<HexCellView>();

        [Header("Drop preview")]
        [Tooltip("How far neighbouring stacks that would merge rise while a matching stack hovers.")]
        [SerializeField] private float _previewLift = 0.14f;
        [SerializeField] private float _previewSpeed = 14f;
        private readonly List<LiftState> _lifts = new List<LiftState>();

        private struct LiftState
        {
            public HexCoord Coord;
            public StackView View;
            public float Current;
            public float Target;
        }

        public VisualConfig Config => _config;
        public StackViewPool StackPool => _stackPool;

        /// <summary>World-space bounds of all cells (for camera framing).</summary>
        public Bounds CellBounds { get; private set; }

        public void Build(BoardModel board)
        {
            Clear();

            var bounds = new Bounds();
            bool first = true;
            foreach (var cell in board.Cells)
            {
                var view = GetCellView();
                view.transform.localPosition = _config.CellToLocal(cell.Coord);
                view.Init(cell.Coord, cell.IsLocked, cell.UnlockScore, _config);
                _cells.Add(cell.Coord, view);

                var world = CellToWorld(cell.Coord);
                if (first) bounds = new Bounds(world, Vector3.zero);
                else bounds.Encapsulate(world);
                first = false;

                if (cell.Stack != null) SetStack(cell.Coord, _stackPool.Spawn(cell.Stack, transform));
            }

            // Grow by one cell radius so the outline, not the centres, is framed.
            bounds.Expand(new Vector3(_config.HexSize * 2f, 0f, _config.HexSize * 2f));
            CellBounds = bounds;
        }

        public void Clear()
        {
            foreach (var stack in _stacks.Values) _stackPool.Despawn(stack);
            _stacks.Clear();
            foreach (var cell in _cells.Values)
            {
                cell.gameObject.SetActive(false);
                _cellPool.Add(cell);
            }
            _cells.Clear();
            _highlighted = null;
            _marked.Clear();
            _lifts.Clear();
        }

        public Vector3 CellToWorld(HexCoord coord) => transform.TransformPoint(_config.CellToLocal(coord));

        /// <summary>World position where the bottom of a stack rests on <paramref name="coord"/>.</summary>
        public Vector3 StackRestPosition(HexCoord coord) => CellToWorld(coord) + Vector3.up * _config.CellTopY;

        private void OnEnable() => BlobShadow.GroundAt = GroundHeightAt;

        private void OnDisable() => BlobShadow.GroundAt = null;

        /// <summary>Height of the surface under a world point: a cell top on the board, the floor elsewhere.</summary>
        public float GroundHeightAt(Vector3 world) =>
            TryGetCellAt(world, out _) ? transform.position.y + _config.CellTopY : 0f;

        /// <summary>The board cell under a world point (any height), if one exists there.</summary>
        public bool TryGetCellAt(Vector3 world, out HexCoord coord)
        {
            coord = _config.LocalToCell(transform.InverseTransformPoint(world));
            return _cells.ContainsKey(coord);
        }

        public StackView GetStack(HexCoord coord) => _stacks.TryGetValue(coord, out var s) ? s : null;

        /// <summary>Registers <paramref name="view"/> at <paramref name="coord"/> and parents it to the board.</summary>
        public void SetStack(HexCoord coord, StackView view)
        {
            _stacks[coord] = view;
            view.CachedTransform.SetParent(transform, true);
            view.CachedTransform.position = StackRestPosition(coord);
        }

        /// <summary>Registers without snapping, for stacks that animate into place.</summary>
        public void AdoptStack(HexCoord coord, StackView view)
        {
            _stacks[coord] = view;
            view.CachedTransform.SetParent(transform, true);
        }

        public StackView RemoveStack(HexCoord coord)
        {
            if (!_stacks.TryGetValue(coord, out var view)) return null;
            _stacks.Remove(coord);
            return view;
        }

        public void SetHighlight(HexCoord? coord)
        {
            HexCellView next = null;
            if (coord.HasValue) _cells.TryGetValue(coord.Value, out next);
            if (next == _highlighted) return;
            if (_highlighted != null && !_marked.Contains(_highlighted)) _highlighted.SetHighlighted(false);
            _highlighted = next;
            if (_highlighted != null) _highlighted.SetHighlighted(true);
        }

        /// <summary>Marks a set of cells (e.g. Move booster destinations); null or empty clears the marks.</summary>
        public void SetMarked(IEnumerable<HexCoord> coords)
        {
            foreach (var cell in _marked)
                if (cell != _highlighted) cell.SetHighlighted(false);
            _marked.Clear();
            if (coords == null) return;
            foreach (var c in coords)
                if (_cells.TryGetValue(c, out var cell))
                {
                    _marked.Add(cell);
                    cell.SetHighlighted(true);
                }
        }

        /// <summary>
        /// Drop preview: neighbours of <paramref name="cell"/> whose top color equals <paramref name="topColor"/>
        /// rise slightly, telling the player which stacks will merge. Pass null to clear.
        /// </summary>
        public void SetMergePreview(HexCoord? cell, int topColor)
        {
            for (int i = 0; i < _lifts.Count; i++)
            {
                var lift = _lifts[i];
                lift.Target = 0f;
                _lifts[i] = lift;
            }
            if (!cell.HasValue) return;

            for (int d = 0; d < HexCoord.DirectionCount; d++)
            {
                var n = cell.Value.Neighbor(d);
                if (!_stacks.TryGetValue(n, out var view) || view.TopColor != topColor) continue;
                int index = IndexOfLift(view);
                if (index < 0) _lifts.Add(new LiftState { Coord = n, View = view, Target = _previewLift });
                else
                {
                    var lift = _lifts[index];
                    lift.Target = _previewLift;
                    _lifts[index] = lift;
                }
            }
        }

        private int IndexOfLift(StackView view)
        {
            for (int i = 0; i < _lifts.Count; i++)
                if (_lifts[i].View == view) return i;
            return -1;
        }

        /// <summary>Snaps every preview lift back to rest immediately (before animations move stacks).</summary>
        public void ClearMergePreviewImmediate()
        {
            foreach (var lift in _lifts)
                if (_stacks.TryGetValue(lift.Coord, out var view) && view == lift.View)
                    view.CachedTransform.position = StackRestPosition(lift.Coord);
            _lifts.Clear();
        }

        private void Update()
        {
            float k = 1f - Mathf.Exp(-_previewSpeed * Time.deltaTime);
            for (int i = _lifts.Count - 1; i >= 0; i--)
            {
                var lift = _lifts[i];
                if (!_stacks.TryGetValue(lift.Coord, out var view) || view != lift.View)
                {
                    _lifts.RemoveAt(i);
                    continue;
                }
                lift.Current = Mathf.Lerp(lift.Current, lift.Target, k);
                if (lift.Target == 0f && lift.Current < 0.002f)
                {
                    view.CachedTransform.position = StackRestPosition(lift.Coord);
                    _lifts.RemoveAt(i);
                    continue;
                }
                view.CachedTransform.position = StackRestPosition(lift.Coord) + Vector3.up * lift.Current;
                _lifts[i] = lift;
            }
        }

        /// <summary>Board stack nearest to a screen point, tested at each stack's mid-height (tilted camera).</summary>
        public bool TryPickStack(Camera camera, Vector2 screen, out HexCoord coord)
        {
            coord = default;
            float best = float.MaxValue;
            bool found = false;
            foreach (var pair in _stacks)
            {
                Vector3 centre = StackRestPosition(pair.Key) + Vector3.up * (pair.Value.Height * 0.5f);
                Vector2 c = camera.WorldToScreenPoint(centre);
                Vector2 edge = camera.WorldToScreenPoint(centre + camera.transform.right * (_config.HexSize * 0.85f));
                float radius = (edge - c).magnitude;
                float sq = (screen - c).sqrMagnitude;
                if (sq > radius * radius || sq >= best) continue;
                best = sq;
                coord = pair.Key;
                found = true;
            }
            return found;
        }

        public void Unlock(HexCoord coord)
        {
            if (_cells.TryGetValue(coord, out var cell)) cell.Unlock();
        }

        /// <summary>Reports any cell whose stack view differs from the model. Returns true when in sync.</summary>
        public bool ValidateAgainst(BoardModel board)
        {
            bool ok = true;
            foreach (var cell in board.Cells)
            {
                var view = GetStack(cell.Coord);
                bool match = view == null ? cell.Stack == null : view.Matches(cell.Stack);
                if (match) continue;
                ok = false;
                Debug.LogError($"[BoardView] View/model mismatch at {cell.Coord}: model {cell.Stack}", this);
            }
            return ok;
        }

        private HexCellView GetCellView()
        {
            int last = _cellPool.Count - 1;
            if (last >= 0)
            {
                var pooled = _cellPool[last];
                _cellPool.RemoveAt(last);
                pooled.gameObject.SetActive(true);
                return pooled;
            }
            return Instantiate(_cellPrefab, transform);
        }
    }
}
