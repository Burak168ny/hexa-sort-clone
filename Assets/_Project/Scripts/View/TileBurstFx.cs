using UnityEngine;

namespace HexaSort.View
{
    /// <summary>
    /// One pooled particle system for every tile pop: bursts are emitted with per-call color, so all
    /// clears share a single system and a single draw call.
    /// </summary>
    public sealed class TileBurstFx : MonoBehaviour
    {
        [SerializeField] private Material _material;
        [Tooltip("Soft round glow (flashes and sparkles). Optional.")]
        [SerializeField] private Material _glowMaterial;
        [SerializeField] private int _maxParticles = 600;

        private ParticleSystem _system;
        private ParticleSystem.EmitParams _params;
        private ParticleSystem _glow;

        private void Awake()
        {
            _system = gameObject.AddComponent<ParticleSystem>();
            _system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.17f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 1.2f;
            main.maxParticles = _maxParticles;

            var emission = _system.emission;
            emission.enabled = false; // emitted manually

            var shape = _system.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.25f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // hemisphere opens upwards

            var size = _system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            var rotation = _system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _params = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            _system.Play();
            if (_glowMaterial != null) _glow = CreateGlowSystem();
        }

        /// <summary>Second system: soft glows that grow and fade (flashes, sparkles), no gravity.</summary>
        private ParticleSystem CreateGlowSystem()
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.35f;
            main.startSpeed = 0f;
            main.maxParticles = 300;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.35f, 1f, 1f));
            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _glowMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            system.Play();
            return system;
        }

        /// <summary>A soft flash that swells and fades at a point.</summary>
        public void Flash(Vector3 position, Color color, float size)
        {
            if (_glow == null) return;
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                startColor = color,
                startSize = size,
                startLifetime = 0.3f,
                velocity = Vector3.zero,
                applyShapeToPosition = false,
            };
            _glow.Emit(p, 1);
        }

        /// <summary>A few small bright sparks rising from a point.</summary>
        public void Sparkle(Vector3 position, int count)
        {
            if (_glow == null) return;
            for (int i = 0; i < count; i++)
            {
                var p = new ParticleSystem.EmitParams
                {
                    position = position,
                    startColor = new Color(1f, 0.97f, 0.85f),
                    startSize = Random.Range(0.12f, 0.22f),
                    startLifetime = Random.Range(0.35f, 0.6f),
                    velocity = (Random.insideUnitSphere + Vector3.up * 1.4f) * 1.3f,
                    applyShapeToPosition = false,
                };
                _glow.Emit(p, 1);
            }
        }

        public void Burst(Vector3 position, Color color, int count)
        {
            _params.position = position;
            _params.startColor = color;
            _system.Emit(_params, count);
        }
    }
}
