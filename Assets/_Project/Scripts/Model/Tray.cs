using System;

namespace HexaSort.Model
{
    /// <summary>The offer slots under the board. Refilled (by <see cref="StackGenerator.FillTray"/>) only when every slot is used.</summary>
    public sealed class Tray
    {
        public const int DefaultSlotCount = 3;

        private readonly HexStack[] _slots;

        public int SlotCount => _slots.Length;

        public Tray(int slotCount = DefaultSlotCount)
        {
            if (slotCount < 1) throw new ArgumentOutOfRangeException(nameof(slotCount));
            _slots = new HexStack[slotCount];
        }

        /// <summary>Stack in <paramref name="slot"/>, or null once it has been placed.</summary>
        public HexStack Get(int slot) => _slots[slot];

        public bool IsEmpty
        {
            get
            {
                for (int i = 0; i < _slots.Length; i++)
                    if (_slots[i] != null) return false;
                return true;
            }
        }

        public HexStack Take(int slot)
        {
            var stack = _slots[slot] ?? throw new InvalidOperationException($"Tray slot {slot} is empty.");
            _slots[slot] = null;
            return stack;
        }

        public void Set(int slot, HexStack stack) => _slots[slot] = stack;
    }
}
