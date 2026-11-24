using HexaSort.Model;
using UnityEngine;

namespace HexaSort.View
{
    /// <summary>Shared look and timing settings for the board, tiles and their animations.</summary>
    [CreateAssetMenu(menuName = "HexaSort/Visual Config", fileName = "VisualConfig")]
    public sealed class VisualConfig : ScriptableObject
    {
        [Header("Layout")]
        [Tooltip("Hex circumradius used for cell spacing (centre to corner).")]
        [SerializeField] private float _hexSize = 0.5f;
        [Tooltip("Height of one tile; also the vertical step between stacked tiles.")]
        [SerializeField] private float _tileHeight = 0.13f;
        [Tooltip("World Y of the cell surface that stacks rest on.")]
        [SerializeField] private float _cellTopY = 0.05f;

        [Header("Colors")]
        [Tooltip("Tile color per color id. Materials are generated from this by the scene builder.")]
        [SerializeField] private Color[] _palette =
        {
            new Color32(0xFF, 0x4D, 0x5E, 0xFF), // red
            new Color32(0x3D, 0x8B, 0xFF, 0xFF), // blue
            new Color32(0x4C, 0xD9, 0x64, 0xFF), // green
            new Color32(0xFF, 0xD2, 0x3F, 0xFF), // yellow
            new Color32(0xA6, 0x6C, 0xFF, 0xFF), // purple
            new Color32(0xFF, 0x8A, 0x3D, 0xFF), // orange
            new Color32(0x2E, 0xD3, 0xE6, 0xFF), // cyan
            new Color32(0xFF, 0x6F, 0xB5, 0xFF), // pink
        };
        [Tooltip("One shared material per palette entry (keeps tiles SRP-batched).")]
        [SerializeField] private Material[] _tileMaterials;

        [Header("Cells")]
        [SerializeField] private Material _cellMaterial;
        [SerializeField] private Material _cellHighlightMaterial;
        [SerializeField] private Material _cellLockedMaterial;

        [Header("Transfer animation")]
        [SerializeField] private float _transferTileInterval = 0.06f;
        [SerializeField] private float _transferTileDuration = 0.28f;
        [SerializeField] private float _transferArcHeight = 0.7f;

        [Header("Clear animation")]
        [Tooltip("Delay between consecutive tiles popping.")]
        [SerializeField] private float _clearStagger = 0.045f;
        [Tooltip("Pop + fly-away duration of one tile.")]
        [SerializeField] private float _clearDurationPerTile = 0.38f;

        [Header("Placement")]
        [SerializeField] private float _landDuration = 0.12f;
        [SerializeField] private float _stepPause = 0.05f;

        public float HexSize => _hexSize;
        public float TileHeight => _tileHeight;
        public float CellTopY => _cellTopY;
        public Color[] Palette => _palette;
        public Material CellMaterial => _cellMaterial;
        public Material CellHighlightMaterial => _cellHighlightMaterial;
        public Material CellLockedMaterial => _cellLockedMaterial;
        public float TransferTileInterval => _transferTileInterval;
        public float TransferTileDuration => _transferTileDuration;
        public float TransferArcHeight => _transferArcHeight;
        public float ClearTileInterval => _clearStagger;
        public float ClearTileDuration => _clearDurationPerTile;
        public float LandDuration => _landDuration;
        public float StepPause => _stepPause;

        public Material GetTileMaterial(int colorId) => _tileMaterials[colorId % _tileMaterials.Length];

        public Vector3 CellToLocal(HexCoord coord)
        {
            HexLayout.ToPlanar(coord, _hexSize, out float x, out float y);
            return new Vector3(x, 0f, y);
        }

        public HexCoord LocalToCell(Vector3 local) => HexLayout.FromPlanar(local.x, local.z, _hexSize);

#if UNITY_EDITOR
        /// <summary>Editor hook for the scene builder, which owns material generation.</summary>
        public void EditorSetMaterials(Material[] tiles, Material cell, Material cellHighlight, Material cellLocked)
        {
            _tileMaterials = tiles;
            _cellMaterial = cell;
            _cellHighlightMaterial = cellHighlight;
            _cellLockedMaterial = cellLocked;
        }
#endif
    }
}
