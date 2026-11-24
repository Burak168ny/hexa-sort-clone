using HexaSort.Model;
using UnityEngine;

namespace HexaSort.Core
{
    /// <summary><see cref="IKeyValueStore"/> backed by PlayerPrefs.</summary>
    public sealed class PlayerPrefsStore : IKeyValueStore
    {
        public int GetInt(string key, int defaultValue) => PlayerPrefs.GetInt(key, defaultValue);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void Save() => PlayerPrefs.Save();
    }
}
