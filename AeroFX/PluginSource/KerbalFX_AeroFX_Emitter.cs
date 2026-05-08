using System.Collections.Generic;
using System.Globalization;
using KSP.Localization;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace KerbalFX.AeroFX
{
    internal sealed class WingtipRibbonEmitter
    {
        private struct RibbonPoint
        {
            public Vector3 BodyLocalPosition;
            public float Time;
        }

        private readonly GameObject root;
        private readonly LineRenderer line;
        private readonly Part sourcePart;
        private readonly string debugId;
        private readonly List<RibbonPoint> ribbon = new List<RibbonPoint>(512);

        private bool disposed;
        private float smoothedIntensity;
        private float curlPhase;
        private float debugTimer;
        private float lightAwareDebugTimer;
        private float appliedLightAlphaScale = 1f;
        private KerbalFxLightAwareEntry cachedLightAwareEntry;
        private string cachedLightAwareBodyName = string.Empty;
        private bool hasCachedLightAwareEntry;
        private readonly KerbalFxLightAwareSampler lightAware = new KerbalFxLightAwareSampler(
            LightAwareRefreshSeconds,
            LightAwareSmoothingSpeed,
            LightAwareUseShadowProbe,
            LightAwareSampleLocalLights,
            LightAwareUseAerialHorizon);
        private KerbalFxRevisionStamp appliedProfileRevision;
        private Vector3 lastEmitBodyLocalPosition;
        private bool hasLastEmitBodyLocalPosition;
        private Vector3 smoothedAirflow;
        private bool hasSmoothedAirflow;
        private Vessel.Situations lastSituation;
        private Gradient cachedGradient;
        private GradientColorKey[] cachedColorKeys;
        private GradientAlphaKey[] cachedAlphaKeys;
        private float lastAppliedRgbDim = -1f;
        private Transform bodyTransform;
        private bool isEmitting;
        private bool hasLiveHeadSegment;
        private bool pendingManeuverRestartClear;
        private float lastAppliedHeadAlpha = -1f;
        private Vector3 liveHeadWorldPosition;
        private Vector3 liveBridgeWorldPosition;

        private const float DebugLogInterval = 1.2f;
        private const float IntensityOnThreshold = 0.012f;
        private const float IntensityOffThreshold = 0.0015f;
        private const float MinVertexDistanceBase = 0.55f;
        private const float MinVertexDistanceSpeedScale = 0.0045f;
        private const float MaxVertexDistance = 2.1f;
        private const float BaseAlpha = 0.58f;
        private const float TeleportThreshold = 220f;
        private const float AirflowSmoothSpeed = 1.6f;
        private const float LightAwareRefreshSeconds = 0.30f;
        private const float LightAwareSmoothingSpeed = 1.2f;
        private const bool LightAwareUseShadowProbe = false;
        private const bool LightAwareSampleLocalLights = false;
        private const bool LightAwareUseAerialHorizon = true;
        private const float LightAwareRibbonStrength = 0.90f;
        private const float LightAwareDebugInterval = 1.6f;
        private const float LightAwareAlphaApplyEpsilon = 0.01f;
        private const float LightAwareRibbonVisualExponent = 2.35f;
        private const float LightAwareAirSunFloorMin = 0.10f;
        private const float LightAwareAirSunFloorMax = 0.82f;
        private const float RibbonRgbDimFloor = KerbalFxLightingCore.RgbDimFloor;
        internal const int MaxRibbonPoints = 512;
        internal const int SegmentSubdivisions = 6;
        internal const int MaxRibbonRenderPoints = (MaxRibbonPoints - 1) * SegmentSubdivisions + 3;
        private static readonly ProfilerMarker UpdateLineRendererMarker =
            new ProfilerMarker("KerbalFX.AeroFX.UpdateLineRenderer");
        private static readonly ProfilerMarker SetPositionsMarker =
            new ProfilerMarker("KerbalFX.AeroFX.LineRendererSetPositionLoop");

        public WingtipRibbonEmitter(Part part, string label)
        {
            sourcePart = part;
            string partName = part != null
                ? (part.partInfo != null ? part.partInfo.name : part.name)
                : "unknown";
            debugId = partName + ":" + label;

            root = new GameObject("KerbalFX_WingtipRibbon_" + label);
            root.transform.SetParent(null, false);
            root.layer = part != null ? part.gameObject.layer : 0;

            line = root.AddComponent<LineRenderer>();
            ConfigureLineBase();
            ApplyRuntimeProfile(true);

            float seed = part != null ? Mathf.Abs(part.GetInstanceID()) * 0.031f : 0f;
            curlPhase = seed;
        }

        public bool IsBoundTo(Part part)
        {
            return sourcePart == part;
        }

        public void Tick(Vessel vessel, WingtipRibbonAnchor anchor, AeroRibbonSample sample, float dt)
        {
            if (disposed || line == null || !anchor.IsValid)
                return;

            if (appliedProfileRevision.NeedsApply(AeroFxConfig.Revision, AeroFxRuntimeConfig.Revision))
            {
                ApplyRuntimeProfile(false);
            }

            Vessel.Situations currentSituation = vessel != null ? vessel.situation : Vessel.Situations.FLYING;
            if (currentSituation != lastSituation)
            {
                if (lastSituation == Vessel.Situations.LANDED
                    || lastSituation == Vessel.Situations.PRELAUNCH
                    || lastSituation == Vessel.Situations.SPLASHED)
                {
                    ribbon.Clear();
                    hasLastEmitBodyLocalPosition = false;
                    hasSmoothedAirflow = false;
                    isEmitting = false;
                    hasLiveHeadSegment = false;
                    pendingManeuverRestartClear = false;
                    lastAppliedHeadAlpha = -1f;
                }
                lastSituation = currentSituation;
            }

            Transform currentBodyTransform = vessel != null && vessel.mainBody != null
                ? vessel.mainBody.transform
                : null;
            if (currentBodyTransform == null)
                return;

            if (bodyTransform != currentBodyTransform)
            {
                bodyTransform = currentBodyTransform;
                ribbon.Clear();
                hasSmoothedAirflow = false;
                hasLastEmitBodyLocalPosition = false;
                isEmitting = false;
                hasLiveHeadSegment = false;
                pendingManeuverRestartClear = false;
                lastAppliedHeadAlpha = -1f;
            }

            float trailTime = Mathf.Lerp(
                AeroFxRuntimeConfig.TrailTimeMin,
                AeroFxRuntimeConfig.TrailTimeMax,
                sample.Length01);
            float speedLengthScale = Mathf.Lerp(1.20f, 0.45f, sample.Speed01);
            float machLengthScale = Mathf.Lerp(1.00f, 0.70f, sample.Mach01);
            float highSpeedCutoff = 1f - Mathf.InverseLerp(480f, 650f, sample.Speed) * 0.55f;
            float highSpeedHistoryBoost = Mathf.InverseLerp(240f, 620f, sample.Speed) * sample.Maneuver01;
            float recoveredHighSpeedCutoff = Mathf.Lerp(highSpeedCutoff, Mathf.Max(highSpeedCutoff, 0.96f), highSpeedHistoryBoost);
            float maneuverHistoryScale = Mathf.Lerp(1f, 3.15f, highSpeedHistoryBoost);
            trailTime *= speedLengthScale * machLengthScale * recoveredHighSpeedCutoff * maneuverHistoryScale;

            float now = Time.time;
            int trimCount = 0;
            while (trimCount < ribbon.Count && now - ribbon[trimCount].Time > trailTime)
                trimCount++;
            if (trimCount >= ribbon.Count)
                ribbon.Clear();
            else if (trimCount > 0)
                ribbon.RemoveRange(0, trimCount);

            Vector3 anchorPoint = anchor.WorldPoint;
            Vector3 anchorOutward = anchor.Outward;
            Part trackingPart = anchor.TrackingPart != null ? anchor.TrackingPart : anchor.Part;
            if (trackingPart != null && trackingPart.transform != null)
            {
                anchorPoint = trackingPart.transform.TransformPoint(anchor.LocalPoint);
                anchorOutward = trackingPart.transform.TransformDirection(anchor.LocalOutward);
                if (anchorOutward.sqrMagnitude > 0.0001f)
                    anchorOutward.Normalize();
                else
                    anchorOutward = anchor.Outward;
            }

            float targetIntensity = Mathf.Clamp01(sample.Activation);
            if (anchor.Role == WingtipAnchorRole.Control)
                targetIntensity *= 0.65f;
            float fadeSpeed = targetIntensity > smoothedIntensity
                ? AeroFxRuntimeConfig.FadeInSpeed
                : AeroFxRuntimeConfig.FadeOutSpeed;
            if (AeroFxConfig.UseManeuverOnly && targetIntensity <= smoothedIntensity)
            {
                float maneuverGateHold01 = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.22f, sample.ManeuverGate01));
                fadeSpeed *= Mathf.Lerp(10.00f, 1f, maneuverGateHold01);
            }
            smoothedIntensity = Mathf.Lerp(smoothedIntensity, targetIntensity, Mathf.Clamp01(dt * fadeSpeed));
            UpdateLightAware(vessel, anchorPoint, dt);
            LogLightSampleIfNeeded(vessel, dt);

            if (AeroFxConfig.UseManeuverOnly)
            {
                if (sample.ManeuverGate01 < 0.02f && smoothedIntensity < 0.02f && ribbon.Count > 0)
                    pendingManeuverRestartClear = true;
            }
            else
            {
                pendingManeuverRestartClear = false;
            }

            Vector3 tipOffset = anchorOutward * Mathf.Lerp(0.02f, 0.05f, sample.Length01);
            Vector3 headWorldPosition = anchorPoint + tipOffset;

            Vector3 currentAirflow = sample.AirflowBack;
            if (!hasSmoothedAirflow)
            {
                smoothedAirflow = currentAirflow;
                hasSmoothedAirflow = true;
            }
            else
            {
                float highSpeedFlexBoost = Mathf.InverseLerp(300f, 650f, sample.Speed);
                float airflowSmoothSpeed = Mathf.Lerp(AirflowSmoothSpeed, 8.40f, highSpeedFlexBoost);
                smoothedAirflow = Vector3.Lerp(smoothedAirflow, currentAirflow, Mathf.Clamp01(dt * airflowSmoothSpeed));
                if (smoothedAirflow.sqrMagnitude > 0.0001f)
                    smoothedAirflow.Normalize();
            }

            Vector3 airflowOffset = smoothedAirflow * Mathf.Lerp(0.40f, 1.20f, sample.Speed01);

            float lowSpeedMotionBoost = 1f - Mathf.InverseLerp(125f, 180f, sample.Speed);
            float highSpeedMotionFade = 1f - Mathf.InverseLerp(260f, 520f, sample.Speed);
            float maneuverMotionBoost = Mathf.InverseLerp(1.10f, 2.80f, sample.LoadFactor);
            float post120FrequencyBoost = Mathf.InverseLerp(120f, 260f, sample.Speed);
            float curlPhaseSpeed = Mathf.Lerp(2.0f, 4.6f, sample.Curl01);
            curlPhaseSpeed *= Mathf.Lerp(0.92f, 1.18f, lowSpeedMotionBoost);
            curlPhaseSpeed *= Mathf.Lerp(0.98f, 1.22f, maneuverMotionBoost);
            curlPhaseSpeed *= Mathf.Lerp(1.00f, 1.10f, post120FrequencyBoost);
            curlPhase += dt * curlPhaseSpeed;
            float curlAmount = Mathf.Lerp(
                AeroFxRuntimeConfig.CurlAmplitudeMin,
                AeroFxRuntimeConfig.CurlAmplitudeMax,
                sample.Curl01);
            float post120AmplitudeFade = Mathf.Lerp(1.00f, 0.74f, post120FrequencyBoost);
            float highSpeedManeuverBoost = Mathf.InverseLerp(220f, 560f, sample.Speed) * sample.Maneuver01;
            float curlDamping = Mathf.Lerp(0.62f, 1.00f, highSpeedMotionFade);
            curlDamping *= Mathf.Lerp(0.92f, 1.28f, lowSpeedMotionBoost);
            curlDamping *= Mathf.Lerp(0.98f, 1.30f, maneuverMotionBoost);
            curlDamping *= post120AmplitudeFade;
            curlDamping *= Mathf.Lerp(1.00f, 3.75f, highSpeedManeuverBoost);
            Vector3 curlOffset = (sample.UpAxis * (Mathf.Sin(curlPhase) * curlAmount)
                + anchorOutward * (Mathf.Cos(curlPhase * 0.7f) * curlAmount * 0.6f)) * curlDamping;

            Vector3 radialUp = (anchorPoint - bodyTransform.position).normalized;
            if (radialUp.sqrMagnitude < 0.0001f)
                radialUp = sample.UpAxis;

            Vector3 sinkOffset = -radialUp * Mathf.Lerp(
                AeroFxRuntimeConfig.SinkBiasMin,
                AeroFxRuntimeConfig.SinkBiasMax,
                sample.Pressure01);

            Vector3 emitOffset = tipOffset + airflowOffset + curlOffset + sinkOffset;
            Vector3 newWorldPosition = anchorPoint + emitOffset;
            Vector3 newBodyLocalPosition = bodyTransform.InverseTransformPoint(newWorldPosition);

            if (hasLastEmitBodyLocalPosition)
            {
                float jumpSq = (newBodyLocalPosition - lastEmitBodyLocalPosition).sqrMagnitude;
                if (jumpSq > TeleportThreshold * TeleportThreshold)
                {
                    ribbon.Clear();
                    hasSmoothedAirflow = false;
                    hasLastEmitBodyLocalPosition = false;
                    isEmitting = false;
                    hasLiveHeadSegment = false;
                    pendingManeuverRestartClear = false;
                    lastAppliedHeadAlpha = -1f;
                }
            }

            bool wasEmitting = isEmitting;
            if (!isEmitting && smoothedIntensity > IntensityOnThreshold)
                isEmitting = true;
            else if (isEmitting && smoothedIntensity < IntensityOffThreshold)
                isEmitting = false;

            if (pendingManeuverRestartClear && !wasEmitting && isEmitting)
            {
                ribbon.Clear();
                hasLastEmitBodyLocalPosition = false;
                hasSmoothedAirflow = false;
                hasLiveHeadSegment = false;
                pendingManeuverRestartClear = false;
            }

            bool shouldEmit = isEmitting;
            if (shouldEmit && ribbon.Count < MaxRibbonPoints)
            {
                float effectiveMinDist = Mathf.Min(MinVertexDistanceBase + sample.Speed * MinVertexDistanceSpeedScale, MaxVertexDistance);
                float highSpeedFlexBoost = Mathf.InverseLerp(300f, 650f, sample.Speed);
                effectiveMinDist *= Mathf.Lerp(1.00f, 0.22f, highSpeedFlexBoost);
                bool shouldAdd = ribbon.Count == 0;
                if (!shouldAdd)
                {
                    float distSq = (newBodyLocalPosition - ribbon[ribbon.Count - 1].BodyLocalPosition).sqrMagnitude;
                    shouldAdd = distSq >= effectiveMinDist * effectiveMinDist;
                }

                if (shouldAdd)
                {
                    RibbonPoint rp;
                    rp.BodyLocalPosition = newBodyLocalPosition;
                    rp.Time = now;
                    ribbon.Add(rp);
                }
            }
            if (shouldEmit)
            {
                hasLiveHeadSegment = true;
                liveHeadWorldPosition = headWorldPosition;
                liveBridgeWorldPosition = Vector3.Lerp(newWorldPosition, headWorldPosition, 0.72f);
            }
            else
            {
                hasLiveHeadSegment = false;
            }

            lastEmitBodyLocalPosition = newBodyLocalPosition;
            hasLastEmitBodyLocalPosition = true;

            if (AeroFxConfig.DebugLogging && vessel == FlightGlobals.ActiveVessel)
            {
                debugTimer -= dt;
                if (debugTimer <= 0f)
                {
                    debugTimer = DebugLogInterval;
                    AeroFxLog.DebugLog(Localizer.Format(
                        AeroFxLoc.LogEmitter,
                        debugId,
                        shouldEmit,
                        smoothedIntensity.ToString("F2", CultureInfo.InvariantCulture),
                        sample.Speed.ToString("F1", CultureInfo.InvariantCulture),
                        sample.LoadFactor.ToString("F2", CultureInfo.InvariantCulture),
                        sample.DynamicPressure.ToString("F1", CultureInfo.InvariantCulture)
                        + " pts=" + ribbon.Count.ToString(CultureInfo.InvariantCulture)
                        + " ln=" + (line != null && line.enabled
                            ? line.positionCount.ToString(CultureInfo.InvariantCulture)
                            : "off")));
                }
            }
        }

        public void StopEmission()
        {
            smoothedIntensity = Mathf.Min(smoothedIntensity, 0.02f);
            isEmitting = false;
            hasLiveHeadSegment = false;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            ribbon.Clear();
            if (line != null)
            {
                line.positionCount = 0;
                line.enabled = false;
            }
            pendingManeuverRestartClear = false;
            if (root != null)
                Object.Destroy(root);
        }

        internal int BuildRibbonPathRequest(
            NativeArray<float3> input,
            int inputOffset,
            int outputOffset,
            out AeroRibbonPathRequest request)
        {
            request = default(AeroRibbonPathRequest);
            if (disposed || bodyTransform == null || !input.IsCreated)
                return 0;

            int inputCount = Mathf.Min(ribbon.Count, MaxRibbonPoints);
            if (inputCount <= 0 || inputOffset < 0 || inputOffset + inputCount > input.Length)
                return 0;

            int renderCount = AeroRibbonPathBuilder.ComputeRenderCount(inputCount, SegmentSubdivisions);
            if (renderCount <= 0)
                return 0;

            for (int i = 0; i < inputCount; i++)
                input[inputOffset + i] = AeroRibbonPathBuilder.ToFloat3(ribbon[i].BodyLocalPosition);

            request.InputOffset = inputOffset;
            request.OutputOffset = outputOffset;
            request.BaseCount = inputCount;
            request.SegmentSubdivisions = SegmentSubdivisions;
            request.LocalToWorld = AeroRibbonPathBuilder.ToFloat4x4(bodyTransform.localToWorldMatrix);
            return renderCount;
        }

        internal int AppendLiveHead(NativeArray<float3> output, int outputOffset, int renderCount)
        {
            if (!hasLiveHeadSegment || !output.IsCreated)
                return renderCount;
            if (outputOffset < 0 || outputOffset + renderCount + 1 >= output.Length)
                return renderCount;

            output[outputOffset + renderCount++] = AeroRibbonPathBuilder.ToFloat3(liveBridgeWorldPosition);
            output[outputOffset + renderCount++] = AeroRibbonPathBuilder.ToFloat3(liveHeadWorldPosition);
            return renderCount;
        }

        internal void HideLineRenderer()
        {
            if (line == null)
                return;

            line.enabled = false;
            line.positionCount = 0;
        }

        internal void ApplyLineRenderer(NativeArray<float3> output, int outputOffset, int renderCount)
        {
            UpdateLineRendererMarker.Begin();
            try
            {
                if (line == null || !output.IsCreated || renderCount <= 0 || outputOffset < 0)
                {
                    HideLineRenderer();
                    return;
                }

                line.enabled = true;
                int effectiveCount = renderCount >= 2 ? renderCount : 2;
                line.positionCount = effectiveCount;
                SetPositionsMarker.Begin();
                try
                {
                    for (int i = 0; i < renderCount; i++)
                        line.SetPosition(i, AeroRibbonPathBuilder.ToVector3(output[outputOffset + i]));
                    if (renderCount == 1)
                        line.SetPosition(1, AeroRibbonPathBuilder.ToVector3(output[outputOffset]));
                }
                finally
                {
                    SetPositionsMarker.End();
                }
            }
            finally
            {
                UpdateLineRendererMarker.End();
            }
        }

        internal void UpdateLineDynamics(AeroRibbonSample sample)
        {
            if (!line.enabled)
                return;

            float intensity = smoothedIntensity;

            float widthBase = Mathf.Lerp(
                AeroFxRuntimeConfig.TrailWidthMin,
                AeroFxRuntimeConfig.TrailWidthMax,
                intensity);
            float speedWidthScale = Mathf.Lerp(1.10f, 0.98f, sample.Speed01);
            float loadWidthScale = Mathf.Lerp(0.90f, 1.40f, sample.Load01);
            line.widthMultiplier = widthBase * speedWidthScale * loadWidthScale;

            float alphaScale = Mathf.Sqrt(Mathf.Clamp01(intensity)) * appliedLightAlphaScale;
            float rgbDim = Mathf.Lerp(RibbonRgbDimFloor, 1f, Mathf.Clamp01(appliedLightAlphaScale));
            bool alphaChanged = cachedAlphaKeys != null && cachedAlphaKeys.Length >= 6
                && Mathf.Abs(alphaScale - lastAppliedHeadAlpha) > 0.004f;
            bool colorChanged = cachedColorKeys != null && cachedColorKeys.Length >= 2
                && Mathf.Abs(rgbDim - lastAppliedRgbDim) > 0.01f;
            if (cachedGradient == null || (!alphaChanged && !colorChanged))
                return;

            if (alphaChanged)
            {
                cachedAlphaKeys[1].alpha = BaseAlpha * 0.24f * alphaScale;
                cachedAlphaKeys[2].alpha = BaseAlpha * 0.72f * alphaScale;
                cachedAlphaKeys[3].alpha = BaseAlpha * 0.94f * alphaScale;
                cachedAlphaKeys[4].alpha = BaseAlpha * 0.92f * alphaScale;
                cachedAlphaKeys[5].alpha = 0.02f * alphaScale;
                lastAppliedHeadAlpha = alphaScale;
            }
            if (colorChanged)
            {
                Color tinted = new Color(rgbDim, rgbDim, rgbDim, 1f);
                cachedColorKeys[0].color = tinted;
                cachedColorKeys[1].color = tinted;
                lastAppliedRgbDim = rgbDim;
            }
            cachedGradient.SetKeys(cachedColorKeys, cachedAlphaKeys);
            line.colorGradient = cachedGradient;
        }

        private void UpdateLightAware(Vessel vessel, Vector3 anchorWorldPoint, float dt)
        {
            if (!AeroFxConfig.UseLightAware)
            {
                if (!lightAware.IsReset)
                {
                    lightAware.Reset();
                    ClearLightAwareEntryCache();
                }
                appliedLightAlphaScale = 1f;
                return;
            }

            lightAware.Tick(vessel, anchorWorldPoint, Vector3.zero, dt);
            float rawMultiplier = lightAware.GetAlphaMultiplier(
                GetLightAwareEntry(vessel),
                LightAwareRibbonStrength);
            float multiplier = KerbalFxLightingCore.RemapVisualAlpha(rawMultiplier, 0f, LightAwareRibbonVisualExponent);
            float airSun = Mathf.InverseLerp(0f, 0.65f, lightAware.Current.DirectSun);
            if (airSun > 0f)
                multiplier = Mathf.Max(multiplier, Mathf.Lerp(LightAwareAirSunFloorMin, LightAwareAirSunFloorMax, airSun));
            if (Mathf.Abs(multiplier - appliedLightAlphaScale) >= LightAwareAlphaApplyEpsilon)
                appliedLightAlphaScale = multiplier;
        }

        private KerbalFxLightAwareEntry GetLightAwareEntry(Vessel vessel)
        {
            string bodyName = vessel != null && vessel.mainBody != null ? vessel.mainBody.bodyName : string.Empty;
            if (!hasCachedLightAwareEntry || bodyName != cachedLightAwareBodyName)
            {
                cachedLightAwareBodyName = bodyName;
                cachedLightAwareEntry = AeroFxRuntimeConfig.LightAwareProfile.Get(bodyName);
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

        private void LogLightSampleIfNeeded(Vessel vessel, float dt)
        {
            if (!AeroFxConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel || !AeroFxConfig.UseLightAware)
                return;

            KerbalFxLightDebugReporter.Report("AeroFX", debugId, lightAware.Current, appliedLightAlphaScale, LightAwareRibbonStrength);

            lightAwareDebugTimer -= dt;
            if (lightAwareDebugTimer > 0f)
                return;

            lightAwareDebugTimer = LightAwareDebugInterval;
            AeroFxLog.DebugLog(Localizer.Format(
                AeroFxLoc.LogLightSample,
                debugId,
                KerbalFxLightFormat.Describe(lightAware.Current, string.Empty),
                appliedLightAlphaScale.ToString("F2", CultureInfo.InvariantCulture)));
        }

        private void ConfigureLineBase()
        {
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.generateLightingData = false;
            line.numCornerVertices = 8;
            line.numCapVertices = 4;
            line.positionCount = 0;
            line.enabled = false;

            line.startWidth = AeroFxRuntimeConfig.TrailWidthMax;
            line.endWidth = 0f;

            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.00f, 0f, 0.18f),
                new Keyframe(0.12f, 0.16f, 0.30f, 0.30f),
                new Keyframe(0.32f, 0.62f, 0.22f, 0.16f),
                new Keyframe(0.72f, 1.00f, 0.06f, -0.04f),
                new Keyframe(0.90f, 0.58f, -0.18f, -0.22f),
                new Keyframe(1f, 0.08f, -0.08f, 0f)
            );

            cachedGradient = new Gradient();
            cachedAlphaKeys = new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(BaseAlpha * 0.24f, 0.12f),
                new GradientAlphaKey(BaseAlpha * 0.72f, 0.34f),
                new GradientAlphaKey(BaseAlpha * 0.94f, 0.78f),
                new GradientAlphaKey(BaseAlpha * 0.92f, 0.94f),
                new GradientAlphaKey(0.02f, 1f)
            };
            cachedColorKeys = new GradientColorKey[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            };
            cachedGradient.SetKeys(
                cachedColorKeys,
                cachedAlphaKeys
            );
            line.colorGradient = cachedGradient;

            Material material = AeroFxAssets.GetTrailMaterial();
            if (material != null)
                line.material = material;
        }

        private void ApplyRuntimeProfile(bool force)
        {
            if (appliedProfileRevision.ShouldSkipApply(force, AeroFxConfig.Revision, AeroFxRuntimeConfig.Revision))
            {
                return;
            }

            appliedProfileRevision.MarkApplied(AeroFxConfig.Revision, AeroFxRuntimeConfig.Revision);
            ClearLightAwareEntryCache();
            lastAppliedHeadAlpha = -1f;
        }

    }
}
