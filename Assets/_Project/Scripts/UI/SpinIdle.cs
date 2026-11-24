using UnityEngine;

namespace HexaSort.UI
{
    /// <summary>Endless idle motion for decorative UI: spin (light rays) and/or a gentle float (logos).</summary>
    public sealed class SpinIdle : MonoBehaviour
    {
        [SerializeField] private float _degreesPerSecond = 20f;
        [SerializeField] private float _floatAmplitude;
        [SerializeField] private float _floatSpeed = 2f;
        [SerializeField] private float _pulse;

        private Vector2 _origin;
        private bool _hasOrigin;

        private void OnEnable()
        {
            if (!_hasOrigin && transform is RectTransform rt)
            {
                _origin = rt.anchoredPosition;
                _hasOrigin = true;
            }
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            if (_degreesPerSecond != 0f) transform.localRotation = Quaternion.Euler(0f, 0f, time * _degreesPerSecond);
            if (_floatAmplitude != 0f && transform is RectTransform rt)
                rt.anchoredPosition = _origin + Vector2.up * (Mathf.Sin(time * _floatSpeed) * _floatAmplitude);
            if (_pulse != 0f) transform.localScale = Vector3.one * (1f + Mathf.Sin(time * _floatSpeed * 1.3f) * _pulse);
        }
    }
}
