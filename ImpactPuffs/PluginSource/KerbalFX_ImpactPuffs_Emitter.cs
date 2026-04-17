using System;
using System.Collections.Generic;
using System.Globalization;
using KSP.Localization;
using UnityEngine;

namespace KerbalFX.ImpactPuffs
{
    internal sealed partial class EngineGroundPuffEmitter
    {
        private readonly Part part;
        private readonly ModuleEngines engine;
        private readonly List<Transform> thrustTransforms;
        private readonly GameObject root;
        private readonly ParticleSystem particleSystem;
        private readonly VolumetricPlumeField volumetricField;
        private readonly string debugId;

        private bool disposed;
        private float smoothedRate;
        private float profileRefreshTimer;
        private float colorRefreshTimer;
        private float debugTimer;
        private float suppressionLogTimer;
        private float groundDebugLogTimer;
        private float lastCenteredness;
        private Vector3 smoothedTangent;
        private Vector3 smoothedNormal;
        private float startupRampTimer;
        private float ignitionPrimeTimer;
        private bool wasEngineIgnited;

        private KerbalFxRevisionStamp appliedProfileRevision;
        private int engineClusterCount = 1;

        private Color currentColor = new Color(0.72f, 0.68f, 0.61f, 1f);
        private float cachedLightFactor = 1f;
        private float cachedBodyVisibility = 1f;

        private const float BaseAlpha = 0.60f;
        private const float StartupRampDuration = 0.25f;
        private const float IgnitionPrimeDuration = 1.20f;
        private const float TerrainGateMultiplier = 1.35f;
        private const float TerrainGateOffset = 6f;
        private const float MaxTargetRateSingle = 18000f;
        private const float MaxTargetRateMulti = 7500f;
        private const float SunOcclusionRayDistance = 1200f;
        private const float DebugEmitterInterval = 1.2f;
        private const float ColorRefreshInterval = 0.25f;
        private const float SuppressionLogInterval = 1.5f;
        private const float GroundDebugLogInterval = 0.35f;
        private const float ProfileRefreshInterval = 0.33f;
        private static readonly string[] LaunchsiteSurfaceTokens =
        {
            "launchpad",
            "launch_pad",
            "launch pad",
            "launchsite",
            "launch_site",
            "ksc",
            "runway"
        };
        private const int ScaledSceneryLayer = 10;
        private static readonly int TerrainRaycastMask = Physics.DefaultRaycastLayers & ~(1 << ScaledSceneryLayer);
        private static readonly RaycastHit[] SharedHits = new RaycastHit[24];
        private static readonly RaycastHit[] SunOcclusionHits = new RaycastHit[16];
        private static readonly Dictionary<Guid, SunOcclusionCacheEntry> SunOcclusionCache = new Dictionary<Guid, SunOcclusionCacheEntry>();
        private static readonly List<Guid> SunOcclusionRemoveIds = new List<Guid>(32);
        private static readonly AnimationCurve SharedSizeOverLifetimeCurve = CreateSizeOverLifetimeCurve();
        private const float SunOcclusionPurgeIntervalSeconds = 900f;
        private static float nextSunOcclusionPurgeAt;
        private static Gradient cachedColorOverLifetimeGradient;
        private static float cachedColorOverLifetimeAlpha = -1f;

        private struct SunOcclusionCacheEntry
        {
            public bool Occluded;
            public float ValidUntil;
            public Vector3 SamplePoint;
            public Vector3 SunDirection;
        }

        public static void CleanupSunOcclusionCache(bool force)
        {
            if (force)
            {
                SunOcclusionCache.Clear();
                SunOcclusionRemoveIds.Clear();
                nextSunOcclusionPurgeAt = Time.time + SunOcclusionPurgeIntervalSeconds;
                return;
            }

            float now = Time.time;
            if (now < nextSunOcclusionPurgeAt)
            {
                return;
            }

            nextSunOcclusionPurgeAt = now + SunOcclusionPurgeIntervalSeconds;
            if (SunOcclusionCache.Count == 0)
            {
                return;
            }

            SunOcclusionRemoveIds.Clear();
            var e = SunOcclusionCache.GetEnumerator();
            while (e.MoveNext())
            {
                SunOcclusionCacheEntry entry = e.Current.Value;
                bool cacheExpired = entry.ValidUntil + 2f < now;
                Vessel vessel = FlightGlobals.FindVessel(e.Current.Key);
                bool vesselInvalid = vessel == null || !vessel.loaded || vessel.packed;
                if (cacheExpired || vesselInvalid)
                {
                    SunOcclusionRemoveIds.Add(e.Current.Key);
                }
            }
            e.Dispose();

            for (int i = 0; i < SunOcclusionRemoveIds.Count; i++)
            {
                SunOcclusionCache.Remove(SunOcclusionRemoveIds[i]);
            }
        }

        public EngineGroundPuffEmitter(Part part, ModuleEngines engine, List<Transform> thrustTransforms)
        {
            this.part = part;
            this.engine = engine;
            this.thrustTransforms = CopyValidTransforms(thrustTransforms);

            string transformName = GetDebugTransformName(this.thrustTransforms);
            debugId = (part.partInfo != null ? part.partInfo.name : part.name) + ":" + transformName;

            root = new GameObject("KerbalFX_EngineGroundPuff");
            root.transform.SetParent(null, false);
            root.transform.position = GetThrustOrigin();
            root.layer = part.gameObject.layer;

            particleSystem = root.AddComponent<ParticleSystem>();
            ConfigureParticleSystemBase();
            ApplyRuntimeVisualProfile(true);
            volumetricField = new VolumetricPlumeField(root.transform, part.gameObject.layer);
        }

        public bool IsEngineActive
        {
            get { return engine != null && engine.EngineIgnited && !engine.flameout && engine.finalThrust > 0f; }
        }

        public ModuleEngines EngineModule
        {
            get { return engine; }
        }

        public void SetEngineClusterCount(int count)
        {
            engineClusterCount = Mathf.Max(1, count);
        }

        public void Tick(Vessel vessel, float dt)
        {
            if (disposed)
            {
                return;
            }

            if (part == null || engine == null)
            {
                StopAllEmission(dt, false);
                return;
            }

            RefreshRuntimeProfileIfNeeded(dt);

            if (ShouldSkipForVesselState(vessel, dt))
            {
                return;
            }

            float currentThrust;
            float normalizedThrust;
            if (!TryResolveThrustInputs(vessel, dt, out currentThrust, out normalizedThrust))
            {
                return;
            }

            RaycastHit groundHit;
            Vector3 exhaustDirection;
            float terrainHeight;
            float exhaustToBodyDown;
            float alignment;
            float distanceFactor;
            if (!TryResolveGroundInteraction(
                vessel,
                normalizedThrust,
                dt,
                out groundHit,
                out exhaustDirection,
                out terrainHeight,
                out exhaustToBodyDown,
                out alignment,
                out distanceFactor))
            {
                return;
            }

            float qualityNorm;
            float pressure;
            float thrustPowerNorm;
            float targetRate = ComputeTargetRate(
                vessel,
                currentThrust,
                normalizedThrust,
                distanceFactor,
                out qualityNorm,
                out pressure,
                out thrustPowerNorm);

            targetRate = ApplyLightAwarenessRate(vessel, groundHit, normalizedThrust, targetRate);

            Vector3 stableNormal;
            Vector3 outwardDirForCoreClamp;
            Vector3 finalPosition = ComputeEmitterPosition(
                vessel,
                groundHit,
                pressure,
                out stableNormal,
                out outwardDirForCoreClamp);

            root.transform.position = finalPosition;

            UpdateVolumetricFrame(vessel, finalPosition, stableNormal, outwardDirForCoreClamp, exhaustDirection, pressure, dt);

            colorRefreshTimer -= dt;
            if (colorRefreshTimer <= 0f)
            {
                colorRefreshTimer = ColorRefreshInterval;
                UpdateSurfaceColor(vessel, groundHit.collider);
                ApplyCurrentStartColor();
            }

            SetTargetRate(0f, dt);
            if (volumetricField != null)
            {
                volumetricField.Update(
                    root.transform.position,
                    root.transform.rotation,
                    targetRate,
                    pressure,
                    qualityNorm,
                    ImpactPuffsConfig.GetQualityScaleMultiplier(),
                    currentColor,
                    cachedLightFactor,
                    lastCenteredness,
                    engineClusterCount,
                    dt
                );
            }

            LogEmitterDebug(
                vessel,
                dt,
                normalizedThrust,
                groundHit.distance,
                targetRate,
                pressure,
                alignment,
                exhaustToBodyDown,
                terrainHeight,
                currentThrust);
        }

        private bool ShouldSkipForVesselState(Vessel vessel, float dt)
        {
            if (vessel == null || !vessel.loaded || vessel.packed || vessel.Splashed)
            {
                StopAllEmission(dt, false);
                return true;
            }

            if (vessel.situation == Vessel.Situations.PRELAUNCH)
            {
                StopAllEmission(dt, true);
                return true;
            }

            if (IsInKerbinLaunchsiteZone(vessel))
            {
                LogLaunchsiteSuppression("zone", vessel, null, dt);
                StopAllEmission(dt, true);
                return true;
            }

            return false;
        }

        private bool TryResolveThrustInputs(Vessel vessel, float dt, out float currentThrust, out float normalizedThrust)
        {
            currentThrust = 0f;
            normalizedThrust = 0f;

            bool engineIgnited = engine.EngineIgnited;
            if (engineIgnited && !wasEngineIgnited)
            {
                ResetGroundAnchorState(true);
                ignitionPrimeTimer = IgnitionPrimeDuration;
            }

            wasEngineIgnited = engineIgnited;
            ignitionPrimeTimer = Mathf.Max(0f, ignitionPrimeTimer - dt);
            if (!engineIgnited)
            {
                HardResetEmissionState();
                return false;
            }

            currentThrust = Mathf.Max(0f, (float)engine.finalThrust);
            normalizedThrust = GetNormalizedLiveThrust(engine);

            float throttleCommand = 0f;
            if (vessel != null && vessel.ctrlState != null)
            {
                throttleCommand = Mathf.Clamp01(vessel.ctrlState.mainThrottle);
            }

            if (ignitionPrimeTimer > 0f)
            {
                float primeT = ignitionPrimeTimer / IgnitionPrimeDuration;
                float ignitionAssist = throttleCommand * Mathf.Lerp(0.30f, 0.88f, primeT);
                normalizedThrust = Mathf.Max(normalizedThrust, ignitionAssist);
            }

            if (normalizedThrust < 0.005f)
            {
                StopAllEmission(dt, false);
                return false;
            }

            return true;
        }

        private bool TryResolveGroundInteraction(
            Vessel vessel,
            float normalizedThrust,
            float dt,
            out RaycastHit groundHit,
            out Vector3 exhaustDirection,
            out float terrainHeight,
            out float exhaustToBodyDown,
            out float alignment,
            out float distanceFactor)
        {
            groundHit = default(RaycastHit);
            exhaustDirection = Vector3.down;
            terrainHeight = -1f;
            exhaustToBodyDown = 0f;
            alignment = 0f;
            distanceFactor = 0f;
            int rawHitCount = 0;
            int rigidbodySkipped = 0;
            int partSkipped = 0;
            int normalSkipped = 0;

            float maxEffectiveDistance = Mathf.Lerp(
                ImpactPuffsRuntimeConfig.MaxDistanceAtLowThrust,
                ImpactPuffsRuntimeConfig.MaxDistanceAtHighThrust,
                Mathf.Clamp01(normalizedThrust)
            );
            maxEffectiveDistance = Mathf.Clamp(maxEffectiveDistance, 2f, ImpactPuffsRuntimeConfig.MaxRayDistance);

            terrainHeight = GetSafeTerrainHeightAgl(vessel);
            float terrainGate = maxEffectiveDistance * TerrainGateMultiplier + TerrainGateOffset;
            if (terrainHeight >= 0f && terrainHeight > terrainGate)
            {
                LogGroundProbe(vessel, dt, "terrain-gate", groundHit, false, maxEffectiveDistance, terrainHeight, exhaustToBodyDown, alignment, rawHitCount, rigidbodySkipped, partSkipped, normalSkipped);
                LoseGroundContact(dt, false);
                return false;
            }

            Vector3 origin = GetThrustOrigin();
            exhaustDirection = GetPrimaryExhaustDirection();

            Vector3 bodyDown = Vector3.down;
            if (vessel != null && vessel.mainBody != null)
            {
                Vector3 toBody = vessel.mainBody.position - origin;
                if (toBody.sqrMagnitude > 0.0001f)
                {
                    bodyDown = toBody.normalized;
                }
            }

            exhaustToBodyDown = Vector3.Dot(exhaustDirection, bodyDown);
            if (exhaustToBodyDown < ImpactPuffsRuntimeConfig.MinExhaustToBodyDown)
            {
                LogGroundProbe(vessel, dt, "body-align", groundHit, false, maxEffectiveDistance, terrainHeight, exhaustToBodyDown, alignment, rawHitCount, rigidbodySkipped, partSkipped, normalSkipped);
                LoseGroundContact(dt, false);
                return false;
            }

            if (!TryFindGroundHit(origin, exhaustDirection, vessel, ImpactPuffsRuntimeConfig.MaxRayDistance, out groundHit, out rawHitCount, out rigidbodySkipped, out partSkipped, out normalSkipped))
            {
                LogGroundProbe(vessel, dt, "no-hit", groundHit, false, maxEffectiveDistance, terrainHeight, exhaustToBodyDown, alignment, rawHitCount, rigidbodySkipped, partSkipped, normalSkipped);
                LoseGroundContact(dt, true);
                return false;
            }

            if (IsLaunchsiteExcludedSurface(groundHit.collider))
            {
                LogLaunchsiteSuppression("surface", vessel, groundHit.collider, dt);
                StopAllEmission(dt, true);
                return false;
            }

            Vector3 toGround = groundHit.point - origin;
            if (toGround.sqrMagnitude < 0.0001f)
            {
                LogGroundProbe(vessel, dt, "zero-ground-vector", groundHit, true, maxEffectiveDistance, terrainHeight, exhaustToBodyDown, alignment, rawHitCount, rigidbodySkipped, partSkipped, normalSkipped);
                LoseGroundContact(dt, true);
                return false;
            }

            Vector3 toGroundDirection = toGround.normalized;
            alignment = Vector3.Dot(exhaustDirection, toGroundDirection);
            float minAlignment = Mathf.Lerp(0.42f, ImpactPuffsRuntimeConfig.MinExhaustToGroundAlignment, normalizedThrust);
            if (alignment < minAlignment)
            {
                LogGroundProbe(vessel, dt, "ground-align", groundHit, true, maxEffectiveDistance, terrainHeight, exhaustToBodyDown, alignment, rawHitCount, rigidbodySkipped, partSkipped, normalSkipped);
                LoseGroundContact(dt, false);
                return false;
            }

            if (groundHit.distance > maxEffectiveDistance)
            {
                LogGroundProbe(vessel, dt, "distance", groundHit, true, maxEffectiveDistance, terrainHeight, exhaustToBodyDown, alignment, rawHitCount, rigidbodySkipped, partSkipped, normalSkipped);
                LoseGroundContact(dt, false);
                return false;
            }

            float rawDistanceFactor = Mathf.Clamp01(1f - (groundHit.distance / maxEffectiveDistance));
            distanceFactor = rawDistanceFactor * rawDistanceFactor * rawDistanceFactor;

            if (distanceFactor <= 0.04f)
            {
                LogGroundProbe(vessel, dt, "distance-factor", groundHit, true, maxEffectiveDistance, terrainHeight, exhaustToBodyDown, alignment, rawHitCount, rigidbodySkipped, partSkipped, normalSkipped);
                LoseGroundContact(dt, false);
                return false;
            }

            LogGroundProbe(vessel, dt, "hit", groundHit, true, maxEffectiveDistance, terrainHeight, exhaustToBodyDown, alignment, rawHitCount, rigidbodySkipped, partSkipped, normalSkipped);
            return true;
        }

        private float ComputeTargetRate(
            Vessel vessel,
            float currentThrust,
            float normalizedThrust,
            float distanceFactor,
            out float qualityNorm,
            out float pressure,
            out float thrustPowerNorm)
        {
            float quality = GetModeQualityScale();
            qualityNorm = Mathf.InverseLerp(0.25f, 2.0f, quality);
            float qualityRateScale = 1f + (Mathf.Pow(quality, 1.50f) - 1f) * 1.35f;

            cachedBodyVisibility = ImpactPuffsRuntimeConfig.GetBodyVisibilityMultiplier(
                vessel.mainBody != null ? vessel.mainBody.bodyName : string.Empty);

            float thrustPowerScale = ComputeThrustPowerScale(currentThrust);
            thrustPowerNorm = Mathf.Clamp01(
                (thrustPowerScale - ImpactPuffsRuntimeConfig.ThrustPowerMinScale)
                / Mathf.Max(0.01f, ImpactPuffsRuntimeConfig.ThrustPowerMaxScale - ImpactPuffsRuntimeConfig.ThrustPowerMinScale));

            float lowThrustBoost = Mathf.Lerp(1.42f, 1.00f, normalizedThrust);
            pressure = Mathf.Clamp01(
                normalizedThrust
                * lowThrustBoost
                * Mathf.Lerp(0.52f, 1.95f, distanceFactor)
                * Mathf.Lerp(0.78f, 1.46f, thrustPowerNorm));

            float baseRate = (1320f + 14800f * pressure * pressure) * Mathf.Lerp(0.42f, 1.36f, distanceFactor);
            float engineClusterScale = ComputeEngineClusterScale(engineClusterCount);
            float modeDensityScale = 1.10f;
            float qualityScale = ImpactPuffsConfig.GetQualityScaleMultiplier();

            float targetRate = baseRate
                * qualityRateScale
                * thrustPowerScale
                * engineClusterScale
                * ImpactPuffsRuntimeConfig.EmissionMultiplier
                * ImpactPuffsRuntimeConfig.SharedEmissionMultiplier
                * cachedBodyVisibility
                * modeDensityScale
                * qualityScale;

            targetRate *= 1.42f;
            float rateCap = (engineClusterCount > 1 ? MaxTargetRateMulti : MaxTargetRateSingle) * qualityScale;
            return Mathf.Clamp(targetRate, 0f, rateCap);
        }

        private float ApplyLightAwarenessRate(Vessel vessel, RaycastHit groundHit, float normalizedThrust, float targetRate)
        {
            if (ImpactPuffsConfig.UseLightAware)
            {
                cachedLightFactor = EvaluateVolumetricLightFactor(vessel, groundHit.point, groundHit.normal, normalizedThrust);

                float volumetricLightRate = Mathf.Pow(Mathf.Clamp01(cachedLightFactor), 0.65f);
                volumetricLightRate = Mathf.Lerp(0.28f, 1f, volumetricLightRate);
                targetRate *= volumetricLightRate;
            }
            else
            {
                cachedLightFactor = 1f;
            }

            return targetRate;
        }

        private Vector3 ComputeEmitterPosition(
            Vessel vessel,
            RaycastHit groundHit,
            float pressure,
            out Vector3 stableNormal,
            out Vector3 outwardDirForCoreClamp)
        {
            stableNormal = groundHit.normal.sqrMagnitude > 0.0001f ? groundHit.normal.normalized : Vector3.up;

            float surfaceOffset = -0.05f;
            Vector3 lateralOffset = Vector3.zero;
            outwardDirForCoreClamp = Vector3.zero;

            Vector3 centerPlane = Vector3.ProjectOnPlane(groundHit.point - vessel.CoM, stableNormal);
            float centerPlaneMag = centerPlane.magnitude;
            float underCenter = Mathf.Clamp01(1f - (centerPlaneMag / 0.85f));

            Vector3 outwardDir = centerPlane;
            if (outwardDir.sqrMagnitude < 0.0001f)
            {
                outwardDir = Vector3.ProjectOnPlane((Vector3)vessel.srf_velocity, stableNormal);
            }
            if (outwardDir.sqrMagnitude < 0.0001f)
            {
                outwardDir = Vector3.ProjectOnPlane(part.transform.right, stableNormal);
            }

            if (outwardDir.sqrMagnitude > 0.0001f)
            {
                outwardDir.Normalize();
                outwardDirForCoreClamp = outwardDir;
                float outwardShift = Mathf.Lerp(0.08f, 0.42f, pressure) * Mathf.Lerp(0.30f, 0.92f, underCenter);
                if (engineClusterCount > 1)
                {
                    float clusterNorm = Mathf.InverseLerp(2f, 6f, engineClusterCount);
                    outwardShift *= Mathf.Lerp(1.10f, 1.50f, clusterNorm);
                }
                lateralOffset = outwardDir * outwardShift;
            }

            Vector3 finalPosition = groundHit.point + stableNormal * surfaceOffset + lateralOffset;
            Vector3 finalPlane = Vector3.ProjectOnPlane(finalPosition - vessel.CoM, stableNormal);
            float finalRadius = finalPlane.magnitude;
            float minCoreRadius = Mathf.Lerp(0.55f, 1.95f, pressure);
            if (engineClusterCount > 1)
            {
                float clusterNorm = Mathf.InverseLerp(2f, 6f, engineClusterCount);
                minCoreRadius *= Mathf.Lerp(1.00f, 1.40f, clusterNorm);
            }
            else
            {
                minCoreRadius *= 0.30f;
            }
            if (finalRadius < minCoreRadius)
            {
                Vector3 pushDir = outwardDirForCoreClamp;
                if (pushDir.sqrMagnitude < 0.0001f && finalPlane.sqrMagnitude > 0.0001f)
                {
                    pushDir = finalPlane.normalized;
                }
                if (pushDir.sqrMagnitude > 0.0001f)
                {
                    finalPosition += pushDir * (minCoreRadius - finalRadius);
                }
            }

            return finalPosition;
        }

        private void RefreshRuntimeProfileIfNeeded(float dt)
        {
            profileRefreshTimer -= dt;
            if (profileRefreshTimer <= 0f
                || appliedProfileRevision.NeedsApply(ImpactPuffsConfig.Revision, ImpactPuffsRuntimeConfig.Revision))
            {
                profileRefreshTimer = ProfileRefreshInterval;
                ApplyRuntimeVisualProfile(false);
            }
        }

        private void LogEmitterDebug(
            Vessel vessel,
            float dt,
            float normalizedThrust,
            float groundDistance,
            float displayedRate,
            float pressure,
            float alignment,
            float exhaustToBodyDown,
            float terrainHeight,
            float currentThrust)
        {
            if (!ImpactPuffsConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel)
            {
                return;
            }

            debugTimer -= dt;
            if (debugTimer > 0f)
            {
                return;
            }

            debugTimer = DebugEmitterInterval;
            ImpactPuffsLog.DebugLog(Localizer.Format(
                ImpactPuffsLoc.LogEngineEmitter,
                debugId,
                normalizedThrust.ToString("F2", CultureInfo.InvariantCulture),
                groundDistance.ToString("F2", CultureInfo.InvariantCulture),
                displayedRate.ToString("F1", CultureInfo.InvariantCulture),
                cachedLightFactor.ToString("F2", CultureInfo.InvariantCulture)
                + " pressure=" + pressure.ToString("F2", CultureInfo.InvariantCulture)
                + " align=" + alignment.ToString("F2", CultureInfo.InvariantCulture)
                + " bodyAlign=" + exhaustToBodyDown.ToString("F2", CultureInfo.InvariantCulture)
                + " terrainH=" + terrainHeight.ToString("F1", CultureInfo.InvariantCulture)
                + " thrust=" + currentThrust.ToString("F0", CultureInfo.InvariantCulture)
                + " cluster=" + engineClusterCount
                + " mode=volumetric"
            ));
        }

        public void StopEmission()
        {
            StopAllEmission(0.12f, false);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (particleSystem != null)
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (volumetricField != null)
            {
                volumetricField.Dispose();
            }

            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        private void StopAllEmission(float dt, bool immediateVolumetric)
        {
            SetTargetRate(0f, dt);
            if (volumetricField == null)
            {
                return;
            }

            if (immediateVolumetric)
            {
                volumetricField.StopImmediate();
            }
            else
            {
                volumetricField.StopSoft(dt);
            }
        }

        private void HardResetEmissionState()
        {
            startupRampTimer = Mathf.Min(startupRampTimer, StartupRampDuration * 0.50f);
            smoothedRate = 0f;
            lastCenteredness = 0f;
            ResetGroundAnchorState(true);

            if (particleSystem != null)
            {
                ParticleSystem.EmissionModule emission = particleSystem.emission;
                emission.rateOverTime = 0f;
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (volumetricField != null)
            {
                volumetricField.StopImmediate();
            }
        }

        private void LoseGroundContact(float dt, bool immediateVolumetric)
        {
            lastCenteredness = 0f;
            ResetGroundAnchorState(true);
            StopAllEmission(dt, immediateVolumetric);
        }

        private void LogGroundProbe(
            Vessel vessel,
            float dt,
            string reason,
            RaycastHit hit,
            bool hasHit,
            float maxEffectiveDistance,
            float terrainHeight,
            float exhaustToBodyDown,
            float alignment,
            int rawHitCount,
            int rigidbodySkipped,
            int partSkipped,
            int normalSkipped)
        {
            if (!ImpactPuffsConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel)
            {
                return;
            }

            groundDebugLogTimer -= dt;
            if (groundDebugLogTimer > 0f)
            {
                return;
            }

            groundDebugLogTimer = GroundDebugLogInterval;
            string hitName = hasHit && hit.collider != null ? GetSurfaceNameChain(hit.collider, 2) : "n/a";
            string hitDistance = hasHit ? hit.distance.ToString("F2", CultureInfo.InvariantCulture) : "n/a";

            ImpactPuffsLog.DebugLog(
                "[KerbalFX] GroundProbe "
                + debugId
                + " reason=" + reason
                + " hit=" + hitName
                + " hitDist=" + hitDistance
                + " rayMax=" + maxEffectiveDistance.ToString("F2", CultureInfo.InvariantCulture)
                + " terrainH=" + terrainHeight.ToString("F2", CultureInfo.InvariantCulture)
                + " bodyAlign=" + exhaustToBodyDown.ToString("F2", CultureInfo.InvariantCulture)
                + " align=" + alignment.ToString("F2", CultureInfo.InvariantCulture)
                + " rawHits=" + rawHitCount
                + " skipRb=" + rigidbodySkipped
                + " skipPart=" + partSkipped
                + " skipNormal=" + normalSkipped
                + " cluster=" + engineClusterCount
                + " mode=volumetric");
        }

        private void ResetGroundAnchorState(bool snapRootToOrigin)
        {
            smoothedTangent = Vector3.zero;
            smoothedNormal = Vector3.zero;

            if (!snapRootToOrigin || root == null)
            {
                return;
            }

            root.transform.position = GetThrustOrigin();
        }

        private void LogLaunchsiteSuppression(string reason, Vessel vessel, Collider collider, float dt)
        {
            if (!ImpactPuffsConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel)
            {
                return;
            }

            suppressionLogTimer -= dt;
            if (suppressionLogTimer > 0f)
            {
                return;
            }

            suppressionLogTimer = SuppressionLogInterval;
            string colliderName = collider != null ? GetSurfaceNameChain(collider, 2) : "n/a";
            ImpactPuffsLog.DebugLog(Localizer.Format(
                ImpactPuffsLoc.LogLaunchsiteSuppression,
                reason,
                vessel.vesselName,
                vessel.latitude.ToString("F3", CultureInfo.InvariantCulture),
                vessel.longitude.ToString("F3", CultureInfo.InvariantCulture),
                vessel.altitude.ToString("F1", CultureInfo.InvariantCulture),
                colliderName));
        }

        private void ConfigureParticleSystemBase()
        {
            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(currentColor.r, currentColor.g, currentColor.b, BaseAlpha);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 68f;
            shape.radius = 1.10f;

            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-2.20f, 2.40f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.10f, 0.75f);
            velocity.z = new ParticleSystem.MinMaxCurve(-2.00f, 2.00f);
            velocity.radial = new ParticleSystem.MinMaxCurve(0.45f, 1.20f);

            ParticleSystem.NoiseModule noise = particleSystem.noise;
            noise.enabled = true;
            noise.strength = 0.90f;
            noise.frequency = 0.55f;
            noise.scrollSpeed = 0.35f;
            noise.damping = true;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 2f;

            Material material = ImpactPuffsAssets.GetSharedMaterial();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private void ApplyRuntimeVisualProfile(bool force)
        {
            if (appliedProfileRevision.ShouldSkipApply(force, ImpactPuffsConfig.Revision, ImpactPuffsRuntimeConfig.Revision))
            {
                return;
            }

            appliedProfileRevision.MarkApplied(ImpactPuffsConfig.Revision, ImpactPuffsRuntimeConfig.Revision);

            float quality = GetModeQualityScale();
            float qualityNorm = Mathf.InverseLerp(0.25f, 2.0f, quality);
            float qualityScale = ImpactPuffsConfig.GetQualityScaleMultiplier();
            float volumetricBoost = 1.30f;

            ParticleSystem.MainModule main = particleSystem.main;
            float maxParticles = 2200f
                * (1f + (Mathf.Pow(quality, 1.25f) - 1f) * 1.15f)
                * volumetricBoost
                * ImpactPuffsRuntimeConfig.MaxParticlesMultiplier
                * ImpactPuffsRuntimeConfig.SharedMaxParticlesMultiplier
                * qualityScale;
            main.maxParticles = Mathf.RoundToInt(Mathf.Clamp(maxParticles, 480f, Mathf.Max(480f, 22000f * qualityScale)));

            float minSize = 0.22f * Mathf.Lerp(0.72f, 1.70f, qualityNorm) * ImpactPuffsRuntimeConfig.RadiusScaleMultiplier * ImpactPuffsRuntimeConfig.SharedRadiusScaleMultiplier;
            float maxSize = 0.72f * Mathf.Lerp(0.72f, 1.70f, qualityNorm) * ImpactPuffsRuntimeConfig.RadiusScaleMultiplier * ImpactPuffsRuntimeConfig.SharedRadiusScaleMultiplier;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);

            main.startLifetime = new ParticleSystem.MinMaxCurve(
                0.78f * Mathf.Lerp(0.90f, 1.38f, qualityNorm),
                3.35f * Mathf.Lerp(0.90f, 1.45f, qualityNorm) * volumetricBoost
            );
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, Mathf.Lerp(0.45f, 1.20f, qualityNorm));
            main.gravityModifier = Mathf.Lerp(0.004f, 0.018f, qualityNorm);

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.angle = Mathf.Lerp(58f, 86f, qualityNorm);
            shape.radius = Mathf.Lerp(0.75f, 2.60f, qualityNorm) * ImpactPuffsRuntimeConfig.RadiusScaleMultiplier * ImpactPuffsRuntimeConfig.SharedRadiusScaleMultiplier;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, SharedSizeOverLifetimeCurve);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            float colorAlpha = Mathf.Lerp(0.30f, 0.56f, qualityNorm) * volumetricBoost;
            colorOverLifetime.color = GetOrCreateColorOverLifetimeGradient(colorAlpha);

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.maxParticleSize = Mathf.Lerp(0.24f, 0.62f, qualityNorm);

            ApplyCurrentStartColor();

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            if (smoothedRate < 0.01f)
            {
                emission.rateOverTime = 0f;
            }
        }

        private static AnimationCurve CreateSizeOverLifetimeCurve()
        {
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.55f);
            curve.AddKey(0.48f, 1.95f);
            curve.AddKey(1f, 0.25f);
            return curve;
        }

        private static Gradient GetOrCreateColorOverLifetimeGradient(float alphaAtStart)
        {
            if (cachedColorOverLifetimeGradient != null && Mathf.Abs(cachedColorOverLifetimeAlpha - alphaAtStart) < 0.001f)
            {
                return cachedColorOverLifetimeGradient;
            }

            cachedColorOverLifetimeAlpha = alphaAtStart;
            cachedColorOverLifetimeGradient = new Gradient();
            cachedColorOverLifetimeGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(alphaAtStart, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            return cachedColorOverLifetimeGradient;
        }

        private void UpdateVolumetricFrame(Vessel vessel, Vector3 plumePosition, Vector3 surfaceNormal, Vector3 outwardHint, Vector3 exhaustDirection, float pressure, float dt)
        {
            UpdateVolumetricFrameSingle(vessel, plumePosition, surfaceNormal, outwardHint, exhaustDirection, dt);
        }

        private void UpdateVolumetricFrameSingle(Vessel vessel, Vector3 plumePosition, Vector3 surfaceNormal, Vector3 outwardHint, Vector3 exhaustDirection, float dt)
        {
            if (smoothedNormal.sqrMagnitude > 0.0001f)
            {
                smoothedNormal = Vector3.Slerp(smoothedNormal, surfaceNormal, Mathf.Clamp01(dt * 2.5f));
            }
            else
            {
                smoothedNormal = surfaceNormal;
            }
            smoothedNormal.Normalize();

            Vector3 tangentForward = Vector3.zero;
            float centerPlaneMag = 0f;
            if (vessel != null)
            {
                tangentForward = Vector3.ProjectOnPlane(plumePosition - vessel.CoM, smoothedNormal);
                centerPlaneMag = tangentForward.magnitude;
            }

            if (tangentForward.sqrMagnitude < 0.0001f && outwardHint.sqrMagnitude > 0.0001f)
            {
                tangentForward = Vector3.ProjectOnPlane(outwardHint, smoothedNormal);
            }
            if (tangentForward.sqrMagnitude < 0.0001f)
            {
                tangentForward = Vector3.ProjectOnPlane(exhaustDirection, smoothedNormal);
            }
            if (tangentForward.sqrMagnitude < 0.0001f)
            {
                tangentForward = Vector3.ProjectOnPlane(part.transform.forward, smoothedNormal);
            }
            if (tangentForward.sqrMagnitude < 0.0001f)
            {
                tangentForward = Vector3.Cross(smoothedNormal, Vector3.right);
            }

            tangentForward.Normalize();
            lastCenteredness = Mathf.Clamp01(1f - centerPlaneMag / 1.8f);
            lastCenteredness = Mathf.Max(lastCenteredness, 0.15f);

            if (smoothedTangent.sqrMagnitude > 0.0001f)
            {
                float angleDiff = Vector3.Angle(smoothedTangent, tangentForward);
                float smoothRate = angleDiff > 5.0f ? 3.0f : 0.3f;
                smoothedTangent = Vector3.Slerp(smoothedTangent, tangentForward, Mathf.Clamp01(dt * smoothRate));
            }
            else
            {
                smoothedTangent = tangentForward;
            }
            if (smoothedTangent.sqrMagnitude > 0.0001f)
            {
                smoothedTangent.Normalize();
            }
            tangentForward = smoothedTangent;

            Vector3 tangentRight = Vector3.Cross(smoothedNormal, tangentForward);
            if (tangentRight.sqrMagnitude < 0.0001f)
            {
                tangentRight = Vector3.Cross(smoothedNormal, Vector3.forward);
            }
            tangentRight.Normalize();

            Vector3 awayFromVessel = tangentForward;
            if (awayFromVessel.sqrMagnitude < 0.0001f)
            {
                awayFromVessel = tangentRight;
            }
            awayFromVessel.Normalize();

            root.transform.position = plumePosition;
            root.transform.rotation = Quaternion.LookRotation(smoothedNormal, awayFromVessel);
        }

        private void SetTargetRate(float targetRate, float dt)
        {
            if (targetRate > 0.25f)
            {
                startupRampTimer = Mathf.Min(StartupRampDuration, startupRampTimer + Mathf.Max(0f, dt));
            }
            else
            {
                startupRampTimer = Mathf.Max(0f, startupRampTimer - Mathf.Max(0f, dt) * 1.8f);
            }
            float startupFactor = Mathf.Lerp(0.18f, 1f, Mathf.Clamp01(startupRampTimer / StartupRampDuration));
            targetRate *= startupFactor;

            float smoothingSpeed = targetRate > smoothedRate ? 4.5f : 7.50f;
            float lerpSpeed = Mathf.Clamp01(dt * smoothingSpeed);
            smoothedRate = Mathf.Lerp(smoothedRate, targetRate, lerpSpeed);

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = smoothedRate;

            if (smoothedRate > 0.18f)
            {
                if (!particleSystem.isPlaying)
                {
                    particleSystem.Play(true);
                }
            }
            else if (particleSystem.isPlaying)
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private Vector3 GetPrimaryExhaustDirection()
        {
            if (thrustTransforms != null && thrustTransforms.Count > 0)
            {
                Vector3 average = Vector3.zero;
                for (int i = 0; i < thrustTransforms.Count; i++)
                {
                    Transform transform = thrustTransforms[i];
                    if (transform == null)
                    {
                        continue;
                    }

                    average += GetTransformExhaustDirection(transform);
                }

                if (average.sqrMagnitude > 0.0001f)
                {
                    return average.normalized;
                }
            }

            if (part != null && part.transform != null)
            {
                Vector3 fallback = -part.transform.up;
                if (fallback.sqrMagnitude > 0.0001f)
                {
                    return fallback.normalized;
                }
            }

            return Vector3.down;
        }

        private Vector3 GetTransformExhaustDirection(Transform transform)
        {
            Vector3 dirA = -transform.forward;
            Vector3 dirB = transform.forward;
            if (dirA.sqrMagnitude <= 0.0001f || dirB.sqrMagnitude <= 0.0001f)
            {
                return Vector3.down;
            }

            dirA.Normalize();
            dirB.Normalize();

            float scoreA = 0f;
            float scoreB = 0f;

            if (part != null && part.transform != null)
            {
                Vector3 fromPartCenter = transform.position - part.transform.position;
                if (fromPartCenter.sqrMagnitude > 0.0001f)
                {
                    fromPartCenter.Normalize();
                    scoreA += Vector3.Dot(dirA, fromPartCenter) * 1.45f;
                    scoreB += Vector3.Dot(dirB, fromPartCenter) * 1.45f;
                }

                Vector3 partDown = -part.transform.up;
                if (partDown.sqrMagnitude > 0.0001f)
                {
                    partDown.Normalize();
                    scoreA += Vector3.Dot(dirA, partDown) * 0.85f;
                    scoreB += Vector3.Dot(dirB, partDown) * 0.85f;
                }
            }

            return scoreB > scoreA ? dirB : dirA;
        }

        private Vector3 GetThrustOrigin()
        {
            if (thrustTransforms != null && thrustTransforms.Count > 0)
            {
                Vector3 sum = Vector3.zero;
                int validCount = 0;
                for (int i = 0; i < thrustTransforms.Count; i++)
                {
                    Transform transform = thrustTransforms[i];
                    if (transform == null)
                    {
                        continue;
                    }

                    sum += transform.position;
                    validCount++;
                }

                if (validCount > 0)
                {
                    return sum / validCount;
                }
            }

            if (part != null && part.transform != null)
            {
                return part.transform.position;
            }

            return Vector3.zero;
        }

        private static List<Transform> CopyValidTransforms(List<Transform> source)
        {
            if (source == null || source.Count == 0)
            {
                return null;
            }

            List<Transform> copy = new List<Transform>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null)
                {
                    copy.Add(source[i]);
                }
            }

            return copy.Count > 0 ? copy : null;
        }

        private static string GetDebugTransformName(List<Transform> transforms)
        {
            if (transforms == null || transforms.Count == 0)
            {
                return "part";
            }

            if (transforms.Count == 1)
            {
                return transforms[0] != null ? transforms[0].name : "part";
            }

            string firstName = transforms[0] != null ? transforms[0].name : "thrustTransform";
            return firstName + "x" + transforms.Count.ToString(CultureInfo.InvariantCulture);
        }

        internal static float GetModeQualityScale()
        {
            return 1.70f;
        }

        private static float GetNormalizedLiveThrust(ModuleEngines module)
        {
            if (module == null)
            {
                return 0f;
            }

            float finalThrust = Mathf.Max(0f, (float)module.finalThrust);
            if (finalThrust <= 0.06f)
            {
                return 0f;
            }

            float thrustPercent = Mathf.Clamp01(module.thrustPercentage / 100f);
            float maxPossible = Mathf.Max(0.01f, module.maxThrust * thrustPercent);
            if (maxPossible > 0.01f)
            {
                return Mathf.Clamp01(finalThrust / maxPossible);
            }

            return 0f;
        }

        private static float ComputeThrustPowerScale(float currentThrust)
        {
            float safeThrust = Mathf.Max(0f, currentThrust);
            float normalized = safeThrust / Mathf.Max(10f, ImpactPuffsRuntimeConfig.ThrustPowerReference);
            float scaled = Mathf.Pow(Mathf.Max(0.001f, normalized), ImpactPuffsRuntimeConfig.ThrustPowerExponent);
            return Mathf.Clamp(scaled, ImpactPuffsRuntimeConfig.ThrustPowerMinScale, ImpactPuffsRuntimeConfig.ThrustPowerMaxScale);
        }

        private static float ComputeEngineClusterScale(int clusterCount)
        {
            float count = Mathf.Max(1f, clusterCount);
            float scale = 1f / Mathf.Pow(count, ImpactPuffsRuntimeConfig.EngineCountExponent);
            return Mathf.Clamp(scale, ImpactPuffsRuntimeConfig.EngineCountMinScale, 1f);
        }

        private void UpdateSurfaceColor(Vessel vessel, Collider collider)
        {
            Color bodyColor = KerbalFxSurfaceColor.GetBaseDustColor(vessel);
            Color finalColor = bodyColor;

            Color colliderColor;
            if (KerbalFxSurfaceColor.TryGetColliderColor(collider, out colliderColor))
            {
                finalColor = KerbalFxSurfaceColor.BlendWithColliderColor(bodyColor, colliderColor);
            }

            Color toned = KerbalFxSurfaceColor.NormalizeDustTone(finalColor);
            Color softWhite = new Color(0.90f, 0.90f, 0.89f);
            currentColor = Color.Lerp(toned, softWhite, 0.18f);
        }

        private void ApplyCurrentStartColor()
        {
            if (particleSystem == null)
            {
                return;
            }

            float volumetricBoost = 1.15f;
            float baseAlpha = BaseAlpha * 0.68f * volumetricBoost;
            float alpha = baseAlpha * Mathf.Lerp(0.26f, 0.92f, Mathf.Clamp01(cachedLightFactor));
            alpha *= Mathf.Lerp(0.92f, 1.12f, Mathf.Clamp01((cachedBodyVisibility - 1f) / 0.75f));
            float alphaCap = 0.68f;
            alpha = Mathf.Clamp(alpha, 0f, alphaCap);

            ParticleSystem.MainModule main = particleSystem.main;
            main.startColor = new Color(currentColor.r, currentColor.g, currentColor.b, alpha);
        }

    }

}
