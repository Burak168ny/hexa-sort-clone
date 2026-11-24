using System;
using UnityEngine;

namespace HexaSort.View
{
    /// <summary>
    /// Soft contact shadow under a stack. It stays on the ground (board cell top or tray floor)
    /// while the stack is lifted or flies, and shrinks and fades with height, which sells depth
    /// without real-time shadow maps.
    /// </summary>
    public sealed class BlobShadow : MonoBehaviour
    {
        /// <summary>Ground height under a world point; set by the board view.</summary>
        public static Func<Vector3, float> GroundAt;

        [SerializeField] private float _size = 1.2f;
        [SerializeField] private float _alpha = 0.42f;
        [SerializeField] private Vector2 _offset = new Vector2(0.05f, -0.06f);

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private Color _tint;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _block = new MaterialPropertyBlock();
            _tint = _renderer.sharedMaterial != null ? _renderer.sharedMaterial.GetColor(TintId) : Color.black;
        }

        private void LateUpdate()
        {
            var parent = transform.parent;
            if (parent == null) return;
            Vector3 p = parent.position;
            float ground = GroundAt != null ? GroundAt(p) : 0f;
            float height = Mathf.Max(0f, p.y - ground);
            float k = 1f / (1f + height * 1.6f);

            transform.position = new Vector3(p.x + _offset.x, ground + 0.004f, p.z + _offset.y);
            transform.rotation = Quaternion.identity;
            Vector3 parentScale = parent.lossyScale;
            float size = _size * Mathf.Lerp(0.6f, 1f, k);
            transform.localScale = new Vector3(size / Mathf.Max(0.01f, parentScale.x), 1f, size / Mathf.Max(0.01f, parentScale.z));

            var c = _tint;
            c.a = _alpha * k;
            _block.SetColor(TintId, c);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
