using System.Globalization;
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
        public float Curl01;
    }

    internal sealed class VesselAeroController
    {
        private const int MaxEmitters = 4;
        private const float SampleDebugInterval = 0.8f;
        private const float AnchorSwitchMargin = 0.45f;
        private const float EmitterGraceSeconds = 4.0f;

        private readonly Vessel vessel;
        private readonly WingtipRibbonAnchor[] anchors = new WingtipRibbonAnchor[MaxEmitters];
        private readonly WingtipRibbonEmitter[] emitters = new WingtipRibbonEmitter[MaxEmitters];
        private readonly float[] emitterGraceTimers = new float[MaxEmitters];
        private int activeSlotCount;

        private int cachedPartCount = -1;
        private int lastLiftPartCount;
        private int lastCandidateCount;
        private float anchorRefreshTimer;
        private float sampleDebugTimer;

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

        public string AnchorSummary
        {
            get
            {
                string summary = "";
                for (int i = 0; i < activeSlotCount; i++)
                {
                    if (i > 0) summary += " / ";
                    summary += anchors[i].PartName;
                }
                return summary.Length > 0 ? summary : "none";
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

        public void TryRebuild()
        {
            if (!IsStillValid())
                return;

            anchorRefreshTimer -= 1.0f;
            int currentPartCount = vessel.parts != null ? vessel.parts.Count : 0;
            if (currentPartCount != cachedPartCount || anchorRefreshTimer <= 0f || !HasAnyEmitters)
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
            if (!TryBuildSample(out sample))
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

            WingtipRibbonAnchor[] newAnchors = new WingtipRibbonAnchor[MaxEmitters];
            int newCount = AeroTrailAnchors.TryResolveAll(
                vessel,
                newAnchors,
                MaxEmitters,
                out lastLiftPartCount,
                out lastCandidateCount);

            bool[] oldMatched = new bool[MaxEmitters];
            WingtipRibbonAnchor[] resultAnchors = new WingtipRibbonAnchor[MaxEmitters];
            WingtipRibbonEmitter[] resultEmitters = new WingtipRibbonEmitter[MaxEmitters];
            float[] resultGrace = new float[MaxEmitters];

            for (int i = 0; i < newCount; i++)
            {
                int existingIdx = FindExistingEmitterForPart(newAnchors[i].Part, oldMatched);
                if (existingIdx >= 0)
                {
                    oldMatched[existingIdx] = true;

                    if (ShouldAdoptNewAnchor(anchors[existingIdx], newAnchors[i]))
                        resultAnchors[i] = newAnchors[i];
                    else
                        resultAnchors[i] = anchors[existingIdx];

                    resultEmitters[i] = emitters[existingIdx];
                    resultGrace[i] = 0f;
                }
                else
                {
                    resultAnchors[i] = newAnchors[i];
                    string label = newAnchors[i].SideSign >= 0 ? "R" + i : "L" + i;
                    resultEmitters[i] = new WingtipRibbonEmitter(newAnchors[i].Part, label);
                    resultGrace[i] = 0f;
                }
            }

            int resultCount = newCount;

            for (int j = 0; j < activeSlotCount; j++)
            {
                if (oldMatched[j] || emitters[j] == null)
                    continue;

                bool partStillExists = IsPartInVessel(emitters[j], vessel);
                if (partStillExists && resultCount < MaxEmitters)
                {
                    float grace = emitterGraceTimers[j] + AeroFxRuntimeConfig.AnchorRefreshInterval;
                    if (grace < EmitterGraceSeconds)
                    {
                        resultAnchors[resultCount] = anchors[j];
                        resultEmitters[resultCount] = emitters[j];
                        resultGrace[resultCount] = grace;
                        resultEmitters[resultCount].StopEmission();
                        resultCount++;
                        continue;
                    }
                }

                emitters[j].Dispose();
            }

            for (int i = 0; i < MaxEmitters; i++)
            {
                anchors[i] = i < resultCount ? resultAnchors[i] : default(WingtipRibbonAnchor);
                emitters[i] = i < resultCount ? resultEmitters[i] : null;
                emitterGraceTimers[i] = i < resultCount ? resultGrace[i] : 0f;
            }
            activeSlotCount = resultCount;

            if ((forceLog || newCount == 0) && AeroFxConfig.DebugLogging && vessel == FlightGlobals.ActiveVessel)
            {
                AeroFxLog.DebugLog(Localizer.Format(
                    AeroFxLoc.LogAnchorScan,
                    vessel.vesselName,
                    lastLiftPartCount,
                    lastCandidateCount,
                    activeSlotCount > 0 ? anchors[0].PartName : "none",
                    activeSlotCount > 1 ? anchors[1].PartName : "none"));
            }
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

        private bool TryBuildSample(out AeroRibbonSample sample)
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

            float baseCondense = density01 * Mathf.Lerp(0.58f, 1.00f, pressure01);
            float speedBias = Mathf.Lerp(0.94f, 1f, speed01);
            float maneuverBias = Mathf.Lerp(0.48f + 0.22f * mach01, 1f, load01);
            float nearGroundBias = Mathf.Lerp(1f, 1.15f, nearGround01 * density01);
            float visibilityBias = Mathf.Lerp(0.62f, 1.12f, Mathf.Max(load01, mach01 * 0.70f));
            float activation = Mathf.Clamp01(baseCondense * speedBias * maneuverBias * nearGroundBias * visibilityBias);
            float speedActivationGate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(50f, 85f, speed));
            float bodyVisibility = AeroFxRuntimeConfig.GetBodyVisibilityMultiplier(vessel.mainBody.bodyName);
            activation *= speedActivationGate;
            activation *= bodyVisibility;
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
            float maneuverCurl = Mathf.InverseLerp(1.08f, 2.60f, loadFactor);
            float angularMotion = vessel.angularVelocity != Vector3.zero
                ? vessel.angularVelocity.magnitude
                : 0f;
            float angularCurl = Mathf.InverseLerp(0.06f, 0.65f, angularMotion);
            float straightCurl = pressure01 * 0.04f + density01 * 0.03f + nearGround01 * 0.02f;
            float maneuverResponse = maneuverCurl * (0.52f + lowSpeedCurlBoost * 0.58f)
                + angularCurl * (0.42f + lowSpeedCurlBoost * 0.44f);
            float dynamicCurl = straightCurl + maneuverResponse + load01 * 0.06f;
            float speedCurlEnvelope = Mathf.Lerp(0.30f, 1.00f, lowSpeedCurlBoost) * Mathf.Lerp(0.84f, 1.00f, post180CurlFade);
            float highSpeedManeuverRecover = Mathf.InverseLerp(300f, 860f, speed) * (maneuverCurl * 0.60f + angularCurl * 0.72f);
            sample.Curl01 = Mathf.Clamp01(dynamicCurl * speedCurlEnvelope * Mathf.Lerp(0.55f, 1.00f, sonicCurlFade) + highSpeedManeuverRecover);

            if (AeroFxConfig.DebugLogging && vessel == FlightGlobals.ActiveVessel)
            {
                sampleDebugTimer -= Time.deltaTime;
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
