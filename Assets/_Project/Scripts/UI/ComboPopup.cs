using HexaSort.Model;
using HexaSort.Utils;
using HexaSort.View;
using TMPro;
using UnityEngine;

namespace HexaSort.UI
{
    /// <summary>"COMBO x2" / "GREAT!" text that pops above a clearing stack, rises and fades.</summary>
    public sealed class ComboPopup : MonoBehaviour
    {
        [SerializeField] private MergeSequencePlayer _player;
        [SerializeField] private BoardView _board;
        [SerializeField] private Camera _camera;
        [SerializeField] private TMP_Text[] _texts;
        [SerializeField] private float _duration = 1f;
        [SerializeField] private float _rise = 160f;
        [Tooltip("A single clear of at least this many tiles gets a compliment even without a combo.")]
        [SerializeField] private int _bigClear = 15;
        [SerializeField] private Color[] _comboColors =
        {
            new Color32(0xFF, 0xE0, 0x4A, 0xFF), new Color32(0xFF, 0xA0, 0x3D, 0xFF),
            new Color32(0xFF, 0x5E, 0x7A, 0xFF), new Color32(0xC0, 0x7C, 0xFF, 0xFF),
        };

        private RectTransform _area;
        private float[] _times;
        private Vector2[] _origins;
        private int _next;

        private void Awake()
        {
            _area = (RectTransform)transform;
            _times = new float[_texts.Length];
            _origins = new Vector2[_texts.Length];
            for (int i = 0; i < _texts.Length; i++)
            {
                _times[i] = -1f;
                _texts[i].gameObject.SetActive(false);
            }
        }

        private void OnEnable() => _player.GroupCleared += OnGroupCleared;
        private void OnDisable() => _player.GroupCleared -= OnGroupCleared;

        private void OnGroupCleared(ClearStep step)
        {
            string label;
            if (step.Combo >= 2) label = $"COMBO x{step.Combo}";
            else if (step.Count >= _bigClear) label = "GREAT!";
            else return;

            Vector3 world = _board.StackRestPosition(step.Cell) + Vector3.up * 1.2f;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, _camera.WorldToScreenPoint(world), null, out var local);

            int i = _next;
            _next = (_next + 1) % _texts.Length;
            var text = _texts[i];
            text.text = label;
            text.color = _comboColors[Mathf.Clamp(step.Combo - 1, 0, _comboColors.Length - 1)];
            text.gameObject.SetActive(true);
            _origins[i] = local;
            _times[i] = 0f;
            enabled = true;
        }

        private void Update()
        {
            bool any = false;
            for (int i = 0; i < _texts.Length; i++)
            {
                if (_times[i] < 0f) continue;
                _times[i] += Time.unscaledDeltaTime;
                float t = _times[i] / _duration;
                var text = _texts[i];
                if (t >= 1f)
                {
                    _times[i] = -1f;
                    text.gameObject.SetActive(false);
                    continue;
                }
                any = true;
                float scale = t < 0.25f ? Easing.OutBack(t / 0.25f) : 1f;
                text.rectTransform.localScale = new Vector3(scale, scale, 1f);
                text.rectTransform.anchoredPosition = _origins[i] + Vector2.up * (_rise * Easing.OutCubic(t));
                text.alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
            }
            if (!any) enabled = false;
        }
    }
}
