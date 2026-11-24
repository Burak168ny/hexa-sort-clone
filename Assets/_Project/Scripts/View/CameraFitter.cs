using UnityEngine;

namespace HexaSort.View
{
    /// <summary>
    /// Frames a world-space box with a perspective camera at a fixed pitch: finds the closest
    /// distance at which the box fits inside the viewport minus UI margins, then centres it in
    /// that area. Re-fits automatically when the screen size or aspect changes.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFitter : MonoBehaviour
    {
        [SerializeField, Range(30f, 89f)] private float _pitch = 58f;

        [Header("Viewport margins (fraction of screen) kept free for UI")]
        [SerializeField, Range(0f, 0.4f)] private float _top = 0.2f;
        [SerializeField, Range(0f, 0.4f)] private float _bottom = 0.17f;
        [SerializeField, Range(0f, 0.2f)] private float _sides = 0.04f;

        private Camera _camera;
        private readonly Vector3[] _corners = new Vector3[8];
        private Bounds _target;
        private bool _hasTarget;
        private Vector2Int _screen;
        private Vector3 _restPosition;

        // Shake: decaying noise offset applied on top of the fitted position.
        private float _shakeAmplitude;
        private float _shakeTime;
        private float _shakeDuration;
        [SerializeField] private float _shakeFrequency = 28f;

        private Camera Camera => _camera != null ? _camera : _camera = GetComponent<Camera>();

        public void Fit(Bounds worldBounds)
        {
            _target = worldBounds;
            _hasTarget = true;
            Apply();
        }

        /// <summary>Adds a short shake; stronger requests override weaker ones still running.</summary>
        public void Shake(float amplitude, float duration)
        {
            float remaining = _shakeDuration > 0f ? _shakeAmplitude * (1f - _shakeTime / _shakeDuration) : 0f;
            if (amplitude < remaining) return;
            _shakeAmplitude = amplitude;
            _shakeDuration = duration;
            _shakeTime = 0f;
        }

        private void LateUpdate()
        {
            if (_hasTarget && (Camera.pixelWidth != _screen.x || Camera.pixelHeight != _screen.y)) Apply();
            if (_shakeDuration <= 0f) return;

            _shakeTime += Time.unscaledDeltaTime;
            float k = 1f - _shakeTime / _shakeDuration;
            if (k <= 0f)
            {
                _shakeDuration = 0f;
                transform.position = _restPosition;
                return;
            }

            float t = _shakeTime * _shakeFrequency;
            var offset = new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * (2f * _shakeAmplitude * k * k);
            transform.position = _restPosition + transform.rotation * offset;
        }

        private void Apply()
        {
            _screen = new Vector2Int(Camera.pixelWidth, Camera.pixelHeight); // camera pixels: also right for render textures
            var cam = Camera;
            var rotation = Quaternion.Euler(_pitch, 0f, 0f);
            transform.rotation = rotation;

            Vector3 min = _target.min, max = _target.max;
            for (int i = 0; i < 8; i++)
                _corners[i] = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);

            Vector3 forward = rotation * Vector3.forward;
            Vector3 up = rotation * Vector3.up;
            Vector3 right = rotation * Vector3.right;
            Vector3 pivot = _target.center;

            // Alternate: fit distance, then shift so the box is centred in the free area. Converges in a few rounds.
            float distance = 10f;
            for (int round = 0; round < 4; round++)
            {
                distance = FindDistance(cam, pivot, forward);
                transform.position = pivot - forward * distance;

                Measure(cam, out Vector2 vpMin, out Vector2 vpMax);
                Vector2 current = (vpMin + vpMax) * 0.5f;
                Vector2 wanted = new Vector2(0.5f, (_bottom + (1f - _top)) * 0.5f);

                float height = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                pivot += up * ((current.y - wanted.y) * height) + right * ((current.x - wanted.x) * height * cam.aspect);
            }
            transform.position = pivot - forward * FindDistance(cam, pivot, forward);
            _restPosition = transform.position;
        }

        private float FindDistance(Camera cam, Vector3 pivot, Vector3 forward)
        {
            float lo = 1f, hi = 200f;
            for (int i = 0; i < 24; i++)
            {
                float mid = (lo + hi) * 0.5f;
                transform.position = pivot - forward * mid;
                if (Fits(cam)) hi = mid;
                else lo = mid;
            }
            return hi;
        }

        private bool Fits(Camera cam)
        {
            Measure(cam, out Vector2 vpMin, out Vector2 vpMax);
            float width = vpMax.x - vpMin.x;
            float height = vpMax.y - vpMin.y;
            return width <= 1f - 2f * _sides && height <= 1f - _top - _bottom;
        }

        private void Measure(Camera cam, out Vector2 vpMin, out Vector2 vpMax)
        {
            vpMin = new Vector2(float.MaxValue, float.MaxValue);
            vpMax = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 vp = cam.WorldToViewportPoint(_corners[i]);
                if (vp.z <= cam.nearClipPlane)
                {
                    // Behind the camera: treat as not fitting.
                    vpMin = new Vector2(-100f, -100f);
                    vpMax = new Vector2(100f, 100f);
                    return;
                }
                vpMin = Vector2.Min(vpMin, vp);
                vpMax = Vector2.Max(vpMax, vp);
            }
        }
    }
}
