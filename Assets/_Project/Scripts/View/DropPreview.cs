using UnityEngine;

namespace HexaSort.View
{
    /// <summary>
    /// Drag feedback: a translucent silhouette of the dragged stack on the hovered cell, and a soft
    /// shadow on the ground under the stack in the air. Both share one transparent material and are
    /// tinted per use through a MaterialPropertyBlock.
    /// </summary>
    public sealed class DropPreview : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private VisualConfig _config;
        [SerializeField] private MeshRenderer _ghost;
        [SerializeField] private MeshRenderer _shadow;
        [SerializeField, Range(0f, 1f)] private float _ghostAlpha = 0.45f;
        [SerializeField] private Color _shadowColor = new Color(0f, 0f, 0.05f, 0.28f);

        private MaterialPropertyBlock _block;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, _shadowColor);
            _shadow.SetPropertyBlock(_block);
            Hide();
        }

        /// <summary>Silhouette of <paramref name="tiles"/> tiles standing on a cell, in the stack's top color.</summary>
        public void ShowGhost(Vector3 restPosition, int tiles, int topColor)
        {
            float height = tiles * _config.TileHeight;
            var t = _ghost.transform;
            t.position = restPosition + Vector3.up * (height * 0.5f);
            t.localScale = new Vector3(1f, tiles, 1f);

            Color c = _config.Palette[topColor % _config.Palette.Length];
            c = Color.Lerp(c, Color.white, 0.35f);
            c.a = _ghostAlpha;
            _block.SetColor(BaseColorId, c);
            _ghost.SetPropertyBlock(_block);
            _ghost.enabled = true;
        }

        public void HideGhost() => _ghost.enabled = false;

        /// <summary>Flat shadow on the ground under a lifted stack; shrinks with height.</summary>
        public void ShowShadow(Vector3 groundPosition, float heightAboveGround)
        {
            var t = _shadow.transform;
            t.position = new Vector3(groundPosition.x, _config.CellTopY + 0.01f, groundPosition.z);
            float s = Mathf.Clamp(1.05f - heightAboveGround * 0.25f, 0.6f, 1.05f);
            t.localScale = new Vector3(s, 1f, s);
            _shadow.enabled = true;
        }

        public void HideShadow() => _shadow.enabled = false;

        public void Hide()
        {
            HideGhost();
            HideShadow();
        }
    }
}
