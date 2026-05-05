using System.Globalization;
using System.Text;
using KSP.Localization;
using UnityEngine;

namespace KerbalFX.AeroFX
{
    internal struct AeroRibbonSample
    {
        public Vector3 ForwardAxis;
        public Vector3 RightAxis;
        public Vector3 UpAxis;
        public Vector3 AirflowBack;
        public float Speed;
        public float Mach;
        public float AtmosphereDensity;
        public float DynamicPressure;
        public float LoadFactor;
        public float RadarAltitude;
        public float BodyVisibility;
        public float Activation;
        public float Speed01;
        public float Pressure01;
        public float Density01;
        public float Mach01;
        public float Load01;
        public float NearGround01;
        public float Length01;
        public float Maneuver01;
        public float ManeuverGate01;
        public float Curl01;
    }

    internal sealed class VesselAeroController : IVesselFxController
    {
        private const int MaxEmitters = 6;
        private const float SampleDebugInterval = 0.8f;
        private const float AnchorSwitchMargin = 0.45f;
        private const float EmitterGraceSeconds = 4.0f;
        private const float ManeuverGateFadeInSpeed = 0.62f;
        private const float ManeuverGateFadeOutSpeed = 12.00f;

        private readonly Vessel vessel;
        private readonly WingtipRibbonAnchor[] anchors = new WingtipRibbonAnchor[MaxEmitters];
        private readonly WingtipRibbonEmitter[] emitters = new WingtipRibbonEmitter[MaxEmitters];
        private readonly float[] emitterGraceTimers = new float[MaxEmitters];
        private readonly WingtipRibbonAnchor[] resolvedAnchors = new WingtipRibbonAnchor[MaxEmitters];
        private readonly WingtipRibbonAnchor[] rebuildAnchors = new WingtipRibbonAnchor[MaxEmitters];
        private readonly WingtipRibbonEmitter[] rebuildEmitters = new WingtipRibbonEmitter[MaxEmitters];
        private readonly float[] rebuildGraceTimers = new float[MaxEmitters];
        private readonly bool[] matchedEmitters = new bool[MaxEmitters];
        private readonly StringBuilder anchorDebugBuilder = new StringBuilder(160);
        private int activeSlotCount;

        private int cachedPartCount = -1;
        private int lastLiftPartCount;
        private int lastCandidateCount;
        private int appliedConfigRevision = -1;
        private string lastCandidateSummary = "none";
        private float anchorRefreshTimer;
        private float sampleDebugTimer;
        private float smoothedManeuverGate = 1f;
        private bool hasSmoothedManeuverGate;

        public VesselAeroController(Vessel vessel)
        {
            this.vessel = vessel;
            RebuildAnchors(forceLog: false);
        }

        public bool HasAnyEmitters
        {
            get
            {
                for (int i = 0; i < activeSlotCount; i++)
                    if (emitters[i] != null)
                        return true;
                return false;
            }
        }

        public int EmitterCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < activeSlotCount; i++)
                    if (emitters[i] != null)
                        count++;
                return count;
            }
        }

        public bool IsStillValid()
        {
            return vessel != null
                && vessel.loaded
                && !vessel.packed
                && vessel.rootPart != null
                && vessel.parts != null
                && vessel.mainBody != null
                && vessel.mainBody.atmosphere;
        }

        public void TryRebuild(float refreshElapsed)
        {
            if (!IsStillValid())
                return;

            anchorRefreshTimer -= refreshElapsed;
            int currentPartCount = vessel.parts != null ? vessel.parts.Count : 0;
            if (currentPartCount != cachedPartCount
                || anchorRefreshTimer <= 0f
                || !HasAnyEmitters
                || appliedConfigRevision != AeroFxConfig.Revision)
                RebuildAnchors(forceLog: false);
        }

        public void Tick(float dt)
        {
            if (!IsStillValid())
            {
                StopAll();
                return;
            }

            AeroRibbonSample sample;
            if (!TryBuildSample(dt, out sample))
            {
                StopAll();
                return;
            }

            for (int i = 0; i < activeSlotCount; i++)
            {
                if (emitters[i] != null && anchors[i].IsValid)
                    emitters[i].Tick(vessel, anchors[i], sample, dt);
            }
        }

        public void StopAll()
        {
            for (int i = 0; i < activeSlotCount; i++)
                if (emitters[i] != null)
                    emitters[i].StopEmission();
        }

        public void Dispose()
        {
            for (int i = 0; i < MaxEmitters; i++)
            {
                if (emitters[i] != null)
                {
                    emitters[i].Dispose();
                    emitters[i] = null;
                }
                anchors[i] = default(WingtipRibbonAnchor);
            }
            activeSlotCount = 0;
        }

        private void RebuildAnchors(bool forceLog)
        {
            anchorRefreshTimer = AeroFxRuntimeConfig.AnchorRefreshInterval;
            cachedPartCount = vessel != null && vessel.parts != null ? vessel.parts.Count : -1;
            appliedConfigRevision = AeroFxConfig.Revision;
            int maxRibbonCount = Mathf.Clamp(AeroFxConfig.MaxRibbonCount, 2, MaxEmitters);
            bool shouldLog = vessel == FlightGlobals.ActiveVessel && (forceLog || AeroFxConfig.DebugLogging);

            int newCount = AeroTrailAnchors.TryResolveAll(
                vessel,
                resolvedAnchors,
                maxRibbonCount,
                AeroFxConfig.FastAnchorScan,
                shouldLog,
                out lastLiftPartCount,
                out lastCandidateCount,
                out lastCandidateSummary);

            for (int i = 0; i < MaxEmitters; i++)
            {
                matchedEmitters[i] = false;
                rebuildAnchors[i] = default(WingtipRibbonAnchor);
                rebuildEmitters[i] = null;
                rebuildGraceTimers[i] = 0f;
            }

            for (int i = 0; i < newCount; i++)
            {
                int existingIdx = FindExistingEmitterForPart(resolvedAnchors[i].Part, matchedEmitters);
                if (existingIdx >= 0)
                {
                    matchedEmitters[existingIdx] = true;

                    if (ShouldAdoptNewAnchor(anchors[existingIdx], resolvedAnchors[i]))
                        rebuildAnchors[i] = resolvedAnchors[i];
                    else
                        rebuildAnchors[i] = anchors[existingIdx];

                    rebuildEmitters[i] = emitters[existingIdx];
                    rebuildGraceTimers[i] = 0f;
                }
                else
                {
                    rebuildAnchors[i] = resolvedAnchors[i];
                    string label = resolvedAnchors[i].SideSign >= 0 ? "R" + i : "L" + i;
                    rebuildEmitters[i] = new WingtipRibbonEmitter(resolvedAnchors[i].Part, label);
                    rebuildGraceTimers[i] = 0f;
                }
            }

            int resultCount = newCount;

            for (int j = 0; j < activeSlotCount; j++)
            {
                if (matchedEmitters[j] || emitters[j] == null)
                    continue;

                bool partStillExists = IsPartInVessel(emitters[j], vessel);
                if (partStillExists && resultCount < maxRibbonCount)
                {
                    float grace = emitterGraceTimers[j] + AeroFxRuntimeConfig.AnchorRefreshInterval;
                    if (grace <= EmitterGraceSeconds)
                    {
                        rebuildAnchors[resultCount] = anchors[j];
                        rebuildEmitters[resultCount] = emitters[j];
                        rebuildGraceTimers[resultCount] = grace;
                        if (!AeroFxConfig.FastAnchorScan)
                            rebuildEmitters[resultCount].StopEmission();
                        resultCount++;
                        continue;
                    }
                }

                emitters[j].Dispose();
            }

            for (int i = 0; i < MaxEmitters; i++)
            {
                anchors[i] = i < resultCount ? rebuildAnchors[i] : default(WingtipRibbonAnchor);
                emitters[i] = i < resultCount ? rebuildEmitters[i] : null;
                emitterGraceTimers[i] = i < resultCount ? rebuildGraceTimers[i] : 0f;
            }
            activeSlotCount = resultCount;

            if (shouldLog)
            {
                string anchorScanMessage = Localizer.Format(
                    AeroFxLoc.LogAnchorScan,
                    vessel.vesselName,
                    lastLiftPartCount,
                    lastCandidateCount,
                    maxRibbonCount,
                    activeSlotCount,
                    BuildAnchorDebugSummary());

                if (AeroFxConfig.DebugLogging)
                    AeroFxLog.DebugLog(anchorScanMessage);
                else
                    AeroFxLog.Info(anchorScanMessage);

                if (!string.IsNullOrEmpty(lastCandidateSummary))
                {
                    string candidateMessage = Localizer.Format(
                        AeroFxLoc.LogAnchorCandidates,
                        vessel.vesselName,
                        lastCandidateSummary);

                    if (AeroFxConfig.DebugLogging)
                        AeroFxLog.DebugLog(candidateMessage);
                    else
                        AeroFxLog.Info(candidateMessage);
                }
            }
        }

        private string BuildAnchorDebugSummary()
        {
            if (activeSlotCount <= 0)
            {
                return "none";
            }

            anchorDebugBuilder.Length = 0;
            for (int i = 0; i < activeSlotCount; i++)
            {
                if (!anchors[i].IsValid)
                {
                    continue;
                }

                if (anchorDebugBuilder.Length > 0)
                {
                    anchorDebugBuilder.Append(" / ");
                }

                anchorDebugBuilder.Append(anchors[i].SideSign >= 0f ? "R" : "L");
                anchorDebugBuilder.Append("-");
                anchorDebugBuilder.Append(anchors[i].RoleName);
                anchorDebugBuilder.Append(":");
                anchorDebugBuilder.Append(anchors[i].PartName);
                anchorDebugBuilder.Append("(");
                anchorDebugBuilder.Append(anchors[i].Score.ToString("F2", CultureInfo.InvariantCulture));
                anchorDebugBuilder.Append(")");
            }

            return anchorDebugBuilder.Length > 0 ? anchorDebugBuilder.ToString() : "none";
        }

        private int FindExistingEmitterForPart(Part part, bool[] matched)
        {
            if (part == null) return -1;
            for (int j = 0; j < activeSlotCount; j++)
            {
                if (!matched[j] && emitters[j] != null && emitters[j].IsBoundTo(part))
                    return j;
            }
            return -1;
        }

        private bool ShouldAdoptNewAnchor(WingtipRibbonAnchor current, WingtipRibbonAnchor candidate)
        {
            if (!current.IsValid)
                return true;
            if (!candidate.IsValid)
                return false;
            if (current.Part == candidate.Part)
                return true;
            return candidate.Score > current.Score + AnchorSwitchMargin;
        }

        private static bool IsPartInVessel(WingtipRibbonEmitter emitter, Vessel vessel)
        {
            if (vessel == null || vessel.parts == null)
                return false;
            for (int i = 0; i < vessel.parts.Count; i++)
            {
                if (emitter.IsBoundTo(vessel.parts[i]))
                    return true;
            }
            return false;
        }

        private bool TryBuildSample(float dt, out AeroRibbonSample sample)
        {
            sample = default(AeroRibbonSample);
            if (vessel == null || vessel.mainBody == null || !vessel.mainBody.atmosphere)
                return false;

            if (vessel.Splashed || vessel.isEVA)
                return false;

            Vector3 forward;
            Vector3 right;
            Vector3 up;
            Vector3 airflowBack;
            if (!AeroFlightUtil.TryGetFlightBasis(vessel, out forward, out right, out up, out airflowBack))
                return false;

            float speed = Mathf.Abs((float)vessel.srfSpeed);
            float density = Mathf.Max(0f, (float)vessel.atmDensity);
            float dynamicPressure = Mathf.Max(0f, (float)vessel.dynamicPressurekPa);
            float mach = Mathf.Max(0f, (float)vessel.mach);
            float loadFactor = Mathf.Max(0.85f, (float)vessel.geeForce_immediate);
            float radarAltitude = Mathf.Max(0f, (float)vessel.radarAltitude);

            float density01 = Mathf.InverseLerp(AeroFxRuntimeConfig.MinAtmDensity, AeroFxRuntimeConfig.FullAtmDensity, density);
            float speed01 = Mathf.InverseLerp(AeroFxRuntimeConfig.MinSurfaceSpeed, AeroFxRuntimeConfig.FullSurfaceSpeed, speed);
            float pressure01 = Mathf.InverseLerp(AeroFxRuntimeConfig.MinDynamicPressure, AeroFxRuntimeConfig.FullDynamicPressure, dynamicPressure);
            float mach01 = Mathf.InverseLerp(AeroFxRuntimeConfig.MinMach, AeroFxRuntimeConfig.FullMach, mach);
            float load01 = Mathf.InverseLerp(AeroFxRuntimeConfig.MinLoadFactor, AeroFxRuntimeConfig.FullLoadFactor, loadFactor);
            float nearGround01 = 1f - Mathf.InverseLerp(1200f, 9000f, radarAltitude);
            float angularMotion = vessel.angularVelocity.sqrMagnitude > 0f
                ? vessel.angularVelocity.magnitude
                : 0f;
            float maneuverCurl = Mathf.InverseLerp(1.08f, 2.60f, loadFactor);
            float angularCurl = Mathf.InverseLerp(0.06f, 0.65f, angularMotion);

            float baseCondense = density01 * Mathf.Lerp(0.58f, 1.00f, pressure01);
            float speedBias = Mathf.Lerp(0.94f, 1f, speed01);
            float maneuverBias = Mathf.Lerp(0.48f + 0.22f * mach01, 1f, load01);
            float nearGroundBias = Mathf.Lerp(1f, 1.15f, nearGround01 * density01);
            float visibilityBias = Mathf.Lerp(0.62f, 1.12f, Mathf.Max(load01, mach01 * 0.70f));
            float baseActivation = Mathf.Clamp01(baseCondense * speedBias * maneuverBias * nearGroundBias * visibilityBias);
            float speedActivationGate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(50f, 85f, speed));
            float machThreshold = AeroFxRuntimeConfig.GetMachThreshold(AeroFxConfig.MachThresholdMode);
            float machThresholdGate = machThreshold > 0f
                ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(machThreshold, machThreshold + AeroFxRuntimeConfig.MachThresholdFadeRange, mach))
                : 1f;
            float bodyVisibility = AeroFxRuntimeConfig.GetBodyVisibilityMultiplier(vessel.mainBody.bodyName);
            float gatedActivation = baseActivation * speedActivationGate * machThresholdGate;
            float activation;
            if (AeroFxConfig.UseManeuverOnly)
            {
                float targetManeuverGate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.10f, 0.28f, angularMotion));
                if (!hasSmoothedManeuverGate)
                {
                    smoothedManeuverGate = targetManeuverGate;
                    hasSmoothedManeuverGate = true;
                }
                else
                {
                    float maneuverGateFadeSpeed = targetManeuverGate > smoothedManeuverGate
                        ? ManeuverGateFadeInSpeed
                        : ManeuverGateFadeOutSpeed;
                    smoothedManeuverGate = Mathf.Lerp(
                        smoothedManeuverGate,
                        targetManeuverGate,
                        Mathf.Clamp01(dt * maneuverGateFadeSpeed));
                }

                float maneuverLoadBoost = Mathf.Lerp(0.90f, 1.15f, maneuverCurl);
                float maneuverActivation = baseActivation * speedActivationGate * smoothedManeuverGate * maneuverLoadBoost;
                activation = AeroFxConfig.MachThresholdMode == 0
                    ? maneuverActivation
                    : Mathf.Max(gatedActivation, maneuverActivation);
                sample.ManeuverGate01 = smoothedManeuverGate;
            }
            else
            {
                smoothedManeuverGate = 1f;
                hasSmoothedManeuverGate = false;
                sample.ManeuverGate01 = 1f;
                activation = gatedActivation;
            }
            activation = Mathf.Clamp01(activation * bodyVisibility);
            if (activation < AeroFxRuntimeConfig.ActivationFloor)
                activation = 0f;

            sample.ForwardAxis = forward;
            sample.RightAxis = right;
            sample.UpAxis = up;
            sample.AirflowBack = airflowBack;
            sample.Speed = speed;
            sample.Mach = mach;
            sample.AtmosphereDensity = density;
            sample.DynamicPressure = dynamicPressure;
            sample.LoadFactor = loadFactor;
            sample.RadarAltitude = radarAltitude;
            sample.BodyVisibility = bodyVisibility;
            sample.Activation = Mathf.Clamp01(activation);
            sample.Speed01 = speed01;
            sample.Pressure01 = pressure01;
            sample.Density01 = density01;
            sample.Mach01 = mach01;
            sample.Load01 = load01;
            sample.NearGround01 = nearGround01;
            float lengthBias = pressure01 * 0.26f + density01 * 0.16f + nearGround01 * 0.10f + load01 * 0.12f;
            float lowSpeedLengthBoost = 1f - Mathf.InverseLerp(110f, 200f, speed);
            float highSpeedShorten = 1f - Mathf.InverseLerp(120f, 320f, speed);
            float sonicShorten = 1f - Mathf.InverseLerp(0.92f, 1.10f, mach);
            sample.Length01 = Mathf.Clamp01(lengthBias * 0.38f + lowSpeedLengthBoost * 0.40f + highSpeedShorten * 0.14f + sonicShorten * 0.08f);

            float lowSpeedCurlBoost = 1f - Mathf.InverseLerp(125f, 180f, speed);
            float post180CurlFade = 1f - Mathf.InverseLerp(220f, 420f, speed);
            float sonicCurlFade = 1f - Mathf.InverseLerp(1.10f, 1.55f, mach);
            sample.Maneuver01 = Mathf.Clamp01(maneuverCurl * 0.58f + angularCurl * 0.72f + load01 * 0.12f);
            float straightCurl = pressure01 * 0.04f + density01 * 0.03f + nearGround01 * 0.02f;
            float maneuverResponse = maneuverCurl * (0.52f + lowSpeedCurlBoost * 0.58f)
                + angularCurl * (0.42f + lowSpeedCurlBoost * 0.44f);
            float dynamicCurl = straightCurl + maneuverResponse + load01 * 0.06f;
            float speedCurlEnvelope = Mathf.Lerp(0.30f, 1.00f, lowSpeedCurlBoost) * Mathf.Lerp(0.84f, 1.00f, post180CurlFade);
            float highSpeedManeuverRecover = Mathf.InverseLerp(300f, 860f, speed) * (maneuverCurl * 0.60f + angularCurl * 0.72f);
            sample.Curl01 = Mathf.Clamp01(dynamicCurl * speedCurlEnvelope * Mathf.Lerp(0.55f, 1.00f, sonicCurlFade) + highSpeedManeuverRecover);

            if (AeroFxConfig.DebugLogging && vessel == FlightGlobals.ActiveVessel)
            {
                sampleDebugTimer -= dt;
                if (sampleDebugTimer <= 0f)
                {
                    sampleDebugTimer = SampleDebugInterval;
                    AeroFxLog.DebugLog(Localizer.Format(
                        AeroFxLoc.LogVesselScan,
                        vessel.vesselName,
                        lastLiftPartCount,
                        lastCandidateCount,
                        sample.Activation.ToString("F2", CultureInfo.InvariantCulture),
                        sample.Speed.ToString("F1", CultureInfo.InvariantCulture)));
                }
            }

            return true;
        }
    }
}
