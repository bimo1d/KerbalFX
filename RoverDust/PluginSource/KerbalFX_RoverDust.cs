using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using KSP.Localization;
using UnityEngine;

namespace KerbalFX.RoverDust
{
    internal static class RoverDustLoc
    {
        public const string UiTitle = "#LOC_KerbalFX_RoverDust_UI_Title";
        public const string UiSectionMain = "#LOC_KerbalFX_UI_SectionMain";

        public const string UiEnableDust = "#LOC_KerbalFX_RoverDust_UI_EnableDust";
        public const string UiEnableDustTip = "#LOC_KerbalFX_RoverDust_UI_EnableDust_TT";

        public const string UiQualityScale = "#LOC_KerbalFX_RoverDust_UI_QualityScale";
        public const string UiQualityScaleTip = "#LOC_KerbalFX_RoverDust_UI_QualityScale_TT";

        public const string UiSurfaceTint = "#LOC_KerbalFX_RoverDust_UI_SurfaceTint";
        public const string UiSurfaceTintTip = "#LOC_KerbalFX_RoverDust_UI_SurfaceTint_TT";
        public const string UiLightAware = "#LOC_KerbalFX_RoverDust_UI_LightAware";
        public const string UiLightAwareTip = "#LOC_KerbalFX_RoverDust_UI_LightAware_TT";

        public const string UiDebug = "#LOC_KerbalFX_RoverDust_UI_Debug";
        public const string UiDebugTip = "#LOC_KerbalFX_RoverDust_UI_Debug_TT";

        public const string LogSettingsUpdated = "#LOC_KerbalFX_RoverDust_Log_SettingsUpdated";
        public const string LogBootstrapStart = "#LOC_KerbalFX_RoverDust_Log_BootstrapStart";
        public const string LogBootstrapStop = "#LOC_KerbalFX_RoverDust_Log_BootstrapStop";
        public const string LogHeartbeat = "#LOC_KerbalFX_RoverDust_Log_Heartbeat";
        public const string LogAttached = "#LOC_KerbalFX_RoverDust_Log_Attached";
        public const string LogVesselScan = "#LOC_KerbalFX_RoverDust_Log_VesselScan";
        public const string LogProfile = "#LOC_KerbalFX_RoverDust_Log_Profile";
        public const string LogSuppressed = "#LOC_KerbalFX_RoverDust_Log_Suppressed";
        public const string LogEmitter = "#LOC_KerbalFX_RoverDust_Log_Emitter";
        public const string LogTickSkip = "#LOC_KerbalFX_RoverDust_Log_TickSkip";
        public const string LogEmitterSkip = "#LOC_KerbalFX_RoverDust_Log_EmitterSkip";
        public const string LogConfig = "#LOC_KerbalFX_RoverDust_Log_Config";
        public const string LogHotReloadFailed = "#LOC_KerbalFX_RoverDust_Log_HotReloadFailed";
        public const string LogSurfaceSample = "#LOC_KerbalFX_RoverDust_Log_SurfaceSample";
        public const string LogLightSample = "#LOC_KerbalFX_RoverDust_Log_LightSample";

    }

    public class RoverDustParameters : GameParameters.CustomParameterNode
    {
        [GameParameters.CustomParameterUI(RoverDustLoc.UiEnableDust, toolTip = RoverDustLoc.UiEnableDustTip)]
        public bool enableDust = true;

        [GameParameters.CustomIntParameterUI(
            RoverDustLoc.UiQualityScale,
            toolTip = RoverDustLoc.UiQualityScaleTip,
            minValue = 25,
            maxValue = 200,
            stepSize = 25,
            displayFormat = "N0"
        )]
        public int qualityScale = 100;

        [GameParameters.CustomParameterUI(RoverDustLoc.UiSurfaceTint, toolTip = RoverDustLoc.UiSurfaceTintTip)]
        public bool adaptSurfaceColor = true;

        [GameParameters.CustomParameterUI(RoverDustLoc.UiLightAware, toolTip = RoverDustLoc.UiLightAwareTip)]
        public bool useLightAware = true;

        [GameParameters.CustomParameterUI(RoverDustLoc.UiDebug, toolTip = RoverDustLoc.UiDebugTip)]
        public bool debugLogging;

        public override string Title { get { return Localizer.Format(RoverDustLoc.UiTitle); } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override string Section { get { return "KerbalFX_01_Main"; } }
        public override string DisplaySection { get { return Localizer.Format(RoverDustLoc.UiSectionMain); } }
        public override int SectionOrder { get { return 10; } }
        public override bool HasPresets { get { return false; } }
    }

    internal static class RoverDustConfig
    {
        public static bool EnableDust = true;
        public static bool DebugLogging;
        public static bool AdaptSurfaceColor = true;
        public static bool UseLightAware = true;
        public static int QualityPercent = 100;
        public static int Revision;

        private static bool initialized;

        public static void Refresh()
        {
            bool newEnable = true;
            bool newAdaptSurfaceColor = true;
            bool newUseLightAware = true;
            bool newDebug = false;
            int newQualityPercent = 100;

            if (HighLogic.CurrentGame != null && HighLogic.CurrentGame.Parameters != null)
            {
                RoverDustParameters p = HighLogic.CurrentGame.Parameters.CustomParams<RoverDustParameters>();
                if (p != null)
                {
                    newEnable = p.enableDust;
                    newAdaptSurfaceColor = p.adaptSurfaceColor;
                    newUseLightAware = p.useLightAware;
                    newDebug = p.debugLogging;
                    newQualityPercent = Mathf.Clamp(p.qualityScale, 25, 200);
                }
            }

            bool changed = !initialized
                || newEnable != EnableDust
                || newAdaptSurfaceColor != AdaptSurfaceColor
                || newUseLightAware != UseLightAware
                || newDebug != DebugLogging
                || newQualityPercent != QualityPercent;

            EnableDust = newEnable;
            AdaptSurfaceColor = newAdaptSurfaceColor;
            UseLightAware = newUseLightAware;
            DebugLogging = newDebug;
            QualityPercent = newQualityPercent;

            if (changed)
            {
                initialized = true;
                Revision++;
                RoverDustLog.Info(Localizer.Format(
                    RoverDustLoc.LogSettingsUpdated,
                    EnableDust, QualityPercent, AdaptSurfaceColor, UseLightAware, DebugLogging));
            }
        }
    }

    internal static class RoverDustRuntimeConfig
    {
        public static int Revision;

        public const float EmissionMultiplier = 2.55f;
        public const float MaxParticlesMultiplier = 1.70f;
        public const float RadiusScaleMultiplier = 1.38f;
        public const float WheelBoostPower = 1.72f;
        public const float WheelBoostMax = 6.20f;

        private static readonly KerbalFxBodyVisibilityProfile bodyProfile =
            new KerbalFxBodyVisibilityProfile("KERBALFX_ROVER_DUST", GetConfigPath, SeedDefaultBodyVisibility, 0.30f, 3.00f);

        private static readonly KerbalFxSurfaceTintProfile tintProfile =
            new KerbalFxSurfaceTintProfile("KERBALFX_ROVER_DUST", GetConfigPath, SeedDefaultBodyTints);

        private static readonly KerbalFxLightAwareProfile lightAwareProfile =
            new KerbalFxLightAwareProfile(
                "KERBALFX_ROVER_DUST",
                GetConfigPath,
                SeedDefaultLightAware,
                new KerbalFxLightAwareEntry
                {
                    DarkScale = 0.12f,
                    BrightScale = 1f,
                    TwilightFloor = 0.24f,
                    MinPerceived = 0.05f,
                    ColorTintStrength = 0.32f
                });

        public static KerbalFxSurfaceTintProfile TintProfile { get { return tintProfile; } }

        public static KerbalFxLightAwareProfile LightAwareProfile { get { return lightAwareProfile; } }

        public static void Refresh()
        {
            bodyProfile.Refresh();
            tintProfile.Refresh();
            lightAwareProfile.Refresh();
            Revision++;
            RoverDustLog.Info(Localizer.Format(RoverDustLoc.LogConfig, "GameDatabase",
                "BodyVisibility=" + bodyProfile.Count + " BodyTint=" + tintProfile.Count
                + " LightAware=" + lightAwareProfile.Count));
        }

        public static void TryHotReloadFromDisk()
        {
            string visibilityFailure;
            string tintFailure;
            string lightAwareFailure;
            bool visibilityChanged = bodyProfile.TryHotReloadFromDisk(out visibilityFailure);
            bool tintChanged = tintProfile.TryHotReloadFromDisk(out tintFailure);
            bool lightAwareChanged = lightAwareProfile.TryHotReloadFromDisk(out lightAwareFailure);
            if (!visibilityChanged && !tintChanged && !lightAwareChanged)
                return;

            if (visibilityFailure != null)
                RoverDustLog.Info(Localizer.Format(RoverDustLoc.LogHotReloadFailed, GetConfigPath(), visibilityFailure));
            if (tintFailure != null)
                RoverDustLog.Info(Localizer.Format(RoverDustLoc.LogHotReloadFailed, GetConfigPath(), tintFailure));
            if (lightAwareFailure != null)
                RoverDustLog.Info(Localizer.Format(RoverDustLoc.LogHotReloadFailed, GetConfigPath(), lightAwareFailure));

            Revision++;
            RoverDustLog.Info(Localizer.Format(RoverDustLoc.LogConfig, "HotReload",
                "BodyVisibility=" + bodyProfile.Count + " BodyTint=" + tintProfile.Count
                + " LightAware=" + lightAwareProfile.Count));
        }

        public static float GetBodyVisibilityMultiplier(string bodyName)
        {
            return bodyProfile.Get(bodyName);
        }

        private static void SeedDefaultBodyVisibility(Dictionary<string, float> dict)
        {
            dict["Kerbin"] = 1.00f;
            dict["Mun"] = 1.65f;
            dict["Minmus"] = 1.55f;
            dict["Duna"] = 1.00f;
            dict["Moho"] = 1.85f;
            dict["Eeloo"] = 2.00f;
            dict["Eve"] = 1.55f;
            dict["Vall"] = 1.45f;
            dict["Bop"] = 1.45f;
            dict["Dres"] = 1.50f;
            dict["Ike"] = 1.12f;
            dict["Pol"] = 1.15f;
            dict["Tylo"] = 0.92f;
        }

        private static void SeedDefaultLightAware(Dictionary<string, KerbalFxLightAwareEntry> dict)
        {
            dict["Kerbin"] = new KerbalFxLightAwareEntry { DarkScale = 0.18f, BrightScale = 1f, TwilightFloor = 0.36f, MinPerceived = 0.07f, ColorTintStrength = 0.32f };
            dict["Laythe"] = new KerbalFxLightAwareEntry { DarkScale = 0.18f, BrightScale = 1f, TwilightFloor = 0.34f, MinPerceived = 0.07f, ColorTintStrength = 0.34f };
            dict["Eve"] = new KerbalFxLightAwareEntry { DarkScale = 0.24f, BrightScale = 1f, TwilightFloor = 0.42f, MinPerceived = 0.10f, ColorTintStrength = 0.40f };
            dict["Duna"] = new KerbalFxLightAwareEntry { DarkScale = 0.16f, BrightScale = 1f, TwilightFloor = 0.32f, MinPerceived = 0.06f, ColorTintStrength = 0.36f };
            dict["Mun"] = new KerbalFxLightAwareEntry { DarkScale = 0.08f, BrightScale = 1f, TwilightFloor = 0.08f, MinPerceived = 0.04f, ColorTintStrength = 0.28f };
            dict["Minmus"] = new KerbalFxLightAwareEntry { DarkScale = 0.08f, BrightScale = 1f, TwilightFloor = 0.08f, MinPerceived = 0.04f, ColorTintStrength = 0.30f };
            dict["Ike"] = new KerbalFxLightAwareEntry { DarkScale = 0.09f, BrightScale = 1f, TwilightFloor = 0.09f, MinPerceived = 0.04f, ColorTintStrength = 0.30f };
            dict["Gilly"] = new KerbalFxLightAwareEntry { DarkScale = 0.07f, BrightScale = 1f, TwilightFloor = 0.07f, MinPerceived = 0.04f, ColorTintStrength = 0.28f };
            dict["Vall"] = new KerbalFxLightAwareEntry { DarkScale = 0.07f, BrightScale = 1f, TwilightFloor = 0.07f, MinPerceived = 0.04f, ColorTintStrength = 0.28f };
            dict["Tylo"] = new KerbalFxLightAwareEntry { DarkScale = 0.09f, BrightScale = 1f, TwilightFloor = 0.09f, MinPerceived = 0.04f, ColorTintStrength = 0.28f };
            dict["Bop"] = new KerbalFxLightAwareEntry { DarkScale = 0.07f, BrightScale = 1f, TwilightFloor = 0.07f, MinPerceived = 0.04f, ColorTintStrength = 0.28f };
            dict["Pol"] = new KerbalFxLightAwareEntry { DarkScale = 0.08f, BrightScale = 1f, TwilightFloor = 0.08f, MinPerceived = 0.04f, ColorTintStrength = 0.30f };
            dict["Dres"] = new KerbalFxLightAwareEntry { DarkScale = 0.07f, BrightScale = 1f, TwilightFloor = 0.07f, MinPerceived = 0.04f, ColorTintStrength = 0.28f };
            dict["Eeloo"] = new KerbalFxLightAwareEntry { DarkScale = 0.07f, BrightScale = 1f, TwilightFloor = 0.07f, MinPerceived = 0.04f, ColorTintStrength = 0.30f };
            dict["Moho"] = new KerbalFxLightAwareEntry { DarkScale = 0.09f, BrightScale = 1f, TwilightFloor = 0.09f, MinPerceived = 0.04f, ColorTintStrength = 0.32f };
        }

        private static void SeedDefaultBodyTints(Dictionary<string, KerbalFxBodyTintEntry> dict)
        {
            dict["Kerbin"] = KerbalFxBodyTintEntry.FromColor(new Color(0.62f, 0.55f, 0.42f));
            dict["Mun"] = KerbalFxBodyTintEntry.FromColor(new Color(0.60f, 0.58f, 0.54f), 0.40f);
            dict["Minmus"] = KerbalFxBodyTintEntry.FromColor(new Color(0.66f, 0.75f, 0.68f), 0.40f);
            dict["Duna"] = KerbalFxBodyTintEntry.FromColor(new Color(0.50f, 0.36f, 0.30f), 0.40f);
            dict["Ike"] = KerbalFxBodyTintEntry.FromColor(new Color(0.55f, 0.52f, 0.50f));
            dict["Eve"] = KerbalFxBodyTintEntry.FromColor(new Color(0.58f, 0.42f, 0.62f), 0.80f);
            dict["Gilly"] = KerbalFxBodyTintEntry.FromColor(new Color(0.54f, 0.50f, 0.44f), 0.65f);
            dict["Laythe"] = KerbalFxBodyTintEntry.FromColor(new Color(0.52f, 0.60f, 0.56f), 1.35f);
            dict["Moho"] = KerbalFxBodyTintEntry.FromColor(new Color(0.58f, 0.40f, 0.30f), 0.85f);
            dict["Dres"] = KerbalFxBodyTintEntry.FromColor(new Color(0.62f, 0.58f, 0.52f), 0.45f);
            dict["Vall"] = KerbalFxBodyTintEntry.FromColor(new Color(0.66f, 0.70f, 0.74f));
            dict["Tylo"] = KerbalFxBodyTintEntry.FromColor(new Color(0.74f, 0.70f, 0.62f));
            dict["Bop"] = KerbalFxBodyTintEntry.FromColor(new Color(0.50f, 0.46f, 0.40f), 1.10f);
            dict["Pol"] = KerbalFxBodyTintEntry.FromColor(new Color(0.78f, 0.72f, 0.50f), 0.85f);
            dict["Eeloo"] = KerbalFxBodyTintEntry.FromColor(new Color(0.55f, 0.62f, 0.59f), 0.45f);
        }

        private static string GetConfigPath()
        {
            return Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "KerbalFX", "RoverDust", "KerbalFX_RoverDust.cfg");
        }
    }

    internal static class RoverDustLog
    {
        private static readonly KerbalFxLog impl = new KerbalFxLog(() => RoverDustConfig.DebugLogging);
        public static void Info(string message) { impl.Info(message); }
        public static void DebugLog(string message) { impl.DebugLog(message); }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    internal class RoverDustBootstrap : KerbalFxVesselControllerBootstrap<VesselDustController>
    {
        protected override bool IsModuleEnabled { get { return RoverDustConfig.EnableDust; } }
        protected override bool IsDebugLogging { get { return RoverDustConfig.DebugLogging; } }

        protected override void RefreshSettings()
        {
            RoverDustConfig.Refresh();
            RoverDustRuntimeConfig.TryHotReloadFromDisk();
        }

        protected override void OnBeforeStart()
        {
            RoverDustRuntimeConfig.Refresh();
        }

        protected override bool IsSupportedVessel(Vessel vessel)
        {
            return KerbalFxVesselUtil.IsSupportedFlightVessel(vessel);
        }

        protected override VesselDustController CreateController(Vessel vessel)
        {
            return new VesselDustController(vessel);
        }

        protected override bool ControllerHasEmitters(VesselDustController controller)
        {
            return controller.HasEmitters;
        }

        protected override int ControllerEmitterCount(VesselDustController controller)
        {
            return controller.EmitterCount;
        }

        protected override void TryRebuildController(VesselDustController controller, float refreshElapsed)
        {
            controller.TryRebuild();
        }

        protected override void LogBootstrapStart()
        {
            RoverDustLog.Info(Localizer.Format(RoverDustLoc.LogBootstrapStart));
        }

        protected override void LogBootstrapStop()
        {
            RoverDustLog.Info(Localizer.Format(RoverDustLoc.LogBootstrapStop));
        }

        protected override void LogHeartbeat(int controllerCount)
        {
            RoverDustLog.DebugLog(Localizer.Format(RoverDustLoc.LogHeartbeat, controllerCount));
        }

        protected override void LogAttached(int emitterCount, string vesselName)
        {
            RoverDustLog.DebugLog(Localizer.Format(RoverDustLoc.LogAttached, emitterCount, vesselName));
        }
    }

    internal sealed class VesselDustController : IVesselFxController
    {
        private readonly Vessel vessel;
        private readonly List<WheelDustEmitter> emitters = new List<WheelDustEmitter>();
        private readonly List<WheelEmitterSpec> emitterSpecs = new List<WheelEmitterSpec>();
        private readonly List<WheelPartScan> wheelPartScans = new List<WheelPartScan>();
        private readonly List<WheelCandidate> wheelCandidates = new List<WheelCandidate>();
        private KerbalFxVesselPartSnapshot partSnapshot;
        private float skipDebugTimer;
        private string lastSkipReason = string.Empty;
        private const int MaxEffectWheelColliders = 4;
        private const float SkipDebugInterval = 1.2f;

        public int EmitterCount { get { return emitters.Count; } }
        public bool HasEmitters { get { return emitters.Count > 0; } }

        public VesselDustController(Vessel vessel)
        {
            this.vessel = vessel;
            RebuildEmitters();
        }

        public bool IsStillValid()
        {
            return vessel != null && vessel.loaded && !vessel.packed;
        }

        public void TryRebuild()
        {
            if (vessel == null || vessel.parts == null)
                return;

            if (partSnapshot.HasChanged(vessel))
                RebuildEmitters();
        }

        public void Tick(float dt)
        {
            if (emitters.Count == 0)
            {
                LogTickSkip("noEmitters", dt);
                return;
            }
            if (vessel == null || !vessel.loaded || vessel.packed || vessel.Splashed)
            {
                StopAll();
                LogTickSkip(
                    "invalid loaded=" + (vessel != null && vessel.loaded)
                    + " packed=" + (vessel != null && vessel.packed)
                    + " splashed=" + (vessel != null && vessel.Splashed),
                    dt);
                return;
            }

            bool moving = vessel.srfSpeed > 0.35;
            bool nearGround = vessel.Landed || vessel.heightFromTerrain < 7.0;
            if (!moving || !nearGround)
            {
                StopAll();
                LogTickSkip(
                    "gate moving=" + moving
                    + " nearGround=" + nearGround
                    + " speed=" + vessel.srfSpeed.ToString("F2", CultureInfo.InvariantCulture)
                    + " landed=" + vessel.Landed
                    + " hTerrain=" + vessel.heightFromTerrain.ToString("F2", CultureInfo.InvariantCulture),
                    dt);
                return;
            }

            lastSkipReason = string.Empty;
            for (int i = 0; i < emitters.Count; i++)
                emitters[i].Tick(vessel, dt);
        }

        private void LogTickSkip(string reason, float dt)
        {
            if (!RoverDustConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel)
                return;

            if (reason != lastSkipReason)
            {
                skipDebugTimer = 0f;
                lastSkipReason = reason;
            }

            skipDebugTimer -= dt;
            if (skipDebugTimer > 0f)
                return;

            skipDebugTimer = SkipDebugInterval;
            RoverDustLog.DebugLog(Localizer.Format(
                RoverDustLoc.LogTickSkip,
                vessel != null ? vessel.vesselName : "null",
                emitters.Count,
                reason));
        }

        public void StopAll()
        {
            for (int i = 0; i < emitters.Count; i++)
                emitters[i].StopEmission();
        }

        public void Dispose()
        {
            DisposeEmitters();
        }

        private void RebuildEmitters()
        {
            DisposeEmitters();
            emitterSpecs.Clear();
            wheelPartScans.Clear();
            wheelCandidates.Clear();
            partSnapshot.Capture(vessel);
            if (vessel == null || vessel.parts == null)
                return;

            int wheelPartCount = 0;
            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part part = vessel.parts[i];
                WheelCollider[] colliders;
                if (!PartLooksLikeWheel(part, out colliders))
                    continue;

                wheelPartCount++;
                wheelPartScans.Add(new WheelPartScan(part, colliders));
                AddWheelCandidates(part, colliders);
            }

            HashSet<int> selectedWheelIds = SelectEffectWheelIds();
            for (int i = 0; i < wheelPartScans.Count; i++)
            {
                WheelCollider[] selected = FilterSelectedColliders(wheelPartScans[i].Colliders, selectedWheelIds);
                AddPartEmitterSpecs(wheelPartScans[i].Part, selected);
            }

            float vesselBudgetScale = GetVesselEmitterBudgetScale(emitterSpecs.Count);
            for (int i = 0; i < emitterSpecs.Count; i++)
                emitters.Add(new WheelDustEmitter(emitterSpecs[i].Part, emitterSpecs[i].Wheels, vesselBudgetScale, i));

            LogVesselScan(wheelPartCount);
            wheelPartScans.Clear();
            wheelCandidates.Clear();
        }

        private void AddWheelCandidates(Part part, WheelCollider[] colliders)
        {
            if (part == null || colliders == null)
                return;

            Transform vesselTransform = vessel != null ? vessel.transform : null;
            for (int i = 0; i < colliders.Length; i++)
            {
                WheelCollider collider = colliders[i];
                if (collider == null || collider.transform == null)
                    continue;

                Vector3 worldPos = collider.transform.position;
                Vector3 vesselLocal = vesselTransform != null ? vesselTransform.InverseTransformPoint(worldPos) : part.transform.InverseTransformPoint(worldPos);
                wheelCandidates.Add(new WheelCandidate(collider, vesselLocal));
            }
        }

        private HashSet<int> SelectEffectWheelIds()
        {
            HashSet<int> selected = new HashSet<int>();
            if (wheelCandidates.Count <= MaxEffectWheelColliders)
            {
                for (int i = 0; i < wheelCandidates.Count; i++)
                    selected.Add(wheelCandidates[i].Id);
                return selected;
            }

            Vector3 center = Vector3.zero;
            for (int i = 0; i < wheelCandidates.Count; i++)
                center += wheelCandidates[i].VesselLocalPosition;
            center /= wheelCandidates.Count;

            int[] quadrantCandidate = { -1, -1, -1, -1 };
            float[] quadrantScore = { float.MinValue, float.MinValue, float.MinValue, float.MinValue };
            for (int i = 0; i < wheelCandidates.Count; i++)
            {
                Vector3 delta = wheelCandidates[i].VesselLocalPosition - center;
                int quadrant = (delta.z >= 0f ? 2 : 0) + (delta.x >= 0f ? 1 : 0);
                float score = Mathf.Abs(delta.x) + Mathf.Abs(delta.z) + Mathf.Abs(delta.y) * 0.15f;
                if (score <= quadrantScore[quadrant])
                    continue;
                quadrantScore[quadrant] = score;
                quadrantCandidate[quadrant] = i;
            }

            for (int i = 0; i < quadrantCandidate.Length; i++)
                if (quadrantCandidate[i] >= 0)
                    selected.Add(wheelCandidates[quadrantCandidate[i]].Id);

            while (selected.Count < MaxEffectWheelColliders)
            {
                int best = -1;
                float bestScore = float.MinValue;
                for (int i = 0; i < wheelCandidates.Count; i++)
                {
                    if (selected.Contains(wheelCandidates[i].Id))
                        continue;
                    float score = (wheelCandidates[i].VesselLocalPosition - center).sqrMagnitude;
                    if (score <= bestScore)
                        continue;
                    bestScore = score;
                    best = i;
                }
                if (best < 0)
                    break;
                selected.Add(wheelCandidates[best].Id);
            }

            return selected;
        }

        private static WheelCollider[] FilterSelectedColliders(WheelCollider[] colliders, HashSet<int> selectedWheelIds)
        {
            if (colliders == null || colliders.Length == 0 || selectedWheelIds == null || selectedWheelIds.Count == 0)
                return new WheelCollider[0];

            List<WheelCollider> selected = new List<WheelCollider>(Mathf.Min(colliders.Length, MaxEffectWheelColliders));
            for (int i = 0; i < colliders.Length; i++)
            {
                WheelCollider collider = colliders[i];
                if (collider != null && selectedWheelIds.Contains(collider.GetInstanceID()))
                    selected.Add(collider);
            }
            return selected.ToArray();
        }

        private void AddPartEmitterSpecs(Part part, WheelCollider[] colliders)
        {
            List<WheelCollider[]> clusters = BuildWheelClusters(part, colliders);
            for (int i = 0; i < clusters.Count; i++)
            {
                WheelCollider[] cluster = clusters[i];
                if (cluster != null && cluster.Length > 0)
                    emitterSpecs.Add(new WheelEmitterSpec(part, cluster));
            }
        }

        private void LogVesselScan(int wheelPartCount)
        {
            if (!RoverDustConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel)
                return;
            RoverDustLog.DebugLog(Localizer.Format(RoverDustLoc.LogVesselScan, vessel.vesselName, wheelPartCount, emitters.Count));
        }

        private void DisposeEmitters()
        {
            for (int i = 0; i < emitters.Count; i++)
                emitters[i].Dispose();
            emitters.Clear();
        }

        private static bool PartLooksLikeWheel(Part part, out WheelCollider[] colliders)
        {
            colliders = null;
            if (part == null)
                return false;
            colliders = part.GetComponentsInChildren<WheelCollider>(true);
            return colliders != null && colliders.Length > 0;
        }

        private static float GetVesselEmitterBudgetScale(int emitterCount)
        {
            if (emitterCount <= 4)
                return 1f;

            float norm = Mathf.InverseLerp(4f, 10f, emitterCount);
            return Mathf.Lerp(1f, 0.52f, norm);
        }

        private static List<WheelCollider[]> BuildWheelClusters(Part part, WheelCollider[] colliders)
        {
            List<WheelCollider[]> result = new List<WheelCollider[]>();
            if (part == null || colliders == null || colliders.Length == 0)
                return result;

            List<WheelCluster> clusters = new List<WheelCluster>();
            for (int i = 0; i < colliders.Length; i++)
            {
                WheelCollider collider = colliders[i];
                if (collider == null || collider.transform == null)
                    continue;

                Vector3 localPos = part.transform.InverseTransformPoint(collider.transform.position);
                float radius = GetClusterWheelRadius(collider);
                bool attached = false;

                for (int c = 0; c < clusters.Count; c++)
                {
                    if (!clusters[c].TryAdd(collider, localPos, radius))
                        continue;

                    attached = true;
                    break;
                }

                if (!attached)
                    clusters.Add(new WheelCluster(collider, localPos, radius));
            }

            for (int i = 0; i < clusters.Count; i++)
                result.Add(clusters[i].ToArray());

            return result;
        }

        private static float GetClusterWheelRadius(WheelCollider collider)
        {
            if (collider == null)
                return 0.15f;

            float radius = Mathf.Max(0.05f, collider.radius);
            if (collider.transform != null)
            {
                Vector3 scale = collider.transform.lossyScale;
                float axisScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                if (axisScale > 0.001f)
                    radius = Mathf.Max(radius, collider.radius * axisScale);
            }

            return Mathf.Clamp(radius, 0.05f, 2.4f);
        }

        private sealed class WheelCluster
        {
            private readonly List<WheelCollider> wheels = new List<WheelCollider>();
            private Vector3 center;
            private Vector3 min;
            private Vector3 max;
            private float maxRadius;

            public WheelCluster(WheelCollider collider, Vector3 localPos, float radius)
            {
                wheels.Add(collider);
                center = localPos;
                min = localPos;
                max = localPos;
                maxRadius = radius;
            }

            public bool TryAdd(WheelCollider collider, Vector3 localPos, float radius)
            {
                float joinDistance = Mathf.Max(maxRadius, radius) * 2.60f;
                if (Vector3.Distance(localPos, center) > joinDistance)
                    return false;

                Vector3 newMin = Vector3.Min(min, localPos);
                Vector3 newMax = Vector3.Max(max, localPos);
                Vector3 span = newMax - newMin;
                float maxSpan = Mathf.Max(span.x, Mathf.Max(span.y, span.z));
                float compactLimit = Mathf.Max(maxRadius, radius) * 3.60f;
                if (maxSpan > compactLimit)
                    return false;

                wheels.Add(collider);
                min = newMin;
                max = newMax;
                maxRadius = Mathf.Max(maxRadius, radius);
                center = (center * (wheels.Count - 1) + localPos) / wheels.Count;
                return true;
            }

            public WheelCollider[] ToArray()
            {
                return wheels.ToArray();
            }
        }

        private sealed class WheelEmitterSpec
        {
            public readonly Part Part;
            public readonly WheelCollider[] Wheels;

            public WheelEmitterSpec(Part part, WheelCollider[] wheels)
            {
                Part = part;
                Wheels = wheels;
            }
        }

        private sealed class WheelPartScan
        {
            public readonly Part Part;
            public readonly WheelCollider[] Colliders;

            public WheelPartScan(Part part, WheelCollider[] colliders)
            {
                Part = part;
                Colliders = colliders;
            }
        }

        private readonly struct WheelCandidate
        {
            public readonly int Id;
            public readonly Vector3 VesselLocalPosition;

            public WheelCandidate(WheelCollider collider, Vector3 vesselLocalPosition)
            {
                Id = collider != null ? collider.GetInstanceID() : 0;
                VesselLocalPosition = vesselLocalPosition;
            }
        }
    }
}
