using System.Collections.Generic;
using HexaSort.Utils;
using HexaSort.View;
using UnityEngine;
using UnityEngine.UI;

namespace HexaSort.UI
{
    /// <summary>
    /// Every scored tile that pops spawns a small hex icon at its screen position which curves up
    /// into the HUD goal badge; the counter ticks when it arrives. Icons are pooled and animated in
    /// one Update loop (no coroutines, no allocation while playing).
    /// </summary>
    public sealed class GoalIconFlight : MonoBehaviour
    {
        [SerializeField] private MergeSequencePlayer _player;
        [SerializeField] private Camera _camera;
        [SerializeField] private HudView _hud;
        [SerializeField] private VisualConfig _visuals;
        [SerializeField] private Sprite _iconSprite;
        [SerializeField] private int _poolSize = 48;
        [SerializeField] private float _iconSize = 54f;
        [SerializeField] private float _duration = 0.6f;
        [SerializeField] private float _curve = 260f;

        private struct Flight
        {
            public Image Icon;
            public Vector2 From;
            public Vector2 Control;
            public float Time;
        }

        private RectTransform _area;
        private readonly Stack<Image> _free = new Stack<Image>();
        private readonly List<Flight> _active = new List<Flight>();

        private void Awake()
        {
            _area = (RectTransform)transform;
            for (int i = 0; i < _poolSize; i++) _free.Push(CreateIcon());
        }

        private void OnEnable() => _player.TilePopped += OnTilePopped;
        private void OnDisable() => _player.TilePopped -= OnTilePopped;

        private Image CreateIcon()
        {
            var go = new GameObject("GoalIcon", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_area, false);
            rt.sizeDelta = new Vector2(_iconSize, _iconSize);
            var image = go.GetComponent<Image>();
            image.sprite = _iconSprite;
            image.raycastTarget = false;
            go.SetActive(false);
            return image;
        }

        private void OnTilePopped(Vector3 world, int color)
        {
            if (_free.Count == 0 || !_hud.isActiveAndEnabled)
            {
                _hud.AddDisplayed(1);
                return;
            }

            Vector2 screen = _camera.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, screen, null, out var from);
            Vector2 to = BadgeLocal();

            var icon = _free.Pop();
            icon.gameObject.SetActive(true);
            icon.rectTransform.anchoredPosition = from;
            icon.rectTransform.localScale = Vector3.one;
            icon.color = _visuals.Palette[Mathf.Clamp(color, 0, _visuals.Palette.Length - 1)];

            // Control point bows the path sideways (alternating) and upwards for a lively arc.
            float side = (_active.Count & 1) == 0 ? 1f : -1f;
            Vector2 mid = (from + to) * 0.5f;
            Vector2 normal = new Vector2(-(to - from).y, (to - from).x).normalized;
            _active.Add(new Flight { Icon = icon, From = from, Control = mid + normal * (_curve * side * 0.5f) + Vector2.up * _curve * 0.3f });
        }

        private Vector2 BadgeLocal()
        {
            var badge = _hud.GoalBadge;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, badge.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, screen, null, out var local);
            return local;
        }

        private void Update()
        {
            if (_active.Count == 0) return;
            Vector2 to = BadgeLocal();
            float dt = Time.unscaledDeltaTime;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var f = _active[i];
                f.Time += dt;
                float t = Mathf.Clamp01(f.Time / _duration);
                float e = Easing.InQuad(t) * 0.6f + Easing.InOutQuad(t) * 0.4f;

                Vector2 a = Vector2.Lerp(f.From, f.Control, e);
                Vector2 b = Vector2.Lerp(f.Control, to, e);
                f.Icon.rectTransform.anchoredPosition = Vector2.Lerp(a, b, e);
                float scale = t < 0.15f ? Mathf.Lerp(0.4f, 1.2f, t / 0.15f) : Mathf.Lerp(1.2f, 0.7f, (t - 0.15f) / 0.85f);
                f.Icon.rectTransform.localScale = new Vector3(scale, scale, 1f);

                if (t >= 1f)
                {
                    f.Icon.gameObject.SetActive(false);
                    _free.Push(f.Icon);
                    _active.RemoveAt(i);
                    _hud.AddDisplayed(1);
                    continue;
                }
                _active[i] = f;
            }
        }
    }
}
