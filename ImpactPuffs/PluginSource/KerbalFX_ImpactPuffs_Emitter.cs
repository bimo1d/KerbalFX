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
        private readonly VolumetricPlumeField volumetricField;
        private readonly string debugId;

        private bool disposed;
        private float debugTimer;
        private float suppressionLogTimer;
        private float groundDebugLogTimer;
        private float lastCenteredness;
        private Vector3 smoothedTangent;
        private Vector3 smoothedNormal;
        private float ignitionPrimeTimer;
        private bool wasEngineIgnited;

        private KerbalFxRevisionStamp appliedProfileRevision;
        private int engineClusterCount = 1;

        private float cachedBodyVisibility = 1f;
        private KerbalFxLightAwareEntry cachedLightAwareEntry;
        private string cachedLightAwareBodyName = string.Empty;
        private bool hasCachedLightAwareEntry;
        private float surfaceColorDebugTimer;
        private float lightAwareDebugTimer;
        private readonly KerbalFxSurfaceColorSampler surfaceColor = new KerbalFxSurfaceColorSampler(
            KerbalFxDustVisualDefaults.Color,
            SurfaceColorBlendStrength,
            SurfaceColorRefreshSeconds,
            SurfaceColorSmoothingSpeed,
            SurfaceColorAllowRendererFallback,
            ImpactPuffsRuntimeConfig.TintProfile);
        private readonly KerbalFxLightAwareSampler lightAware = new KerbalFxLightAwareSampler(
            LightAwareRefreshSeconds,
            LightAwareSmoothingSpeed,
            LightAwareUseShadowProbe,
            LightAwareSampleLocalLights);

        internal const float ModeQualityScale = 1.70f;
        private const float IgnitionPrimeDuration = 1.20f;
        private const float TerrainGateMultiplier = 1.35f;
        private const float TerrainGateOffset = 6f;
        private const float MaxTargetRateSingle = 18000f;
        private const float MaxTargetRateMulti = 7500f;
        private const float DebugEmitterInterval = 1.2f;
        private const float SuppressionLogInterval = 1.5f;
        private const float GroundDebugLogInterval = 0.35f;
        private const float SurfaceColorDebugInterval = 1.5f;
        private const float SurfaceColorBlendStrength = 0.32f;
        private const float SurfaceColorRefreshSeconds = 0.45f;
        private const float SurfaceColorSmoothingSpeed = 1.6f;
        private const bool SurfaceColorAllowRendererFallback = false;
        private const float LightAwareRefreshSeconds = 0.28f;
        private const float LightAwareSmoothingSpeed = 1.4f;
        private const bool LightAwareUseShadowProbe = true;
        private const bool LightAwareSampleLocalLights = false;
        private const float LightAwareEngineStrength = 1.00f;
        private const float LightAwareDebugInterval = 1.6f;
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

            ApplyRuntimeVisualProfile(false);

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

            Vector3 stableNormal;
            Vector3 outwardDirForCoreClamp;
            Vector3 finalPosition = ComputeEmitterPosition(
                vessel,
                groundHit,
                pressure,
                out stableNormal,
                out outwardDirForCoreClamp);

            root.transform.position = finalPosition;

            UpdateVolumetricFrame(vessel, finalPosition, stableNormal, outwardDirForCoreClamp, exhaustDirection, dt);

            UpdateSurfaceColor(vessel, groundHit, dt);
            UpdateLightAware(vessel, groundHit, dt);
            targetRate *= GetLightAwareRateMultiplier();
            LogSurfaceSampleIfNeeded(vessel, dt);
            LogLightSampleIfNeeded(vessel, dt);

            if (volumetricField != null)
            {
                volumetricField.Update(
                    root.transform.position,
                    root.transform.rotation,
                    targetRate,
                    pressure,
                    qualityNorm,
                    ImpactPuffsConfig.GetQualityScaleMultiplier(),
                    GetCurrentDustColor(),
                    lastCenteredness,
                    engineClusterCount,
                    GetLightAwareVisualAlphaMultiplier(),
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

            if (normalizedThrust < ImpactPuffsRuntimeConfig.MinNormalizedThrust)
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
            float quality = ModeQualityScale;
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
            string lightDescriptor = ImpactPuffsConfig.UseLightAware
                ? KerbalFxLightFormat.Describe(lightAware.Current, string.Empty)
                + " mul=" + GetLightAwareAlphaMultiplier().ToString("F2", CultureInfo.InvariantCulture)
                : "off";
            ImpactPuffsLog.DebugLog(Localizer.Format(
                ImpactPuffsLoc.LogEngineEmitter,
                debugId,
                normalizedThrust.ToString("F2", CultureInfo.InvariantCulture),
                groundDistance.ToString("F2", CultureInfo.InvariantCulture),
                displayedRate.ToString("F1", CultureInfo.InvariantCulture),
                lightDescriptor
                + " | pressure=" + pressure.ToString("F2", CultureInfo.InvariantCulture)
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
            lastCenteredness = 0f;
            ResetGroundAnchorState(true);

            if (volumetricField != null)
            {
                volumetricField.StopImmediate();
            }
        }

        private void LoseGroundContact(float dt, bool immediateVolumetric)
        {
            lastCenteredness = 0f;
            ResetGroundAnchorState(immediateVolumetric);
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

        private void ApplyRuntimeVisualProfile(bool force)
        {
            if (appliedProfileRevision.ShouldSkipApply(force, ImpactPuffsConfig.Revision, ImpactPuffsRuntimeConfig.Revision))
            {
                return;
            }

            appliedProfileRevision.MarkApplied(ImpactPuffsConfig.Revision, ImpactPuffsRuntimeConfig.Revision);
            ClearLightAwareEntryCache();
        }

        private void UpdateVolumetricFrame(Vessel vessel, Vector3 plumePosition, Vector3 surfaceNormal, Vector3 outwardHint, Vector3 exhaustDirection, float dt)
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

        internal float GetLightAwareAlphaMultiplier()
        {
            if (!ImpactPuffsConfig.UseLightAware)
                return 1f;
            return lightAware.GetAlphaMultiplier(
                GetLightAwareEntry(),
                LightAwareEngineStrength);
        }

        internal float GetLightAwareVisualAlphaMultiplier()
        {
            return KerbalFxLightingCore.ApplySurfaceBoost(GetLightAwareAlphaMultiplier());
        }

        private float GetLightAwareRateMultiplier()
        {
            return KerbalFxLightingCore.ApplyRateCap(GetLightAwareVisualAlphaMultiplier());
        }

        private Color GetCurrentDustColor()
        {
            Color baseColor = ImpactPuffsConfig.AdaptSurfaceColor ? surfaceColor.CurrentColor : KerbalFxDustVisualDefaults.Color;
            if (!ImpactPuffsConfig.UseLightAware)
                return baseColor;
            return lightAware.ApplyColorTint(
                baseColor,
                GetLightAwareEntry(),
                LightAwareEngineStrength);
        }

        private KerbalFxLightAwareEntry GetLightAwareEntry()
        {
            string bodyName = part != null && part.vessel != null && part.vessel.mainBody != null
                ? part.vessel.mainBody.bodyName
                : string.Empty;
            if (!hasCachedLightAwareEntry || bodyName != cachedLightAwareBodyName)
            {
                cachedLightAwareBodyName = bodyName;
                cachedLightAwareEntry = ImpactPuffsRuntimeConfig.LightAwareProfile.Get(bodyName);
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

        private void UpdateSurfaceColor(Vessel vessel, RaycastHit groundHit, float dt)
        {
            if (!ImpactPuffsConfig.AdaptSurfaceColor)
            {
                surfaceColor.Reset();
                return;
            }

            surfaceColor.Tick(vessel, groundHit.point, groundHit.collider, dt);
        }

        private void UpdateLightAware(Vessel vessel, RaycastHit groundHit, float dt)
        {
            if (!ImpactPuffsConfig.UseLightAware)
            {
                if (!lightAware.IsReset)
                {
                    lightAware.Reset();
                    ClearLightAwareEntryCache();
                }
                return;
            }

            Vector3 normal = groundHit.normal.sqrMagnitude > 0.0001f ? groundHit.normal : Vector3.zero;
            lightAware.Tick(vessel, groundHit.point, normal, dt);
        }

        internal KerbalFxLightSample CurrentLightSample
        {
            get { return lightAware.Current; }
        }

        private void LogLightSampleIfNeeded(Vessel vessel, float dt)
        {
            if (!ImpactPuffsConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel || !ImpactPuffsConfig.UseLightAware)
            {
                return;
            }

            float multiplier = GetLightAwareAlphaMultiplier();
            KerbalFxLightDebugReporter.Report("ImpactPuffs", debugId, lightAware.Current, multiplier, LightAwareEngineStrength);

            lightAwareDebugTimer -= dt;
            if (lightAwareDebugTimer > 0f)
            {
                return;
            }

            lightAwareDebugTimer = LightAwareDebugInterval;
            ImpactPuffsLog.DebugLog(Localizer.Format(
                ImpactPuffsLoc.LogLightSample,
                debugId,
                KerbalFxLightFormat.Describe(lightAware.Current, string.Empty),
                multiplier.ToString("F2", CultureInfo.InvariantCulture)));
        }

        private void LogSurfaceSampleIfNeeded(Vessel vessel, float dt)
        {
            if (!ImpactPuffsConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel || !ImpactPuffsConfig.AdaptSurfaceColor)
            {
                return;
            }

            surfaceColorDebugTimer -= dt;
            if (surfaceColorDebugTimer > 0f)
            {
                return;
            }

            surfaceColorDebugTimer = SurfaceColorDebugInterval;
            ImpactPuffsLog.DebugLog(Localizer.Format(
                ImpactPuffsLoc.LogSurfaceSample,
                debugId,
                string.IsNullOrEmpty(surfaceColor.LastBodyName) ? "n/a" : surfaceColor.LastBodyName,
                string.IsNullOrEmpty(surfaceColor.LastBiomeName) ? "n/a" : surfaceColor.LastBiomeName,
                surfaceColor.LastSource.ToString(),
                KerbalFxSurfaceColorCore.FormatColor(surfaceColor.LastRawSample),
                KerbalFxSurfaceColorCore.FormatColor(surfaceColor.TargetColor),
                KerbalFxSurfaceColorCore.FormatColor(surfaceColor.CurrentColor)));
        }

    }

}
