using System;
using HexaSort.Core;
using HexaSort.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HexaSort.UI
{
    /// <summary>Bottom bar with the three booster buttons, their counts and the active-targeting state.</summary>
    public sealed class BoosterBar : MonoBehaviour
    {
        [Serializable]
        public sealed class Slot
        {
            public BoosterType Type;
            public Button Button;
            public TMP_Text Count;
            public Image Background;
        }

        [SerializeField] private GameManager _game;
        [SerializeField] private BoosterController _boosters;
        [SerializeField] private HudView _hud;
        [SerializeField] private Slot[] _slots;
        [SerializeField] private Color _idleColor = new Color(1f, 1f, 1f, 0.18f);
        [SerializeField] private Color _activeColor = new Color32(0xFF, 0xD2, 0x3F, 0xFF);

        // Subscriptions live for the whole lifetime (not OnEnable) because the HUD hides this bar on menus.
        private void Awake()
        {
            foreach (var slot in _slots)
            {
                var type = slot.Type;
                slot.Button.onClick.AddListener(() => _boosters.Toggle(type));
            }
            _game.StateChanged += OnStateChanged;
            _boosters.ModeChanged += OnModeChanged;
        }

        private void Start()
        {
            // Inventory exists after GameManager.Awake.
            _game.Boosters.Changed += OnCountChanged;
            Refresh();
        }

        private void OnEnable() => Refresh();

        private void OnDestroy()
        {
            if (_game == null) return;
            _game.StateChanged -= OnStateChanged;
            if (_game.Boosters != null) _game.Boosters.Changed -= OnCountChanged;
            if (_boosters != null) _boosters.ModeChanged -= OnModeChanged;
        }

        private void OnCountChanged(BoosterType type, int count) => Refresh();
        private void OnStateChanged(GameState state) => Refresh();

        private void OnModeChanged(BoosterType? active, string hint)
        {
            _hud.SetOverrideHint(hint);
            Refresh();
        }

        private void Refresh()
        {
            if (_game.Boosters == null) return;
            bool playing = _game.State == GameState.Playing;
            foreach (var slot in _slots)
            {
                int count = _game.Boosters.Count(slot.Type);
                bool active = _boosters.Active == slot.Type;
                slot.Count.text = count.ToString();
                slot.Button.interactable = playing && (count > 0 || active);
                slot.Background.color = active ? _activeColor : _idleColor;
            }
        }
    }
}
