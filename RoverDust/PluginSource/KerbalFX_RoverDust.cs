using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using KSP.Localization;
using UnityEngine;

namespace RoverDustFX
{
    internal static class RoverDustLoc
    {
        public const string UiTitle = "#LOC_KerbalFX_RoverDust_UI_Title";
        public const string UiSection = "#LOC_KerbalFX_UI_Section";

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

        public static string Format(string key)
        {
            return Localizer.Format(key);
        }

        public static string Format(string key, object a)
        {
            return Localizer.Format(key, a);
        }

        public static string Format(string key, object a, object b)
        {
            return Localizer.Format(key, a, b);
        }

        public static string Format(string key, object a, object b, object c)
        {
            return Localizer.Format(key, a, b, c);
        }

        public static string Format(string key, object a, object b, object c, object d)
        {
            return Localizer.Format(key, a, b, c, d);
        }

        public static string Format(string key, object a, object b, object c, object d, object e)
        {
            return Localizer.Format(key, a, b, c, d, e);
        }

        public static string Format(string key, object a, object b, object c, object d, object e, object f)
        {
            return Localizer.Format(key, a, b, c, d, e, f);
        }
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

        public override string Title
        {
            get { return RoverDustLoc.Format(RoverDustLoc.UiTitle); }
        }

        public override GameParameters.GameMode GameMode
        {
            get { return GameParameters.GameMode.ANY; }
        }

        public override string Section
        {
            get { return "KerbalFX"; }
        }

        public override string DisplaySection
        {
            get { return RoverDustLoc.Format(RoverDustLoc.UiSection); }
        }

        public override int SectionOrder
        {
            get { return 4; }
        }

        public override bool HasPresets
        {
            get { return false; }
        }
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
                RoverDustLog.Info(RoverDustLoc.Format(
                    RoverDustLoc.LogSettingsUpdated,
                    EnableDust,
                    QualityPercent,
                    AdaptSurfaceColor,
                    UseLightAware,
                    DebugLogging
                ));
            }
        }
    }

    internal static class KerbalFxRuntimeConfig
    {
        public static bool Loaded;
        public static int Revision;

        public static float EmissionMultiplier = 2.30f;
        public static float MaxParticlesMultiplier = 1.45f;
        public static float RadiusScaleMultiplier = 1.22f;
        public static float WheelBoostPower = 1.65f;
        public static float WheelBoostMax = 5.40f;
        public static float LightRateExponent = 1.05f;
        public static float LightAlphaExponent = 1.25f;
        public static float MinCombinedLight = 0.040f;
        public static float ShadowLightFactor = 0.20f;
        public static float DaylightRateFloor = 0.42f;
        public static float DaylightAlphaFloor = 0.40f;

        private static readonly Dictionary<string, float> BodyVisibilityMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        private static DateTime lastCoreConfigWriteUtc = DateTime.MinValue;
        private static DateTime lastRoverDustConfigWriteUtc = DateTime.MinValue;

        public static void Refresh()
        {
            ReloadFromGameDatabase();
            PrimeConfigFileStamp(GetCoreConfigPath(), ref lastCoreConfigWriteUtc);
            PrimeConfigFileStamp(GetRoverDustConfigPath(), ref lastRoverDustConfigWriteUtc);
        }

        public static void TryHotReloadFromDisk()
        {
            bool coreChanged = HasFileChanged(GetCoreConfigPath(), ref lastCoreConfigWriteUtc);
            bool roverChanged = HasFileChanged(GetRoverDustConfigPath(), ref lastRoverDustConfigWriteUtc);
            if (!coreChanged && !roverChanged)
            {
                return;
            }

            ReloadFromDiskFiles();
        }

        public static float GetBodyVisibilityMultiplier(string bodyName)
        {
            if (string.IsNullOrEmpty(bodyName))
            {
                return 1f;
            }

            float multiplier;
            if (BodyVisibilityMultipliers.TryGetValue(bodyName.Trim(), out multiplier))
            {
                return Mathf.Clamp(multiplier, 0.30f, 3.00f);
            }

            return 1f;
        }

        private static void ReloadFromGameDatabase()
        {
            SeedDefaultValues();

            ApplyFromNodes("KERBALFX_CORE");
            ApplyFromNodes("KERBALFX_ROVER_DUST");

            Loaded = true;
            Revision++;
            LogCurrentConfig("GameDatabase");
        }

        private static void ReloadFromDiskFiles()
        {
            SeedDefaultValues();

            ApplyFromDiskConfigNode(GetCoreConfigPath(), "KERBALFX_CORE");
            ApplyFromDiskConfigNode(GetRoverDustConfigPath(), "KERBALFX_ROVER_DUST");

            Loaded = true;
            Revision++;
            LogCurrentConfig("HotReload");
        }

        private static void SeedDefaultValues()
        {
            EmissionMultiplier = 2.30f;
            MaxParticlesMultiplier = 1.45f;
            RadiusScaleMultiplier = 1.22f;
            WheelBoostPower = 1.65f;
            WheelBoostMax = 5.40f;
            LightRateExponent = 1.05f;
            LightAlphaExponent = 1.25f;
            MinCombinedLight = 0.040f;
            ShadowLightFactor = 0.20f;
            DaylightRateFloor = 0.42f;
            DaylightAlphaFloor = 0.40f;

            SeedDefaultBodyVisibilityMultipliers();
        }

        private static void SeedDefaultBodyVisibilityMultipliers()
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

        private static void LogCurrentConfig(string sourceTag)
        {
            string details =
                "EmissionMultiplier=" + EmissionMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " MaxParticlesMultiplier=" + MaxParticlesMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " RadiusScaleMultiplier=" + RadiusScaleMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " WheelBoostPower=" + WheelBoostPower.ToString("F2", CultureInfo.InvariantCulture)
                + " WheelBoostMax=" + WheelBoostMax.ToString("F2", CultureInfo.InvariantCulture)
                + " DaylightRateFloor=" + DaylightRateFloor.ToString("F2", CultureInfo.InvariantCulture)
                + " DaylightAlphaFloor=" + DaylightAlphaFloor.ToString("F2", CultureInfo.InvariantCulture)
                + " BodyVisibilityEntries=" + BodyVisibilityMultipliers.Count.ToString(CultureInfo.InvariantCulture);

            RoverDustLog.Info(
                RoverDustLoc.Format(RoverDustLoc.LogConfig, sourceTag, details));
        }

        private static string GetCoreConfigPath()
        {
            return Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "KerbalFX", "Core", "KerbalFX_Core.cfg");
        }

        private static string GetRoverDustConfigPath()
        {
            return Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "KerbalFX", "RoverDust", "KerbalFX_RoverDust.cfg");
        }

        private static void PrimeConfigFileStamp(string path, ref DateTime stamp)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            stamp = File.GetLastWriteTimeUtc(path);
        }

        private static bool HasFileChanged(string path, ref DateTime stamp)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            DateTime writeUtc = File.GetLastWriteTimeUtc(path);
            if (writeUtc <= DateTime.MinValue)
            {
                return false;
            }

            if (writeUtc <= stamp)
            {
                return false;
            }

            stamp = writeUtc;
            return true;
        }

        private static void ApplyFromNodes(string nodeName)
        {
            if (GameDatabase.Instance == null)
            {
                return;
            }

            ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes(nodeName);
            if (nodes == null || nodes.Length == 0)
            {
                return;
            }

            for (int i = 0; i < nodes.Length; i++)
            {
                ConfigNode node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                ApplyFromNode(node);
            }
        }

        private static void ApplyFromDiskConfigNode(string configPath, string nodeName)
        {
            try
            {
                if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
                {
                    return;
                }

                ConfigNode root = ConfigNode.Load(configPath);
                if (root == null)
                {
                    return;
                }

                ConfigNode[] nodes = root.GetNodes(nodeName);
                if (nodes == null || nodes.Length == 0)
                {
                    return;
                }

                for (int i = 0; i < nodes.Length; i++)
                {
                    ConfigNode node = nodes[i];
                    if (node != null)
                    {
                        ApplyFromNode(node);
                    }
                }
            }
            catch (Exception ex)
            {
                RoverDustLog.Info(RoverDustLoc.Format(RoverDustLoc.LogHotReloadFailed, configPath, ex.Message));
            }
        }

        private static void ApplyFromNode(ConfigNode node)
        {
            EmissionMultiplier = ReadFloat(node, "EmissionMultiplier", EmissionMultiplier, 0.10f, 12f);
            MaxParticlesMultiplier = ReadFloat(node, "MaxParticlesMultiplier", MaxParticlesMultiplier, 0.30f, 6f);
            RadiusScaleMultiplier = ReadFloat(node, "RadiusScaleMultiplier", RadiusScaleMultiplier, 0.50f, 3f);
            WheelBoostPower = ReadFloat(node, "WheelBoostPower", WheelBoostPower, 0.60f, 3f);
            WheelBoostMax = ReadFloat(node, "WheelBoostMax", WheelBoostMax, 1f, 12f);
            LightRateExponent = ReadFloat(node, "LightRateExponent", LightRateExponent, 0.50f, 3f);
            LightAlphaExponent = ReadFloat(node, "LightAlphaExponent", LightAlphaExponent, 0.60f, 3f);
            MinCombinedLight = ReadFloat(node, "MinCombinedLight", MinCombinedLight, 0f, 0.3f);
            ShadowLightFactor = ReadFloat(node, "ShadowLightFactor", ShadowLightFactor, 0f, 1f);
            DaylightRateFloor = ReadFloat(node, "DaylightRateFloor", DaylightRateFloor, 0f, 1f);
            DaylightAlphaFloor = ReadFloat(node, "DaylightAlphaFloor", DaylightAlphaFloor, 0f, 1f);

            ApplyBodyVisibilityFromNode(node);
        }

        private static void ApplyBodyVisibilityFromNode(ConfigNode node)
        {
            if (node == null)
            {
                return;
            }

            ConfigNode[] bodyNodes = node.GetNodes("BODY_VISIBILITY");
            if (bodyNodes == null || bodyNodes.Length == 0)
            {
                return;
            }

            for (int i = 0; i < bodyNodes.Length; i++)
            {
                ConfigNode bodyNode = bodyNodes[i];
                if (bodyNode == null)
                {
                    continue;
                }

                string name = bodyNode.GetValue("name");
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                float multiplier = ReadFloat(bodyNode, "multiplier", 1f, 0.30f, 3.00f);
                BodyVisibilityMultipliers[name.Trim()] = multiplier;
            }
        }

        private static float ReadFloat(ConfigNode node, string key, float fallback, float minValue, float maxValue)
        {
            if (node == null || string.IsNullOrEmpty(key))
            {
                return fallback;
            }

            if (!node.HasValue(key))
            {
                return fallback;
            }

            string raw = node.GetValue(key);
            if (string.IsNullOrEmpty(raw))
            {
                return fallback;
            }

            float value;
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return Mathf.Clamp(value, minValue, maxValue);
            }

            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return Mathf.Clamp(value, minValue, maxValue);
            }

            return fallback;
        }
    }

    internal static class RoverDustLog
    {
        public static void Info(string message)
        {
            Debug.Log("[KerbalFX] " + message);
        }

        public static void DebugLog(string message)
        {
            if (RoverDustConfig.DebugLogging)
            {
                Debug.Log("[KerbalFX] " + message);
            }
        }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class RoverDustBootstrap : MonoBehaviour
    {
        private readonly Dictionary<Guid, VesselDustController> controllers = new Dictionary<Guid, VesselDustController>();
        private readonly List<Guid> removeControllerIds = new List<Guid>(32);

        private float controllerRefreshTimer;
        private float settingsRefreshTimer;
        private float debugHeartbeatTimer;
        private bool emittersStoppedWhileDisabled;

        private const float ControllerRefreshInterval = 1.0f;
        private const float SettingsRefreshInterval = 0.5f;

        private void Start()
        {
            RoverDustConfig.Refresh();
            KerbalFxRuntimeConfig.Refresh();
            RoverDustLog.Info(RoverDustLoc.Format(RoverDustLoc.LogBootstrapStart));
        }

        private void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight)
            {
                return;
            }

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
            {
                return;
            }

            settingsRefreshTimer = SettingsRefreshInterval;
            RoverDustConfig.Refresh();
            KerbalFxRuntimeConfig.TryHotReloadFromDisk();
        }

        private void RefreshControllersIfNeeded(float dt)
        {
            controllerRefreshTimer -= dt;
            if (controllerRefreshTimer > 0f)
            {
                return;
            }

            controllerRefreshTimer = ControllerRefreshInterval;
            RefreshControllers();
        }

        private void TickControllers(float dt)
        {
            foreach (KeyValuePair<Guid, VesselDustController> pair in controllers)
            {
                pair.Value.Tick(dt);
            }
        }

        private void LogHeartbeatIfNeeded(float dt)
        {
            if (!RoverDustConfig.DebugLogging)
            {
                return;
            }

            debugHeartbeatTimer -= dt;
            if (debugHeartbeatTimer > 0f)
            {
                return;
            }

            debugHeartbeatTimer = 2.5f;
            RoverDustLog.DebugLog(RoverDustLoc.Format(RoverDustLoc.LogHeartbeat, controllers.Count));
        }

        private void RefreshControllers()
        {
            RemoveInvalidControllers();
            AttachOrRefreshLoadedVessels();
        }

        private void RemoveInvalidControllers()
        {
            removeControllerIds.Clear();
            foreach (KeyValuePair<Guid, VesselDustController> pair in controllers)
            {
                if (!pair.Value.IsStillValid())
                {
                    pair.Value.Dispose();
                    removeControllerIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < removeControllerIds.Count; i++)
            {
                controllers.Remove(removeControllerIds[i]);
            }
        }

        private void AttachOrRefreshLoadedVessels()
        {
            List<Vessel> loaded = FlightGlobals.VesselsLoaded;
            if (loaded == null)
            {
                return;
            }

            for (int i = 0; i < loaded.Count; i++)
            {
                Vessel vessel = loaded[i];
                if (!IsSupportedVessel(vessel))
                {
                    continue;
                }

                VesselDustController controller;
                if (controllers.TryGetValue(vessel.id, out controller))
                {
                    controller.TryRebuild();
                }
                else
                {
                    TryAttachController(vessel);
                }
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
            RoverDustLog.DebugLog(RoverDustLoc.Format(RoverDustLoc.LogAttached, controller.EmitterCount, vessel.vesselName));
        }

        private static bool IsSupportedVessel(Vessel vessel)
        {
            if (vessel == null || !vessel.loaded || vessel.packed || vessel.isEVA)
            {
                return false;
            }

            return vessel.vesselType != VesselType.Flag && vessel.vesselType != VesselType.Debris;
        }

        private void StopAllEmitters()
        {
            foreach (KeyValuePair<Guid, VesselDustController> pair in controllers)
            {
                pair.Value.StopAll();
            }
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<Guid, VesselDustController> pair in controllers)
            {
                pair.Value.Dispose();
            }

            controllers.Clear();
            RoverDustLog.Info(RoverDustLoc.Format(RoverDustLoc.LogBootstrapStop));
        }
    }

    internal sealed class VesselDustController
    {
        private readonly Vessel vessel;
        private readonly List<WheelDustEmitter> emitters = new List<WheelDustEmitter>();
        private int cachedPartCount = -1;

        public int EmitterCount
        {
            get { return emitters.Count; }
        }

        public bool HasEmitters
        {
            get { return emitters.Count > 0; }
        }

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
            {
                return;
            }

            if (vessel.parts.Count != cachedPartCount)
            {
                RebuildEmitters();
            }
        }

        public void Tick(float dt)
        {
            if (emitters.Count == 0)
            {
                return;
            }

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
            {
                emitters[i].Tick(vessel, dt);
            }
        }

        public void StopAll()
        {
            for (int i = 0; i < emitters.Count; i++)
            {
                emitters[i].StopEmission();
            }
        }

        public void Dispose()
        {
            DisposeEmitters();
        }

        private void RebuildEmitters()
        {
            DisposeEmitters();
            cachedPartCount = vessel != null && vessel.parts != null ? vessel.parts.Count : -1;

            if (vessel == null || vessel.parts == null)
            {
                return;
            }

            int wheelPartCount = 0;
            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part part = vessel.parts[i];
                if (!PartLooksLikeWheel(part))
                {
                    continue;
                }

                wheelPartCount++;
                AddPartEmitters(part);
            }

            LogVesselScan(wheelPartCount);
        }

        private void AddPartEmitters(Part part)
        {
            WheelCollider[] colliders = part.GetComponentsInChildren<WheelCollider>(true);
            if (colliders == null || colliders.Length == 0)
            {
                RoverDustLog.DebugLog(RoverDustLoc.Format(RoverDustLoc.LogNoCollider, part.partInfo.title));
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    emitters.Add(new WheelDustEmitter(part, colliders[i]));
                }
            }
        }

        private void LogVesselScan(int wheelPartCount)
        {
            if (!RoverDustConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel)
            {
                return;
            }

            RoverDustLog.DebugLog(RoverDustLoc.Format(RoverDustLoc.LogVesselScan, vessel.vesselName, wheelPartCount, emitters.Count));
        }

        private void DisposeEmitters()
        {
            for (int i = 0; i < emitters.Count; i++)
            {
                emitters[i].Dispose();
            }

            emitters.Clear();
        }

        private static bool PartLooksLikeWheel(Part part)
        {
            if (part == null)
            {
                return false;
            }

            if (part.Modules != null)
            {
                for (int i = 0; i < part.Modules.Count; i++)
                {
                    PartModule module = part.Modules[i];
                    if (module == null || string.IsNullOrEmpty(module.moduleName))
                    {
                        continue;
                    }

                    if (module.moduleName.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            WheelCollider[] colliders = part.GetComponentsInChildren<WheelCollider>(true);
            return colliders != null && colliders.Length > 0;
        }
    }

}

