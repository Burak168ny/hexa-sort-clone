using System;

namespace HexaSort.Model
{
    public enum BoosterType
    {
        /// <summary>Destroys one stack on the board.</summary>
        Hammer,
        /// <summary>Moves one board stack to an empty cell (merges resolve there).</summary>
        Move,
        /// <summary>Replaces all tray stacks with new ones.</summary>
        Shuffle,
    }

    /// <summary>
    /// Persistent booster counts. Every booster starts at <see cref="StartingCount"/>. When the starting
    /// count is raised in an update, existing players are topped up once (tracked by <see cref="GrantKey"/>).
    /// </summary>
    public sealed class BoosterInventory
    {
        public const int StartingCount = 300;
        public const string GrantKey = "hexasort.booster.grant";
        public static readonly BoosterType[] All = { BoosterType.Hammer, BoosterType.Move, BoosterType.Shuffle };

        private readonly IKeyValueStore _store;

        public event Action<BoosterType, int> Changed;

        public BoosterInventory(IKeyValueStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));

            if (_store.GetInt(GrantKey, 0) >= StartingCount) return;
            foreach (var type in All) _store.SetInt(KeyFor(type), Math.Max(Count(type), StartingCount));
            _store.SetInt(GrantKey, StartingCount);
            _store.Save();
        }

        public static string KeyFor(BoosterType type) => "hexasort.booster." + type.ToString().ToLowerInvariant();

        public int Count(BoosterType type) => Math.Max(0, _store.GetInt(KeyFor(type), StartingCount));

        public bool TryConsume(BoosterType type)
        {
            int count = Count(type);
            if (count <= 0) return false;
            Set(type, count - 1);
            return true;
        }

        public void Add(BoosterType type, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Set(type, Count(type) + amount);
        }

        private void Set(BoosterType type, int value)
        {
            _store.SetInt(KeyFor(type), value);
            _store.Save();
            Changed?.Invoke(type, value);
        }
    }
}
