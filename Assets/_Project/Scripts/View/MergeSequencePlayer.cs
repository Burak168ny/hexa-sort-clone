using System;
using System.Collections;
using System.Collections.Generic;
using HexaSort.Model;
using HexaSort.Utils;
using UnityEngine;

namespace HexaSort.View
{
    /// <summary>
    /// Replays a <see cref="MergeResult"/> on the <see cref="BoardView"/>, one step at a time.
    /// Steps were computed up front by the model; this class only animates them, and raises
    /// events that feedback systems (audio, haptics, FX, UI) hook into.
    /// </summary>
    public sealed class MergeSequencePlayer : MonoBehaviour
    {
        [SerializeField] private BoardView _board;

        [Header("Feel")]
        [SerializeField] private float _landPunch = 0.07f;
        [SerializeField] private float _clearPopScale = 1.25f;
        [SerializeField] private float _clearRise = 0.9f;

        private readonly List<TileView> _moving = new List<TileView>(16);
        private readonly List<Vector3> _from = new List<Vector3>(16);
        private readonly List<Vector3> _to = new List<Vector3>(16);
        private readonly List<StackView> _removing = new List<StackView>(4);

        public event Action<TransferStep> TransferStarted;
        /// <summary>(tile, index within its transfer) - index lets audio rise in pitch.</summary>
        public event Action<TileView, int> TileLanded;
        public event Action<ClearStep> GroupCleared;
        /// <summary>A scored tile pops during a clear: (world position, color id).</summary>
        public event Action<Vector3, int> TilePopped;
        /// <summary>A tile is destroyed without scoring (hammer, revive): (world position, color id).</summary>
        public event Action<Vector3, int> TileShattered;

        private VisualConfig Config => _board.Config;

        public IEnumerator Play(MergeResult result)
        {
            for (int i = 0; i < result.Steps.Count; i++)
            {
                switch (result.Steps[i])
                {
                    case TransferStep transfer:
                        yield return PlayTransfer(transfer);
                        break;
                    case ClearStep clear:
                        yield return PlayClear(clear);
                        break;
                }
                if (Config.StepPause > 0f) yield return Wait(Config.StepPause);
            }
        }

        private IEnumerator PlayTransfer(TransferStep step)
        {
            var source = _board.GetStack(step.From);
            var target = _board.GetStack(step.To);
            TransferStarted?.Invoke(step);

            _moving.Clear();
            _from.Clear();
            _to.Clear();
            for (int i = 0; i < step.Count; i++)
            {
                var tile = source.PopTop();
                int destinationIndex = target.Count;
                target.PushTop(tile);
                _moving.Add(tile);
                _from.Add(tile.CachedTransform.position);
                _to.Add(target.WorldTilePosition(destinationIndex));
            }
            if (source.Count == 0) _board.StackPool.Despawn(_board.RemoveStack(step.From));

            // Tiles flip over the shared edge: rotate about the horizontal axis perpendicular to travel.
            Vector3 travel = _board.CellToWorld(step.To) - _board.CellToWorld(step.From);
            Vector3 axis = Vector3.Cross(Vector3.up, travel).normalized;

            float interval = Config.TransferTileInterval;
            float duration = Config.TransferTileDuration;
            float arc = Config.TransferArcHeight;
            float total = interval * (_moving.Count - 1) + duration;
            int landed = 0;

            for (float t = 0f; landed < _moving.Count; t += Time.deltaTime)
            {
                for (int i = landed; i < _moving.Count; i++)
                {
                    float local = (t - i * interval) / duration;
                    if (local <= 0f) break;

                    var tr = _moving[i].CachedTransform;
                    if (local >= 1f)
                    {
                        tr.SetPositionAndRotation(_to[i], target.CachedTransform.rotation);
                        if (i == landed)
                        {
                            landed++;
                            target.Punch(_landPunch);
                            TileLanded?.Invoke(_moving[i], i);
                        }
                        continue;
                    }

                    float e = Easing.InOutQuad(local);
                    Vector3 p = Vector3.LerpUnclamped(_from[i], _to[i], e) + Vector3.up * (arc * Easing.Arc(local));
                    tr.SetPositionAndRotation(p, Quaternion.AngleAxis(180f * e, axis) * target.CachedTransform.rotation);
                }
                if (t > total + 1f) break; // Safety net for a stalled frame clock.
                yield return null;
            }

            for (int i = 0; i < _moving.Count; i++)
            {
                var tr = _moving[i].CachedTransform;
                tr.localPosition = target.LocalTilePosition(target.Count - _moving.Count + i);
                tr.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// Clear: from the top down, each tile pops (grows a little), then shoots up while shrinking away.
        /// <see cref="TilePopped"/> fires as each tile pops so FX and the goal counter stay in sync.
        /// </summary>
        private IEnumerator PlayClear(ClearStep step)
        {
            var stack = _board.GetStack(step.Cell);
            GroupCleared?.Invoke(step);
            stack.Punch(_landPunch * 1.5f);

            _moving.Clear();
            _from.Clear();
            for (int i = 0; i < step.Count; i++)
            {
                var tile = stack.PopTop();
                _moving.Add(tile);
                _from.Add(tile.CachedTransform.localPosition);
            }

            float interval = Config.ClearTileInterval;
            float duration = Config.ClearTileDuration;
            float total = interval * (_moving.Count - 1) + duration;
            int popped = 0;
            var pool = _board.StackPool.Tiles;

            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                for (int i = 0; i < _moving.Count; i++)
                {
                    float local = Mathf.Clamp01((t - i * interval) / duration);
                    if (local <= 0f) break;
                    if (i == popped)
                    {
                        popped++;
                        TilePopped?.Invoke(_moving[i].CachedTransform.position, _moving[i].ColorId);
                    }
                    AnimateClearTile(_moving[i].CachedTransform, _from[i], local);
                }
                yield return null;
            }

            for (; popped < _moving.Count; popped++)
                TilePopped?.Invoke(_moving[popped].CachedTransform.position, _moving[popped].ColorId);
            for (int i = 0; i < _moving.Count; i++) pool.Release(_moving[i]);
            _moving.Clear();

            if (stack.Count == 0) _board.StackPool.Despawn(_board.RemoveStack(step.Cell));
        }

        private void AnimateClearTile(Transform tile, Vector3 start, float local)
        {
            const float popPhase = 0.3f;
            float scale;
            float rise;
            if (local < popPhase)
            {
                float k = local / popPhase;
                scale = Mathf.Lerp(1f, _clearPopScale, Easing.OutQuad(k));
                rise = 0.08f * k;
            }
            else
            {
                float k = (local - popPhase) / (1f - popPhase);
                scale = _clearPopScale * (1f - Easing.InQuad(k));
                rise = 0.08f + _clearRise * Easing.OutCubic(k);
            }
            tile.localScale = Vector3.one * scale;
            tile.localPosition = start + Vector3.up * rise;
        }

        /// <summary>Hammer: the stack is squashed flat and shatters (no score).</summary>
        public IEnumerator PlaySmash(HexCoord cell)
        {
            var view = _board.RemoveStack(cell);
            if (view == null) yield break;

            var tr = view.CachedTransform;
            const float duration = 0.16f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = Easing.InQuad(t / duration);
                tr.localScale = new Vector3(1f + 0.35f * k, Mathf.Max(0.05f, 1f - k), 1f + 0.35f * k);
                yield return null;
            }

            for (int i = 0; i < view.Count; i++) TileShattered?.Invoke(view.WorldTilePosition(i), view[i].ColorId);
            _board.StackPool.Despawn(view);
        }

        /// <summary>Shrinks whole stacks away (revive) and returns them to the pool.</summary>
        public IEnumerator PlayRemoveStacks(IReadOnlyList<HexCoord> cells)
        {
            _removing.Clear();
            for (int i = 0; i < cells.Count; i++)
            {
                var view = _board.RemoveStack(cells[i]);
                if (view != null) _removing.Add(view);
            }

            float duration = Config.ClearTileDuration * 1.5f;
            float stagger = Config.ClearTileInterval * 2f;
            float total = duration + stagger * Mathf.Max(0, _removing.Count - 1);
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                for (int i = 0; i < _removing.Count; i++)
                {
                    float local = Mathf.Clamp01((t - i * stagger) / duration);
                    _removing[i].CachedTransform.localScale = Vector3.one * (1f - Easing.InBack(local));
                }
                yield return null;
            }

            for (int i = 0; i < _removing.Count; i++)
            {
                var view = _removing[i];
                for (int k = 0; k < view.Count; k++) TileShattered?.Invoke(view.WorldTilePosition(k), view[k].ColorId);
                _board.StackPool.Despawn(view);
            }
            _removing.Clear();
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
        }
    }
}
