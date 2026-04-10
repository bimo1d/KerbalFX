using UnityEngine;

namespace KerbalFX.ImpactPuffs
{
    internal sealed class VolumetricPlumeField
    {
        private readonly GameObject root;
        private readonly ParticleSystem core;
        private readonly ParticleSystem shell;
        private readonly ParticleSystem loft;

        private float coreRate;
        private float shellRate;
        private float loftRate;
        private float phaseA;
        private float phaseB;
        private float phaseC;
        private float phaseD;

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
            phaseD = 2.4f + seed * 0.93f;

            StopImmediate();
        }

        public void Update(
            Vector3 worldPosition,
            Quaternion worldRotation,
            float targetRate,
            float pressure,
            float qualityNorm,
            Color dustColor,
            float lightFactor,
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
            phaseD += dt * Mathf.Lerp(2.8f, 6.8f, pressure);

            float pulsePrimary = 0.5f + 0.5f * Mathf.Sin(phaseA);
            float pulseSecondary = 0.5f + 0.5f * Mathf.Sin(phaseB);
            float pulse = Mathf.Lerp(0.50f, 1.30f, pulsePrimary * 0.58f + pulseSecondary * 0.42f);
            float burstGate = Mathf.Lerp(0.72f, 1.24f, 0.5f + 0.5f * Mathf.Sin(phaseC));

            float driftX = Mathf.Sin(phaseA * 0.96f + phaseD * 0.24f);
            float driftZ = Mathf.Cos(phaseB * 1.08f - phaseA * 0.19f);

            root.transform.rotation = worldRotation;
            float density = Mathf.Clamp(targetRate / 16000f, 0f, 3.2f);
            root.transform.position = worldPosition;

            float qualityBoost = Mathf.Lerp(1.10f, 1.85f, qualityNorm);
            float pressureBoost = Mathf.Lerp(0.55f, 1.60f, pressure);

            float coreTarget = Mathf.Clamp(targetRate * 0.66f * qualityBoost * pulse, 0f, 56000f);
            float shellTarget = Mathf.Clamp(targetRate * 0.52f * qualityBoost * burstGate, 0f, 48000f);
            float loftTarget = Mathf.Clamp(targetRate * 0.29f * qualityBoost * Mathf.Lerp(pulse, burstGate, 0.5f), 0f, 34000f);

            float smoothIn = Mathf.Clamp01(dt * 4.8f);
            float smoothOut = Mathf.Clamp01(dt * 7.2f);

            coreRate = Mathf.Lerp(coreRate, coreTarget, coreTarget > coreRate ? smoothIn : smoothOut);
            shellRate = Mathf.Lerp(shellRate, shellTarget, shellTarget > shellRate ? smoothIn : smoothOut);
            loftRate = Mathf.Lerp(loftRate, loftTarget, loftTarget > loftRate ? smoothIn : smoothOut);

            UpdateLayer(core, coreRate, pressureBoost, density, qualityNorm, lightFactor, Color.Lerp(dustColor, Color.white, 0.30f), 0.86f, 0.18f, 0.36f, driftX, driftZ, pulse);
            UpdateLayer(shell, shellRate, pressureBoost, density, qualityNorm, lightFactor, Color.Lerp(dustColor, Color.white, 0.22f), 1.20f, 0.24f, 0.30f, -driftX * 0.62f, driftZ * 0.70f, burstGate);
            UpdateLayer(loft, loftRate, pressureBoost, density, qualityNorm, lightFactor, Color.Lerp(dustColor, Color.white, 0.38f), 1.58f, 0.36f, 0.20f, driftX * 0.40f, -driftZ * 0.30f, pulse * 0.90f);
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
            float rate,
            float pressureBoost,
            float density,
            float qualityNorm,
            float lightFactor,
            Color color,
            float sizeScale,
            float liftScale,
            float alphaScale,
            float driftX,
            float driftZ,
            float pulse
        )
        {
            if (ps == null)
            {
                return;
            }

            SetRate(ps, rate);
            if (rate > 0.25f)
            {
                if (!ps.isPlaying)
                {
                    ps.Play(true);
                }
            }

            float light01 = Mathf.Clamp01(lightFactor);
            float lightCurve = Mathf.Pow(light01, 1.08f);
            float lightFloor = Mathf.Lerp(0.045f, 0.085f, Mathf.Clamp01(pressureBoost / 1.60f));
            float visibility = Mathf.Lerp(lightFloor, 1f, lightCurve);
            float alpha = Mathf.Clamp(visibility * Mathf.Lerp(0.26f, 0.50f, density / 3.2f) * alphaScale * Mathf.Lerp(0.74f, 1.06f, pulse), 0f, 0.27f);

            ParticleSystem.MainModule main = ps.main;
            main.startColor = new Color(color.r, color.g, color.b, alpha);
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.34f * sizeScale * Mathf.Lerp(0.90f, 1.55f, qualityNorm),
                1.12f * sizeScale * Mathf.Lerp(0.90f, 1.55f, qualityNorm)
            );
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                0.34f * Mathf.Lerp(0.86f, 1.26f, qualityNorm),
                1.56f * sizeScale * Mathf.Lerp(0.86f, 1.26f, qualityNorm)
            );

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.angle = Mathf.Lerp(44f, 82f, pressureBoost / 1.60f);
            shape.radius = Mathf.Lerp(0.58f, 2.85f, density / 3.2f) * sizeScale * Mathf.Lerp(0.88f, 1.14f, pulse);

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            float lateral = Mathf.Lerp(2.2f, 14.8f, density / 3.2f) * sizeScale;
            float lift = Mathf.Lerp(0.18f, 2.15f, pressureBoost / 1.60f) * liftScale;
            float driftLateral = driftX * lateral * 0.62f;
            velocity.x = new ParticleSystem.MinMaxCurve((-lateral * 0.42f) + driftLateral, (lateral * 0.98f) + driftLateral);
            velocity.y = new ParticleSystem.MinMaxCurve(lift * 0.22f, lift * 1.42f);
            float driftForward = driftZ * lateral * 0.58f;
            float zMin = Mathf.Max(-lateral * 0.08f, driftForward - lateral * 0.12f);
            float zMax = (lateral * 1.40f) + driftForward;
            if (zMax <= zMin + 0.05f)
            {
                zMax = zMin + 0.05f;
            }
            velocity.z = new ParticleSystem.MinMaxCurve(zMin, zMax);
            velocity.radial = new ParticleSystem.MinMaxCurve(lateral * 0.05f, lateral * 0.36f);

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = Mathf.Lerp(0.78f, 3.70f, density / 3.2f) * Mathf.Lerp(0.90f, 1.34f, pulse);
            noise.frequency = Mathf.Lerp(0.52f, 1.62f, pressureBoost / 1.60f);
            noise.scrollSpeed = Mathf.Lerp(0.40f, 1.92f, pressureBoost / 1.60f);
            noise.damping = true;
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
            main.startRotation = new ParticleSystem.MinMaxCurve(-3.14159f, 3.14159f);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, maxLifetime);
            main.maxParticles = 28000;
            main.gravityModifier = 0.006f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 70f;
            shape.radius = 1.2f;

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

            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 1.6f;
            renderer.maxParticleSize = 0.92f;

            Material material = ImpactPuffsAssets.GetSharedMaterial();
            if (material != null)
            {
                renderer.material = material;
            }

            return ps;
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

