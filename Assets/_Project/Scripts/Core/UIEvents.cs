using System;

namespace HexaSort.Core
{
    /// <summary>Cross-cutting UI signals that feedback systems listen to without knowing about UI classes.</summary>
    public static class UIEvents
    {
        public static event Action ButtonPressed;

        public static void RaiseButtonPressed() => ButtonPressed?.Invoke();
    }
}
