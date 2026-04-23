using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using KSP.Localization;
using UnityEngine;

namespace KerbalFX.RoverDust
{
    internal sealed class WheelDustEmitter
    {
        private readonly Part part;
        private readonly WheelCollider[] wheels;
        private readonly GameObject root;
        private readonly ParticleSystem particleSystem;
        private readonly string debugId;
        private readonly int wheelGroupCount;
        private readonly float vesselEmitterBudgetScale;

        private float smoothedRate;
        private float debugTimer;
        private float colorRefreshTimer;
        private float lightRefreshTimer;
        private float cachedLightFactor = 1f;
        private float wheelDustRateScale = 1f;
        private float wheelEffectiveRadius = 0.35f;
        private float wheelVisualScaleDebug = 1f;
        private float wheelVisualFootprintScale = 1f;
        private float wheelVisualFootprintRadius;
        private float continuityDistanceRemainder;
        private Vector3 lastContinuityPoint;
        private bool advancedQualityFeatures;
        private bool disposed;
        private bool colorInitialized;
        private bool lastSuppressed;
        private bool hasLastContinuityPoint;
        private KerbalFxRevisionStamp appliedProfileRevision;
        private string lastSuppressionKey = string.Empty;
        private int lastSurfaceColliderId = int.MinValue;
        private string lastSurfaceBody = string.Empty;
        private string lastSurfaceBiome = string.Empty;
        private Color currentColor = new Color(KerbalFxSurfaceColor.DefaultDustColor.r, KerbalFxSurfaceColor.DefaultDustColor.g, KerbalFxSurfaceColor.DefaultDustColor.b, 1f);

        private const float BaseDustAlpha = 0.82f;
        private const float MinDustSpeed = 0.7f;
        private const float SlipBoostScale = 2.4f;
        private const float BaseRateMin = 120f;
        private const float BaseRateRange = 480f;
        private const float ColorRefreshInterval = 0.3f;
        private const float LightRefreshInterval = 0.20f;
        private const float DebugLogInterval = 1.2f;
        private const float RateSmoothingSpeed = 6.5f;
        private const float RatePlayThreshold = 0.18f;
        private const float SceneLightsRefreshPeriod = 4.0f;
        private static readonly string[] KscSurfaceTokens = { "runway", "launchpad", "launch_pad", "launch pad", "crawlerway", "launchsite", "launch_site" };
        private static readonly string[] KscMaterialTokens = { "runway", "launchpad", "crawlerway" };
        private static readonly string[] KerbalKonstructsTokens = { "kerbalkonstructs", "staticobject" };
        private static readonly string[] LampTokens = { "headlamp", "headlight", "floodlight", "spotlight", "searchlight", "lamp", "projector" };
        private static readonly List<Light> sharedSceneLights = new List<Light>();
        private static readonly List<Component> sharedComponentBuffer = new List<Component>(24);
        private static float sharedSceneLightsRefreshAt;
        private static int sharedSceneLightsRefreshFrame = -1;
        private static Guid sharedSceneLightsVesselId = Guid.Empty;
        private static Light cachedSunLight;
        private static bool sunLightSearched;

        private static Gradient cachedFadeGradient;
        private static AnimationCurve cachedSizeCurve;
        private static float cachedGradientQualityNorm = -1f;
        private static float cachedSizeCurveQuality = -1f;

        public WheelDustEmitter(Part part, WheelCollider[] wheels, float vesselEmitterBudgetScale)
        {
            this.part = part;
            this.wheels = wheels ?? new WheelCollider[0];
            this.vesselEmitterBudgetScale = Mathf.Clamp(vesselEmitterBudgetScale, 0.40f, 1f);
            wheelGroupCount = GetActiveWheelColliderCount(this.wheels);
            debugId = (part.partInfo != null ? part.partInfo.name : part.name) + ":wheelGroup";

            root = new GameObject("RoverDustFXEmitter");
            root.transform.parent = part.transform;
            root.transform.position = GetWheelGroupCenter(part, this.wheels);
            root.layer = part.gameObject.layer;

            particleSystem = root.AddComponent<ParticleSystem>();
            ConfigureParticleSystemBase();
            ApplyRuntimeVisualProfile(true);
        }

        public void Tick(Vessel vessel, float dt)
        {
            if (disposed || part == null || wheels == null || wheels.Length == 0)
                return;

            if (appliedProfileRevision.NeedsApply(RoverDustConfig.Revision, RoverDustRuntimeConfig.Revision))
            {
                ApplyRuntimeVisualProfile(false);
            }

            WheelHit surfaceHit;
            Vector3 hitPoint;
            Vector3 hitNormal;
            float slip;
            bool hasHit = TryGetWheelGroupHit(out surfaceHit, out hitPoint, out hitNormal, out slip);
            if (!hasHit)
            {
                ResetContinuityTrail();
                SetTargetRate(0f, dt);
                return;
            }

            string suppressionKey;
            bool suppressed = ShouldSuppressDustSurface(surfaceHit.collider, out suppressionKey);
            if (suppressed)
            {
                if ((!lastSuppressed || suppressionKey != lastSuppressionKey) && RoverDustConfig.DebugLogging && vessel == FlightGlobals.ActiveVessel)
                    RoverDustLog.DebugLog(Localizer.Format(RoverDustLoc.LogSuppressed, debugId, suppressionKey));
                lastSuppressed = true;
                lastSuppressionKey = suppressionKey;
                ResetContinuityTrail();
                SetTargetRate(0f, dt);
                return;
            }

            lastSuppressed = false;
            lastSuppressionKey = string.Empty;

            float speed = Mathf.Abs((float)vessel.srfSpeed);
            float speedFactor = Mathf.InverseLerp(MinDustSpeed, 20f, speed);
            float slipBoost = Mathf.Clamp01(slip * SlipBoostScale);

            float quality = RoverDustConfig.QualityPercent / 100f;
            float qualityRateScale = 1f + (Mathf.Pow(quality, 1.60f) - 1f) * 0.75f;
            float bodyVisibility = GetBodyDustVisibilityMultiplier(vessel);
            float baseRate = (BaseRateMin + BaseRateRange * speedFactor) * (0.45f + 0.55f * slipBoost) * RoverDustRuntimeConfig.EmissionMultiplier;
            float targetRate = baseRate * qualityRateScale * wheelDustRateScale * bodyVisibility
                * GetWheelClusterRateScale() * GetWheelVisualRateScale() * vesselEmitterBudgetScale;
            bool useLightAware = advancedQualityFeatures && RoverDustConfig.UseLightAware;

            Vector3 stableNormal = GetStableGroundNormal(vessel, hitPoint, hitNormal);
            RefreshLightingState(vessel, hitPoint, stableNormal, dt);
            if (useLightAware)
            {
                float light = Mathf.Clamp01(cachedLightFactor);
                float lightRateFactor = Mathf.Pow(light, RoverDustRuntimeConfig.LightRateExponent);
                if (light > 0.001f)
                    lightRateFactor = Mathf.Lerp(RoverDustRuntimeConfig.DaylightRateFloor, 1f, lightRateFactor);
                targetRate *= lightRateFactor;
                if (light < 0.025f)
                    targetRate = 0f;
            }

            if (speed < MinDustSpeed)
            {
                targetRate = 0f;
                ResetContinuityTrail();
            }

            SetTargetRate(targetRate, dt);
            Vector3 emitPosition = hitPoint + stableNormal * 0.04f;
            root.transform.position = emitPosition;
            root.transform.rotation = Quaternion.LookRotation(GetDustForward(vessel, stableNormal, surfaceHit.forwardDir), stableNormal);
            EmitContinuityTrail(emitPosition, stableNormal, targetRate, speed, dt);

            colorRefreshTimer -= dt;
            if (colorRefreshTimer <= 0f)
            {
                colorRefreshTimer = ColorRefreshInterval;
                UpdateSurfaceColor(vessel, surfaceHit);
            }

            if (RoverDustConfig.DebugLogging && vessel == FlightGlobals.ActiveVessel)
            {
                debugTimer -= dt;
                if (debugTimer <= 0f)
                {
                    debugTimer = DebugLogInterval;
                    RoverDustLog.DebugLog(Localizer.Format(
                        RoverDustLoc.LogEmitter, debugId, hasHit,
                        speed.ToString("F2", CultureInfo.InvariantCulture),
                        slip.ToString("F3", CultureInfo.InvariantCulture),
                        smoothedRate.ToString("F1", CultureInfo.InvariantCulture),
                        currentColor.r.ToString("F2", CultureInfo.InvariantCulture) + "," + currentColor.g.ToString("F2", CultureInfo.InvariantCulture) + "," + currentColor.b.ToString("F2", CultureInfo.InvariantCulture)
                        + " L=" + cachedLightFactor.ToString("F2", CultureInfo.InvariantCulture)
                        + " W=" + wheelDustRateScale.ToString("F2", CultureInfo.InvariantCulture)
                        + " V=" + wheelVisualScaleDebug.ToString("F2", CultureInfo.InvariantCulture)
                        + " F=" + wheelVisualFootprintScale.ToString("F2", CultureInfo.InvariantCulture)
                        + " FR=" + wheelVisualFootprintRadius.ToString("F2", CultureInfo.InvariantCulture)
                        + " R=" + wheelEffectiveRadius.ToString("F2", CultureInfo.InvariantCulture)
                        + " O=" + vesselEmitterBudgetScale.ToString("F2", CultureInfo.InvariantCulture)
                        + " B=" + bodyVisibility.ToString("F2", CultureInfo.InvariantCulture)));
                }
            }
        }

        public void StopEmission()
        {
            ResetContinuityTrail();
            SetTargetRate(0f, 0.12f);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            if (particleSystem != null)
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (root != null)
                UnityEngine.Object.Destroy(root);
        }

        private void ConfigureParticleSystemBase()
        {
            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = new Color(currentColor.r, currentColor.g, currentColor.b, BaseDustAlpha);

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;

            ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = particleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = false;

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 2f;

            Material material = RoverDustAssets.GetSharedMaterial();
            if (material != null)
                renderer.sharedMaterial = material;
        }

        private void ApplyRuntimeVisualProfile(bool force)
        {
            if (appliedProfileRevision.ShouldSkipApply(force, RoverDustConfig.Revision, RoverDustRuntimeConfig.Revision))
                return;

            appliedProfileRevision.MarkApplied(RoverDustConfig.Revision, RoverDustRuntimeConfig.Revision);

            int qualityPercent = Mathf.Clamp(RoverDustConfig.QualityPercent, 25, 200);
            float quality = qualityPercent / 100f;
            float qualityNorm = Mathf.InverseLerp(0.25f, 2.0f, quality);
            float qualityParticleScale = 1f + (Mathf.Pow(quality, 1.70f) - 1f) * 0.75f;
            float qualitySizeScale = Mathf.Lerp(0.76f, 1.36f, qualityNorm);
            float bodyVisibility = GetBodyDustVisibilityMultiplier(part != null ? part.vessel : null);
            float bodyBoostNorm = Mathf.Clamp01((bodyVisibility - 1f) / 0.5f);
            float bodyParticleScale = Mathf.Lerp(1f, 1.28f, bodyBoostNorm);
            float bodySizeScale = Mathf.Lerp(1f, 1.16f, bodyBoostNorm);
            advancedQualityFeatures = qualityPercent >= 100;
            wheelEffectiveRadius = GetEffectiveWheelGroupRadius(wheels);
            wheelVisualFootprintScale = EstimateWheelVisualFootprintScale(part, wheels, wheelEffectiveRadius, out wheelVisualFootprintRadius);
            float wheelVisualNorm = Mathf.InverseLerp(0.18f, 1.15f, wheelEffectiveRadius);
            float wheelGroupNorm = Mathf.InverseLerp(1f, 6f, wheelGroupCount);
            float wheelGroupScale = Mathf.Lerp(1f, 1.28f, wheelGroupNorm);
            float wheelVisualScale = GetWheelVisualScale(wheelVisualNorm) * wheelGroupScale * wheelVisualFootprintScale;
            wheelVisualScaleDebug = wheelVisualScale;
            float wheelLifetimeScale = Mathf.Lerp(0.98f, 1.14f, wheelVisualNorm) * Mathf.Lerp(1f, 1.14f, wheelGroupNorm);
            float wheelSpeedScale = Mathf.Lerp(0.96f, 1.08f, wheelVisualNorm) * Mathf.Lerp(1f, 1.08f, wheelGroupNorm);
            float wheelClusterRateScale = GetWheelClusterRateScale();
            wheelDustRateScale = advancedQualityFeatures ? GetWheelDustRateScale(wheelEffectiveRadius) : 1f;
            if (!advancedQualityFeatures)
                cachedLightFactor = 1f;

            ParticleSystem.MainModule main = particleSystem.main;
            float maxParticlesBase = 760f * qualityParticleScale * RoverDustRuntimeConfig.MaxParticlesMultiplier;
            float maxParticleBudgetScale = Mathf.Lerp(0.72f, 1f, vesselEmitterBudgetScale);
            main.maxParticles = Mathf.RoundToInt(Mathf.Clamp(maxParticlesBase * wheelDustRateScale * bodyParticleScale
                * wheelClusterRateScale * GetWheelVisualRateScale() * maxParticleBudgetScale, 220f, 4600f));

            float minSize = 0.036f * qualitySizeScale * bodySizeScale * wheelVisualScale;
            float maxSize = 0.102f * qualitySizeScale * bodySizeScale * wheelVisualScale;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);

            float minLifetime = 0.21f * Mathf.Lerp(0.98f, 1.18f, qualityNorm) * wheelLifetimeScale;
            float maxLifetime = 0.64f * Mathf.Lerp(0.98f, 1.20f, qualityNorm) * wheelLifetimeScale;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.60f * wheelSpeedScale, Mathf.Lerp(1.9f, 3.4f, qualityNorm) * wheelSpeedScale);
            main.gravityModifier = Mathf.Lerp(0.014f, 0.024f, qualityNorm);

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.angle = Mathf.Lerp(11.5f, 16.5f, qualityNorm) * Mathf.Lerp(1.00f, 1.14f, wheelGroupNorm);
            float wheelRadiusVisual = wheelEffectiveRadius;
            float radiusScale = advancedQualityFeatures
                ? Mathf.Clamp(Mathf.Lerp(0.90f, 1.70f, Mathf.InverseLerp(0.22f, 1.05f, wheelRadiusVisual)), 0.90f, 1.80f) * RoverDustRuntimeConfig.RadiusScaleMultiplier
                : 1f;
            float profileShapeRadius = Mathf.Lerp(0.062f, 0.132f, qualityNorm) * radiusScale * bodySizeScale
                * Mathf.Lerp(1.00f, 1.28f, wheelGroupNorm) * Mathf.Lerp(1f, wheelVisualFootprintScale, 0.85f);
            float footprintShapeRadius = wheelVisualFootprintRadius > 0.01f ? wheelVisualFootprintRadius * 0.64f : 0f;
            shape.radius = Mathf.Max(profileShapeRadius, footprintShapeRadius);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.color = GetOrCreateFadeGradient(qualityNorm);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = quality >= 0.50f;
            if (sizeOverLifetime.enabled)
            {
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(Mathf.Lerp(1.00f, 1.20f, wheelGroupNorm), GetOrCreateSizeCurve(quality));
            }

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.maxParticleSize = Mathf.Clamp(Mathf.Lerp(0.11f, 0.19f, qualityNorm) * Mathf.Lerp(1.00f, 1.16f, wheelGroupNorm), 0.11f, 0.24f);
            ApplyCurrentStartColor();

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            if (smoothedRate < 0.01f)
                emission.rateOverTime = 0f;

            if (RoverDustConfig.DebugLogging)
            {
                RoverDustLog.DebugLog(Localizer.Format(
                    RoverDustLoc.LogProfile, debugId, qualityPercent,
                    main.maxParticles, minSize.ToString("F3", CultureInfo.InvariantCulture), maxSize.ToString("F3", CultureInfo.InvariantCulture)));
            }
        }

        private void EmitContinuityTrail(Vector3 emitPosition, Vector3 stableNormal, float targetRate, float speed, float dt)
        {
            if (particleSystem == null || wheelVisualFootprintScale <= 1.02f || targetRate <= 50f || speed < 2.8f || dt <= 0f)
            {
                ResetContinuityTrail();
                return;
            }

            if (!hasLastContinuityPoint)
            {
                lastContinuityPoint = emitPosition;
                hasLastContinuityPoint = true;
                continuityDistanceRemainder = 0f;
                return;
            }

            Vector3 delta = emitPosition - lastContinuityPoint;
            float distance = delta.magnitude;
            if (distance < 0.015f)
                return;

            if (distance > 6.0f || dt > 0.25f)
            {
                lastContinuityPoint = emitPosition;
                continuityDistanceRemainder = 0f;
                return;
            }

            Vector3 travelDir = delta / distance;
            Vector3 sideDir = Vector3.Cross(stableNormal, travelDir);
            if (sideDir.sqrMagnitude < 0.0001f)
                sideDir = Vector3.Cross(stableNormal, Vector3.forward);
            if (sideDir.sqrMagnitude < 0.0001f)
                sideDir = Vector3.right;
            sideDir.Normalize();

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            float spread = Mathf.Clamp(shape.radius * 0.58f, 0.025f, 0.85f);
            float visualNorm = Mathf.Clamp01((wheelVisualScaleDebug - 1f) / 2.4f);
            float speedNorm = Mathf.InverseLerp(3f, 24f, speed);
            float spacing = Mathf.Lerp(0.26f, 0.115f, Mathf.Max(visualNorm, speedNorm));
            float distanceUntilNext = spacing - continuityDistanceRemainder;
            int maxEmit = Mathf.Clamp(Mathf.CeilToInt(targetRate * dt * 0.34f), 1, 18);
            int emitted = 0;

            while (distanceUntilNext <= distance && emitted < maxEmit)
            {
                float t = Mathf.Clamp01(distanceUntilNext / distance);
                Vector3 position = Vector3.Lerp(lastContinuityPoint, emitPosition, t);
                position += sideDir * UnityEngine.Random.Range(-spread, spread);
                position += stableNormal * UnityEngine.Random.Range(0f, 0.035f);

                ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
                emitParams.position = position;
                particleSystem.Emit(emitParams, 1);

                distanceUntilNext += spacing;
                emitted++;
            }

            if (distanceUntilNext > distance)
                continuityDistanceRemainder = spacing - (distanceUntilNext - distance);
            else
                continuityDistanceRemainder = 0f;

            lastContinuityPoint = emitPosition;
        }

        private void ResetContinuityTrail()
        {
            hasLastContinuityPoint = false;
            continuityDistanceRemainder = 0f;
        }

        private static Gradient GetOrCreateFadeGradient(float qualityNorm)
        {
            if (cachedFadeGradient != null && Mathf.Abs(cachedGradientQualityNorm - qualityNorm) < 0.01f)
                return cachedFadeGradient;
            cachedGradientQualityNorm = qualityNorm;
            cachedFadeGradient = new Gradient();
            cachedFadeGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(Mathf.Lerp(0.66f, 0.84f, qualityNorm), 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            return cachedFadeGradient;
        }

        private static AnimationCurve GetOrCreateSizeCurve(float quality)
        {
            if (cachedSizeCurve != null && Mathf.Abs(cachedSizeCurveQuality - quality) < 0.01f)
                return cachedSizeCurve;
            cachedSizeCurveQuality = quality;
            cachedSizeCurve = new AnimationCurve();
            cachedSizeCurve.AddKey(0f, 0.95f);
            cachedSizeCurve.AddKey(0.55f, 1.40f);
            cachedSizeCurve.AddKey(1f, 0.35f);
            return cachedSizeCurve;
        }

        private void SetTargetRate(float targetRate, float dt)
        {
            if (particleSystem == null)
                return;

            float lerpSpeed = Mathf.Clamp01(dt * RateSmoothingSpeed);
            smoothedRate = Mathf.Lerp(smoothedRate, targetRate, lerpSpeed);

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = smoothedRate;

            if (smoothedRate > RatePlayThreshold)
            {
                if (!particleSystem.isPlaying)
                    particleSystem.Play(true);
            }
            else if (particleSystem.isPlaying)
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void RefreshLightingState(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal, float dt)
        {
            if (!advancedQualityFeatures || !RoverDustConfig.UseLightAware)
            {
                if (cachedLightFactor != 1f)
                {
                    cachedLightFactor = 1f;
                    ApplyCurrentStartColor();
                }
                return;
            }

            lightRefreshTimer -= dt;
            if (lightRefreshTimer > 0f)
                return;

            lightRefreshTimer = LightRefreshInterval;
            float newLightFactor = EvaluateCombinedLighting(vessel, worldPoint, surfaceNormal);
            if (Mathf.Abs(newLightFactor - cachedLightFactor) > 0.02f)
            {
                cachedLightFactor = newLightFactor;
                ApplyCurrentStartColor();
            }
        }

        private void ApplyCurrentStartColor()
        {
            if (particleSystem == null)
                return;

            float alpha = BaseDustAlpha;
            if (advancedQualityFeatures && RoverDustConfig.UseLightAware)
            {
                float light = Mathf.Clamp01(cachedLightFactor);
                if (light < 0.14f)
                {
                    alpha = 0f;
                }
                else
                {
                    float alphaLight = Mathf.Pow(light, RoverDustRuntimeConfig.LightAlphaExponent);
                    alphaLight = Mathf.Lerp(RoverDustRuntimeConfig.DaylightAlphaFloor, 1f, alphaLight);
                    alpha *= alphaLight;
                }
            }

            float bodyVisibility = GetBodyDustVisibilityMultiplier(part != null ? part.vessel : null);
            float bodyBoostNorm = Mathf.Clamp01((bodyVisibility - 1f) / 0.5f);
            alpha *= Mathf.Lerp(1f, 1.16f, bodyBoostNorm);
            alpha = Mathf.Clamp(alpha, 0f, 0.97f);

            ParticleSystem.MainModule main = particleSystem.main;
            main.startColor = new Color(currentColor.r, currentColor.g, currentColor.b, alpha);
        }

        private static float EvaluateCombinedLighting(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal)
        {
            RefreshSharedSceneLights(vessel);

            float sunLight = EvaluateSunLighting(vessel, worldPoint, surfaceNormal);
            float artificialLight = EvaluateNearbyArtificialLights(worldPoint, surfaceNormal);

            if (sunLight <= 0.001f && artificialLight < 0.055f)
                return 0f;

            float combined = Mathf.Max(sunLight, artificialLight);
            if (combined < RoverDustRuntimeConfig.MinCombinedLight)
                return 0f;

            return Mathf.Clamp01(combined);
        }

        private static float EvaluateSunLighting(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal)
        {
            Vector3 safeNormal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;

            float directionalBest = 0f;
            for (int i = 0; i < sharedSceneLights.Count; i++)
            {
                float strength = EvaluateSingleDirectionalSunLight(sharedSceneLights[i], safeNormal);
                if (strength > directionalBest)
                    directionalBest = strength;
            }

            float geometricSun = 0f;
            Vector3 sunDirection;
            if (KerbalFxSunLight.TryGetSunDirection(worldPoint, out sunDirection))
            {
                float sunDot = Vector3.Dot(safeNormal, sunDirection);
                if (sunDot > 0f)
                    geometricSun = Mathf.Lerp(0.20f, 1f, Mathf.Clamp01(sunDot));
            }

            bool isDayAtPoint = geometricSun > 0.001f;
            float best = Mathf.Max(directionalBest, geometricSun);

            if (best <= 0.01f && vessel != null && vessel.directSunlight)
            {
                best = 0.90f;
                isDayAtPoint = true;
            }

            if (!isDayAtPoint)
                return 0f;

            if (vessel != null && !vessel.directSunlight)
            {
                float shadowed = best * RoverDustRuntimeConfig.ShadowLightFactor;
                float cloudyDayFloor = geometricSun * 0.22f;
                best = Mathf.Max(shadowed, cloudyDayFloor);
            }

            return Mathf.Clamp01(best);
        }

        private static float EvaluateNearbyArtificialLights(Vector3 worldPoint, Vector3 surfaceNormal)
        {
            float best = 0f;
            for (int i = 0; i < sharedSceneLights.Count; i++)
            {
                float strength = EvaluateSingleArtificialLight(sharedSceneLights[i], worldPoint, surfaceNormal);
                if (strength > best)
                    best = strength;
            }
            return Mathf.Clamp01(best);
        }

        private static float EvaluateSingleDirectionalSunLight(Light light, Vector3 surfaceNormal)
        {
            if (light == null || !light.enabled || light.intensity <= 0.03f || !light.gameObject.activeInHierarchy)
                return 0f;
            if (light.type != LightType.Directional || light.transform == null)
                return 0f;

            Vector3 toLightDir = (-light.transform.forward).normalized;
            float normalDot = Mathf.Clamp01(Vector3.Dot(surfaceNormal.normalized, toLightDir));
            float strength = light.intensity * Mathf.Lerp(0.35f, 1f, normalDot);
            return Mathf.Clamp01(strength / 1.05f);
        }

        private static void RefreshSharedSceneLights(Vessel vessel)
        {
            Guid vesselId = vessel != null ? vessel.id : Guid.Empty;
            int frame = Time.frameCount;
            if (sharedSceneLightsRefreshFrame == frame && vesselId == sharedSceneLightsVesselId)
                return;
            sharedSceneLightsRefreshFrame = frame;

            if (vesselId != sharedSceneLightsVesselId)
            {
                sharedSceneLightsVesselId = vesselId;
                sharedSceneLightsRefreshAt = 0f;
            }

            if (!RoverDustConfig.UseLightAware)
            {
                if (sharedSceneLights.Count > 0)
                    sharedSceneLights.Clear();
                sharedSceneLightsRefreshAt = 0f;
                return;
            }

            if (Time.time < sharedSceneLightsRefreshAt)
                return;

            sharedSceneLights.Clear();

            CollectSunDirectionalLight();
            CollectVesselPartLights(vessel);

            sharedSceneLightsRefreshAt = Time.time + SceneLightsRefreshPeriod;
        }

        private static void CollectSunDirectionalLight()
        {
            if (cachedSunLight != null && cachedSunLight.enabled && cachedSunLight.gameObject.activeInHierarchy)
            {
                sharedSceneLights.Add(cachedSunLight);
                return;
            }

            cachedSunLight = null;
            if (!sunLightSearched)
            {
                sunLightSearched = true;
                GameObject sunObj = GameObject.Find("SunLight");
                if (sunObj != null)
                {
                    Light l = sunObj.GetComponent<Light>();
                    if (l != null && l.type == LightType.Directional)
                    {
                        cachedSunLight = l;
                    }
                }
            }
            if (cachedSunLight != null && cachedSunLight.enabled && cachedSunLight.gameObject.activeInHierarchy)
            {
                sharedSceneLights.Add(cachedSunLight);
                return;
            }

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Light[] nearbyLights = mainCam.GetComponentsInChildren<Light>(false);
                if (nearbyLights != null)
                {
                    for (int i = 0; i < nearbyLights.Length; i++)
                    {
                        Light l = nearbyLights[i];
                        if (l != null && l.enabled && l.type == LightType.Directional && l.gameObject.activeInHierarchy)
                        {
                            cachedSunLight = l;
                            sharedSceneLights.Add(l);
                            return;
                        }
                    }
                }
            }
        }

        private static void CollectVesselPartLights(Vessel vessel)
        {
            if (vessel == null || !vessel.loaded || vessel.parts == null)
                return;
            for (int p = 0; p < vessel.parts.Count; p++)
            {
                Part part = vessel.parts[p];
                if (part == null) continue;
                Light[] partLights = part.GetComponentsInChildren<Light>(false);
                if (partLights == null) continue;
                for (int i = 0; i < partLights.Length; i++)
                {
                    Light light = partLights[i];
                    if (light == null || !light.enabled || !light.gameObject.activeInHierarchy)
                        continue;
                    if ((light.type == LightType.Point || light.type == LightType.Spot)
                        && light.intensity > 0.01f && light.range > 0.50f)
                    {
                        sharedSceneLights.Add(light);
                    }
                }
            }
        }

        private static float EvaluateSingleArtificialLight(Light light, Vector3 worldPoint, Vector3 surfaceNormal)
        {
            if (light == null || !light.enabled || light.intensity <= 0.01f || !light.gameObject.activeInHierarchy)
                return 0f;
            if ((light.type != LightType.Point && light.type != LightType.Spot) || light.transform == null)
                return 0f;
            if (!IsLikelyIntentionalLampLight(light))
                return 0f;

            float range = light.range;
            if (range <= 0.01f)
                return 0f;

            Vector3 pointToLight = light.transform.position - worldPoint;
            float distance = pointToLight.magnitude;
            if (distance > range)
                return 0f;

            Vector3 toLightDir = distance > 0.001f ? pointToLight / distance : Vector3.up;
            float attenuation = Mathf.Clamp01(1f - distance / range);
            attenuation *= attenuation;

            float spotFactor = 1f;
            if (light.type == LightType.Spot)
            {
                float cosLimit = Mathf.Cos(light.spotAngle * 0.5f * Mathf.Deg2Rad);
                float cosAngle = Vector3.Dot(light.transform.forward, -toLightDir);
                if (cosAngle <= cosLimit)
                    return 0f;
                spotFactor = Mathf.InverseLerp(cosLimit, 1f, cosAngle);
            }

            float normalDot = Mathf.Clamp01(Vector3.Dot(surfaceNormal.normalized, toLightDir));
            float normalFactor = Mathf.Lerp(0.40f, 1f, normalDot);

            float strength = light.intensity * attenuation * spotFactor * normalFactor;
            return Mathf.Clamp01(strength / 1.45f);
        }

        private static bool IsLikelyIntentionalLampLight(Light light)
        {
            if (light == null || light.transform == null)
                return false;

            bool fromPart = light.GetComponentInParent<Part>() != null;
            if (fromPart)
            {
                if (light.range < 1.20f || light.intensity < 0.10f)
                    return false;
                if (KerbalFxUtil.ContainsAnyToken(light.name, LampTokens)
                    || KerbalFxUtil.ContainsAnyTokenInHierarchy(light.transform, LampTokens, 12))
                    return true;
                return light.range <= 85f && light.intensity <= 8.5f;
            }

            if (light.range > 120f || light.intensity < 0.35f)
                return false;
            return KerbalFxUtil.ContainsAnyToken(light.name, LampTokens)
                || KerbalFxUtil.ContainsAnyTokenInHierarchy(light.transform, LampTokens, 12);
        }

        private static Vector3 GetStableGroundNormal(Vessel vessel, Vector3 worldPoint, Vector3 hitNormal)
        {
            Vector3 normal = hitNormal;
            if (normal.sqrMagnitude < 0.0001f)
                normal = Vector3.up;
            normal.Normalize();

            if (vessel != null && vessel.mainBody != null)
            {
                Vector3 bodyUp = worldPoint - vessel.mainBody.position;
                if (bodyUp.sqrMagnitude > 0.0001f)
                {
                    bodyUp.Normalize();
                    normal = Vector3.Slerp(normal, bodyUp, 0.70f);
                }
            }

            return normal.normalized;
        }

        private static float GetWheelDustRateScale(float effectiveRadius)
        {
            float normalized = Mathf.Clamp(effectiveRadius / 0.30f, 0.9f, 3.8f);
            float baseScale = Mathf.Pow(normalized, RoverDustRuntimeConfig.WheelBoostPower);
            float amplifiedScale = 1f + (baseScale - 1f) * 1.25f;
            return Mathf.Clamp(amplifiedScale, 1f, RoverDustRuntimeConfig.WheelBoostMax * 1.25f);
        }

        private static float GetWheelVisualScale(float visualNorm)
        {
            return Mathf.Clamp(Mathf.Lerp(0.76f, 3.20f, visualNorm), 0.75f, 3.25f);
        }

        private float GetWheelClusterRateScale()
        {
            return Mathf.Lerp(1f, 1.42f, Mathf.InverseLerp(1f, 6f, wheelGroupCount));
        }

        private float GetWheelVisualRateScale()
        {
            return Mathf.Lerp(1f, wheelVisualFootprintScale, 0.45f);
        }

        private static int GetActiveWheelColliderCount(WheelCollider[] colliders)
        {
            if (colliders == null || colliders.Length == 0)
                return 1;

            int count = 0;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    count++;
            }
            return Mathf.Max(1, count);
        }

        private static Vector3 GetWheelGroupCenter(Part sourcePart, WheelCollider[] colliders)
        {
            if (sourcePart == null)
                return Vector3.zero;

            if (colliders == null || colliders.Length == 0)
                return sourcePart.transform.position;

            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < colliders.Length; i++)
            {
                WheelCollider collider = colliders[i];
                if (collider == null || collider.transform == null)
                    continue;
                sum += collider.transform.position;
                count++;
            }

            return count > 0 ? sum / count : sourcePart.transform.position;
        }

        private static float EstimateWheelVisualFootprintScale(Part sourcePart, WheelCollider[] colliders, float effectiveRadius, out float visualRadius)
        {
            visualRadius = 0f;
            if (sourcePart == null || effectiveRadius <= 0.05f)
                return 1f;
            if (!SupportsWheelVisualFootprint(sourcePart))
                return 1f;

            Vector3 groupCenter = GetWheelGroupCenter(sourcePart, colliders);
            visualRadius = EstimateWheelModuleVisualRadius(sourcePart, groupCenter);
            if (visualRadius <= effectiveRadius * 1.12f)
                return 1f;

            float ratio = Mathf.Clamp(visualRadius / effectiveRadius, 1f, 2.45f);
            return Mathf.Clamp(Mathf.Lerp(1f, ratio, 0.65f), 1f, 2.0f);
        }

        private static float EstimateWheelModuleVisualRadius(Part sourcePart, Vector3 groupCenter)
        {
            List<Transform> wheelTransforms = new List<Transform>();
            CollectWheelVisualTransforms(sourcePart, wheelTransforms);
            if (wheelTransforms.Count == 0)
                return 0f;

            float best = 0f;
            for (int i = 0; i < wheelTransforms.Count; i++)
            {
                Transform wheelTransform = wheelTransforms[i];
                if (wheelTransform == null)
                    continue;

                Renderer[] renderers = wheelTransform.GetComponentsInChildren<Renderer>(true);
                if (renderers == null || renderers.Length == 0)
                    continue;

                Bounds bounds = new Bounds();
                bool initialized = false;
                for (int r = 0; r < renderers.Length; r++)
                {
                    Renderer renderer = renderers[r];
                    if (renderer == null || renderer is ParticleSystemRenderer)
                        continue;

                    Bounds rendererBounds = renderer.bounds;
                    float extent = Mathf.Max(rendererBounds.extents.x, Mathf.Max(rendererBounds.extents.y, rendererBounds.extents.z));
                    if (extent < 0.03f || extent > 4.5f)
                        continue;

                    if (!initialized)
                    {
                        bounds = rendererBounds;
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(rendererBounds);
                    }
                }

                if (!initialized)
                    continue;

                float distancePenalty = Mathf.Clamp01(Vector3.Distance(bounds.center, groupCenter) / 3.0f);
                float visualRadius = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
                visualRadius *= Mathf.Lerp(1f, 0.65f, distancePenalty);
                if (visualRadius > best)
                    best = visualRadius;
            }

            return best;
        }

        private static bool SupportsWheelVisualFootprint(Part sourcePart)
        {
            if (sourcePart == null || sourcePart.Modules == null)
                return false;

            for (int i = 0; i < sourcePart.Modules.Count; i++)
            {
                PartModule module = sourcePart.Modules[i];
                if (!KerbalFxUtil.ModuleNameMatches(module, "ModuleWheelBase"))
                    continue;

                string wheelType = ReadMemberString(module, "wheelType");
                if (string.Equals(wheelType, "FREE", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void CollectWheelVisualTransforms(Part sourcePart, List<Transform> target)
        {
            if (sourcePart == null || sourcePart.Modules == null || target == null)
                return;

            for (int i = 0; i < sourcePart.Modules.Count; i++)
            {
                PartModule module = sourcePart.Modules[i];
                if (!KerbalFxUtil.ModuleNameMatches(module, "ModuleWheelBase"))
                    continue;
                string wheelType = ReadMemberString(module, "wheelType");
                if (!string.Equals(wheelType, "FREE", StringComparison.OrdinalIgnoreCase))
                    continue;

                Transform runtimeTransform = ReadMemberTransform(module, "wheelTransform");
                AddUniqueTransform(target, runtimeTransform);

                string wheelTransformName = ReadMemberString(module, "wheelTransformName");
                if (string.IsNullOrEmpty(wheelTransformName))
                    continue;

                Transform[] found = sourcePart.FindModelTransforms(wheelTransformName);
                if (found == null)
                    continue;

                for (int t = 0; t < found.Length; t++)
                    AddUniqueTransform(target, found[t]);
            }
        }

        private static void AddUniqueTransform(List<Transform> target, Transform transform)
        {
            if (target == null || transform == null)
                return;

            for (int i = 0; i < target.Count; i++)
            {
                if (target[i] == transform)
                    return;
            }

            target.Add(transform);
        }

        private static string ReadMemberString(object target, string memberName)
        {
            object value = ReadMemberValue(target, memberName);
            return value != null ? value.ToString() : string.Empty;
        }

        private static Transform ReadMemberTransform(object target, string memberName)
        {
            return ReadMemberValue(target, memberName) as Transform;
        }

        private static object ReadMemberValue(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
                return null;

            Type type = target.GetType();
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                FieldInfo field = type.GetField(memberName, Flags);
                if (field != null)
                    return field.GetValue(target);

                PropertyInfo property = type.GetProperty(memberName, Flags);
                if (property != null)
                    return property.GetValue(target, null);
            }
            catch
            {
            }

            return null;
        }

        private Vector3 GetDustForward(Vessel vessel, Vector3 stableNormal, Vector3 wheelForward)
        {
            Vector3 tangent = Vector3.zero;
            if (vessel != null)
                tangent = Vector3.ProjectOnPlane((Vector3)vessel.srf_velocity, stableNormal);
            if (tangent.sqrMagnitude > 0.04f)
                return (-tangent).normalized;

            tangent = Vector3.ProjectOnPlane(wheelForward, stableNormal);
            if (tangent.sqrMagnitude > 0.0001f)
                return (-tangent).normalized;

            tangent = part != null ? Vector3.ProjectOnPlane(part.transform.forward, stableNormal) : Vector3.forward;
            if (tangent.sqrMagnitude > 0.0001f)
                return tangent.normalized;

            return Vector3.forward;
        }

        private bool TryGetWheelGroupHit(out WheelHit representativeHit, out Vector3 averagePoint, out Vector3 averageNormal, out float averageSlip)
        {
            representativeHit = default(WheelHit);
            averagePoint = Vector3.zero;
            averageNormal = Vector3.zero;
            averageSlip = 0f;

            int hitCount = 0;
            for (int i = 0; i < wheels.Length; i++)
            {
                WheelCollider collider = wheels[i];
                if (collider == null)
                    continue;

                WheelHit hit;
                if (!collider.GetGroundHit(out hit) || hit.collider == null)
                    continue;

                if (hitCount == 0)
                    representativeHit = hit;

                averagePoint += hit.point;
                averageNormal += hit.normal;
                averageSlip += Mathf.Abs(hit.forwardSlip) + Mathf.Abs(hit.sidewaysSlip);
                hitCount++;
            }

            if (hitCount <= 0)
                return false;

            averagePoint /= hitCount;
            averageNormal = hitCount > 0 ? (averageNormal / hitCount).normalized : Vector3.up;
            averageSlip /= hitCount;
            return true;
        }

        private static float GetEffectiveWheelGroupRadius(WheelCollider[] colliders)
        {
            if (colliders == null || colliders.Length == 0)
                return 0.35f;

            float radius = 0.05f;
            for (int i = 0; i < colliders.Length; i++)
            {
                WheelCollider collider = colliders[i];
                if (collider == null)
                    continue;
                radius = Mathf.Max(radius, GetSingleWheelRadius(collider));
            }

            return Mathf.Clamp(radius, 0.05f, 2.4f);
        }

        private static float GetSingleWheelRadius(WheelCollider wheelCollider)
        {
            if (wheelCollider == null)
                return 0.35f;

            float radius = Mathf.Max(0.05f, wheelCollider.radius);
            if (wheelCollider.transform != null)
            {
                Vector3 scale = wheelCollider.transform.lossyScale;
                float axisScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                if (axisScale > 0.001f)
                    radius = Mathf.Max(radius, wheelCollider.radius * axisScale);
            }

            return Mathf.Clamp(radius, 0.05f, 2.4f);
        }

        private void UpdateSurfaceColor(Vessel vessel, WheelHit hit)
        {
            if (!RoverDustConfig.AdaptSurfaceColor)
            {
                ApplyColor(KerbalFxSurfaceColor.DefaultDustColor);
                return;
            }

            if (colorInitialized && IsSameSurfaceSignature(vessel, hit.collider))
                return;
            UpdateSurfaceSignature(vessel, hit.collider);

            Color baseColor = KerbalFxSurfaceColor.GetBaseDustColor(vessel);
            Color newColor = baseColor;
            Color colliderColor;
            if (KerbalFxSurfaceColor.TryGetColliderColor(hit.collider, out colliderColor))
                newColor = KerbalFxSurfaceColor.BlendWithColliderColor(baseColor, colliderColor);

            ApplyColor(newColor);
        }

        private void ApplyColor(Color color)
        {
            Color target = new Color(
                Mathf.Clamp01(color.r),
                Mathf.Clamp01(color.g),
                Mathf.Clamp01(color.b),
                1f);

            target = KerbalFxSurfaceColor.NormalizeDustTone(target);
            currentColor = colorInitialized ? Color.Lerp(currentColor, target, 0.45f) : target;
            colorInitialized = true;
            ApplyCurrentStartColor();
        }

        private static bool ShouldSuppressDustSurface(Collider collider, out string reason)
        {
            reason = string.Empty;
            if (collider == null)
                return false;

            if (KerbalFxUtil.ContainsAnyToken(collider.name, KscSurfaceTokens)
                || KerbalFxUtil.ContainsAnyToken(collider.gameObject != null ? collider.gameObject.name : string.Empty, KscSurfaceTokens)
                || KerbalFxUtil.ContainsAnyTokenInHierarchy(collider.transform, KscSurfaceTokens, 14))
            {
                reason = "KSC_Surface";
                return true;
            }

            Renderer renderer = collider.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                if (KerbalFxUtil.ContainsAnyToken(renderer.sharedMaterial.name, KscMaterialTokens))
                {
                    reason = "KSC_Material";
                    return true;
                }
            }

            if (IsKerbalKonstructsStatic(collider))
            {
                reason = "KerbalKonstructs_Static";
                return true;
            }

            return false;
        }

        private static bool IsKerbalKonstructsStatic(Collider collider)
        {
            if (collider == null)
                return false;

            Transform t = collider.transform;
            int depth = 0;
            while (t != null && depth < 10)
            {
                sharedComponentBuffer.Clear();
                t.GetComponents<Component>(sharedComponentBuffer);
                if (ContainsAnyTokenInTypes(sharedComponentBuffer, KerbalKonstructsTokens))
                    return true;
                t = t.parent;
                depth++;
            }
            return false;
        }

        private static bool ContainsAnyTokenInTypes(List<Component> components, string[] tokens)
        {
            if (components == null || tokens == null)
                return false;

            for (int i = 0; i < components.Count; i++)
            {
                Component c = components[i];
                if (c == null)
                    continue;
                Type type = c.GetType();
                if (type == null)
                    continue;

                string fullName = type.FullName;
                if (!string.IsNullOrEmpty(fullName) && KerbalFxUtil.ContainsAnyToken(fullName, tokens))
                    return true;
                if (KerbalFxUtil.ContainsAnyToken(type.Name, tokens))
                    return true;
            }
            return false;
        }

        private bool IsSameSurfaceSignature(Vessel vessel, Collider collider)
        {
            int colliderId = collider != null ? collider.GetInstanceID() : 0;
            string body = vessel.mainBody != null ? vessel.mainBody.bodyName : "UnknownBody";
            string biome = string.IsNullOrEmpty(vessel.landedAt) ? "UnknownBiome" : vessel.landedAt;
            return colliderId == lastSurfaceColliderId
                && string.Equals(body, lastSurfaceBody, StringComparison.Ordinal)
                && string.Equals(biome, lastSurfaceBiome, StringComparison.Ordinal);
        }

        private void UpdateSurfaceSignature(Vessel vessel, Collider collider)
        {
            lastSurfaceColliderId = collider != null ? collider.GetInstanceID() : 0;
            lastSurfaceBody = vessel.mainBody != null ? vessel.mainBody.bodyName : "UnknownBody";
            lastSurfaceBiome = string.IsNullOrEmpty(vessel.landedAt) ? "UnknownBiome" : vessel.landedAt;
        }

        private static float GetBodyDustVisibilityMultiplier(Vessel vessel)
        {
            if (vessel == null || vessel.mainBody == null || string.IsNullOrEmpty(vessel.mainBody.bodyName))
                return 1f;
            return RoverDustRuntimeConfig.GetBodyVisibilityMultiplier(vessel.mainBody.bodyName);
        }
    }
}
