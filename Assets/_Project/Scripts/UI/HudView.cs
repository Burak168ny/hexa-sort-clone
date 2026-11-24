using HexaSort.Utils;
using TMPro;
using UnityEngine;

namespace HexaSort.UI
{
    /// <summary>
    /// In-game top bar: level number, goal progress bar and tutorial hint. The model's cleared count
    /// is the ceiling; the shown number climbs as flying tile icons arrive (<see cref="AddDisplayed"/>)
    /// and snaps to the ceiling if icons stop arriving, so it can never drift from the model.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _goalText;
        [Tooltip("Fill rect whose anchorMax.x is driven 0..1.")]
        [SerializeField] private RectTransform _fill;
        [SerializeField] private RectTransform _goalBadge;
        [Tooltip("Hint line for tutorial levels and booster targeting; hidden when empty.")]
        [SerializeField] private TMP_Text _tutorialText;
        [SerializeField] private float _fillSpeed = 8f;
        [SerializeField] private float _punchScale = 1.2f;
        [SerializeField] private float _punchDuration = 0.2f;
        [Tooltip("Seconds without arriving icons after which the counter snaps to the real value.")]
        [SerializeField] private float _catchUpDelay = 1.2f;

        private int _target;
        private int _ceiling;
        private int _displayed;
        private float _shownFill;
        private float _punchTime = -1f;
        private float _lastChange;
        private string _tutorial = "";

        public RectTransform GoalBadge => _goalBadge;

        public void SetLevel(int number) => _levelText.text = $"Level {number}";

        public void SetTutorial(string text)
        {
            _tutorial = text ?? "";
            ShowHint(_tutorial);
        }

        /// <summary>Temporary hint (e.g. booster targeting); null restores the tutorial hint.</summary>
        public void SetOverrideHint(string text) => ShowHint(string.IsNullOrEmpty(text) ? _tutorial : text);

        private void ShowHint(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            _tutorialText.gameObject.SetActive(show);
            if (show) _tutorialText.text = text;
        }

        /// <summary>New authoritative cleared count from the model.</summary>
        public void SetGoal(int cleared, int target, bool instant)
        {
            _target = target;
            _ceiling = cleared;
            if (instant || cleared < _displayed)
            {
                _displayed = cleared;
                _shownFill = Progress;
            }
            _lastChange = Time.unscaledTime;
            Refresh();
            enabled = true;
        }

        /// <summary>One or more flying icons arrived at the badge.</summary>
        public void AddDisplayed(int amount)
        {
            int next = Mathf.Min(_ceiling, _displayed + amount);
            if (next == _displayed) return;
            _displayed = next;
            _lastChange = Time.unscaledTime;
            _punchTime = 0f;
            Refresh();
            enabled = true;
        }

        private float Progress => _target <= 0 ? 1f : Mathf.Clamp01(_displayed / (float)_target);

        private void Refresh() => _goalText.text = $"{_displayed} / {_target}";

        private void Update()
        {
            if (_displayed < _ceiling && Time.unscaledTime - _lastChange > _catchUpDelay)
            {
                _displayed = _ceiling;
                Refresh();
            }

            _shownFill = Mathf.Lerp(_shownFill, Progress, 1f - Mathf.Exp(-_fillSpeed * Time.unscaledDeltaTime));
            if (Mathf.Abs(_shownFill - Progress) < 0.002f) _shownFill = Progress;
            ApplyFill();

            if (_punchTime >= 0f)
            {
                _punchTime += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_punchTime / _punchDuration);
                float s = 1f + (_punchScale - 1f) * Easing.Arc(t);
                if (_goalBadge != null) _goalBadge.localScale = new Vector3(s, s, 1f);
                if (t >= 1f) _punchTime = -1f;
            }

            if (_shownFill == Progress && _punchTime < 0f && _displayed == _ceiling) enabled = false;
        }

        private void ApplyFill()
        {
            var max = _fill.anchorMax;
            max.x = _shownFill;
            _fill.anchorMax = max;
            _fill.gameObject.SetActive(_shownFill > 0.001f);
        }
    }
}
