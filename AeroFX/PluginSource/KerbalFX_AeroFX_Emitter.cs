using System.Collections.Generic;
using System.Globalization;
using KSP.Localization;
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
        private readonly List<Vector3> renderPositions = new List<Vector3>(2048);
        private readonly List<Vector3> worldPoints = new List<Vector3>(512);
        private Vector3[] renderPositionsBuffer = new Vector3[64];

        private bool disposed;
        private float smoothedIntensity;
        private float curlPhase;
        private float debugTimer;
        private KerbalFxRevisionStamp appliedProfileRevision;
        private Vector3 lastEmitBodyLocalPosition;
        private bool hasLastEmitBodyLocalPosition;
        private Vector3 smoothedAirflow;
        private bool hasSmoothedAirflow;
        private Vessel.Situations lastSituation;
        private Gradient cachedGradient;
        private GradientAlphaKey[] cachedAlphaKeys;
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
        private const int MaxRibbonPoints = 512;
        private const int SegmentSubdivisions = 6;

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

            UpdateLineRenderer();
            UpdateLineDynamics(sample);

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

        private void UpdateLineRenderer()
        {
            int count = ribbon.Count;
            if (count <= 0 || bodyTransform == null)
            {
                line.enabled = false;
                line.positionCount = 0;
                return;
            }

            line.enabled = true;
            BuildSmoothedRenderPath(count);

            int renderCount = renderPositions.Count;
            if (renderCount <= 0)
            {
                line.enabled = false;
                line.positionCount = 0;
                return;
            }

            if (hasLiveHeadSegment)
            {
                renderPositions.Add(liveBridgeWorldPosition);
                renderPositions.Add(liveHeadWorldPosition);
                renderCount = renderPositions.Count;
            }

            int effectiveCount = renderCount >= 2 ? renderCount : 2;
            if (renderPositionsBuffer.Length < effectiveCount)
            {
                int newCapacity = Mathf.Max(renderPositionsBuffer.Length, 64);
                while (newCapacity < effectiveCount)
                    newCapacity *= 2;
                renderPositionsBuffer = new Vector3[newCapacity];
            }

            for (int i = 0; i < renderCount; i++)
                renderPositionsBuffer[i] = renderPositions[i];
            if (renderCount == 1)
                renderPositionsBuffer[1] = renderPositions[0];

            line.positionCount = effectiveCount;
            line.SetPositions(renderPositionsBuffer);
        }

        private void UpdateLineDynamics(AeroRibbonSample sample)
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

            float lightAlphaScale = Mathf.Pow(Mathf.Clamp01(sample.LightFactor), 1.35f);
            float alphaScale = Mathf.Sqrt(Mathf.Clamp01(intensity)) * lightAlphaScale;
            if (cachedGradient != null && cachedAlphaKeys != null && cachedAlphaKeys.Length >= 6
                && Mathf.Abs(alphaScale - lastAppliedHeadAlpha) > 0.004f)
            {
                cachedAlphaKeys[1].alpha = BaseAlpha * 0.24f * alphaScale;
                cachedAlphaKeys[2].alpha = BaseAlpha * 0.72f * alphaScale;
                cachedAlphaKeys[3].alpha = BaseAlpha * 0.94f * alphaScale;
                cachedAlphaKeys[4].alpha = BaseAlpha * 0.92f * alphaScale;
                cachedAlphaKeys[5].alpha = 0.02f * alphaScale;
                cachedGradient.SetKeys(cachedGradient.colorKeys, cachedAlphaKeys);
                line.colorGradient = cachedGradient;
                lastAppliedHeadAlpha = alphaScale;
            }
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
                new GradientAlphaKey(BaseAlpha * 0.40f, 0.94f),
                new GradientAlphaKey(0.02f, 1f)
            };
            cachedGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
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
            lastAppliedHeadAlpha = -1f;
        }

        private void BuildSmoothedRenderPath(int baseCount)
        {
            renderPositions.Clear();
            worldPoints.Clear();

            if (baseCount <= 0 || bodyTransform == null)
                return;

            for (int i = 0; i < baseCount; i++)
            {
                Vector3 worldPoint = bodyTransform.TransformPoint(ribbon[i].BodyLocalPosition);
                worldPoints.Add(worldPoint);
            }

            if (baseCount == 1)
            {
                renderPositions.Add(worldPoints[0]);
                return;
            }

            for (int i = 0; i < baseCount - 1; i++)
            {
                Vector3 p0 = worldPoints[Mathf.Max(0, i - 1)];
                Vector3 p1 = worldPoints[i];
                Vector3 p2 = worldPoints[i + 1];
                Vector3 p3 = worldPoints[Mathf.Min(baseCount - 1, i + 2)];

                for (int step = 0; step < SegmentSubdivisions; step++)
                {
                    float u = (float)step / SegmentSubdivisions;
                    renderPositions.Add(i == baseCount - 2
                        ? Vector3.Lerp(p1, p2, u)
                        : CatmullRom(p0, p1, p2, p3, u));
                }
            }

            renderPositions.Add(worldPoints[baseCount - 1]);
        }

        private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
