using HexaSort.Core.Audio;
using HexaSort.Model;
using HexaSort.View;
using UnityEngine;

namespace HexaSort.Core.Feedback
{
    /// <summary>
    /// The single place where gameplay events become feel: sound, haptics, camera shake and particles.
    /// Gameplay code only raises events; tuning the "juice" never touches rules or views.
    /// </summary>
    public sealed class FeedbackController : MonoBehaviour
    {
        [SerializeField] private GameManager _game;
        [SerializeField] private MergeSequencePlayer _player;
        [SerializeField] private DragController _drag;
        [SerializeField] private CameraFitter _camera;
        [SerializeField] private AudioManager _audio;
        [SerializeField] private TileBurstFx _burst;
        [SerializeField] private VisualConfig _visuals;
        [SerializeField] private Material _confettiMaterial;

        [Header("Tuning")]
        [SerializeField] private int _particlesPerPop = 7;
        [SerializeField] private int _particlesPerShatter = 4;
        [SerializeField] private float _tileHapticInterval = 0.07f;
        [SerializeField] private float _comboShake = 0.05f;
        [SerializeField] private float _hammerShake = 0.14f;

        private readonly Haptics _haptics = new Haptics();
        private ConfettiEffect _confetti;
        private Camera _cam;
        private float _lastTileHaptic;
        private int _popsInGroup;

        private void Awake()
        {
            _cam = _camera.GetComponent<Camera>();
            _confetti = ConfettiEffect.Create(_confettiMaterial, _visuals.Palette);
            _confetti.transform.SetParent(transform, false);
        }

        private void Start()
        {
            // Progress exists after GameManager.Awake.
            _game.Progress.SettingsChanged += ApplySettings;
            ApplySettings();
        }

        private void OnEnable()
        {
            _game.StateChanged += OnStateChanged;
            _game.StackLanded += OnStackLanded;
            _game.CellUnlocked += OnCellUnlocked;
            _game.BoosterUsed += OnBoosterUsed;
            _player.TileLanded += OnTileLanded;
            _player.GroupCleared += OnGroupCleared;
            _player.TilePopped += OnTilePopped;
            _player.TileShattered += OnTileShattered;
            _drag.DragStarted += OnDragStarted;
            _drag.DragEnded += OnDragEnded;
            UIEvents.ButtonPressed += OnButtonPressed;
        }

        private void OnDisable()
        {
            _game.StateChanged -= OnStateChanged;
            _game.StackLanded -= OnStackLanded;
            _game.CellUnlocked -= OnCellUnlocked;
            _game.BoosterUsed -= OnBoosterUsed;
            _player.TileLanded -= OnTileLanded;
            _player.GroupCleared -= OnGroupCleared;
            _player.TilePopped -= OnTilePopped;
            _player.TileShattered -= OnTileShattered;
            _drag.DragStarted -= OnDragStarted;
            _drag.DragEnded -= OnDragEnded;
            UIEvents.ButtonPressed -= OnButtonPressed;
            if (_game.Progress != null) _game.Progress.SettingsChanged -= ApplySettings;
        }

        private void ApplySettings()
        {
            _audio.Enabled = _game.Progress.SoundEnabled;
            _haptics.Enabled = _game.Progress.HapticsEnabled;
        }

        private void OnButtonPressed() => _audio.Play(SoundId.Button);

        private void OnDragStarted(int slot)
        {
            _audio.Play(SoundId.Pick);
            _haptics.Play(HapticType.Light);
        }

        private void OnDragEnded(bool dropped)
        {
            if (!dropped) _audio.Play(SoundId.Invalid, 1.4f, 0.35f);
        }

        private void OnStackLanded(HexCoord cell)
        {
            _audio.Play(SoundId.Drop);
            _haptics.Play(HapticType.Medium);
        }

        private void OnTileLanded(TileView tile, int index)
        {
            _audio.Play(SoundId.TileLand, Mathf.Min(1f + 0.06f * index, 1.9f), 0.7f);
            if (Time.unscaledTime - _lastTileHaptic < _tileHapticInterval) return;
            _lastTileHaptic = Time.unscaledTime;
            _haptics.Play(HapticType.Light);
        }

        private void OnGroupCleared(ClearStep step)
        {
            _popsInGroup = 0;
            if (step.Combo >= 2)
            {
                _audio.Play(SoundId.Combo, 1f + 0.08f * (step.Combo - 2));
                _camera.Shake(_comboShake * Mathf.Min(step.Combo, 5), 0.3f);
                _haptics.Play(HapticType.Heavy);
            }
            else
            {
                _haptics.Play(HapticType.Medium);
            }
        }

        private void OnTilePopped(Vector3 position, int color)
        {
            _burst.Burst(position, PaletteColor(color), _particlesPerPop);
            _burst.Flash(position, Color.Lerp(PaletteColor(color), Color.white, 0.55f), 0.9f + Mathf.Min(_popsInGroup, 10) * 0.04f);
            if (_popsInGroup % 3 == 0) _burst.Sparkle(position, 2);
            _audio.Play(SoundId.Clear, Mathf.Min(1f + 0.04f * _popsInGroup++, 1.8f), 0.55f);
        }

        private void OnTileShattered(Vector3 position, int color) =>
            _burst.Burst(position, PaletteColor(color), _particlesPerShatter);

        private void OnCellUnlocked(HexCoord cell)
        {
            _audio.Play(SoundId.Unlock, 1.2f);
            _haptics.Play(HapticType.Success);
        }

        private void OnBoosterUsed(BoosterType type)
        {
            switch (type)
            {
                case BoosterType.Hammer:
                    _audio.Play(SoundId.Hammer, 0.8f);
                    _camera.Shake(_hammerShake, 0.35f);
                    _haptics.Play(HapticType.Heavy);
                    break;
                case BoosterType.Move:
                    _audio.Play(SoundId.Pick, 0.9f);
                    break;
                case BoosterType.Shuffle:
                    _audio.Play(SoundId.Shuffle);
                    _haptics.Play(HapticType.Medium);
                    break;
            }
        }

        private void OnStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Win:
                    _confetti.Play(_cam);
                    _audio.Play(SoundId.Win);
                    _haptics.Play(HapticType.Success);
                    break;
                case GameState.Fail:
                    _audio.Play(SoundId.Fail);
                    _haptics.Play(HapticType.Failure);
                    _camera.Shake(0.08f, 0.3f);
                    break;
            }
        }

        private Color PaletteColor(int id) => _visuals.Palette[Mathf.Clamp(id, 0, _visuals.Palette.Length - 1)];
    }
}
