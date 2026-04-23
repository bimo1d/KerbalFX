using System;
using System.Collections.Generic;
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
        public const string LogNoCollider = "#LOC_KerbalFX_RoverDust_Log_NoCollider";
        public const string LogVesselScan = "#LOC_KerbalFX_RoverDust_Log_VesselScan";
        public const string LogProfile = "#LOC_KerbalFX_RoverDust_Log_Profile";
        public const string LogSuppressed = "#LOC_KerbalFX_RoverDust_Log_Suppressed";
        public const string LogEmitter = "#LOC_KerbalFX_RoverDust_Log_Emitter";
        public const string LogConfig = "#LOC_KerbalFX_RoverDust_Log_Config";
        public const string LogHotReloadFailed = "#LOC_KerbalFX_RoverDust_Log_HotReloadFailed";

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
        public static bool AdaptSurfaceColor = true;
        public static bool UseLightAware = true;
        public static bool DebugLogging;
        public static int QualityPercent = 100;
        public static int Revision;

        private static bool initialized;

        public static void Refresh()
        {
            bool newEnable = true;
            bool newAdaptColor = true;
            bool newUseLightAware = true;
            bool newDebug = false;
            int newQualityPercent = 100;

            if (HighLogic.CurrentGame != null && HighLogic.CurrentGame.Parameters != null)
            {
                RoverDustParameters p = HighLogic.CurrentGame.Parameters.CustomParams<RoverDustParameters>();
                if (p != null)
                {
                    newEnable = p.enableDust;
                    newAdaptColor = p.adaptSurfaceColor;
                    newUseLightAware = p.useLightAware;
                    newDebug = p.debugLogging;
                    newQualityPercent = Mathf.Clamp(p.qualityScale, 25, 200);
                }
            }

            bool changed = !initialized
                || newEnable != EnableDust
                || newAdaptColor != AdaptSurfaceColor
                || newUseLightAware != UseLightAware
                || newDebug != DebugLogging
                || newQualityPercent != QualityPercent;

            EnableDust = newEnable;
            AdaptSurfaceColor = newAdaptColor;
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
        public const float LightRateExponent = 1.05f;
        public const float LightAlphaExponent = 1.25f;
        public const float MinCombinedLight = 0.040f;
        public const float ShadowLightFactor = 0.20f;
        public const float DaylightRateFloor = 0.42f;
        public const float DaylightAlphaFloor = 0.40f;

        private static readonly KerbalFxBodyVisibilityProfile bodyProfile =
            new KerbalFxBodyVisibilityProfile("KERBALFX_ROVER_DUST", GetConfigPath, SeedDefaultBodyVisibility, 0.30f, 3.00f);

        public static void Refresh()
        {
            bodyProfile.Refresh();
            Revision++;
            RoverDustLog.Info(Localizer.Format(RoverDustLoc.LogConfig, "GameDatabase",
                "BodyVisibility=" + bodyProfile.Count));
        }

        public static void TryHotReloadFromDisk()
        {
            string failure;
            if (!bodyProfile.TryHotReloadFromDisk(out failure))
                return;

            if (failure != null)
                RoverDustLog.Info(Localizer.Format(RoverDustLoc.LogHotReloadFailed, GetConfigPath(), failure));

            Revision++;
            RoverDustLog.Info(Localizer.Format(RoverDustLoc.LogConfig, "HotReload",
                "BodyVisibility=" + bodyProfile.Count));
        }

        public static float GetBodyVisibilityMultiplier(string bodyName)
        {
            return bodyProfile.Get(bodyName);
        }

        private static void SeedDefaultBodyVisibility(Dictionary<string, float> dict)
        {
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
        private KerbalFxVesselPartSnapshot partSnapshot;

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
                return;
            if (vessel == null || !vessel.loaded || vessel.packed || vessel.Splashed)
            {
                StopAll();
                return;
            }

            bool moving = vessel.srfSpeed > 0.35;
            bool nearGround = vessel.Landed || vessel.heightFromTerrain < 7.0;
            if (!moving || !nearGround)
            {
                StopAll();
                return;
            }

            for (int i = 0; i < emitters.Count; i++)
                emitters[i].Tick(vessel, dt);
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
                AddPartEmitterSpecs(part, colliders);
            }

            float vesselBudgetScale = GetVesselEmitterBudgetScale(emitterSpecs.Count);
            for (int i = 0; i < emitterSpecs.Count; i++)
                emitters.Add(new WheelDustEmitter(emitterSpecs[i].Part, emitterSpecs[i].Wheels, vesselBudgetScale));

            LogVesselScan(wheelPartCount);
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
    }
}
