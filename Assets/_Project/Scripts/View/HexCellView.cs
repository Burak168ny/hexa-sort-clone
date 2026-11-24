using System.Collections;
using HexaSort.Model;
using HexaSort.Utils;
using TMPro;
using UnityEngine;

namespace HexaSort.View
{
    /// <summary>The slot under a stack. Shows hover highlight, and for locked cells the score that opens them.</summary>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class HexCellView : MonoBehaviour
    {
        [SerializeField] private MeshRenderer _renderer;
        [Tooltip("World-space label lying on the cell; shows the unlock score while locked.")]
        [SerializeField] private TMP_Text _lockLabel;
        [SerializeField] private float _unlockDuration = 0.35f;

        private VisualConfig _config;
        private bool _highlighted;
        private bool _locked;
        private Coroutine _unlockRoutine;

        public HexCoord Coord { get; private set; }

        public void Init(HexCoord coord, bool locked, int unlockScore, VisualConfig config)
        {
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            if (_unlockRoutine != null) StopCoroutine(_unlockRoutine);
            _unlockRoutine = null;

            Coord = coord;
            _config = config;
            _locked = locked;
            _highlighted = false;
            transform.localScale = Vector3.one;
            if (_lockLabel != null)
            {
                _lockLabel.text = unlockScore.ToString();
                _lockLabel.alpha = 1f;
                _lockLabel.transform.localScale = Vector3.one;
                _lockLabel.gameObject.SetActive(locked);
            }
            Refresh();
        }

        public void SetHighlighted(bool value)
        {
            if (_highlighted == value) return;
            _highlighted = value;
            Refresh();
        }

        /// <summary>Opens the cell with a short pop; the label floats up and fades.</summary>
        public void Unlock()
        {
            if (!_locked) return;
            _locked = false;
            Refresh();
            if (_unlockRoutine != null) StopCoroutine(_unlockRoutine);
            _unlockRoutine = StartCoroutine(UnlockRoutine());
        }

        private IEnumerator UnlockRoutine()
        {
            var labelTransform = _lockLabel != null ? _lockLabel.transform : null;
            Vector3 labelStart = labelTransform != null ? labelTransform.localPosition : Vector3.zero;

            for (float t = 0f; t < _unlockDuration; t += Time.deltaTime)
            {
                float k = t / _unlockDuration;
                float pop = 1f + 0.15f * Easing.Arc(k);
                transform.localScale = new Vector3(pop, 1f, pop);
                if (labelTransform != null)
                {
                    labelTransform.localPosition = labelStart + Vector3.up * (0.4f * Easing.OutCubic(k));
                    labelTransform.localScale = Vector3.one * (1f + 0.5f * k);
                    _lockLabel.alpha = 1f - k;
                }
                yield return null;
            }

            transform.localScale = Vector3.one;
            if (labelTransform != null)
            {
                labelTransform.localPosition = labelStart;
                _lockLabel.gameObject.SetActive(false);
            }
            _unlockRoutine = null;
        }

        private void Refresh()
        {
            _renderer.sharedMaterial = _locked ? _config.CellLockedMaterial
                : _highlighted ? _config.CellHighlightMaterial
                : _config.CellMaterial;
        }
    }
}
