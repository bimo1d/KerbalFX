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
        public const string UiSection = "#LOC_KerbalFX_UI_Section";
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

        private static readonly Dictionary<string, float> BodyVisibilityMultipliers =
            new Dictionary<string, float>(16, StringComparer.OrdinalIgnoreCase);
        private static DateTime lastConfigWriteUtc = DateTime.MinValue;

        public static void Refresh()
        {
            SeedDefaultBodyVisibility();
            if (GameDatabase.Instance != null)
            {
                ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("KERBALFX_ROVER_DUST");
                if (nodes != null)
                    for (int i = 0; i < nodes.Length; i++)
                        if (nodes[i] != null)
                            KerbalFxUtil.LoadBodyVisibility(nodes[i], BodyVisibilityMultipliers, 0.30f, 3.00f);
            }
            KerbalFxUtil.PrimeConfigFileStamp(GetConfigPath(), ref lastConfigWriteUtc);
            Revision++;
            RoverDustLog.Info(Localizer.Format(RoverDustLoc.LogConfig, "GameDatabase",
                "BodyVisibility=" + BodyVisibilityMultipliers.Count));
        }

        public static void TryHotReloadFromDisk()
        {
            if (!KerbalFxUtil.HasConfigFileChanged(GetConfigPath(), ref lastConfigWriteUtc))
                return;
            SeedDefaultBodyVisibility();
            try
            {
                string path = GetConfigPath();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    ConfigNode root = ConfigNode.Load(path);
                    if (root != null)
                    {
                        ConfigNode[] nodes = root.GetNodes("KERBALFX_ROVER_DUST");
                        if (nodes != null)
                            for (int i = 0; i < nodes.Length; i++)
                                if (nodes[i] != null)
                                    KerbalFxUtil.LoadBodyVisibility(nodes[i], BodyVisibilityMultipliers, 0.30f, 3.00f);
                    }
                }
            }
            catch (Exception ex)
            {
                RoverDustLog.Info(Localizer.Format(RoverDustLoc.LogHotReloadFailed, GetConfigPath(), ex.Message));
            }
            Revision++;
            RoverDustLog.Info(Localizer.Format(RoverDustLoc.LogConfig, "HotReload",
                "BodyVisibility=" + BodyVisibilityMultipliers.Count));
        }

        public static float GetBodyVisibilityMultiplier(string bodyName)
        {
            if (string.IsNullOrEmpty(bodyName))
                return 1f;
            float m;
            if (BodyVisibilityMultipliers.TryGetValue(bodyName.Trim(), out m))
                return Mathf.Clamp(m, 0.30f, 3.00f);
            return 1f;
        }

        private static void SeedDefaultBodyVisibility()
        {
            BodyVisibilityMultipliers.Clear();
            BodyVisibilityMultipliers["Mun"] = 1.65f;
            BodyVisibilityMultipliers["Minmus"] = 1.55f;
            BodyVisibilityMultipliers["Duna"] = 1.00f;
            BodyVisibilityMultipliers["Moho"] = 1.85f;
            BodyVisibilityMultipliers["Eeloo"] = 2.00f;
            BodyVisibilityMultipliers["Eve"] = 1.55f;
            BodyVisibilityMultipliers["Vall"] = 1.45f;
            BodyVisibilityMultipliers["Bop"] = 1.45f;
            BodyVisibilityMultipliers["Dres"] = 1.50f;
            BodyVisibilityMultipliers["Ike"] = 1.12f;
            BodyVisibilityMultipliers["Pol"] = 1.15f;
            BodyVisibilityMultipliers["Tylo"] = 0.92f;
        }

        private static string GetConfigPath()
        {
            return Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "KerbalFX", "RoverDust", "KerbalFX_RoverDust.cfg");
        }
    }

    internal static class RoverDustLog
    {
        public static void Info(string message) { Debug.Log("[KerbalFX] " + message); }
        public static void DebugLog(string message) { if (RoverDustConfig.DebugLogging) Debug.Log("[KerbalFX] " + message); }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class RoverDustBootstrap : MonoBehaviour
    {
        private readonly Dictionary<Guid, VesselDustController> controllers = new Dictionary<Guid, VesselDustController>();
        private readonly List<VesselDustController> controllerList = new List<VesselDustController>();
        private readonly List<Guid> removeControllerIds = new List<Guid>(32);
        private bool controllerListDirty = true;

        private float controllerRefreshTimer;
        private float settingsRefreshTimer;
        private float debugHeartbeatTimer;
        private bool emittersStoppedWhileDisabled;

        private const float ControllerRefreshInterval = 1.0f;
        private const float SettingsRefreshInterval = 0.5f;
        private const float HeartbeatInterval = 2.5f;

        private void Start()
        {
            RoverDustConfig.Refresh();
            RoverDustRuntimeConfig.Refresh();
            RoverDustLog.Info(Localizer.Format(RoverDustLoc.LogBootstrapStart));
        }

        private void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            float dt = Time.deltaTime;
            RefreshSettingsIfNeeded(dt);

            if (!RoverDustConfig.EnableDust)
            {
                if (!emittersStoppedWhileDisabled)
                {
                    StopAllEmitters();
                    emittersStoppedWhileDisabled = true;
                }
                return;
            }

            emittersStoppedWhileDisabled = false;
            RefreshControllersIfNeeded(dt);
            TickControllers(dt);
            LogHeartbeatIfNeeded(dt);
        }

        private void RefreshSettingsIfNeeded(float dt)
        {
            settingsRefreshTimer -= dt;
            if (settingsRefreshTimer > 0f)
                return;
            settingsRefreshTimer = SettingsRefreshInterval;
            RoverDustConfig.Refresh();
            RoverDustRuntimeConfig.TryHotReloadFromDisk();
        }

        private void RefreshControllersIfNeeded(float dt)
        {
            controllerRefreshTimer -= dt;
            if (controllerRefreshTimer > 0f)
                return;
            controllerRefreshTimer = ControllerRefreshInterval;
            RefreshControllers();
        }

        private void TickControllers(float dt)
        {
            if (controllerListDirty)
            {
                controllerListDirty = false;
                controllerList.Clear();
                var e = controllers.GetEnumerator();
                while (e.MoveNext())
                    controllerList.Add(e.Current.Value);
                e.Dispose();
            }
            for (int i = 0; i < controllerList.Count; i++)
                controllerList[i].Tick(dt);
        }

        private void LogHeartbeatIfNeeded(float dt)
        {
            if (!RoverDustConfig.DebugLogging)
                return;
            debugHeartbeatTimer -= dt;
            if (debugHeartbeatTimer > 0f)
                return;
            debugHeartbeatTimer = HeartbeatInterval;
            RoverDustLog.DebugLog(Localizer.Format(RoverDustLoc.LogHeartbeat, controllers.Count));
        }

        private void RefreshControllers()
        {
            RemoveInvalidControllers();
            AttachOrRefreshLoadedVessels();
        }

        private void RemoveInvalidControllers()
        {
            removeControllerIds.Clear();
            var e = controllers.GetEnumerator();
            while (e.MoveNext())
            {
                if (!e.Current.Value.IsStillValid())
                {
                    e.Current.Value.Dispose();
                    removeControllerIds.Add(e.Current.Key);
                }
            }
            e.Dispose();
            for (int i = 0; i < removeControllerIds.Count; i++)
                controllers.Remove(removeControllerIds[i]);
            if (removeControllerIds.Count > 0)
                controllerListDirty = true;
        }

        private void AttachOrRefreshLoadedVessels()
        {
            List<Vessel> loaded = FlightGlobals.VesselsLoaded;
            if (loaded == null)
                return;
            for (int i = 0; i < loaded.Count; i++)
            {
                Vessel vessel = loaded[i];
                if (!IsSupportedVessel(vessel))
                    continue;

                VesselDustController controller;
                if (controllers.TryGetValue(vessel.id, out controller))
                    controller.TryRebuild();
                else
                    TryAttachController(vessel);
            }
        }

        private void TryAttachController(Vessel vessel)
        {
            VesselDustController controller = new VesselDustController(vessel);
            if (!controller.HasEmitters)
            {
                controller.Dispose();
                return;
            }
            controllers.Add(vessel.id, controller);
            controllerListDirty = true;
            RoverDustLog.DebugLog(Localizer.Format(RoverDustLoc.LogAttached, controller.EmitterCount, vessel.vesselName));
        }

        private static bool IsSupportedVessel(Vessel vessel)
        {
            if (vessel == null || !vessel.loaded || vessel.packed || vessel.isEVA)
                return false;
            return vessel.vesselType != VesselType.Flag && vessel.vesselType != VesselType.Debris;
        }

        private void StopAllEmitters()
        {
            var e = controllers.GetEnumerator();
            while (e.MoveNext())
                e.Current.Value.StopAll();
            e.Dispose();
        }

        private void OnDestroy()
        {
            var e = controllers.GetEnumerator();
            while (e.MoveNext())
                e.Current.Value.Dispose();
            e.Dispose();
            controllers.Clear();
            controllerList.Clear();
            controllerListDirty = true;
            RoverDustLog.Info(Localizer.Format(RoverDustLoc.LogBootstrapStop));
        }
    }

    internal sealed class VesselDustController
    {
        private readonly Vessel vessel;
        private readonly List<WheelDustEmitter> emitters = new List<WheelDustEmitter>();
        private int cachedPartCount = -1;
        private uint cachedPartSignature;

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
            int count = vessel.parts.Count;
            if (count != cachedPartCount)
            {
                RebuildEmitters();
                return;
            }
            uint sig = ComputePartSignature(vessel);
            if (sig != cachedPartSignature)
                RebuildEmitters();
        }

        private static uint ComputePartSignature(Vessel v)
        {
            uint hash = 17;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (p != null)
                    hash = hash * 31 + p.flightID;
            }
            return hash;
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
            cachedPartCount = vessel != null && vessel.parts != null ? vessel.parts.Count : -1;
            cachedPartSignature = vessel != null ? ComputePartSignature(vessel) : 0u;
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
                AddPartEmitters(part, colliders);
            }

            LogVesselScan(wheelPartCount);
        }

        private void AddPartEmitters(Part part, WheelCollider[] colliders)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    emitters.Add(new WheelDustEmitter(part, colliders[i]));
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
    }
}
