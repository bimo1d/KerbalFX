using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using KSP.Localization;
using UnityEngine;

namespace KerbalFX.AeroFX
{
    internal static class AeroFxLoc
    {
        public const string UiSection = "#LOC_KerbalFX_UI_Section";
        public const string UiSectionExtras = "#LOC_KerbalFX_UI_SectionExtras";
        public const string UiTitle = "#LOC_KerbalFX_AeroFX_UI_Title";
        public const string UiEnable = "#LOC_KerbalFX_AeroFX_UI_Enable";
        public const string UiEnableTip = "#LOC_KerbalFX_AeroFX_UI_Enable_TT";
        public const string UiDebug = "#LOC_KerbalFX_AeroFX_UI_Debug";
        public const string UiDebugTip = "#LOC_KerbalFX_AeroFX_UI_Debug_TT";

        public const string LogSettingsUpdated = "#LOC_KerbalFX_AeroFX_Log_SettingsUpdated";
        public const string LogBootstrapStart = "#LOC_KerbalFX_AeroFX_Log_BootstrapStart";
        public const string LogBootstrapStop = "#LOC_KerbalFX_AeroFX_Log_BootstrapStop";
        public const string LogHeartbeat = "#LOC_KerbalFX_AeroFX_Log_Heartbeat";
        public const string LogAttached = "#LOC_KerbalFX_AeroFX_Log_Attached";
        public const string LogVesselScan = "#LOC_KerbalFX_AeroFX_Log_VesselScan";
        public const string LogAnchorScan = "#LOC_KerbalFX_AeroFX_Log_AnchorScan";
        public const string LogEmitter = "#LOC_KerbalFX_AeroFX_Log_Emitter";
        public const string LogConfig = "#LOC_KerbalFX_AeroFX_Log_Config";
        public const string LogHotReloadFailed = "#LOC_KerbalFX_AeroFX_Log_HotReloadFailed";
    }

    public class AeroFxParameters : GameParameters.CustomParameterNode
    {
        [GameParameters.CustomParameterUI(AeroFxLoc.UiEnable, toolTip = AeroFxLoc.UiEnableTip)]
        public bool enableAeroFx = true;

        [GameParameters.CustomParameterUI(AeroFxLoc.UiDebug, toolTip = AeroFxLoc.UiDebugTip)]
        public bool debugLogging = false;

        public override string Title { get { return Localizer.Format(AeroFxLoc.UiTitle); } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override string Section { get { return "KerbalFX_02_Extras"; } }
        public override string DisplaySection { get { return Localizer.Format(AeroFxLoc.UiSectionExtras); } }
        public override int SectionOrder { get { return 5; } }
        public override bool HasPresets { get { return false; } }
    }

    internal static class AeroFxConfig
    {
        public static bool Enabled = true;
        public static bool DebugLogging;
        public static int Revision;

        private static bool initialized;

        public static void Refresh()
        {
            bool newEnabled = true;
            bool newDebug = false;

            if (HighLogic.CurrentGame != null && HighLogic.CurrentGame.Parameters != null)
            {
                AeroFxParameters p = HighLogic.CurrentGame.Parameters.CustomParams<AeroFxParameters>();
                if (p != null)
                {
                    newEnabled = p.enableAeroFx;
                    newDebug = p.debugLogging;
                }
            }

            bool changed = !initialized
                || newEnabled != Enabled
                || newDebug != DebugLogging;

            Enabled = newEnabled;
            DebugLogging = newDebug;

            if (changed)
            {
                initialized = true;
                Revision++;
                AeroFxLog.Info(Localizer.Format(
                    AeroFxLoc.LogSettingsUpdated,
                    Enabled,
                    DebugLogging));
            }
        }
    }

    internal static class AeroFxRuntimeConfig
    {
        public static int Revision;

        public const float MinAtmDensity = 0.12f;
        public const float FullAtmDensity = 1.15f;
        public const float MinSurfaceSpeed = 62f;
        public const float FullSurfaceSpeed = 265f;
        public const float MinDynamicPressure = 4.0f;
        public const float FullDynamicPressure = 65.0f;
        public const float MinMach = 0.22f;
        public const float FullMach = 0.92f;
        public const float MinLoadFactor = 1.02f;
        public const float FullLoadFactor = 3.60f;
        public const float TrailTimeMin = 1.02f;
        public const float TrailTimeMax = 1.24f;
        public const float TrailWidthMin = 0.28f;
        public const float TrailWidthMax = 0.74f;
        public const float CurlAmplitudeMin = 0.10f;
        public const float CurlAmplitudeMax = 0.50f;
        public const float OutwardDriftMin = 0.02f;
        public const float OutwardDriftMax = 0.22f;
        public const float SinkBiasMin = 0.01f;
        public const float SinkBiasMax = 0.08f;
        public const float ActivationFloor = 0.01f;
        public const float FadeInSpeed = 0.55f;
        public const float FadeOutSpeed = 1.00f;
        public const float AnchorRefreshInterval = 6.0f;

        private static readonly Dictionary<string, float> BodyVisibilityMultipliers =
            new Dictionary<string, float>(8, StringComparer.OrdinalIgnoreCase);
        private static DateTime lastConfigWriteUtc = DateTime.MinValue;

        public static void Refresh()
        {
            SeedDefaultBodyVisibility();
            if (GameDatabase.Instance != null)
            {
                ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("KERBALFX_AERO_FX");
                if (nodes != null)
                    for (int i = 0; i < nodes.Length; i++)
                        if (nodes[i] != null)
                            KerbalFxUtil.LoadBodyVisibility(nodes[i], BodyVisibilityMultipliers, 0.20f, 2.50f);
            }
            KerbalFxUtil.PrimeConfigFileStamp(GetConfigPath(), ref lastConfigWriteUtc);
            Revision++;
            AeroFxLog.Info(Localizer.Format(
                AeroFxLoc.LogConfig,
                "GameDatabase",
                "BodyVisibility=" + BodyVisibilityMultipliers.Count.ToString(CultureInfo.InvariantCulture)));
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
                        ConfigNode[] nodes = root.GetNodes("KERBALFX_AERO_FX");
                        if (nodes != null)
                            for (int i = 0; i < nodes.Length; i++)
                                if (nodes[i] != null)
                                    KerbalFxUtil.LoadBodyVisibility(nodes[i], BodyVisibilityMultipliers, 0.20f, 2.50f);
                    }
                }
            }
            catch (Exception ex)
            {
                AeroFxLog.Info(Localizer.Format(AeroFxLoc.LogHotReloadFailed, ex.Message));
            }

            Revision++;
            AeroFxLog.Info(Localizer.Format(
                AeroFxLoc.LogConfig,
                "HotReload",
                "BodyVisibility=" + BodyVisibilityMultipliers.Count.ToString(CultureInfo.InvariantCulture)));
        }

        public static float GetBodyVisibilityMultiplier(string bodyName)
        {
            if (string.IsNullOrEmpty(bodyName))
                return 1f;

            float multiplier;
            if (BodyVisibilityMultipliers.TryGetValue(bodyName.Trim(), out multiplier))
                return Mathf.Clamp(multiplier, 0.20f, 2.50f);
            return 1f;
        }

        private static void SeedDefaultBodyVisibility()
        {
            BodyVisibilityMultipliers.Clear();
            BodyVisibilityMultipliers["Kerbin"] = 1.00f;
            BodyVisibilityMultipliers["Laythe"] = 1.08f;
            BodyVisibilityMultipliers["Eve"] = 1.16f;
            BodyVisibilityMultipliers["Jool"] = 0.95f;
            BodyVisibilityMultipliers["Duna"] = 0.42f;
        }

        private static string GetConfigPath()
        {
            return Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "KerbalFX", "AeroFX", "KerbalFX_AeroFX.cfg");
        }
    }

    internal static class AeroFxLog
    {
        public static void Info(string message) { Debug.Log("[KerbalFX] " + message); }
        public static void DebugLog(string message) { if (AeroFxConfig.DebugLogging) Debug.Log("[KerbalFX] " + message); }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class AeroFxBootstrap : MonoBehaviour
    {
        private readonly Dictionary<Guid, VesselAeroController> controllers = new Dictionary<Guid, VesselAeroController>();
        private readonly List<VesselAeroController> controllerList = new List<VesselAeroController>();
        private readonly Dictionary<Guid, float> invalidControllerTimers = new Dictionary<Guid, float>();
        private readonly List<Guid> removeControllerIds = new List<Guid>(32);
        private bool controllerListDirty = true;

        private float controllerRefreshTimer;
        private float settingsRefreshTimer;
        private float debugHeartbeatTimer;
        private bool emittersStoppedWhileDisabled;

        private const float ControllerRefreshInterval = 1.0f;
        private const float SettingsRefreshInterval = 0.5f;
        private const float ControllerInvalidGraceSeconds = 4.0f;
        private const float HeartbeatInterval = 2.5f;

        private void Start()
        {
            AeroFxConfig.Refresh();
            AeroFxRuntimeConfig.Refresh();
            AeroFxLog.Info(Localizer.Format(AeroFxLoc.LogBootstrapStart));
        }

        private void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            float dt = Time.deltaTime;
            RefreshSettingsIfNeeded(dt);
            if (!AeroFxConfig.Enabled)
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
            AeroFxConfig.Refresh();
            AeroFxRuntimeConfig.TryHotReloadFromDisk();
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
            if (!AeroFxConfig.DebugLogging)
                return;

            debugHeartbeatTimer -= dt;
            if (debugHeartbeatTimer > 0f)
                return;

            debugHeartbeatTimer = HeartbeatInterval;
            AeroFxLog.DebugLog(Localizer.Format(AeroFxLoc.LogHeartbeat, controllers.Count));
        }

        private void StopAllEmitters()
        {
            var e = controllers.GetEnumerator();
            while (e.MoveNext())
                e.Current.Value.StopAll();
            e.Dispose();
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
                    float invalidTimer;
                    invalidControllerTimers.TryGetValue(e.Current.Key, out invalidTimer);
                    invalidTimer += ControllerRefreshInterval;
                    invalidControllerTimers[e.Current.Key] = invalidTimer;

                    if (invalidTimer >= ControllerInvalidGraceSeconds)
                    {
                        e.Current.Value.Dispose();
                        removeControllerIds.Add(e.Current.Key);
                    }
                }
                else
                {
                    invalidControllerTimers.Remove(e.Current.Key);
                }
            }
            e.Dispose();

            for (int i = 0; i < removeControllerIds.Count; i++)
                RemoveController(removeControllerIds[i]);
        }

        private void RemoveController(Guid vesselId)
        {
            controllers.Remove(vesselId);
            invalidControllerTimers.Remove(vesselId);
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

                VesselAeroController controller;
                if (controllers.TryGetValue(vessel.id, out controller))
                {
                    controller.TryRebuild();
                    continue;
                }

                TryAttachController(vessel);
            }
        }

        private void TryAttachController(Vessel vessel)
        {
            VesselAeroController controller = new VesselAeroController(vessel);
            if (!controller.HasAnyEmitters)
            {
                controller.Dispose();
                return;
            }

            controllers.Add(vessel.id, controller);
            invalidControllerTimers.Remove(vessel.id);
            controllerListDirty = true;
            AeroFxLog.DebugLog(Localizer.Format(
                AeroFxLoc.LogAttached,
                controller.EmitterCount,
                vessel.vesselName));
        }

        private static bool IsSupportedVessel(Vessel vessel)
        {
            if (vessel == null || !vessel.loaded || vessel.packed || vessel.isEVA)
                return false;

            if (vessel.vesselType == VesselType.Flag || vessel.vesselType == VesselType.Debris)
                return false;

            return vessel.mainBody != null && vessel.mainBody.atmosphere;
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
            invalidControllerTimers.Clear();
            AeroFxLog.Info(Localizer.Format(AeroFxLoc.LogBootstrapStop));
        }
    }
}
