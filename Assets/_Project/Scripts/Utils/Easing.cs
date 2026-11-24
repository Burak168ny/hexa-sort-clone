using UnityEngine;

namespace HexaSort.Utils
{
    /// <summary>Standard easing curves on t in [0, 1].</summary>
    public static class Easing
    {
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float InQuad(float t) => t * t;
        public static float InOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float InBack(float t, float s = 1.70158f) => t * t * ((s + 1f) * t - s);

        public static float OutBack(float t, float s = 1.70158f)
        {
            t -= 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }

        /// <summary>0 at both ends, 1 in the middle: the height profile of a jump.</summary>
        public static float Arc(float t) => 4f * t * (1f - t);
    }
}
