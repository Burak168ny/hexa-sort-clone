using System.Collections.Generic;

namespace HexaSort.Model
{
    /// <summary>
    /// One atomic change produced by <see cref="MergeResolver"/>. The view replays steps in order,
    /// so the visual state always mirrors the model without the model knowing about animation.
    /// </summary>
    public abstract class MergeStep { }

    /// <summary>The top <see cref="Count"/> tiles (all <see cref="Color"/>) of From move onto To, one by one.</summary>
    public sealed class TransferStep : MergeStep
    {
        public HexCoord From { get; }
        public HexCoord To { get; }
        public int Color { get; }
        public int Count { get; }

        public TransferStep(HexCoord from, HexCoord to, int color, int count)
        {
            From = from;
            To = to;
            Color = color;
            Count = count;
        }

        public override string ToString() => $"Transfer {From}->{To} {Count}x{Color}";
    }

    /// <summary>The top <see cref="Count"/> tiles of Cell reached the clear threshold and pop.</summary>
    public sealed class ClearStep : MergeStep
    {
        public HexCoord Cell { get; }
        public int Color { get; }
        public int Count { get; }

        /// <summary>1-based index of this clear within the resolve; 2+ means a combo.</summary>
        public int Combo { get; }

        public ClearStep(HexCoord cell, int color, int count, int combo)
        {
            Cell = cell;
            Color = color;
            Count = count;
            Combo = combo;
        }

        public override string ToString() => $"Clear {Cell} {Count}x{Color} (combo {Combo})";
    }

    public sealed class MergeResult
    {
        public readonly List<MergeStep> Steps = new List<MergeStep>();

        public int TilesCleared { get; internal set; }
        public int ClearCount { get; internal set; }
        public int TransferCount { get; internal set; }

        /// <summary>True if the safety limit stopped resolution early (indicates a rule bug).</summary>
        public bool HitIterationLimit { get; internal set; }

        public bool IsEmpty => Steps.Count == 0;
    }
}
