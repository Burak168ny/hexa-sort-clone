using HexaSort.Core;
using HexaSort.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HexaSort.UI
{
    /// <summary>
    /// Presenter: maps <see cref="GameManager"/> state and events onto panels, and buttons onto
    /// GameManager commands. Holds no game rules of its own.
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        [SerializeField] private GameManager _game;

        [Header("Home")]
        [SerializeField] private UIPanel _homePanel;
        [SerializeField] private TMP_Text _homeLevelText;
        [SerializeField] private Button _playButton;

        [Header("HUD")]
        [SerializeField] private UIPanel _hudPanel;
        [SerializeField] private HudView _hud;
        [SerializeField] private Button _settingsButton;

        [Header("Win")]
        [SerializeField] private UIPanel _winPanel;
        [SerializeField] private TMP_Text _winTitle;
        [SerializeField] private Button _nextButton;

        [Header("Fail")]
        [SerializeField] private UIPanel _failPanel;
        [SerializeField] private Button _reviveButton;
        [SerializeField] private Button _retryButton;

        [Header("Settings")]
        [SerializeField] private SettingsPanel _settingsPanel;

        private int _levelNumber;

        private void Awake()
        {
            _playButton.onClick.AddListener(_game.Play);
            _nextButton.onClick.AddListener(_game.NextLevel);
            _retryButton.onClick.AddListener(_game.Retry);
            _reviveButton.onClick.AddListener(_game.Revive);
            _settingsButton.onClick.AddListener(() => _settingsPanel.Show());

            _game.StateChanged += OnStateChanged;
            _game.LevelLoaded += OnLevelLoaded;
            _game.GoalChanged += OnGoalChanged;

            foreach (var panel in new[] { _homePanel, _hudPanel, _winPanel, _failPanel, _settingsPanel })
                panel.Hide(instant: true);
        }

        private void Start()
        {
            // Progress is created in GameManager.Awake, so bind here.
            _settingsPanel.Bind(_game.Progress, _game.Retry, _game.GoHome);
        }

        private void OnDestroy()
        {
            if (_game == null) return;
            _game.StateChanged -= OnStateChanged;
            _game.LevelLoaded -= OnLevelLoaded;
            _game.GoalChanged -= OnGoalChanged;
        }

        private void OnLevelLoaded(LevelDefinition level)
        {
            _levelNumber = level.Number;
            _hud.SetLevel(level.Number);
            _hud.SetTutorial(level.TutorialText);
            _homeLevelText.text = string.IsNullOrEmpty(level.Name)
                ? $"Level {level.Number}"
                : $"Level {level.Number}\n<size=65%>{level.Name}</size>";
        }

        private void OnGoalChanged(int cleared, int target) =>
            _hud.SetGoal(cleared, target, instant: _game.State == GameState.Loading);

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Loading) return;

            _homePanel.SetVisible(state == GameState.Home);
            _hudPanel.SetVisible(state != GameState.Home);
            _winPanel.SetVisible(state == GameState.Win);
            _failPanel.SetVisible(state == GameState.Fail);
            _settingsButton.interactable = state == GameState.Playing;
            if (state != GameState.Playing && state != GameState.Resolving) _settingsPanel.Hide();

            if (state == GameState.Win) _winTitle.text = $"Level {_levelNumber}\nComplete!";
            if (state == GameState.Fail) _reviveButton.gameObject.SetActive(_game.CanRevive);
        }
    }
}
