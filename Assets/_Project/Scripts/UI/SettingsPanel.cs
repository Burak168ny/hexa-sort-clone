using System;
using HexaSort.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HexaSort.UI
{
    /// <summary>Sound/vibration toggles (persisted via <see cref="ProgressService"/>) plus restart and home.</summary>
    public sealed class SettingsPanel : UIPanel
    {
        [SerializeField] private Button _soundButton;
        [SerializeField] private TMP_Text _soundLabel;
        [SerializeField] private Button _hapticsButton;
        [SerializeField] private TMP_Text _hapticsLabel;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _homeButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Color _onColor = new Color(0.30f, 0.80f, 0.45f);
        [SerializeField] private Color _offColor = new Color(0.62f, 0.64f, 0.72f);

        private ProgressService _progress;

        public void Bind(ProgressService progress, Action restart, Action home)
        {
            _progress = progress;
            _soundButton.onClick.AddListener(() => _progress.SetSound(!_progress.SoundEnabled));
            _hapticsButton.onClick.AddListener(() => _progress.SetHaptics(!_progress.HapticsEnabled));
            _restartButton.onClick.AddListener(() => { Hide(); restart(); });
            _homeButton.onClick.AddListener(() => { Hide(); home(); });
            _closeButton.onClick.AddListener(() => Hide());
            _progress.SettingsChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_progress != null) _progress.SettingsChanged -= Refresh;
        }

        private void Refresh()
        {
            Apply(_soundButton, _soundLabel, "Sound", _progress.SoundEnabled);
            Apply(_hapticsButton, _hapticsLabel, "Vibration", _progress.HapticsEnabled);
        }

        private void Apply(Button button, TMP_Text label, string name, bool on)
        {
            label.text = on ? $"{name}: ON" : $"{name}: OFF";
            button.image.color = on ? _onColor : _offColor;
        }
    }
}
