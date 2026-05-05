using UnityEngine;

namespace KerbalFX.BlastFX
{
    internal partial class BlastFxController
    {
        private static Gradient cachedSparkGradient;
        private static Gradient cachedChunkGradient;
        private static Gradient cachedSmokeGradient;
        private static Gradient cachedSoftPuffGradient;
        private static Gradient cachedUndockGasGradient;
        private static Gradient cachedVacuumDebrisGradient;

        private static ParticleSystem CreateSparks(Transform parent, int layer,
            float ringRadius, int maxCount, float speedScale, float sizeScale)
        {
            GameObject go = new GameObject("BlastFX_Sparks");
            go.transform.SetParent(parent, false);
            go.layer = layer;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Clamp(maxCount * 4, 64, 8192);
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                BlastFxRuntimeConfig.SparkLife * 0.55f,
                BlastFxRuntimeConfig.SparkLife * 1.20f
                    * Mathf.Lerp(0.95f, 1.12f, Mathf.Clamp01(sizeScale - 0.9f)));
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                BlastFxRuntimeConfig.SparkSpeed * 0.70f * speedScale,
                BlastFxRuntimeConfig.SparkSpeed * 1.55f * speedScale);
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.025f * sizeScale, 0.055f * sizeScale);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;

            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = ringRadius;
            sh.radiusThickness = 0.06f;
            sh.arc = 360f;

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.radial = new ParticleSystem.MinMaxCurve(
                BlastFxRuntimeConfig.SparkSpeed * 0.40f * speedScale,
                BlastFxRuntimeConfig.SparkSpeed * 1.00f * speedScale);
            vel.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.28f, 0.28f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = GetSparkGradient();

            ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.22f;
            r.lengthScale = 1.18f;
            r.sharedMaterial = BlastFxAssets.GetSparkMaterial();
            return ps;
        }

        private static ParticleSystem CreateChunks(Transform parent, int layer,
            float ringRadius, int maxCount, float speedScale, float sizeScale)
        {
            GameObject go = new GameObject("BlastFX_Chunks");
            go.transform.SetParent(parent, false);
            go.layer = layer;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Clamp(maxCount * 4, 24, 2048);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.95f, 2.20f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                BlastFxRuntimeConfig.SparkSpeed * 0.58f * speedScale,
                BlastFxRuntimeConfig.SparkSpeed * 1.28f * speedScale);
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.05f * sizeScale, 0.18f * sizeScale);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.04f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;

            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = ringRadius;
            sh.radiusThickness = 0.04f;
            sh.arc = 360f;

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.radial = new ParticleSystem.MinMaxCurve(
                BlastFxRuntimeConfig.SparkSpeed * 0.95f * speedScale,
                BlastFxRuntimeConfig.SparkSpeed * 1.95f * speedScale);
            vel.x = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.14f, 0.14f);

            ParticleSystem.LimitVelocityOverLifetimeModule limit =
                ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.separateAxes = false;
            limit.space = ParticleSystemSimulationSpace.Local;
            float limit01 = Mathf.Clamp01(sizeScale / 2.2f);
            float initialLimit = BlastFxRuntimeConfig.SparkSpeed
                * Mathf.Lerp(1.55f, 2.85f, limit01);
            float finalLimit = BlastFxRuntimeConfig.SparkSpeed
                * Mathf.Lerp(0.24f, 0.48f, limit01);
            limit.limit = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, initialLimit),
                    new Keyframe(0.38f, initialLimit * 0.62f),
                    new Keyframe(1f, finalLimit)));
            limit.dampen = 0.62f;

            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-16.0f, 16.0f);

            ParticleSystem.SizeOverLifetimeModule sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 1.00f),
                    new Keyframe(0.55f, 1.00f),
                    new Keyframe(0.85f, 0.55f),
                    new Keyframe(1f, 0.00f)));

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = GetChunkGradient();

            ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = BlastFxAssets.GetChunkMesh();
            r.sharedMaterial = BlastFxAssets.GetChunkMaterial();
            return ps;
        }

        private static ParticleSystem CreateSmoke(Transform parent, int layer,
            float ringRadius, int maxCount, float speedScale, float sizeScale)
        {
            GameObject go = new GameObject("BlastFX_Smoke");
            go.transform.SetParent(parent, false);
            go.layer = layer;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Clamp(maxCount * 6, 64, 8192);
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                BlastFxRuntimeConfig.SmokeLife * 0.75f,
                BlastFxRuntimeConfig.SmokeLife * 1.30f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                BlastFxRuntimeConfig.SmokeSpeed * 0.55f * speedScale,
                BlastFxRuntimeConfig.SmokeSpeed * 1.25f * speedScale);
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.15f * sizeScale, 0.42f * sizeScale);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;

            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = ringRadius * 1.02f;
            sh.radiusThickness = 0.34f;
            sh.arc = 360f;

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.radial = new ParticleSystem.MinMaxCurve(
                BlastFxRuntimeConfig.SmokeSpeed * 0.35f * speedScale,
                BlastFxRuntimeConfig.SmokeSpeed * 1.05f * speedScale);
            vel.x = new ParticleSystem.MinMaxCurve(-0.32f, 0.32f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.16f, 0.16f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.22f, 0.22f);

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = GetSmokeGradient();

            ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = BlastFxAssets.GetSmokeMaterial();
            return ps;
        }

        private static ParticleSystem CreateSoftPuffSmoke(Transform parent, int layer,
            float ringRadius, int maxCount, float speedScale, float sizeScale)
        {
            GameObject go = new GameObject("BlastFX_SoftPuffSmoke");
            go.transform.SetParent(parent, false);
            go.layer = layer;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Clamp(maxCount * 12, 64, 1408);
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                BlastFxRuntimeConfig.SoftPuffLife * 0.82f,
                BlastFxRuntimeConfig.SoftPuffLife * 1.08f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                BlastFxRuntimeConfig.SoftPuffSpeed * 1.55f * speedScale,
                BlastFxRuntimeConfig.SoftPuffSpeed * 2.35f * speedScale);
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.10f * sizeScale, 0.24f * sizeScale);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;

            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = ringRadius * 0.96f;
            sh.radiusThickness = 0.12f;
            sh.arc = 360f;

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.radial = new ParticleSystem.MinMaxCurve(
                BlastFxRuntimeConfig.SoftPuffSpeed * 1.10f * speedScale,
                BlastFxRuntimeConfig.SoftPuffSpeed * 2.05f * speedScale);
            vel.x = new ParticleSystem.MinMaxCurve(-0.16f, 0.16f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.14f, 0.14f);

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = GetSoftPuffGradient();

            ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = BlastFxAssets.GetSmokeMaterial();
            return ps;
        }

        private static ParticleSystem CreateUndockGasSmoke(Transform parent, int layer,
            float ringRadius, int maxCount, float speedScale, float sizeScale)
        {
            GameObject go = new GameObject("BlastFX_UndockGas");
            go.transform.SetParent(parent, false);
            go.layer = layer;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Clamp(maxCount * 6, 64, 2304);
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                BlastFxRuntimeConfig.SoftPuffLife * 0.48f,
                BlastFxRuntimeConfig.SoftPuffLife * 0.74f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                BlastFxRuntimeConfig.SoftPuffSpeed * 0.12f * speedScale,
                BlastFxRuntimeConfig.SoftPuffSpeed * 0.28f * speedScale);
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.10f * sizeScale, 0.24f * sizeScale);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;

            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = ringRadius * 1.55f;
            sh.radiusThickness = 0.15f;
            sh.arc = 360f;

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.radial = new ParticleSystem.MinMaxCurve(
                BlastFxRuntimeConfig.SoftPuffSpeed * 0.10f * speedScale,
                BlastFxRuntimeConfig.SoftPuffSpeed * 0.26f * speedScale);
            vel.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = GetUndockGasGradient();

            ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = BlastFxAssets.GetSmokeSoftMaterial();
            return ps;
        }

        private static ParticleSystem CreateVacuumDebris(Transform parent, int layer,
            float ringRadius, int maxCount, float speedScale, float sizeScale)
        {
            GameObject go = new GameObject("BlastFX_VacuumDebris");
            go.transform.SetParent(parent, false);
            go.layer = layer;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Clamp(maxCount * 3, 32, 384);
            main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 18f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                0.30f * speedScale, 1.10f * speedScale);
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.027f * sizeScale, 0.086f * sizeScale);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;

            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Donut;
            sh.radius = ringRadius * 3.40f;
            sh.donutRadius = ringRadius * 0.55f;
            sh.arc = 360f;
            sh.radiusThickness = 0.85f;

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.radial = new ParticleSystem.MinMaxCurve(
                0.55f * speedScale, 1.85f * speedScale);

            ParticleSystem.LimitVelocityOverLifetimeModule limit =
                ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.separateAxes = false;
            limit.space = ParticleSystemSimulationSpace.Local;
            limit.limit = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 1.80f * speedScale),
                    new Keyframe(0.10f, 0.55f * speedScale),
                    new Keyframe(1f, 0.04f * speedScale)));
            limit.dampen = 0.92f;

            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);

            ParticleSystem.SizeOverLifetimeModule debrisSz = ps.sizeOverLifetime;
            debrisSz.enabled = true;
            debrisSz.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 1.00f),
                    new Keyframe(0.50f, 1.00f),
                    new Keyframe(0.85f, 0.55f),
                    new Keyframe(1f, 0.00f)));

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = GetVacuumDebrisGradient();

            ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = BlastFxAssets.GetChunkMesh();
            r.sharedMaterial = BlastFxAssets.GetChunkMaterial();
            return ps;
        }

        private static Gradient GetSparkGradient()
        {
            if (cachedSparkGradient != null) return cachedSparkGradient;
            cachedSparkGradient = new Gradient();
            cachedSparkGradient.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1.00f, 0.96f, 0.84f), 0.00f),
                    new GradientColorKey(new Color(1.00f, 0.68f, 0.20f), 0.12f),
                    new GradientColorKey(new Color(0.96f, 0.26f, 0.10f), 0.36f),
                    new GradientColorKey(new Color(0.25f, 0.25f, 0.25f), 1.00f)
                },
                new[] {
                    new GradientAlphaKey(1.00f, 0.00f),
                    new GradientAlphaKey(0.92f, 0.12f),
                    new GradientAlphaKey(0.35f, 0.55f),
                    new GradientAlphaKey(0.00f, 1.00f)
                });
            return cachedSparkGradient;
        }

        private static Gradient GetChunkGradient()
        {
            if (cachedChunkGradient != null) return cachedChunkGradient;
            cachedChunkGradient = new Gradient();
            cachedChunkGradient.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1.00f, 0.84f, 0.46f), 0.00f),
                    new GradientColorKey(new Color(0.98f, 0.46f, 0.18f), 0.20f),
                    new GradientColorKey(new Color(0.36f, 0.36f, 0.36f), 0.66f),
                    new GradientColorKey(new Color(0.16f, 0.16f, 0.16f), 1.00f)
                },
                new[] {
                    new GradientAlphaKey(1.00f, 0.00f),
                    new GradientAlphaKey(0.98f, 0.25f),
                    new GradientAlphaKey(0.82f, 0.50f),
                    new GradientAlphaKey(0.55f, 0.72f),
                    new GradientAlphaKey(0.28f, 0.88f),
                    new GradientAlphaKey(0.08f, 0.96f),
                    new GradientAlphaKey(0.00f, 1.00f)
                });
            return cachedChunkGradient;
        }

        private static Gradient GetSmokeGradient()
        {
            if (cachedSmokeGradient != null) return cachedSmokeGradient;
            cachedSmokeGradient = new Gradient();
            cachedSmokeGradient.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.50f, 0.50f, 0.50f), 0.00f),
                    new GradientColorKey(new Color(0.36f, 0.36f, 0.36f), 0.45f),
                    new GradientColorKey(new Color(0.28f, 0.28f, 0.28f), 1.00f)
                },
                new[] {
                    new GradientAlphaKey(0.42f, 0.00f),
                    new GradientAlphaKey(0.26f, 0.35f),
                    new GradientAlphaKey(0.08f, 0.75f),
                    new GradientAlphaKey(0.00f, 1.00f)
                });
            return cachedSmokeGradient;
        }

        private static Gradient GetSoftPuffGradient()
        {
            if (cachedSoftPuffGradient != null) return cachedSoftPuffGradient;
            cachedSoftPuffGradient = new Gradient();
            cachedSoftPuffGradient.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.98f, 0.98f, 0.98f), 0.00f),
                    new GradientColorKey(new Color(0.92f, 0.92f, 0.92f), 0.34f),
                    new GradientColorKey(new Color(0.74f, 0.74f, 0.74f), 1.00f)
                },
                new[] {
                    new GradientAlphaKey(0.76f, 0.00f),
                    new GradientAlphaKey(0.66f, 0.20f),
                    new GradientAlphaKey(0.30f, 0.68f),
                    new GradientAlphaKey(0.08f, 0.90f),
                    new GradientAlphaKey(0.00f, 1.00f)
                });
            return cachedSoftPuffGradient;
        }

        private static Gradient GetUndockGasGradient()
        {
            if (cachedUndockGasGradient != null) return cachedUndockGasGradient;
            cachedUndockGasGradient = new Gradient();
            cachedUndockGasGradient.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.98f, 0.99f, 1.00f), 0.00f),
                    new GradientColorKey(new Color(0.92f, 0.94f, 0.98f), 0.40f),
                    new GradientColorKey(new Color(0.78f, 0.82f, 0.90f), 1.00f)
                },
                new[] {
                    new GradientAlphaKey(0.22f, 0.00f),
                    new GradientAlphaKey(0.24f, 0.12f),
                    new GradientAlphaKey(0.18f, 0.32f),
                    new GradientAlphaKey(0.10f, 0.52f),
                    new GradientAlphaKey(0.04f, 0.72f),
                    new GradientAlphaKey(0.00f, 1.00f)
                });
            return cachedUndockGasGradient;
        }

        private static Gradient GetVacuumDebrisGradient()
        {
            if (cachedVacuumDebrisGradient != null) return cachedVacuumDebrisGradient;
            cachedVacuumDebrisGradient = new Gradient();
            cachedVacuumDebrisGradient.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.62f, 0.58f, 0.50f), 0.00f),
                    new GradientColorKey(new Color(0.46f, 0.44f, 0.40f), 0.40f),
                    new GradientColorKey(new Color(0.32f, 0.30f, 0.28f), 1.00f)
                },
                new[] {
                    new GradientAlphaKey(0.95f, 0.00f),
                    new GradientAlphaKey(0.92f, 0.50f),
                    new GradientAlphaKey(0.78f, 0.72f),
                    new GradientAlphaKey(0.52f, 0.85f),
                    new GradientAlphaKey(0.24f, 0.94f),
                    new GradientAlphaKey(0.06f, 0.98f),
                    new GradientAlphaKey(0.00f, 1.00f)
                });
            return cachedVacuumDebrisGradient;
        }
    }
}
