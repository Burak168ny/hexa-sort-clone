using System.Collections.Generic;
using HexaSort.Model;
using UnityEngine;

namespace HexaSort.View
{
    /// <summary>Spawns stack views (and their tiles) from pools.</summary>
    public sealed class StackViewPool : MonoBehaviour
    {
        [SerializeField] private VisualConfig _config;
        [SerializeField] private TilePool _tilePool;
        [SerializeField] private StackView _prefab;

        private readonly Stack<StackView> _free = new Stack<StackView>();

        public TilePool Tiles => _tilePool;

        public StackView Spawn(HexStack stack, Transform parent)
        {
            var view = _free.Count > 0 ? _free.Pop() : Instantiate(_prefab, transform);
            var t = view.CachedTransform;
            t.SetParent(parent, false);
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            view.gameObject.SetActive(true);
            view.Build(stack, _tilePool, _config);
            return view;
        }

        public void Despawn(StackView view)
        {
            view.ReleaseTiles(_tilePool);
            view.gameObject.SetActive(false);
            view.CachedTransform.SetParent(transform, false);
            _free.Push(view);
        }
    }
}
