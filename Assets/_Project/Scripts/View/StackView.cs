using System.Collections.Generic;
using HexaSort.Model;
using UnityEngine;

namespace HexaSort.View
{
    /// <summary>
    /// Visual pile of tiles mirroring a <see cref="HexStack"/>. Tiles are children placed at
    /// index * tileHeight; animations pop and push tiles exactly like the model does.
    /// </summary>
    public sealed class StackView : MonoBehaviour
    {
        private readonly List<TileView> _tiles = new List<TileView>(12);
        private VisualConfig _config;

        public int Count => _tiles.Count;
        public TileView this[int index] => _tiles[index];
        private Transform _transform;

        public Transform CachedTransform => _transform != null ? _transform : _transform = transform;

        /// <summary>Color id of the top tile, or -1 when empty.</summary>
        public int TopColor => _tiles.Count > 0 ? _tiles[_tiles.Count - 1].ColorId : -1;

        /// <summary>World height of the pile above its base.</summary>
        public float Height => _tiles.Count * (_config != null ? _config.TileHeight : 0f);

        // Squash-and-stretch "punch" when tiles land or the stack lands.
        [SerializeField] private float _punchDuration = 0.18f;
        private float _punchTime = -1f;
        private float _punchStrength;

        /// <summary>Quick squash (y down, xz out) that springs back. Only scale is touched.</summary>
        public void Punch(float strength)
        {
            _punchStrength = Mathf.Max(strength, _punchTime >= 0f ? _punchStrength : 0f);
            _punchTime = 0f;
            enabled = true;
        }

        private void Update()
        {
            if (_punchTime < 0f)
            {
                enabled = false;
                return;
            }

            _punchTime += Time.deltaTime;
            float t = Mathf.Clamp01(_punchTime / _punchDuration);
            // Damped bounce: squash first, small overshoot after.
            float wave = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t);
            float a = _punchStrength * wave;
            CachedTransform.localScale = new Vector3(1f + a * 0.5f, 1f - a, 1f + a * 0.5f);
            if (t >= 1f)
            {
                CachedTransform.localScale = Vector3.one;
                _punchTime = -1f;
                enabled = false;
            }
        }

        public void Build(HexStack stack, TilePool pool, VisualConfig config)
        {
            _config = config;
            ReleaseTiles(pool);
            for (int i = 0; i < stack.Count; i++)
            {
                var tile = pool.Get(stack[i], CachedTransform);
                tile.CachedTransform.localPosition = LocalTilePosition(i);
                _tiles.Add(tile);
            }
        }

        /// <summary>Local position of the tile at stack index <paramref name="index"/> (0 = bottom).</summary>
        public Vector3 LocalTilePosition(int index) =>
            new Vector3(0f, (index + 0.5f) * _config.TileHeight, 0f);

        public Vector3 WorldTilePosition(int index) => CachedTransform.TransformPoint(LocalTilePosition(index));

        /// <summary>World position just above the current top tile.</summary>
        public Vector3 WorldTop => CachedTransform.TransformPoint(new Vector3(0f, _tiles.Count * _config.TileHeight, 0f));

        /// <summary>Detaches the top tile. It keeps its world transform until the caller re-parents it.</summary>
        public TileView PopTop()
        {
            int last = _tiles.Count - 1;
            var tile = _tiles[last];
            _tiles.RemoveAt(last);
            return tile;
        }

        /// <summary>Adopts <paramref name="tile"/> as the new top, keeping its world pose for animation.</summary>
        public void PushTop(TileView tile)
        {
            tile.CachedTransform.SetParent(CachedTransform, true);
            _tiles.Add(tile);
        }

        public void ReleaseTiles(TilePool pool)
        {
            _punchTime = -1f;
            for (int i = 0; i < _tiles.Count; i++) pool.Release(_tiles[i]);
            _tiles.Clear();
        }

        /// <summary>True when tile colors equal the model stack (used by debug validation).</summary>
        public bool Matches(HexStack stack)
        {
            if (stack == null) return _tiles.Count == 0;
            if (stack.Count != _tiles.Count) return false;
            for (int i = 0; i < _tiles.Count; i++)
                if (_tiles[i].ColorId != stack[i]) return false;
            return true;
        }
    }
}
