using System.Collections;
using HexaSort.Utils;
using UnityEngine;

namespace HexaSort.View
{
    /// <summary>Small coroutine tweens for one-off transform moves (spawn, return, land).</summary>
    public static class TransformTweens
    {
        public static IEnumerator MoveScale(Transform target, Vector3 toPosition, Vector3 toScale, float duration)
        {
            Vector3 fromPosition = target.position;
            Vector3 fromScale = target.localScale;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float e = Easing.OutCubic(t / duration);
                target.position = Vector3.LerpUnclamped(fromPosition, toPosition, e);
                target.localScale = Vector3.LerpUnclamped(fromScale, toScale, e);
                yield return null;
            }
            target.position = toPosition;
            target.localScale = toScale;
        }

        /// <summary>Parabolic hop to <paramref name="toPosition"/>, peaking <paramref name="height"/> above the straight path.</summary>
        public static IEnumerator Jump(Transform target, Vector3 toPosition, float height, float duration)
        {
            Vector3 from = target.position;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                target.position = Vector3.Lerp(from, toPosition, Easing.InOutQuad(k)) + Vector3.up * (height * Easing.Arc(k));
                yield return null;
            }
            target.position = toPosition;
        }

        public static IEnumerator PopIn(Transform target, float duration, float delay)
        {
            target.localScale = Vector3.zero;
            for (float t = 0f; t < delay; t += Time.deltaTime) yield return null;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                target.localScale = Vector3.one * Easing.OutBack(t / duration);
                yield return null;
            }
            target.localScale = Vector3.one;
        }
    }
}
