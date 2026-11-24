using UnityEngine;

namespace HexaSort.View
{
    /// <summary>One pooled tile. Holds its color id so the view can be checked against the model.</summary>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class TileView : MonoBehaviour
    {
        [SerializeField] private MeshRenderer _renderer;

        public int ColorId { get; private set; } = -1;
        private Transform _transform;

        // Lazy: pooled tiles may be used before their first Awake.
        public Transform CachedTransform => _transform != null ? _transform : _transform = transform;

        public void SetColor(int colorId, Material material)
        {
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            ColorId = colorId;
            _renderer.sharedMaterial = material;
        }
    }
}
