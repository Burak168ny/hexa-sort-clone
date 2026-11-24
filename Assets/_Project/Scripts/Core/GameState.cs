namespace HexaSort.Core
{
    public enum GameState
    {
        /// <summary>Level data is being built; no input.</summary>
        Loading,
        /// <summary>Main screen over the loaded level; waiting for Play.</summary>
        Home,
        /// <summary>Waiting for the player to drag a stack.</summary>
        Playing,
        /// <summary>A placement (or revive) is being animated; input is blocked.</summary>
        Resolving,
        Win,
        Fail,
    }
}
