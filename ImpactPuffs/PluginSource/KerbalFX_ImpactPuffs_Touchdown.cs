using System.Globalization;
using System.Collections.Generic;
using KSP.Localization;
using UnityEngine;

namespace KerbalFX.ImpactPuffs
{
    internal sealed class TouchdownBurstEmitter
    {
        private struct TouchdownContext
        {
            public Vector3 Point;
            public Vector3 Normal;
            public Collider Collider;
            public bool Splash;
            public float QualityNorm;
            public float QualityScale;
            public float BodyVisibility;
            public float BurstStrength;
            public float TouchdownEnergy01;
            public float LightAwareMultiplier;
            public Color Color;
            public int FxLayer;
        }

        private struct TouchdownLayerOptions
        {
            public Color Color;
            public float Alpha;
            public float MinSize;
            public float MaxSize;
            public float MinLife;
            public float MaxLife;
            public float MinSpeed;
            public float MaxSpeed;
            public float Gravity;
            public int BurstCount;
            public float MaxParticlesScale;
            public float ShapeAngle;
            public float ShapeRadius;
            public float Lateral;
            public float LiftMin;
            public float LiftMax;
            public float RadialMin;
            public float RadialMax;
            public float NoiseStrength;
            public float NoiseFrequency;
            public float NoiseScroll;
            public Gradient Gradient;
            public AnimationCurve SizeCurve;
            public float MaxParticleSize;
            public float SortingFudge;
            public float ShapeRadiusThickness;
            public ParticleSystemRenderMode RenderMode;
            public bool UseCircleShape;
        }

        private const float TouchdownCooldown = 0.38f;
        private const float MaxImpactSpeed = 30f;
        private const float MinBurstRayDistance = 14f;
        private const float MaxBurstRayDistance = 42f;
        private const float MinBurstRayOriginOffset = 3.0f;
        private const float MaxBurstRayOriginOffset = 24.0f;

        private const float TeleportDistanceSq = 2500f;

        private const float SurfaceColorBlendStrength = 0.42f;
        private const float SurfaceColorRefreshSeconds = 0.10f;
        private const float SurfaceColorSnapshotSmoothing = 12f;
        private const bool SurfaceColorAllowRendererFallback = false;
        private const float LightAwareRefreshSeconds = 0.20f;
        private const float LightAwareSmoothingSpeed = 12f;
        private const bool LightAwareUseShadowProbe = true;
        private const bool LightAwareSampleLocalLights = false;
        private const float LightAwareTouchdownStrength = 1.00f;

        private readonly Vessel vessel;
        private readonly KerbalFxSurfaceColorSampler surfaceColor = new KerbalFxSurfaceColorSampler(
            KerbalFxDustVisualDefaults.Color,
            SurfaceColorBlendStrength,
            SurfaceColorRefreshSeconds,
            SurfaceColorSnapshotSmoothing,
            SurfaceColorAllowRendererFallback,
            ImpactPuffsRuntimeConfig.TouchdownTintProfile);
        private readonly KerbalFxLightAwareSampler lightAware = new KerbalFxLightAwareSampler(
            LightAwareRefreshSeconds,
            LightAwareSmoothingSpeed,
            LightAwareUseShadowProbe,
            LightAwareSampleLocalLights);
        private bool wasGrounded;
        private float cooldown;
        private Vector3 lastCoM;
        private bool hasLastCoM;
        private static readonly RaycastHit[] BurstHits = new RaycastHit[32];
        private static readonly Gradient RingShockGradient = CreateRingShockGradient();
        private static readonly AnimationCurve RingShockSizeCurve = CreateRingShockSizeCurve();

        public TouchdownBurstEmitter(Vessel vessel)
        {
            this.vessel = vessel;
            wasGrounded = vessel != null && (vessel.Landed || vessel.situation == Vessel.Situations.PRELAUNCH);
        }

        public void Tick(float dt)
        {
            if (vessel == null || !vessel.loaded || vessel.packed)
            {
                return;
            }

            cooldown = Mathf.Max(0f, cooldown - dt);
            TouchdownBurstPool.ReturnExpired();

            Vector3 currentCoM = vessel.CoM;
            if (hasLastCoM && (currentCoM - lastCoM).sqrMagnitude > TeleportDistanceSq)
            {
                bool grounded2 = vessel.Landed
                    || vessel.Splashed
                    || vessel.situation == Vessel.Situations.PRELAUNCH;
                wasGrounded = grounded2;
                lastCoM = currentCoM;
                return;
            }
            lastCoM = currentCoM;
            hasLastCoM = true;

            bool grounded = vessel.Landed
                || vessel.Splashed
                || vessel.situation == Vessel.Situations.PRELAUNCH;
            float descentSpeed = Mathf.Max(0f, (float)(-vessel.verticalSpeed));
            float touchdownSpeedThreshold = Mathf.Max(ImpactPuffsRuntimeConfig.TouchdownMinSpeed * 1.20f, 2.8f);
            if (!wasGrounded && grounded && cooldown <= 0f && descentSpeed >= touchdownSpeedThreshold)
            {
                SpawnRingShock(descentSpeed);
                cooldown = TouchdownCooldown;
            }

            wasGrounded = grounded;
        }

        public void Dispose()
        {
        }

        private static float GetTouchdownEnergy01(float impactSpeed)
        {
            float cappedSpeed = Mathf.Clamp(impactSpeed, 0f, MaxImpactSpeed);
            float speed01 = cappedSpeed / MaxImpactSpeed;
            return speed01 * speed01;
        }

        private bool TryBuildTouchdownContext(
            float impactSpeed,
            bool ringShockMode,
            float baseWhiten,
            float splashWhiten,
            out TouchdownContext context)
        {
            context = default(TouchdownContext);
            if (vessel == null || vessel.mainBody == null)
            {
                return false;
            }

            if (!TryGetTouchdownGroundSurface(out context.Point, out context.Normal, out context.Collider))
            {
                return false;
            }

            context.Splash = vessel.Splashed || vessel.situation == Vessel.Situations.SPLASHED;
            float quality = EngineGroundPuffEmitter.ModeQualityScale;
            context.QualityNorm = Mathf.InverseLerp(0.25f, 2.0f, quality);
            context.QualityScale = Mathf.Clamp(ImpactPuffsConfig.GetQualityScaleMultiplier(), 0.25f, 1.5f);
            context.BodyVisibility = ImpactPuffsRuntimeConfig.GetBodyVisibilityMultiplier(vessel.mainBody.bodyName);
            context.BurstStrength = Mathf.Clamp01(impactSpeed / (ringShockMode ? 10.5f : 11.5f));
            context.TouchdownEnergy01 = GetTouchdownEnergy01(impactSpeed);

            context.LightAwareMultiplier = ResolveTouchdownLightMultiplier(context.Point, context.Normal);
            context.Color = ResolveTouchdownDustColor(context.Splash, baseWhiten, splashWhiten, context.Point, context.Collider);
            context.FxLayer = vessel.rootPart != null ? vessel.rootPart.gameObject.layer : 0;
            return true;
        }

        private float ResolveTouchdownLightMultiplier(Vector3 hitPoint, Vector3 hitNormal)
        {
            if (!ImpactPuffsConfig.UseLightAware)
            {
                if (!lightAware.IsReset)
                    lightAware.Reset();
                return 1f;
            }
            lightAware.ApplySnapshot(vessel, hitPoint, hitNormal);
            string bodyName = vessel != null && vessel.mainBody != null ? vessel.mainBody.bodyName : string.Empty;
            float alphaMultiplier = lightAware.GetAlphaMultiplier(
                ImpactPuffsRuntimeConfig.LightAwareProfile.Get(bodyName),
                LightAwareTouchdownStrength);
            return KerbalFxLightingCore.ApplySurfaceBoost(alphaMultiplier);
        }

        private bool TryGetTouchdownGroundSurface(out Vector3 point, out Vector3 normal, out Collider collider)
        {
            point = Vector3.zero;
            normal = Vector3.up;
            collider = null;

            if (!TryGetGroundPoint(out point, out normal, out collider))
            {
                return false;
            }

            if (EngineGroundPuffEmitter.IsInKerbinLaunchsiteZone(vessel))
            {
                return false;
            }

            if (EngineGroundPuffEmitter.IsLaunchsiteExcludedSurface(collider))
            {
                return false;
            }

            return true;
        }

        private Color ResolveTouchdownDustColor(bool splash, float baseWhiten, float splashWhiten, Vector3 hitPoint, Collider hitCollider)
        {
            Color color;
            if (ImpactPuffsConfig.AdaptSurfaceColor && !splash)
            {
                surfaceColor.ApplySnapshot(vessel, hitPoint, hitCollider);
                color = surfaceColor.CurrentColor;
            }
            else
            {
                color = KerbalFxDustVisualDefaults.Color;
            }

            color = Color.Lerp(color, new Color(0.90f, 0.90f, 0.89f), baseWhiten);
            if (splash)
            {
                color = Color.Lerp(color, new Color(0.93f, 0.94f, 0.95f), splashWhiten);
            }

            if (ImpactPuffsConfig.UseLightAware && !splash)
            {
                string bodyName = vessel != null && vessel.mainBody != null ? vessel.mainBody.bodyName : string.Empty;
                color = lightAware.ApplyColorTint(
                    color,
                    ImpactPuffsRuntimeConfig.LightAwareProfile.Get(bodyName),
                    LightAwareTouchdownStrength);
            }

            LogTouchdownSample(color, splash);
            LogTouchdownLightSample();
            return color;
        }

        private void LogTouchdownLightSample()
        {
            if (!ImpactPuffsConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel || !ImpactPuffsConfig.UseLightAware)
                return;

            string bodyName = vessel != null && vessel.mainBody != null ? vessel.mainBody.bodyName : string.Empty;
            float multiplier = lightAware.GetAlphaMultiplier(
                ImpactPuffsRuntimeConfig.LightAwareProfile.Get(bodyName),
                LightAwareTouchdownStrength);
            KerbalFxLightDebugReporter.Report(
                "ImpactPuffs",
                "Touchdown:" + (vessel != null ? vessel.vesselName : "n/a"),
                lightAware.Current,
                multiplier,
                LightAwareTouchdownStrength);
            ImpactPuffsLog.DebugLog(Localizer.Format(
                ImpactPuffsLoc.LogTouchdownLightSample,
                KerbalFxLightFormat.Describe(lightAware.Current, string.Empty),
                multiplier.ToString("F2", CultureInfo.InvariantCulture)));
        }

        private void LogTouchdownSample(Color appliedColor, bool splash)
        {
            if (!ImpactPuffsConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel)
                return;

            ImpactPuffsLog.DebugLog(Localizer.Format(
                ImpactPuffsLoc.LogTouchdownSample,
                string.IsNullOrEmpty(surfaceColor.LastBodyName) ? "n/a" : surfaceColor.LastBodyName,
                string.IsNullOrEmpty(surfaceColor.LastBiomeName) ? "n/a" : surfaceColor.LastBiomeName,
                surfaceColor.LastSource.ToString(),
                KerbalFxSurfaceColorCore.FormatColor(surfaceColor.LastRawSample),
                KerbalFxSurfaceColorCore.FormatColor(appliedColor),
                splash));
        }

        private void LogRingShockDebug(
            float impactSpeed,
            int ringDustCount,
            float bodyVisibility,
            int ringEdgeCount,
            int ringMistCount,
            float radiusBase,
            float lateralBase,
            float liftBase,
            float surfaceOffset,
            float slopeFactor,
            float ringAlpha,
            float lightAwareMultiplier,
            float touchdownEnergy01)
        {
            if (!ImpactPuffsConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel)
            {
                return;
            }

            ImpactPuffsLog.DebugLog(Localizer.Format(
                ImpactPuffsLoc.LogBurst,
                impactSpeed.ToString("F2", CultureInfo.InvariantCulture),
                ringDustCount,
                bodyVisibility.ToString("F2", CultureInfo.InvariantCulture)
            ));
            ImpactPuffsLog.DebugLog(Localizer.Format(
                ImpactPuffsLoc.LogRingShockDebug,
                impactSpeed.ToString("F2", CultureInfo.InvariantCulture),
                ringDustCount.ToString(CultureInfo.InvariantCulture),
                ringEdgeCount.ToString(CultureInfo.InvariantCulture),
                ringMistCount.ToString(CultureInfo.InvariantCulture),
                radiusBase.ToString("F2", CultureInfo.InvariantCulture),
                lateralBase.ToString("F2", CultureInfo.InvariantCulture),
                liftBase.ToString("F3", CultureInfo.InvariantCulture),
                surfaceOffset.ToString("F3", CultureInfo.InvariantCulture),
                slopeFactor.ToString("F2", CultureInfo.InvariantCulture),
                ringAlpha.ToString("F2", CultureInfo.InvariantCulture),
                lightAwareMultiplier.ToString("F2", CultureInfo.InvariantCulture),
                touchdownEnergy01.ToString("F2", CultureInfo.InvariantCulture)
            ));
        }

        private void SpawnRingShock(float impactSpeed)
        {
            TouchdownContext context;
            if (!TryBuildTouchdownContext(impactSpeed, true, 0.08f, 0.14f, out context))
            {
                return;
            }

            float energyRadiusScale = Mathf.Lerp(1.00f, 1.12f, context.TouchdownEnergy01);
            float energySpreadScale = Mathf.Lerp(1.00f, 1.16f, context.TouchdownEnergy01);
            float energyLiftScale = Mathf.Lerp(1.00f, 1.10f, context.TouchdownEnergy01);
            float energyCountScale = Mathf.Lerp(1.00f, 1.32f, context.TouchdownEnergy01);
            float radiusBase = Mathf.Lerp(0.86f, 2.22f, context.QualityNorm)
                * (context.Splash ? 1.24f : 1.00f)
                * ImpactPuffsRuntimeConfig.RadiusScaleMultiplier
                * ImpactPuffsRuntimeConfig.SharedRadiusScaleMultiplier
                * 0.48f
                * energyRadiusScale;
            float lateralScale = Mathf.Lerp(
                0.72f,
                1.24f,
                Mathf.InverseLerp(1.0f, 3.0f, ImpactPuffsRuntimeConfig.LateralSpreadMultiplier)
            );
            float lateralBase = Mathf.Lerp(0.90f, 2.80f, context.BurstStrength)
                * Mathf.Lerp(0.92f, 1.16f, context.QualityNorm)
                * lateralScale
                * (context.Splash ? 1.12f : 1.00f)
                * energySpreadScale;
            float liftBase = Mathf.Lerp(0.03f, 0.12f, context.BurstStrength)
                * ImpactPuffsRuntimeConfig.VerticalLiftMultiplier
                * (context.Splash ? 0.74f : 1.00f)
                * energyLiftScale;
            float densityScale = Mathf.Lerp(0.96f, 1.14f, context.QualityNorm);
            Vector3 bodyUp = (vessel.CoM - vessel.mainBody.position).normalized;
            float slopeFactor = 1f - Mathf.Clamp01(Vector3.Dot(context.Normal, bodyUp));
            float surfaceOffset = (context.Splash ? 0.018f : 0.026f) + slopeFactor * 0.055f + Mathf.Clamp(radiusBase * 0.004f, 0f, 0.018f);

            TouchdownBurstPool.Slot slot = TouchdownBurstPool.Acquire(context.FxLayer);
            if (slot == null)
            {
                return;
            }
            slot.Root.transform.position = context.Point + context.Normal * surfaceOffset;
            slot.Root.transform.rotation = Quaternion.FromToRotation(Vector3.forward, context.Normal);

            float ringAlpha = Mathf.Lerp(context.Splash ? 0.26f : 0.30f, context.Splash ? 0.36f : 0.43f, context.QualityNorm) * densityScale * 0.86f
                * Mathf.Max(0.05f, context.LightAwareMultiplier);
            float ringBase =
                (140f + 460f * context.BurstStrength)
                * ImpactPuffsRuntimeConfig.TouchdownBurstMultiplier
                * (context.Splash ? 1.08f : 1.00f)
                * context.BodyVisibility
                * 1.15f
                * energyCountScale
                * context.QualityScale;
            int ringDustCount = Mathf.RoundToInt(Mathf.Clamp(ringBase, 360f * context.QualityScale, 4600f * context.QualityScale));
            int ringEdgeMin = Mathf.Max(1, Mathf.RoundToInt(180f * context.QualityScale));
            int ringEdgeMax = Mathf.Max(ringEdgeMin, Mathf.RoundToInt(2800f * context.QualityScale));
            int ringMistMin = Mathf.Max(1, Mathf.RoundToInt(120f * context.QualityScale));
            int ringMistMax = Mathf.Max(ringMistMin, Mathf.RoundToInt(1800f * context.QualityScale));
            int ringEdgeCount = Mathf.Clamp(Mathf.RoundToInt(ringDustCount * 0.62f), ringEdgeMin, ringEdgeMax);
            int ringMistCount = Mathf.Clamp(ringDustCount - ringEdgeCount, ringMistMin, ringMistMax);

            ApplyTouchdownLayerProfile(slot.RingEdge, new TouchdownLayerOptions
            {
                Color = context.Color,
                Alpha = ringAlpha,
                MinSize = 0.16f * Mathf.Lerp(0.94f, 1.18f, context.QualityNorm) * context.BodyVisibility,
                MaxSize = 0.64f * Mathf.Lerp(0.94f, 1.18f, context.QualityNorm) * context.BodyVisibility,
                MinLife = 0.54f,
                MaxLife = (context.Splash ? 1.48f : 1.34f) * Mathf.Lerp(0.96f, 1.14f, context.QualityNorm),
                MinSpeed = 0.00f,
                MaxSpeed = 0.10f * Mathf.Lerp(0.96f, 1.12f, context.QualityNorm),
                Gravity = 0.020f,
                BurstCount = ringEdgeCount,
                MaxParticlesScale = context.QualityScale,
                ShapeAngle = Mathf.Lerp(context.Splash ? 76f : 74f, context.Splash ? 84f : 82f, context.QualityNorm),
                ShapeRadius = radiusBase * 0.98f,
                Lateral = lateralBase * 0.06f,
                LiftMin = context.Splash ? liftBase * 0.03f : liftBase * 0.06f,
                LiftMax = context.Splash ? liftBase * 0.10f : liftBase * 0.18f,
                RadialMin = lateralBase * 1.46f,
                RadialMax = lateralBase * 2.70f,
                NoiseStrength = Mathf.Lerp(0.06f, 0.18f, context.BurstStrength) * ImpactPuffsRuntimeConfig.TurbulenceMultiplier,
                NoiseFrequency = Mathf.Lerp(0.05f, 0.14f, context.BurstStrength),
                NoiseScroll = Mathf.Lerp(0.01f, 0.05f, context.BurstStrength),
                Gradient = RingShockGradient,
                SizeCurve = RingShockSizeCurve,
                MaxParticleSize = Mathf.Lerp(0.78f, 0.98f, context.QualityNorm),
                SortingFudge = 2.2f,
                ShapeRadiusThickness = 0.00f,
                RenderMode = ParticleSystemRenderMode.Billboard,
                UseCircleShape = true
            });

            ApplyTouchdownLayerProfile(slot.RingMist, new TouchdownLayerOptions
            {
                Color = Color.Lerp(context.Color, Color.white, 0.06f),
                Alpha = ringAlpha * 0.58f,
                MinSize = 0.28f * Mathf.Lerp(0.96f, 1.22f, context.QualityNorm) * context.BodyVisibility,
                MaxSize = 1.04f * Mathf.Lerp(0.96f, 1.22f, context.QualityNorm) * context.BodyVisibility,
                MinLife = 0.80f,
                MaxLife = (context.Splash ? 2.15f : 1.96f) * Mathf.Lerp(0.98f, 1.18f, context.QualityNorm),
                MinSpeed = 0.01f,
                MaxSpeed = 0.08f * Mathf.Lerp(0.96f, 1.12f, context.QualityNorm),
                Gravity = 0.022f,
                BurstCount = ringMistCount,
                MaxParticlesScale = context.QualityScale,
                ShapeAngle = Mathf.Lerp(context.Splash ? 72f : 70f, context.Splash ? 80f : 78f, context.QualityNorm),
                ShapeRadius = radiusBase * 0.88f,
                Lateral = lateralBase * 0.05f,
                LiftMin = context.Splash ? liftBase * 0.04f : liftBase * 0.08f,
                LiftMax = context.Splash ? liftBase * 0.12f : liftBase * 0.22f,
                RadialMin = lateralBase * 1.04f,
                RadialMax = lateralBase * 2.00f,
                NoiseStrength = Mathf.Lerp(0.04f, 0.14f, context.BurstStrength) * ImpactPuffsRuntimeConfig.TurbulenceMultiplier,
                NoiseFrequency = Mathf.Lerp(0.04f, 0.12f, context.BurstStrength),
                NoiseScroll = Mathf.Lerp(0.00f, 0.04f, context.BurstStrength),
                Gradient = RingShockGradient,
                SizeCurve = RingShockSizeCurve,
                MaxParticleSize = Mathf.Lerp(0.92f, 1.18f, context.QualityNorm),
                SortingFudge = 2.0f,
                ShapeRadiusThickness = 0.08f,
                RenderMode = ParticleSystemRenderMode.Billboard,
                UseCircleShape = true
            });

            slot.Root.SetActive(true);
            RestartLayer(slot.RingEdge);
            RestartLayer(slot.RingMist);

            float ringLifeMax = context.Splash ? 2.55f : 2.30f;
            TouchdownBurstPool.MarkBusy(slot, ringLifeMax);

            LogRingShockDebug(
                impactSpeed,
                ringDustCount,
                context.BodyVisibility,
                ringEdgeCount,
                ringMistCount,
                radiusBase,
                lateralBase,
                liftBase,
                surfaceOffset,
                slopeFactor,
                ringAlpha,
                context.LightAwareMultiplier,
                context.TouchdownEnergy01);
        }

        internal static ParticleSystem BuildPooledTouchdownLayer(Transform parent, int layer, string name)
        {
            GameObject go = new GameObject("KerbalFX_ImpactBurst_" + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.layer = layer;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;

            ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = ps.limitVelocityOverLifetime;
            limitVelocity.enabled = true;
            limitVelocity.space = ParticleSystemSimulationSpace.Local;
            limitVelocity.separateAxes = false;
            limitVelocity.dampen = 0.46f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.damping = true;

            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            Material dustMaterial = ImpactPuffsAssets.GetSharedMaterial();
            if (dustMaterial == null)
            {
                dustMaterial = ImpactPuffsAssets.GetBurstMaterial();
            }
            if (dustMaterial != null)
            {
                renderer.sharedMaterial = dustMaterial;
            }

            return ps;
        }

        private static void ApplyTouchdownLayerProfile(ParticleSystem ps, TouchdownLayerOptions o)
        {
            if (ps == null)
            {
                return;
            }

            ParticleSystem.MainModule main = ps.main;
            main.startColor = new Color(o.Color.r, o.Color.g, o.Color.b, Mathf.Clamp01(o.Alpha));
            main.startSize = new ParticleSystem.MinMaxCurve(o.MinSize, o.MaxSize);
            main.startLifetime = new ParticleSystem.MinMaxCurve(o.MinLife, o.MaxLife);
            main.startSpeed = new ParticleSystem.MinMaxCurve(o.MinSpeed, o.MaxSpeed);
            main.gravityModifier = o.Gravity;
            float maxParticlesScale = Mathf.Clamp(o.MaxParticlesScale, 0.25f, 1.5f);
            main.maxParticles = Mathf.RoundToInt(Mathf.Clamp(
                36000f * ImpactPuffsRuntimeConfig.MaxParticlesMultiplier * ImpactPuffsRuntimeConfig.SharedMaxParticlesMultiplier * maxParticlesScale,
                6000f * maxParticlesScale,
                360000f * maxParticlesScale));

            ParticleSystem.EmissionModule emission = ps.emission;
            short burstPrimary = (short)Mathf.Clamp(Mathf.RoundToInt(o.BurstCount * 0.40f), 1, short.MaxValue);
            short burstSecondary = (short)Mathf.Clamp(Mathf.RoundToInt(o.BurstCount * 0.28f), 1, short.MaxValue);
            short burstTertiary = (short)Mathf.Clamp(Mathf.RoundToInt(o.BurstCount * 0.20f), 1, short.MaxValue);
            short burstQuaternary = (short)Mathf.Clamp(Mathf.RoundToInt(o.BurstCount * 0.12f), 1, short.MaxValue);
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, burstPrimary),
                new ParticleSystem.Burst(0.04f, burstSecondary),
                new ParticleSystem.Burst(0.11f, burstTertiary),
                new ParticleSystem.Burst(0.21f, burstQuaternary)
            });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = o.UseCircleShape ? ParticleSystemShapeType.Circle : ParticleSystemShapeType.Cone;
            shape.angle = o.UseCircleShape ? 0f : o.ShapeAngle;
            shape.radius = o.ShapeRadius;
            shape.radiusThickness = Mathf.Clamp01(o.ShapeRadiusThickness);

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(-o.Lateral, o.Lateral);
            velocity.y = new ParticleSystem.MinMaxCurve(o.LiftMin, o.LiftMax);
            velocity.z = new ParticleSystem.MinMaxCurve(-o.Lateral, o.Lateral);
            velocity.radial = new ParticleSystem.MinMaxCurve(o.RadialMin, o.RadialMax);

            ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = ps.limitVelocityOverLifetime;
            float maxRadial = Mathf.Max(Mathf.Abs(o.RadialMin), Mathf.Abs(o.RadialMax));
            float speedCap = Mathf.Max(0.20f, Mathf.Max(o.MaxSpeed, maxRadial) * 1.16f);
            limitVelocity.limit = new ParticleSystem.MinMaxCurve(speedCap);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.color = o.Gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, o.SizeCurve);

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.strength = o.NoiseStrength;
            noise.frequency = o.NoiseFrequency;
            noise.scrollSpeed = o.NoiseScroll;

            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = o.RenderMode;
                renderer.sortingFudge = o.SortingFudge;
                renderer.maxParticleSize = o.MaxParticleSize;
            }
        }

        private static void RestartLayer(ParticleSystem ps)
        {
            if (ps == null)
            {
                return;
            }
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear();
            ps.Play(true);
        }

        private static Gradient CreateRingShockGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.68f, 0f),
                    new GradientAlphaKey(0.46f, 0.16f),
                    new GradientAlphaKey(0.22f, 0.52f),
                    new GradientAlphaKey(0.06f, 0.82f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            return gradient;
        }

        private static AnimationCurve CreateRingShockSizeCurve()
        {
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.44f);
            curve.AddKey(0.20f, 0.94f);
            curve.AddKey(0.62f, 1.34f);
            curve.AddKey(1f, 0.60f);
            return curve;
        }

        private bool TryGetGroundPoint(out Vector3 point, out Vector3 normal, out Collider collider)
        {
            point = Vector3.zero;
            normal = Vector3.up;
            collider = null;

            if (vessel == null || vessel.mainBody == null)
            {
                return false;
            }

            Vector3 up = vessel.CoM - vessel.mainBody.position;
            if (up.sqrMagnitude < 0.0001f)
            {
                up = Vector3.up;
            }
            up.Normalize();

            float originOffset;
            float rayDistance;
            GetBurstProbeSettings(up, out originOffset, out rayDistance);

            Vector3 origin = vessel.CoM + up * originOffset;
            Vector3 direction = -up;

            RaycastHit hit;
            if (!TryRayDown(origin, direction, rayDistance, out hit))
            {
                Vector3 fallbackOrigin = vessel.CoM + up * MinBurstRayOriginOffset;
                if ((fallbackOrigin - origin).sqrMagnitude <= 0.01f
                    || !TryRayDown(fallbackOrigin, direction, MinBurstRayDistance, out hit))
                {
                    return false;
                }
            }

            point = hit.point;
            normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : up;
            collider = hit.collider;
            return true;
        }

        private void GetBurstProbeSettings(Vector3 up, out float originOffset, out float rayDistance)
        {
            float terrainHeight = Mathf.Max(0f, EngineGroundPuffEmitter.GetSafeTerrainHeightAgl(vessel));
            float vesselUpExtent;
            if (!TryEstimateVesselUpExtent(up, out vesselUpExtent))
            {
                vesselUpExtent = 0f;
            }

            originOffset = Mathf.Clamp(
                Mathf.Max(MinBurstRayOriginOffset, terrainHeight + 2.0f, vesselUpExtent + 2.0f),
                MinBurstRayOriginOffset,
                MaxBurstRayOriginOffset);

            rayDistance = Mathf.Clamp(
                Mathf.Max(MinBurstRayDistance, terrainHeight + vesselUpExtent + 6.0f, originOffset + 10.0f),
                MinBurstRayDistance,
                MaxBurstRayDistance);
        }

        private bool TryEstimateVesselUpExtent(Vector3 up, out float extent)
        {
            extent = 0f;
            if (vessel == null || vessel.parts == null)
            {
                return false;
            }

            bool found = false;
            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part part = vessel.parts[i];
                if (part == null)
                {
                    continue;
                }

                List<Collider> colliders = part.FindModelComponents<Collider>();
                if (colliders == null)
                {
                    continue;
                }

                for (int j = 0; j < colliders.Count; j++)
                {
                    Collider partCollider = colliders[j];
                    if (partCollider == null || !partCollider.enabled)
                    {
                        continue;
                    }

                    Bounds bounds = partCollider.bounds;
                    float projectedExtent = Mathf.Abs(Vector3.Dot(bounds.center - vessel.CoM, up))
                        + KerbalFxUtil.ProjectBoundsExtent(bounds, up);
                    if (!found || projectedExtent > extent)
                    {
                        extent = projectedExtent;
                        found = true;
                    }
                }
            }

            return found;
        }

        private bool TryRayDown(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit bestHit)
        {
            bestHit = new RaycastHit();
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction.normalized,
                BurstHits,
                maxDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );

            if (hitCount <= 0)
            {
                return false;
            }

            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = BurstHits[i];
                if (candidate.collider == null)
                {
                    continue;
                }

                Part hitPart = candidate.collider.GetComponentInParent<Part>();
                if (hitPart != null && hitPart.vessel == vessel)
                {
                    continue;
                }

                if (vessel != null && vessel.rootPart != null)
                {
                    Transform rootTransform = vessel.rootPart.transform;
                    if (rootTransform != null && candidate.collider.transform != null && candidate.collider.transform.IsChildOf(rootTransform))
                    {
                        continue;
                    }
                }

                Rigidbody hitBody = candidate.rigidbody;
                if (hitBody != null)
                {
                    Part rbPart = hitBody.GetComponentInParent<Part>();
                    if (rbPart != null && rbPart.vessel == vessel)
                    {
                        continue;
                    }
                }

                if (candidate.distance < bestDistance)
                {
                    bestDistance = candidate.distance;
                    bestHit = candidate;
                    found = true;
                }
            }

            return found;
        }
    }
}
