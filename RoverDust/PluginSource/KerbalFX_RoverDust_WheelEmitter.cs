using System;
using System.Collections.Generic;
using System.Globalization;
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

        private readonly KerbalFxSurfaceColorSampler surfaceColor = new KerbalFxSurfaceColorSampler(
            KerbalFxDustVisualDefaults.Color,
            SurfaceColorBlendStrength,
            SurfaceColorRefreshSeconds,
            SurfaceColorSmoothingSpeed,
            SurfaceColorAllowRendererFallback,
            RoverDustRuntimeConfig.TintProfile);
        private readonly KerbalFxLightAwareSampler lightAware = new KerbalFxLightAwareSampler(
            LightAwareRefreshSeconds,
            LightAwareSmoothingSpeed,
            LightAwareUseShadowProbe,
            LightAwareSampleLocalLights);
        private float smoothedRate;
        private float debugTimer;
        private float surfaceColorDebugTimer;
        private float lightAwareDebugTimer;
        private float wheelDustRateScale = 1f;
        private float wheelEffectiveRadius = 0.35f;
        private float wheelVisualScaleDebug = 1f;
        private float wheelVisualFootprintScale = 1f;
        private float wheelVisualFootprintRadius;
        private float continuityDistanceRemainder;
        private Vector3 lastContinuityPoint;
        private bool advancedQualityFeatures;
        private bool disposed;
        private bool lastSuppressed;
        private bool hasLastContinuityPoint;
        private KerbalFxRevisionStamp appliedProfileRevision;
        private KerbalFxLightAwareEntry cachedLightAwareEntry;
        private string cachedLightAwareBodyName = string.Empty;
        private bool hasCachedLightAwareEntry;
        private Color lastAppliedStartColor;
        private bool hasAppliedStartColor;
        private string lastSuppressionKey = string.Empty;

        private const float BaseDustAlpha = 0.76f;
        private const float MinDustSpeed = 0.7f;
        private const float SlipBoostScale = 2.4f;
        private const float BaseRateMin = 120f;
        private const float BaseRateRange = 480f;
        private const float DebugLogInterval = 1.2f;
        private const float SurfaceColorDebugInterval = 1.5f;
        private const float RateSmoothingSpeed = 6.5f;
        private const float RatePlayThreshold = 0.18f;
        private const float SurfaceColorBlendStrength = 0.55f;
        private const float SurfaceColorRefreshSeconds = 0.40f;
        private const float SurfaceColorSmoothingSpeed = 1.4f;
        private const bool SurfaceColorAllowRendererFallback = true;
        private const float LightAwareRefreshSeconds = 0.25f;
        private const float LightAwareSmoothingSpeed = 1.2f;
        private const bool LightAwareUseShadowProbe = true;
        private const bool LightAwareSampleLocalLights = true;
        private const float LightAwareWheelStrength = 1.00f;
        private const float LightAwareDebugInterval = 1.6f;
        private const float StartColorApplyEpsilon = 0.004f;
        private static Gradient cachedFadeGradient;
        private static AnimationCurve cachedSizeCurve;
        private static float cachedGradientQualityNorm = -1f;
        private static float cachedSizeCurveQuality = -1f;

        public WheelDustEmitter(Part part, WheelCollider[] wheels, float vesselEmitterBudgetScale, int emitterIndex)
        {
            this.part = part;
            this.wheels = wheels ?? new WheelCollider[0];
            this.vesselEmitterBudgetScale = Mathf.Clamp(vesselEmitterBudgetScale, 0.40f, 1f);
            wheelGroupCount = GetActiveWheelColliderCount(this.wheels);
            debugId = BuildDebugId(part, emitterIndex);

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

            Collider surfaceCollider;
            Vector3 hitPoint;
            Vector3 hitNormal;
            Vector3 hitForward;
            float slip;
            bool hasHit = TryGetWheelGroupHit(vessel, out surfaceCollider, out hitPoint, out hitNormal, out hitForward, out slip);
            if (!hasHit)
            {
                ResetContinuityTrail();
                SetTargetRate(0f, dt);
                LogEmitterSkip(vessel, dt, "noWheelGroundHit");
                return;
            }

            string suppressionKey;
            bool suppressed = RoverDustSurfaceRules.ShouldSuppressSurface(surfaceCollider, out suppressionKey);
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

            UpdateSurfaceColor(vessel, hitPoint, surfaceCollider, dt);
            UpdateLightAware(vessel, hitPoint, hitNormal, dt);
            RefreshDynamicStartColor();
            LogSurfaceSampleIfNeeded(vessel, dt);
            LogLightSampleIfNeeded(vessel, dt);

            float speed = Mathf.Abs((float)vessel.srfSpeed);
            float speedFactor = Mathf.InverseLerp(MinDustSpeed, 20f, speed);
            float slipBoost = Mathf.Clamp01(slip * SlipBoostScale);

            float quality = RoverDustConfig.QualityPercent / 100f;
            float qualityRateScale = 1f + (Mathf.Pow(quality, 1.60f) - 1f) * 0.75f;
            float bodyVisibility = GetBodyDustVisibilityMultiplier(vessel);
            float baseRate = (BaseRateMin + BaseRateRange * speedFactor) * (0.45f + 0.55f * slipBoost) * RoverDustRuntimeConfig.EmissionMultiplier;
            float targetRate = baseRate * qualityRateScale * wheelDustRateScale * bodyVisibility
                * GetWheelClusterRateScale() * GetWheelVisualRateScale() * vesselEmitterBudgetScale;
            targetRate *= GetLightAwareRateMultiplier();
            Vector3 stableNormal = GetStableGroundNormal(vessel, hitPoint, hitNormal);

            if (speed < MinDustSpeed)
            {
                targetRate = 0f;
                ResetContinuityTrail();
            }

            SetTargetRate(targetRate, dt);
            Vector3 emitPosition = hitPoint + stableNormal * 0.04f;
            root.transform.position = emitPosition;
            root.transform.rotation = Quaternion.LookRotation(GetDustForward(vessel, stableNormal, hitForward), stableNormal);
            EmitContinuityTrail(emitPosition, stableNormal, targetRate, speed, dt);

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
                        FormatDustColor()
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

        private void LogEmitterSkip(Vessel vessel, float dt, string reason)
        {
            if (!RoverDustConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel)
                return;

            debugTimer -= dt;
            if (debugTimer > 0f)
                return;

            debugTimer = DebugLogInterval;
            RoverDustLog.DebugLog(Localizer.Format(
                RoverDustLoc.LogEmitterSkip,
                debugId,
                reason
                    + " wheels=" + (wheels != null ? wheels.Length : 0).ToString(CultureInfo.InvariantCulture)
                    + " speed=" + (vessel != null ? vessel.srfSpeed.ToString("F2", CultureInfo.InvariantCulture) : "null")
                    + " landed=" + (vessel != null && vessel.Landed)
                    + " hTerrain=" + (vessel != null ? vessel.heightFromTerrain.ToString("F2", CultureInfo.InvariantCulture) : "null")));
        }

        private void ConfigureParticleSystemBase()
        {
            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            Color dustColor = KerbalFxDustVisualDefaults.Color;
            main.startColor = new Color(dustColor.r, dustColor.g, dustColor.b, BaseDustAlpha);

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
            ClearLightAwareEntryCache();
            hasAppliedStartColor = false;

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
                    new GradientAlphaKey(Mathf.Lerp(0.62f, 0.79f, qualityNorm), 0f),
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

        private void ApplyCurrentStartColor()
        {
            ApplyDynamicStartColor(true);
        }

        private void RefreshDynamicStartColor()
        {
            ApplyDynamicStartColor(false);
        }

        private void ApplyDynamicStartColor(bool force)
        {
            if (particleSystem == null)
                return;

            Color dustColor = GetCurrentDustColor();
            Color startColor = new Color(dustColor.r, dustColor.g, dustColor.b, ComputeStartAlpha());
            if (!force && hasAppliedStartColor && IsStartColorClose(startColor, lastAppliedStartColor))
                return;

            ParticleSystem.MainModule main = particleSystem.main;
            main.startColor = startColor;
            lastAppliedStartColor = startColor;
            hasAppliedStartColor = true;
        }

        private static bool IsStartColorClose(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) <= StartColorApplyEpsilon
                && Mathf.Abs(a.g - b.g) <= StartColorApplyEpsilon
                && Mathf.Abs(a.b - b.b) <= StartColorApplyEpsilon
                && Mathf.Abs(a.a - b.a) <= StartColorApplyEpsilon;
        }

        private float ComputeStartAlpha()
        {
            float alpha = BaseDustAlpha;
            float bodyVisibility = GetBodyDustVisibilityMultiplier(part != null ? part.vessel : null);
            float bodyBoostNorm = Mathf.Clamp01((bodyVisibility - 1f) / 0.5f);
            alpha *= Mathf.Lerp(1f, 1.16f, bodyBoostNorm);
            alpha *= GetLightAwareVisualAlphaMultiplier();
            return Mathf.Clamp(alpha, 0f, 0.97f);
        }

        private float GetLightAwareAlphaMultiplier()
        {
            if (!RoverDustConfig.UseLightAware)
                return 1f;
            return lightAware.GetAlphaMultiplier(
                GetLightAwareEntry(),
                LightAwareWheelStrength);
        }

        private float GetLightAwareVisualAlphaMultiplier()
        {
            return KerbalFxLightingCore.ApplySurfaceBoost(GetLightAwareAlphaMultiplier());
        }

        private float GetLightAwareRateMultiplier()
        {
            return KerbalFxLightingCore.ApplyRateCap(GetLightAwareVisualAlphaMultiplier());
        }

        private Color GetCurrentDustColor()
        {
            Color baseColor = RoverDustConfig.AdaptSurfaceColor ? surfaceColor.CurrentColor : KerbalFxDustVisualDefaults.Color;
            if (!RoverDustConfig.UseLightAware)
                return baseColor;
            return lightAware.ApplyColorTint(
                baseColor,
                GetLightAwareEntry(),
                LightAwareWheelStrength);
        }

        private KerbalFxLightAwareEntry GetLightAwareEntry()
        {
            string bodyName = part != null && part.vessel != null && part.vessel.mainBody != null
                ? part.vessel.mainBody.bodyName
                : string.Empty;
            if (!hasCachedLightAwareEntry || bodyName != cachedLightAwareBodyName)
            {
                cachedLightAwareBodyName = bodyName;
                cachedLightAwareEntry = RoverDustRuntimeConfig.LightAwareProfile.Get(bodyName);
                hasCachedLightAwareEntry = true;
            }
            return cachedLightAwareEntry;
        }

        private void ClearLightAwareEntryCache()
        {
            cachedLightAwareBodyName = string.Empty;
            cachedLightAwareEntry = KerbalFxLightAwareEntry.Default;
            hasCachedLightAwareEntry = false;
        }

        private void UpdateSurfaceColor(Vessel vessel, Vector3 hitPoint, Collider surfaceCollider, float dt)
        {
            if (!RoverDustConfig.AdaptSurfaceColor)
            {
                surfaceColor.Reset();
                return;
            }

            surfaceColor.Tick(vessel, hitPoint, surfaceCollider, dt);
        }

        private void UpdateLightAware(Vessel vessel, Vector3 hitPoint, Vector3 hitNormal, float dt)
        {
            if (!RoverDustConfig.UseLightAware)
            {
                if (!lightAware.IsReset)
                {
                    lightAware.Reset();
                    ClearLightAwareEntryCache();
                }
                return;
            }

            lightAware.Tick(vessel, hitPoint, hitNormal, dt);
        }

        private void LogLightSampleIfNeeded(Vessel vessel, float dt)
        {
            if (!RoverDustConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel || !RoverDustConfig.UseLightAware)
                return;

            float multiplier = GetLightAwareAlphaMultiplier();
            KerbalFxLightDebugReporter.Report("RoverDust", debugId, lightAware.Current, multiplier, LightAwareWheelStrength);

            lightAwareDebugTimer -= dt;
            if (lightAwareDebugTimer > 0f)
                return;

            lightAwareDebugTimer = LightAwareDebugInterval;
            RoverDustLog.DebugLog(Localizer.Format(
                RoverDustLoc.LogLightSample,
                debugId,
                KerbalFxLightFormat.Describe(lightAware.Current, string.Empty),
                multiplier.ToString("F2", CultureInfo.InvariantCulture)));
        }

        private void LogSurfaceSampleIfNeeded(Vessel vessel, float dt)
        {
            if (!RoverDustConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel || !RoverDustConfig.AdaptSurfaceColor)
                return;

            surfaceColorDebugTimer -= dt;
            if (surfaceColorDebugTimer > 0f)
                return;

            surfaceColorDebugTimer = SurfaceColorDebugInterval;
            RoverDustLog.DebugLog(Localizer.Format(
                RoverDustLoc.LogSurfaceSample,
                debugId,
                string.IsNullOrEmpty(surfaceColor.LastBodyName) ? "n/a" : surfaceColor.LastBodyName,
                string.IsNullOrEmpty(surfaceColor.LastBiomeName) ? "n/a" : surfaceColor.LastBiomeName,
                surfaceColor.LastSource.ToString(),
                KerbalFxSurfaceColorCore.FormatColor(surfaceColor.LastRawSample),
                KerbalFxSurfaceColorCore.FormatColor(surfaceColor.TargetColor),
                KerbalFxSurfaceColorCore.FormatColor(surfaceColor.CurrentColor)));
        }

        private string FormatDustColor()
        {
            return KerbalFxSurfaceColorCore.FormatColor(GetCurrentDustColor());
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

                string wheelType = KerbalFxUtil.ReadMemberString(module, "wheelType");
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
                string wheelType = KerbalFxUtil.ReadMemberString(module, "wheelType");
                if (!string.Equals(wheelType, "FREE", StringComparison.OrdinalIgnoreCase))
                    continue;

                Transform runtimeTransform = KerbalFxUtil.ReadMemberTransform(module, "wheelTransform");
                AddUniqueTransform(target, runtimeTransform);

                string wheelTransformName = KerbalFxUtil.ReadMemberString(module, "wheelTransformName");
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

        private bool TryGetWheelGroupHit(Vessel vessel, out Collider surfaceCollider, out Vector3 averagePoint, out Vector3 averageNormal, out Vector3 averageForward, out float averageSlip)
        {
            surfaceCollider = null;
            averagePoint = Vector3.zero;
            averageNormal = Vector3.zero;
            averageForward = Vector3.zero;
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
                    surfaceCollider = hit.collider;

                averagePoint += hit.point;
                averageNormal += hit.normal;
                averageForward += hit.forwardDir;
                averageSlip += Mathf.Abs(hit.forwardSlip) + Mathf.Abs(hit.sidewaysSlip);
                hitCount++;
            }

            if (hitCount > 0)
            {
                averagePoint /= hitCount;
                averageNormal = (averageNormal / hitCount).normalized;
                averageForward = GetSafeForward(averageForward / hitCount, averageNormal);
                averageSlip /= hitCount;
                return true;
            }

            return false;
        }

        private static Vector3 GetSafeForward(Vector3 forward, Vector3 normal)
        {
            Vector3 projected = Vector3.ProjectOnPlane(forward, normal);
            if (projected.sqrMagnitude > 0.0001f)
                return projected.normalized;
            projected = Vector3.ProjectOnPlane(Vector3.forward, normal);
            if (projected.sqrMagnitude > 0.0001f)
                return projected.normalized;
            return Vector3.right;
        }

        private static string BuildDebugId(Part sourcePart, int emitterIndex)
        {
            string partName = "unknownPart";
            int instanceId = 0;
            if (sourcePart != null)
            {
                partName = sourcePart.partInfo != null ? sourcePart.partInfo.name : sourcePart.name;
                instanceId = sourcePart.GetInstanceID();
            }

            return partName + "#" + instanceId.ToString(CultureInfo.InvariantCulture)
                + ":wheelGroup" + emitterIndex.ToString(CultureInfo.InvariantCulture);
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

        private static float GetBodyDustVisibilityMultiplier(Vessel vessel)
        {
            if (vessel == null || vessel.mainBody == null || string.IsNullOrEmpty(vessel.mainBody.bodyName))
                return 1f;
            return RoverDustRuntimeConfig.GetBodyVisibilityMultiplier(vessel.mainBody.bodyName);
        }
    }
}
