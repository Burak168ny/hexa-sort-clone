using System;
using System.Collections.Generic;

namespace HexaSort.Model
{
    /// <summary>Tiny persistence abstraction so progress logic is testable without PlayerPrefs.</summary>
    public interface IKeyValueStore
    {
        int GetInt(string key, int defaultValue);
        void SetInt(string key, int value);
        void Save();
    }

    /// <summary>In-memory store for tests and editor tooling.</summary>
    public sealed class MemoryStore : IKeyValueStore
    {
        private readonly Dictionary<string, int> _values = new Dictionary<string, int>();
        public int SaveCount { get; private set; }

        public int GetInt(string key, int defaultValue) => _values.TryGetValue(key, out int v) ? v : defaultValue;
        public void SetInt(string key, int value) => _values[key] = value;
        public void Save() => SaveCount++;
    }

    /// <summary>Player progress and settings. The level number is 1-based and keeps growing past the last level.</summary>
    public sealed class ProgressService
    {
        public const string LevelKey = "hexasort.level";
        public const string SoundKey = "hexasort.sound";
        public const string HapticsKey = "hexasort.haptics";

        private readonly IKeyValueStore _store;

        public int LevelNumber { get; private set; }
        public bool SoundEnabled { get; private set; }
        public bool HapticsEnabled { get; private set; }

        public event Action SettingsChanged;

        public ProgressService(IKeyValueStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            LevelNumber = Math.Max(1, _store.GetInt(LevelKey, 1));
            SoundEnabled = _store.GetInt(SoundKey, 1) != 0;
            HapticsEnabled = _store.GetInt(HapticsKey, 1) != 0;
        }

        /// <summary>Marks the current level as beaten and moves to the next one (saved immediately).</summary>
        public void CompleteLevel()
        {
            LevelNumber++;
            _store.SetInt(LevelKey, LevelNumber);
            _store.Save();
        }

        public void SetSound(bool enabled)
        {
            if (SoundEnabled == enabled) return;
            SoundEnabled = enabled;
            Persist(SoundKey, enabled);
        }

        public void SetHaptics(bool enabled)
        {
            if (HapticsEnabled == enabled) return;
            HapticsEnabled = enabled;
            Persist(HapticsKey, enabled);
        }

        private void Persist(string key, bool value)
        {
            _store.SetInt(key, value ? 1 : 0);
            _store.Save();
            SettingsChanged?.Invoke();
        }
    }
}
