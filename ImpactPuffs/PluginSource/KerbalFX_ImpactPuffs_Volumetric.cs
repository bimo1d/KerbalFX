using UnityEngine;

namespace KerbalFX.ImpactPuffs
{
    internal sealed class VolumetricPlumeField
    {
        private struct LayerState
        {
            public float NextProfileTime;
            public Color LastStartColor;
            public bool HasLastStartColor;
        }

        private readonly GameObject root;
        private readonly ParticleSystem core;
        private readonly ParticleSystem shell;
        private readonly ParticleSystem loft;

        private LayerState coreState;
        private LayerState shellState;
        private LayerState loftState;

        private float coreRate;
        private float shellRate;
        private float loftRate;
        private float phaseA;
        private float phaseB;
        private float phaseC;

        private const float DensityMax = 3.2f;
        private const float DensityAlphaFloor = 0.25f;
        private const float DensityAlphaCeiling = 0.50f;
        private const float PressureBoostMax = 1.60f;
        private const float AlphaMax = 0.25f;
        private const int SingleMaxParticles = 16000;
        private const float RateMinThreshold = 0.25f;
        private const float ProfileRefreshInterval = 0.125f;
        private const float StartColorDeltaEpsilon = 0.005f;

        public VolumetricPlumeField(Transform parent, int layer)
        {
            root = new GameObject("KerbalFX_VolumetricPlume");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.layer = layer;

            core = CreateLayer("Core", root.transform, layer, 0.58f, 2.20f, 0.88f);
            shell = CreateLayer("Shell", root.transform, layer, 0.90f, 3.80f, 1.10f);
            loft = CreateLayer("Loft", root.transform, layer, 1.50f, 6.20f, 1.35f);

            float seed = parent != null ? Mathf.Abs(parent.GetInstanceID()) * 0.017f : 0f;
            phaseA = 0.6f + seed;
            phaseB = 1.2f + seed * 1.17f;
            phaseC = 1.8f + seed * 1.31f;

            StopImmediate();
        }

        public void Update(
            Vector3 worldPosition,
            Quaternion worldRotation,
            float targetRate,
            float pressure,
            float qualityNorm,
            float qualityScale,
            Color dustColor,
            float centeredness,
            int activeEngineCount,
            float lightAlphaMultiplier,
            float dt
        )
        {
            if (root == null)
            {
                return;
            }

            phaseA += dt * Mathf.Lerp(2.2f, 5.4f, pressure);
            phaseB += dt * Mathf.Lerp(1.7f, 4.1f, pressure);
            phaseC += dt * Mathf.Lerp(1.1f, 2.9f, pressure);

            float pulsePrimary = 0.5f + 0.5f * Mathf.Sin(phaseA);
            float pulseSecondary = 0.5f + 0.5f * Mathf.Sin(phaseB);
            float pulse = Mathf.Lerp(0.35f, 1.45f, pulsePrimary * 0.58f + pulseSecondary * 0.42f);
            float burstGate = Mathf.Lerp(0.60f, 1.36f, 0.5f + 0.5f * Mathf.Sin(phaseC));

            root.transform.rotation = worldRotation;
            float density = Mathf.Clamp(targetRate / (float)SingleMaxParticles, 0f, DensityMax);
            root.transform.position = worldPosition;

            float qualityBoost = Mathf.Lerp(1.10f, 1.85f, qualityNorm);
            float particleScale = Mathf.Clamp(qualityScale, 0.25f, 1.5f);
            float pressureBoost = Mathf.Lerp(0.55f, 1.60f, pressure);

            float smoothIn = Mathf.Clamp01(dt * 4.8f);
            float smoothOut = Mathf.Clamp01(dt * 7.2f);

            float coreTarget = Mathf.Clamp(targetRate * 0.58f * qualityBoost * pulse, 0f, 36000f * particleScale);
            float shellTarget = Mathf.Clamp(targetRate * 0.44f * qualityBoost * burstGate, 0f, 30000f * particleScale);
            float loftTarget = Mathf.Clamp(targetRate * 0.26f * qualityBoost * Mathf.Lerp(pulse, burstGate, 0.5f), 0f, 22000f * particleScale);

            coreRate = Mathf.Lerp(coreRate, coreTarget, coreTarget > coreRate ? smoothIn : smoothOut);
            shellRate = Mathf.Lerp(shellRate, shellTarget, shellTarget > shellRate ? smoothIn : smoothOut);
            loftRate = Mathf.Lerp(loftRate, loftTarget, loftTarget > loftRate ? smoothIn : smoothOut);

            float petalBias = (activeEngineCount > 1) ? Mathf.Lerp(0.30f, 0.72f, 1f - centeredness) : 0f;
            float radialScale = (activeEngineCount > 1) ? Mathf.Lerp(0.60f, 0.18f, 1f - centeredness) : 1f;
            float lightColorBlend = Mathf.Clamp01(lightAlphaMultiplier);
            float lightMul = Mathf.Clamp(lightAlphaMultiplier, 0f, 1.20f);

            UpdateLayer(core, ref coreState, coreRate, pressureBoost, density, qualityNorm, particleScale, Color.Lerp(dustColor, Color.white, 0.30f * lightColorBlend), 0.86f, 0.34f, 0.36f * lightMul, pulse, centeredness, petalBias, radialScale);
            UpdateLayer(shell, ref shellState, shellRate, pressureBoost, density, qualityNorm, particleScale, Color.Lerp(dustColor, Color.white, 0.22f * lightColorBlend), 1.20f, 0.44f, 0.30f * lightMul, burstGate, centeredness, petalBias, radialScale);
            UpdateLayer(loft, ref loftState, loftRate, pressureBoost, density, qualityNorm, particleScale, Color.Lerp(dustColor, Color.white, 0.38f * lightColorBlend), 1.34f, 0.60f, 0.20f * lightMul, pulse * 0.90f, centeredness, petalBias, radialScale);
        }

        public void StopSoft(float dt)
        {
            float fade = Mathf.Clamp01(dt * 6.5f);
            coreRate = Mathf.Lerp(coreRate, 0f, fade);
            shellRate = Mathf.Lerp(shellRate, 0f, fade);
            loftRate = Mathf.Lerp(loftRate, 0f, fade);

            SetRate(core, coreRate);
            SetRate(shell, shellRate);
            SetRate(loft, loftRate);

            if (coreRate <= 0.25f) core.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (shellRate <= 0.25f) shell.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (loftRate <= 0.25f) loft.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public void StopImmediate()
        {
            coreRate = 0f;
            shellRate = 0f;
            loftRate = 0f;
            SetRate(core, 0f);
            SetRate(shell, 0f);
            SetRate(loft, 0f);
            core.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            shell.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            loft.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            coreState.NextProfileTime = 0f;
            shellState.NextProfileTime = 0f;
            loftState.NextProfileTime = 0f;
        }

        public void Dispose()
        {
            StopImmediate();
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        private static void UpdateLayer(
            ParticleSystem ps,
            ref LayerState state,
            float rate,
            float pressureBoost,
            float density,
            float qualityNorm,
            float particleScale,
            Color color,
            float sizeScale,
            float liftScale,
            float alphaScale,
            float pulse,
            float centeredness,
            float petalBias,
            float radialScale
        )
        {
            if (ps == null) return;

            ApplyPerFrame(ps, ref state, rate, pressureBoost, density, alphaScale, pulse, color);

            float now = Time.time;
            if (now < state.NextProfileTime)
                return;
            state.NextProfileTime = now + ProfileRefreshInterval;

            ApplyProfile(ps, pressureBoost, density, qualityNorm, particleScale, sizeScale, liftScale, pulse, centeredness, petalBias, radialScale);
        }

        private static void ApplyPerFrame(
            ParticleSystem ps,
            ref LayerState state,
            float rate,
            float pressureBoost,
            float density,
            float alphaScale,
            float pulse,
            Color color)
        {
            SetRate(ps, rate);
            if (rate > RateMinThreshold && !ps.isPlaying)
                ps.Play(true);

            float alpha = ComputeLayerAlpha(pressureBoost, density, alphaScale, pulse);
            Color desired = new Color(color.r, color.g, color.b, alpha);
            if (state.HasLastStartColor && ColorsClose(state.LastStartColor, desired))
                return;

            ParticleSystem.MainModule main = ps.main;
            main.startColor = desired;
            state.LastStartColor = desired;
            state.HasLastStartColor = true;
        }

        private static void ApplyProfile(
            ParticleSystem ps,
            float pressureBoost,
            float density,
            float qualityNorm,
            float particleScale,
            float sizeScale,
            float liftScale,
            float pulse,
            float centeredness,
            float petalBias,
            float radialScale)
        {
            ParticleSystem.MainModule main = ps.main;
            float sizeQuality = Mathf.Lerp(0.90f, 1.55f, qualityNorm);
            main.startSize = new ParticleSystem.MinMaxCurve(0.34f * sizeScale * sizeQuality, 1.12f * sizeScale * sizeQuality);

            float lifeQuality = Mathf.Lerp(0.86f, 1.26f, qualityNorm);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.38f * lifeQuality, 1.40f * sizeScale * lifeQuality);
            main.maxParticles = Mathf.RoundToInt(Mathf.Clamp(SingleMaxParticles * particleScale, 4000f, 24000f));

            float dirSpeed = Mathf.Lerp(0.12f, 0.50f, qualityNorm);
            float petalSpeedBoost = 1f + petalBias * 2.2f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                Mathf.Lerp(0.06f, 0.02f, centeredness) * petalSpeedBoost,
                Mathf.Lerp(dirSpeed, 0.05f, centeredness) * petalSpeedBoost);

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.radius = Mathf.Lerp(0.72f, 3.40f, density / DensityMax) * sizeScale * Mathf.Lerp(0.88f, 1.14f, pulse);

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            float lateral = Mathf.Lerp(2.8f, 16.0f, density / DensityMax) * sizeScale;
            float lift = Mathf.Lerp(0.12f, 0.96f, pressureBoost / PressureBoostMax) * liftScale;
            float liftMult = Mathf.Lerp(1.0f, 1.85f, centeredness);

            float turbulence = lateral * 0.06f;
            float outward = lateral * petalBias;
            velocity.x = new ParticleSystem.MinMaxCurve(-turbulence, turbulence);
            velocity.y = new ParticleSystem.MinMaxCurve(-turbulence + outward * 0.55f, turbulence + outward * 1.25f);
            velocity.z = new ParticleSystem.MinMaxCurve(lift * liftMult * 0.20f, lift * liftMult * 0.80f);
            velocity.radial = new ParticleSystem.MinMaxCurve(lateral * 0.28f * radialScale, lateral * 0.80f * radialScale);

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.strength = Mathf.Lerp(1.20f, 5.00f, density / DensityMax) * Mathf.Lerp(1.05f, 1.55f, pulse);
            noise.frequency = Mathf.Lerp(0.52f, 1.62f, pressureBoost / PressureBoostMax);
            noise.scrollSpeed = Mathf.Lerp(0.40f, 1.92f, pressureBoost / PressureBoostMax);
        }

        private static bool ColorsClose(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < StartColorDeltaEpsilon
                && Mathf.Abs(a.g - b.g) < StartColorDeltaEpsilon
                && Mathf.Abs(a.b - b.b) < StartColorDeltaEpsilon
                && Mathf.Abs(a.a - b.a) < StartColorDeltaEpsilon;
        }

        private static ParticleSystem CreateLayer(string name, Transform parent, int layer, float minSize, float maxSize, float maxLifetime)
        {
            GameObject go = new GameObject("KerbalFX_Volumetric_" + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.layer = layer;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, maxLifetime);
            main.maxParticles = 28000;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.30f);
            main.gravityModifier = 0.03f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 82f;
            shape.radius = 1.2f;
            shape.radiusThickness = 0.12f;

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.42f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            color.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.50f);
            curve.AddKey(0.52f, 1.90f);
            curve.AddKey(1f, 0.18f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.damping = true;

            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 1.6f;
            renderer.maxParticleSize = 0.92f;

            Material material = ImpactPuffsAssets.GetSharedMaterial();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            return ps;
        }

        private static float ComputeLayerAlpha(float pressureBoost, float density, float alphaScale, float pulse)
        {
            float densityAlpha = Mathf.Lerp(DensityAlphaFloor, DensityAlphaCeiling, density / DensityMax);
            return Mathf.Clamp(densityAlpha * alphaScale * Mathf.Lerp(0.74f, 1.06f, pulse), 0f, AlphaMax);
        }

        private static void SetRate(ParticleSystem ps, float value)
        {
            if (ps == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = Mathf.Max(0f, value);
        }
    }
}
