using System.Collections.Generic;
using UnityEngine;

namespace HexaSort.View
{
    /// <summary>Recycles tile objects so merges and clears never instantiate or destroy during play.</summary>
    public sealed class TilePool : MonoBehaviour
    {
        [SerializeField] private VisualConfig _config;
        [SerializeField] private TileView _prefab;
        [SerializeField] private int _prewarm = 160;

        private readonly Stack<TileView> _free = new Stack<TileView>();

        private void Awake()
        {
            for (int i = 0; i < _prewarm; i++) _free.Push(Create());
        }

        public TileView Get(int colorId, Transform parent)
        {
            var tile = _free.Count > 0 ? _free.Pop() : Create();
            tile.SetColor(colorId, _config.GetTileMaterial(colorId));
            var t = tile.CachedTransform;
            t.SetParent(parent, false);
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            tile.gameObject.SetActive(true);
            return tile;
        }

        public void Release(TileView tile)
        {
            tile.gameObject.SetActive(false);
            tile.CachedTransform.SetParent(transform, false);
            _free.Push(tile);
        }

        private TileView Create()
        {
            var tile = Instantiate(_prefab, transform);
            tile.gameObject.SetActive(false);
            return tile;
        }
    }
}
