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
        public static bool DebugLogging;
        public static int QualityPercent = 100;
        public static int Revision;

        private static bool initialized;

        public static void Refresh()
        {
            bool newEnable = true;
            bool newAdaptColor = true;
            bool newDebug = false;
            int newQualityPercent = 100;

            if (HighLogic.CurrentGame != null && HighLogic.CurrentGame.Parameters != null)
            {
                RoverDustParameters p = HighLogic.CurrentGame.Parameters.CustomParams<RoverDustParameters>();
                if (p != null)
                {
                    newEnable = p.enableDust;
                    newAdaptColor = p.adaptSurfaceColor;
                    newDebug = p.debugLogging;
                    newQualityPercent = Mathf.Clamp(p.qualityScale, 25, 200);
                }
            }

            bool changed = !initialized
                || newEnable != EnableDust
                || newAdaptColor != AdaptSurfaceColor
                || newDebug != DebugLogging
                || newQualityPercent != QualityPercent;

            EnableDust = newEnable;
            AdaptSurfaceColor = newAdaptColor;
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
            RoverDustLog.Info(
                "[KerbalFX] Config " + sourceTag
                + ": EmissionMultiplier=" + EmissionMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " MaxParticlesMultiplier=" + MaxParticlesMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " RadiusScaleMultiplier=" + RadiusScaleMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " WheelBoostPower=" + WheelBoostPower.ToString("F2", CultureInfo.InvariantCulture)
                + " WheelBoostMax=" + WheelBoostMax.ToString("F2", CultureInfo.InvariantCulture)
                + " DaylightRateFloor=" + DaylightRateFloor.ToString("F2", CultureInfo.InvariantCulture)
                + " DaylightAlphaFloor=" + DaylightAlphaFloor.ToString("F2", CultureInfo.InvariantCulture)
                + " BodyVisibilityEntries=" + BodyVisibilityMultipliers.Count.ToString(CultureInfo.InvariantCulture)
            );
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
                RoverDustLog.Info("[KerbalFX] HotReload failed for " + configPath + ": " + ex.Message);
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

        private float controllerRefreshTimer;
        private float settingsRefreshTimer;
        private float debugHeartbeatTimer;

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

            settingsRefreshTimer -= Time.deltaTime;
            if (settingsRefreshTimer <= 0f)
            {
                settingsRefreshTimer = SettingsRefreshInterval;
                RoverDustConfig.Refresh();
                KerbalFxRuntimeConfig.TryHotReloadFromDisk();
            }

            if (!RoverDustConfig.EnableDust)
            {
                StopAllEmitters();
                return;
            }

            controllerRefreshTimer -= Time.deltaTime;
            if (controllerRefreshTimer <= 0f)
            {
                controllerRefreshTimer = ControllerRefreshInterval;
                RefreshControllers();
            }

            float dt = Time.deltaTime;
            foreach (KeyValuePair<Guid, VesselDustController> pair in controllers)
            {
                pair.Value.Tick(dt);
            }

            if (RoverDustConfig.DebugLogging)
            {
                debugHeartbeatTimer -= dt;
                if (debugHeartbeatTimer <= 0f)
                {
                    debugHeartbeatTimer = 2.5f;
                    RoverDustLog.DebugLog(RoverDustLoc.Format(RoverDustLoc.LogHeartbeat, controllers.Count));
                }
            }
        }

        private void RefreshControllers()
        {
            List<Guid> removeIds = new List<Guid>();
            foreach (KeyValuePair<Guid, VesselDustController> pair in controllers)
            {
                if (!pair.Value.IsStillValid())
                {
                    pair.Value.Dispose();
                    removeIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < removeIds.Count; i++)
            {
                controllers.Remove(removeIds[i]);
            }

            List<Vessel> loaded = FlightGlobals.VesselsLoaded;
            if (loaded == null)
            {
                return;
            }

            for (int i = 0; i < loaded.Count; i++)
            {
                Vessel vessel = loaded[i];
                if (vessel == null || !vessel.loaded || vessel.packed || vessel.isEVA)
                {
                    continue;
                }

                if (vessel.vesselType == VesselType.Flag || vessel.vesselType == VesselType.Debris)
                {
                    continue;
                }

                VesselDustController controller;
                if (!controllers.TryGetValue(vessel.id, out controller))
                {
                    controller = new VesselDustController(vessel);
                    if (controller.HasEmitters)
                    {
                        controllers.Add(vessel.id, controller);
                        RoverDustLog.DebugLog(RoverDustLoc.Format(RoverDustLoc.LogAttached, controller.EmitterCount, vessel.vesselName));
                    }
                    else
                    {
                        controller.Dispose();
                    }
                }
                else
                {
                    controller.TryRebuild();
                }
            }
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
                WheelCollider[] colliders = part.GetComponentsInChildren<WheelCollider>(true);
                if (colliders == null || colliders.Length == 0)
                {
                    RoverDustLog.DebugLog(RoverDustLoc.Format(RoverDustLoc.LogNoCollider, part.partInfo.title));
                    continue;
                }

                for (int c = 0; c < colliders.Length; c++)
                {
                    if (colliders[c] != null)
                    {
                        emitters.Add(new WheelDustEmitter(part, colliders[c]));
                    }
                }
            }

            if (RoverDustConfig.DebugLogging && vessel == FlightGlobals.ActiveVessel)
            {
                RoverDustLog.DebugLog(RoverDustLoc.Format(RoverDustLoc.LogVesselScan, vessel.vesselName, wheelPartCount, emitters.Count));
            }
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

    internal sealed class WheelDustEmitter
    {
        private readonly Part part;
        private readonly WheelCollider wheel;
        private readonly GameObject root;
        private readonly ParticleSystem particleSystem;
        private readonly string debugId;

        private float smoothedRate;
        private float debugTimer;
        private float colorRefreshTimer;
        private float profileRefreshTimer;
        private float lightRefreshTimer;
        private float cachedLightFactor = 1f;
        private float wheelDustRateScale = 1f;
        private float wheelEffectiveRadius = 0.35f;
        private bool advancedQualityFeatures;
        private bool disposed;
        private bool colorInitialized;
        private bool lastSuppressed;
        private int appliedUiRevision = -1;
        private int appliedRuntimeRevision = -1;
        private string lastSurfaceKey = string.Empty;
        private string lastSuppressionKey = string.Empty;
        private Color currentColor = new Color(0.70f, 0.66f, 0.58f, 1f);

        private const float BaseDustAlpha = 0.82f;
        private static readonly List<Light> sharedSceneLights = new List<Light>();
        private static float sharedSceneLightsRefreshAt;
        private static Material sharedMaterial;
        private static Texture2D sharedDustTexture;

        public WheelDustEmitter(Part part, WheelCollider wheel)
        {
            this.part = part;
            this.wheel = wheel;
            debugId = part.partInfo.name + ":" + wheel.name;

            root = new GameObject("RoverDustFXEmitter");
            root.transform.parent = part.transform;
            root.transform.position = wheel.transform.position;
            root.layer = part.gameObject.layer;

            particleSystem = root.AddComponent<ParticleSystem>();

            ConfigureParticleSystemBase();
            ApplyRuntimeVisualProfile(true);
        }

        public void Tick(Vessel vessel, float dt)
        {
            if (disposed || wheel == null || part == null)
            {
                return;
            }

            profileRefreshTimer -= dt;
            if (profileRefreshTimer <= 0f
                || appliedUiRevision != RoverDustConfig.Revision
                || appliedRuntimeRevision != KerbalFxRuntimeConfig.Revision)
            {
                profileRefreshTimer = 0.33f;
                ApplyRuntimeVisualProfile(false);
            }

            WheelHit hit;
            bool hasHit = wheel.GetGroundHit(out hit) && hit.collider != null;
            if (!hasHit)
            {
                SetTargetRate(0f, dt);
                return;
            }

            string suppressionKey;
            bool suppressed = ShouldSuppressDustSurface(hit.collider, out suppressionKey);
            if (suppressed)
            {
                if ((!lastSuppressed || suppressionKey != lastSuppressionKey) && RoverDustConfig.DebugLogging && vessel == FlightGlobals.ActiveVessel)
                {
                    RoverDustLog.DebugLog(RoverDustLoc.Format(RoverDustLoc.LogSuppressed, debugId, suppressionKey));
                }

                lastSuppressed = true;
                lastSuppressionKey = suppressionKey;
                SetTargetRate(0f, dt);
                return;
            }

            lastSuppressed = false;
            lastSuppressionKey = string.Empty;

            float speed = Mathf.Abs((float)vessel.srfSpeed);
            float slip = Mathf.Abs(hit.forwardSlip) + Mathf.Abs(hit.sidewaysSlip);

            float speedFactor = Mathf.InverseLerp(0.7f, 20f, speed);
            float slipBoost = Mathf.Clamp01(slip * 2.4f);

            float quality = RoverDustConfig.QualityPercent / 100f;
            float qualityRateScale = 1f + (Mathf.Pow(quality, 1.60f) - 1f) * 0.75f;
            float bodyVisibility = GetBodyDustVisibilityMultiplier(vessel);
            float baseRate = (120f + 480f * speedFactor) * (0.45f + 0.55f * slipBoost) * KerbalFxRuntimeConfig.EmissionMultiplier;
            float targetRate = baseRate * qualityRateScale * wheelDustRateScale * bodyVisibility;

            Vector3 stableNormal = GetStableGroundNormal(vessel, hit.point, hit.normal);
            RefreshLightingState(vessel, hit.point, stableNormal, dt);
            if (advancedQualityFeatures)
            {
                float light = Mathf.Clamp01(cachedLightFactor);
                float lightRateFactor = Mathf.Pow(light, KerbalFxRuntimeConfig.LightRateExponent);
                if (light > 0.001f)
                {
                    lightRateFactor = Mathf.Lerp(KerbalFxRuntimeConfig.DaylightRateFloor, 1f, lightRateFactor);
                }
                targetRate *= lightRateFactor;
                if (light < 0.025f)
                {
                    targetRate = 0f;
                }
            }

            if (speed < 0.7f)
            {
                targetRate = 0f;
            }

            SetTargetRate(targetRate, dt);

            root.transform.position = hit.point + stableNormal * 0.04f;
            root.transform.rotation = Quaternion.LookRotation(part.transform.forward, stableNormal);

            colorRefreshTimer -= dt;
            if (colorRefreshTimer <= 0f)
            {
                colorRefreshTimer = 0.3f;
                UpdateSurfaceColor(vessel, hit);
            }

            if (RoverDustConfig.DebugLogging && vessel == FlightGlobals.ActiveVessel)
            {
                debugTimer -= dt;
                if (debugTimer <= 0f)
                {
                    debugTimer = 1.2f;
                    RoverDustLog.DebugLog(RoverDustLoc.Format(
                        RoverDustLoc.LogEmitter,
                        debugId,
                        hasHit,
                        speed.ToString("F2"),
                        slip.ToString("F3"),
                        smoothedRate.ToString("F1"),
                        currentColor.r.ToString("F2") + "," + currentColor.g.ToString("F2") + "," + currentColor.b.ToString("F2")
                        + " L=" + cachedLightFactor.ToString("F2")
                        + " W=" + wheelDustRateScale.ToString("F2")
                        + " R=" + wheelEffectiveRadius.ToString("F2")
                        + " B=" + bodyVisibility.ToString("F2")
                    ));
                }
            }
        }

        public void StopEmission()
        {
            SetTargetRate(0f, 0.12f);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (particleSystem != null)
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        private void ConfigureParticleSystemBase()
        {
            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(-3.14159f, 3.14159f);
            main.startColor = new Color(currentColor.r, currentColor.g, currentColor.b, BaseDustAlpha);

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 2f;

            Material material = GetSharedMaterial();
            if (material != null)
            {
                renderer.material = material;
            }
        }

        private void ApplyRuntimeVisualProfile(bool force)
        {
            if (!force
                && appliedUiRevision == RoverDustConfig.Revision
                && appliedRuntimeRevision == KerbalFxRuntimeConfig.Revision)
            {
                return;
            }

            appliedUiRevision = RoverDustConfig.Revision;
            appliedRuntimeRevision = KerbalFxRuntimeConfig.Revision;

            int qualityPercent = Mathf.Clamp(RoverDustConfig.QualityPercent, 25, 200);
            float quality = qualityPercent / 100f;
            float qualityNorm = Mathf.InverseLerp(0.25f, 2.0f, quality);
            float qualityParticleScale = 1f + (Mathf.Pow(quality, 1.70f) - 1f) * 0.75f;
            float qualitySizeScale = Mathf.Lerp(0.76f, 1.36f, qualityNorm);
            float bodyVisibility = GetBodyDustVisibilityMultiplier(part != null ? part.vessel : null);
            float bodyBoostNorm = Mathf.Clamp01((bodyVisibility - 1f) / 0.5f);
            float bodyParticleScale = Mathf.Lerp(1f, 1.28f, bodyBoostNorm);
            float bodySizeScale = Mathf.Lerp(1f, 1.16f, bodyBoostNorm);
            advancedQualityFeatures = qualityPercent >= 100;
            wheelEffectiveRadius = GetEffectiveWheelRadius(wheel, part);
            wheelDustRateScale = advancedQualityFeatures ? GetWheelDustRateScale(wheelEffectiveRadius) : 1f;
            if (!advancedQualityFeatures)
            {
                cachedLightFactor = 1f;
            }

            ParticleSystem.MainModule main = particleSystem.main;
            float maxParticlesBase = 760f * qualityParticleScale * KerbalFxRuntimeConfig.MaxParticlesMultiplier;
            main.maxParticles = Mathf.RoundToInt(Mathf.Clamp(maxParticlesBase * wheelDustRateScale * bodyParticleScale, 220f, 4600f));

            float minSize = 0.036f * qualitySizeScale * bodySizeScale;
            float maxSize = 0.102f * qualitySizeScale * bodySizeScale;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);

            float minLifetime = 0.21f * Mathf.Lerp(0.98f, 1.18f, qualityNorm);
            float maxLifetime = 0.64f * Mathf.Lerp(0.98f, 1.20f, qualityNorm);
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.60f, Mathf.Lerp(1.9f, 3.4f, qualityNorm));
            main.gravityModifier = Mathf.Lerp(0.014f, 0.024f, qualityNorm);

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.angle = Mathf.Lerp(11.5f, 16.5f, qualityNorm);
            float wheelRadiusVisual = wheelEffectiveRadius;
            float radiusScale = advancedQualityFeatures ? Mathf.Clamp(Mathf.Lerp(0.90f, 1.70f, Mathf.InverseLerp(0.22f, 1.05f, wheelRadiusVisual)), 0.90f, 1.80f) * KerbalFxRuntimeConfig.RadiusScaleMultiplier : 1f;
            shape.radius = Mathf.Lerp(0.062f, 0.132f, qualityNorm) * radiusScale * bodySizeScale;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(Mathf.Lerp(0.66f, 0.84f, qualityNorm), 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = quality >= 0.50f;
            if (sizeOverLifetime.enabled)
            {
                AnimationCurve curve = new AnimationCurve();
                curve.AddKey(0f, 0.95f);
                curve.AddKey(0.55f, 1.40f);
                curve.AddKey(1f, 0.35f);
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);
            }

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.maxParticleSize = Mathf.Lerp(0.11f, 0.19f, qualityNorm);
            ApplyCurrentStartColor();

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            if (smoothedRate < 0.01f)
            {
                emission.rateOverTime = 0f;
            }

            if (RoverDustConfig.DebugLogging)
            {
                RoverDustLog.DebugLog(RoverDustLoc.Format(
                    RoverDustLoc.LogProfile,
                    debugId,
                    qualityPercent,
                    main.maxParticles,
                    minSize.ToString("F3"),
                    maxSize.ToString("F3")
                ));
            }
        }

        private void SetTargetRate(float targetRate, float dt)
        {
            if (particleSystem == null)
            {
                return;
            }

            float lerpSpeed = Mathf.Clamp01(dt * 6.5f);
            smoothedRate = Mathf.Lerp(smoothedRate, targetRate, lerpSpeed);

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = smoothedRate;

            if (smoothedRate > 0.18f)
            {
                if (!particleSystem.isPlaying)
                {
                    particleSystem.Play(true);
                }
            }
            else if (particleSystem.isPlaying)
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void RefreshLightingState(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal, float dt)
        {
            if (!advancedQualityFeatures)
            {
                if (cachedLightFactor != 1f)
                {
                    cachedLightFactor = 1f;
                    ApplyCurrentStartColor();
                }

                return;
            }

            lightRefreshTimer -= dt;
            if (lightRefreshTimer > 0f)
            {
                return;
            }

            lightRefreshTimer = 0.20f;
            float newLightFactor = EvaluateCombinedLighting(vessel, worldPoint, surfaceNormal);
            if (Mathf.Abs(newLightFactor - cachedLightFactor) > 0.02f)
            {
                cachedLightFactor = newLightFactor;
                ApplyCurrentStartColor();
            }
        }

        private void ApplyCurrentStartColor()
        {
            if (particleSystem == null)
            {
                return;
            }

            float alpha = BaseDustAlpha;
            if (advancedQualityFeatures)
            {
                float light = Mathf.Clamp01(cachedLightFactor);
                if (light < 0.14f)
                {
                    alpha = 0f;
                }
                else
                {
                    float alphaLight = Mathf.Pow(light, KerbalFxRuntimeConfig.LightAlphaExponent);
                    alphaLight = Mathf.Lerp(KerbalFxRuntimeConfig.DaylightAlphaFloor, 1f, alphaLight);
                    alpha *= alphaLight;
                }
            }

            float bodyVisibility = GetBodyDustVisibilityMultiplier(part != null ? part.vessel : null);
            float bodyBoostNorm = Mathf.Clamp01((bodyVisibility - 1f) / 0.5f);
            alpha *= Mathf.Lerp(1f, 1.16f, bodyBoostNorm);
            alpha = Mathf.Clamp(alpha, 0f, 0.97f);

            ParticleSystem.MainModule main = particleSystem.main;
            main.startColor = new Color(currentColor.r, currentColor.g, currentColor.b, alpha);
        }

        private static float EvaluateCombinedLighting(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal)
        {
            RefreshSharedSceneLights();

            float sunLight = EvaluateSunLighting(vessel, worldPoint, surfaceNormal);
            float artificialLight = EvaluateNearbyArtificialLights(worldPoint, surfaceNormal);

            if (sunLight <= 0.001f && artificialLight < 0.055f)
            {
                return 0f;
            }

            float combined = Mathf.Max(sunLight, artificialLight);
            if (combined < KerbalFxRuntimeConfig.MinCombinedLight)
            {
                return 0f;
            }

            return Mathf.Clamp01(combined);
        }

        private static float EvaluateSunLighting(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal)
        {
            Vector3 safeNormal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;

            float directionalBest = 0f;
            for (int i = 0; i < sharedSceneLights.Count; i++)
            {
                float strength = EvaluateSingleDirectionalSunLight(sharedSceneLights[i], safeNormal);
                if (strength > directionalBest)
                {
                    directionalBest = strength;
                }
            }

            float geometricSun = 0f;
            Vector3 sunDirection;
            if (TryGetSunDirection(worldPoint, out sunDirection))
            {
                float sunDot = Vector3.Dot(safeNormal, sunDirection);
                if (sunDot > 0f)
                {
                    geometricSun = Mathf.Lerp(0.20f, 1f, Mathf.Clamp01(sunDot));
                }
            }

            bool isDayAtPoint = geometricSun > 0.001f;
            float best = Mathf.Max(directionalBest, geometricSun);

            if (best <= 0.01f && vessel != null && vessel.directSunlight)
            {
                best = 0.90f;
                isDayAtPoint = true;
            }

            if (!isDayAtPoint)
            {
                return 0f;
            }

            if (vessel != null && !vessel.directSunlight)
            {
                float shadowed = best * KerbalFxRuntimeConfig.ShadowLightFactor;
                float cloudyDayFloor = geometricSun * 0.22f;
                best = Mathf.Max(shadowed, cloudyDayFloor);
            }

            return Mathf.Clamp01(best);
        }

        private static bool TryGetSunDirection(Vector3 worldPoint, out Vector3 sunDirection)
        {
            sunDirection = Vector3.zero;

            CelestialBody sun = null;
            if (Planetarium.fetch != null)
            {
                sun = Planetarium.fetch.Sun;
            }

            if (sun == null && FlightGlobals.Bodies != null && FlightGlobals.Bodies.Count > 0)
            {
                for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
                {
                    CelestialBody body = FlightGlobals.Bodies[i];
                    if (body != null && !string.IsNullOrEmpty(body.bodyName) && body.bodyName.IndexOf("sun", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        sun = body;
                        break;
                    }
                }

                if (sun == null)
                {
                    sun = FlightGlobals.Bodies[0];
                }
            }

            if (sun == null)
            {
                return false;
            }

            Vector3d toSun = sun.position - (Vector3d)worldPoint;
            if (toSun.sqrMagnitude < 1e-8)
            {
                return false;
            }

            sunDirection = ((Vector3)toSun).normalized;
            return sunDirection.sqrMagnitude > 0.0001f;
        }

        private static float EvaluateNearbyArtificialLights(Vector3 worldPoint, Vector3 surfaceNormal)
        {
            float best = 0f;
            for (int i = 0; i < sharedSceneLights.Count; i++)
            {
                float strength = EvaluateSingleArtificialLight(sharedSceneLights[i], worldPoint, surfaceNormal);
                if (strength > best)
                {
                    best = strength;
                }
            }

            return Mathf.Clamp01(best);
        }

        private static float EvaluateSingleDirectionalSunLight(Light light, Vector3 surfaceNormal)
        {
            if (light == null || !light.enabled || light.intensity <= 0.03f || !light.gameObject.activeInHierarchy)
            {
                return 0f;
            }

            if (light.type != LightType.Directional || light.transform == null)
            {
                return 0f;
            }

            Vector3 toLightDir = (-light.transform.forward).normalized;
            float normalDot = Mathf.Clamp01(Vector3.Dot(surfaceNormal.normalized, toLightDir));
            float strength = light.intensity * Mathf.Lerp(0.35f, 1f, normalDot);
            return Mathf.Clamp01(strength / 1.05f);
        }

        private static void RefreshSharedSceneLights()
        {
            if (Time.time < sharedSceneLightsRefreshAt)
            {
                return;
            }

            sharedSceneLightsRefreshAt = Time.time + 2.25f;
            sharedSceneLights.Clear();

            Light[] foundLights = UnityEngine.Object.FindObjectsOfType<Light>();
            if (foundLights == null)
            {
                return;
            }

            for (int i = 0; i < foundLights.Length; i++)
            {
                Light light = foundLights[i];
                if (light != null)
                {
                    sharedSceneLights.Add(light);
                }
            }
        }

        private static float EvaluateSingleArtificialLight(Light light, Vector3 worldPoint, Vector3 surfaceNormal)
        {
            if (light == null || !light.enabled || light.intensity <= 0.01f || !light.gameObject.activeInHierarchy)
            {
                return 0f;
            }

            if ((light.type != LightType.Point && light.type != LightType.Spot) || light.transform == null)
            {
                return 0f;
            }

            if (!IsLikelyIntentionalLampLight(light))
            {
                return 0f;
            }

            float range = light.range;
            if (range <= 0.01f)
            {
                return 0f;
            }

            Vector3 pointToLight = light.transform.position - worldPoint;
            float distance = pointToLight.magnitude;
            if (distance > range)
            {
                return 0f;
            }

            Vector3 toLightDir = distance > 0.001f ? pointToLight / distance : Vector3.up;
            float attenuation = Mathf.Clamp01(1f - distance / range);
            attenuation *= attenuation;

            float spotFactor = 1f;
            if (light.type == LightType.Spot)
            {
                float cosLimit = Mathf.Cos(light.spotAngle * 0.5f * Mathf.Deg2Rad);
                float cosAngle = Vector3.Dot(light.transform.forward, -toLightDir);
                if (cosAngle <= cosLimit)
                {
                    return 0f;
                }

                spotFactor = Mathf.InverseLerp(cosLimit, 1f, cosAngle);
            }

            float normalDot = Mathf.Clamp01(Vector3.Dot(surfaceNormal.normalized, toLightDir));
            float normalFactor = Mathf.Lerp(0.40f, 1f, normalDot);

            float strength = light.intensity * attenuation * spotFactor * normalFactor;
            return Mathf.Clamp01(strength / 1.45f);
        }

        private static bool IsLikelyIntentionalLampLight(Light light)
        {
            if (light == null || light.transform == null)
            {
                return false;
            }

            bool fromPart = light.GetComponentInParent<Part>() != null;
            if (fromPart)
            {
                if (light.range < 1.20f || light.intensity < 0.10f)
                {
                    return false;
                }

                string partIdentity = (light.name + " " + GetTransformPath(light.transform)).ToLowerInvariant();
                if (ContainsAnyToken(partIdentity, new string[]
                {
                    "headlamp",
                    "headlight",
                    "floodlight",
                    "spotlight",
                    "searchlight",
                    "lamp",
                    "projector"
                }))
                {
                    return true;
                }

                return light.range <= 85f && light.intensity <= 8.5f;
            }

            if (light.range > 120f || light.intensity < 0.35f)
            {
                return false;
            }

            string identity = (light.name + " " + GetTransformPath(light.transform)).ToLowerInvariant();
            return ContainsAnyToken(identity, new string[]
            {
                "headlamp",
                "headlight",
                "floodlight",
                "spotlight",
                "searchlight",
                "lamp",
                "projector"
            });
        }

        private static Vector3 GetStableGroundNormal(Vessel vessel, Vector3 worldPoint, Vector3 hitNormal)
        {
            Vector3 normal = hitNormal;
            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = Vector3.up;
            }

            normal.Normalize();

            if (vessel != null && vessel.mainBody != null)
            {
                Vector3 bodyUp = (worldPoint - vessel.mainBody.position);
                if (bodyUp.sqrMagnitude > 0.0001f)
                {
                    bodyUp.Normalize();
                    normal = Vector3.Slerp(normal, bodyUp, 0.70f);
                }
            }

            return normal.normalized;
        }

        private static float GetWheelDustRateScale(float effectiveRadius)
        {
            float normalized = Mathf.Clamp(effectiveRadius / 0.30f, 0.9f, 3.8f);
            float baseScale = Mathf.Pow(normalized, KerbalFxRuntimeConfig.WheelBoostPower);
            float amplifiedScale = 1f + (baseScale - 1f) * 1.25f;
            return Mathf.Clamp(amplifiedScale, 1f, KerbalFxRuntimeConfig.WheelBoostMax * 1.25f);
        }

        private static float GetEffectiveWheelRadius(WheelCollider wheelCollider, Part sourcePart)
        {
            if (wheelCollider == null)
            {
                return 0.35f;
            }

            float radius = Mathf.Max(0.05f, wheelCollider.radius);
            if (wheelCollider.transform != null)
            {
                Vector3 scale = wheelCollider.transform.lossyScale;
                float axisScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                if (axisScale > 0.001f)
                {
                    radius = Mathf.Max(radius, wheelCollider.radius * axisScale);
                }
            }

            float visualRadius = EstimateVisualWheelRadius(sourcePart, wheelCollider.transform != null ? wheelCollider.transform.position : Vector3.zero);
            if (visualRadius > 0.01f)
            {
                radius = Mathf.Max(radius, visualRadius);
            }

            return Mathf.Clamp(radius, 0.05f, 2.4f);
        }

        private static float EstimateVisualWheelRadius(Part sourcePart, Vector3 wheelWorldPosition)
        {
            if (sourcePart == null)
            {
                return 0f;
            }

            Renderer[] renderers = sourcePart.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return 0f;
            }

            float bestScore = float.MaxValue;
            float bestRadius = 0f;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                float extent = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
                if (extent < 0.03f || extent > 2.8f)
                {
                    continue;
                }

                float dist = Vector3.Distance(bounds.center, wheelWorldPosition);
                string id = (renderer.name + " " + (renderer.transform != null ? renderer.transform.name : string.Empty)).ToLowerInvariant();
                bool wheelLike = ContainsAnyToken(id, new string[] { "wheel", "tire", "track", "bogie", "roller" });

                if (!wheelLike && dist > 1.15f)
                {
                    continue;
                }

                float score = dist + (wheelLike ? 0f : 0.75f);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestRadius = extent;
                }
            }

            return bestRadius;
        }

        private void UpdateSurfaceColor(Vessel vessel, WheelHit hit)
        {
            if (!RoverDustConfig.AdaptSurfaceColor)
            {
                ApplyColor(new Color(0.70f, 0.66f, 0.58f));
                return;
            }

            string surfaceKey = BuildSurfaceKey(vessel, hit.collider);
            if (surfaceKey == lastSurfaceKey && colorInitialized)
            {
                return;
            }
            lastSurfaceKey = surfaceKey;

            Color baseColor = GuessDustColor(vessel);
            Color newColor = baseColor;
            Color colliderColor;
            if (TryGetColliderColor(hit.collider, out colliderColor))
            {
                newColor = BlendWithColliderColor(baseColor, colliderColor);
            }

            ApplyColor(newColor);
        }

        private void ApplyColor(Color color)
        {
            Color target = new Color(
                Mathf.Clamp01(color.r),
                Mathf.Clamp01(color.g),
                Mathf.Clamp01(color.b),
                1f
            );

            target = NormalizeDustTone(target);
            currentColor = colorInitialized ? Color.Lerp(currentColor, target, 0.45f) : target;
            colorInitialized = true;

            ApplyCurrentStartColor();
        }

        private static bool TryGetColliderColor(Collider collider, out Color color)
        {
            color = Color.white;
            if (collider == null)
            {
                return false;
            }

            Renderer renderer = collider.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null)
            {
                return false;
            }

            if (!renderer.sharedMaterial.HasProperty("_Color"))
            {
                return false;
            }

            color = renderer.sharedMaterial.color;
            return true;
        }

        private static bool ShouldSuppressDustSurface(Collider collider, out string reason)
        {
            reason = string.Empty;
            if (collider == null)
            {
                return false;
            }

            string identity = (
                (collider.name ?? string.Empty) + " " +
                (collider.gameObject != null ? collider.gameObject.name : string.Empty) + " " +
                GetTransformPath(collider.transform)
            ).ToLowerInvariant();

            if (ContainsAnyToken(identity, new string[]
            {
                "runway",
                "launchpad",
                "launch_pad",
                "launch pad",
                "crawlerway",
                "launchsite",
                "launch_site"
            }))
            {
                reason = "KSC_Surface";
                return true;
            }

            Renderer renderer = collider.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                string materialName = string.IsNullOrEmpty(renderer.sharedMaterial.name)
                    ? string.Empty
                    : renderer.sharedMaterial.name.ToLowerInvariant();
                if (ContainsAnyToken(materialName, new string[] { "runway", "launchpad", "crawlerway" }))
                {
                    reason = "KSC_Material";
                    return true;
                }
            }

            if (IsKerbalKonstructsStatic(collider))
            {
                reason = "KerbalKonstructs_Static";
                return true;
            }

            return false;
        }

        private static bool IsKerbalKonstructsStatic(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            Transform t = collider.transform;
            int depth = 0;
            while (t != null && depth < 10)
            {
                Component[] components = t.GetComponents<Component>();
                if (ContainsAnyTokenInTypes(components, new string[] { "kerbalkonstructs", "staticobject" }))
                {
                    return true;
                }

                t = t.parent;
                depth++;
            }

            return false;
        }

        private static bool ContainsAnyTokenInTypes(Component[] components, string[] tokens)
        {
            if (components == null || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < components.Length; i++)
            {
                Component c = components[i];
                if (c == null)
                {
                    continue;
                }

                Type type = c.GetType();
                if (type == null)
                {
                    continue;
                }

                string fullName = string.IsNullOrEmpty(type.FullName) ? type.Name : type.FullName;
                if (ContainsAnyToken(fullName, tokens))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsAnyToken(string text, string[] tokens)
        {
            if (string.IsNullOrEmpty(text) || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token) && text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            List<string> names = new List<string>();
            Transform current = transform;
            int depth = 0;
            while (current != null && depth < 14)
            {
                names.Add(current.name);
                current = current.parent;
                depth++;
            }

            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        private static string BuildSurfaceKey(Vessel vessel, Collider collider)
        {
            string body = vessel.mainBody != null ? vessel.mainBody.bodyName : "UnknownBody";
            string biome = string.IsNullOrEmpty(vessel.landedAt) ? "UnknownBiome" : vessel.landedAt;
            string colliderId = collider != null ? collider.GetInstanceID().ToString() : "NoCollider";
            return body + "|" + biome + "|" + colliderId;
        }

        private static float GetBodyDustVisibilityMultiplier(Vessel vessel)
        {
            if (vessel == null || vessel.mainBody == null || string.IsNullOrEmpty(vessel.mainBody.bodyName))
            {
                return 1f;
            }

            return KerbalFxRuntimeConfig.GetBodyVisibilityMultiplier(vessel.mainBody.bodyName);
        }

        private static Color GuessDustColor(Vessel vessel)
        {
            string key = vessel.mainBody != null
                ? vessel.mainBody.bodyName.ToLowerInvariant()
                : string.Empty;

            if (key.Contains("minmus"))
            {
                return new Color(0.73f, 0.80f, 0.74f);
            }
            if (key.Contains("mun"))
            {
                return new Color(0.75f, 0.73f, 0.69f);
            }
            if (key.Contains("duna"))
            {
                return new Color(0.70f, 0.46f, 0.31f);
            }
            if (key.Contains("eve"))
            {
                return new Color(0.77f, 0.71f, 0.60f);
            }
            if (key.Contains("moho"))
            {
                return new Color(0.63f, 0.56f, 0.50f);
            }
            if (key.Contains("gilly"))
            {
                return new Color(0.62f, 0.58f, 0.52f);
            }
            if (key.Contains("bop"))
            {
                return new Color(0.60f, 0.52f, 0.45f);
            }
            if (key.Contains("pol"))
            {
                return new Color(0.66f, 0.64f, 0.62f);
            }
            if (key.Contains("tylo"))
            {
                return new Color(0.67f, 0.67f, 0.66f);
            }
            if (key.Contains("vall"))
            {
                return new Color(0.70f, 0.72f, 0.74f);
            }
            if (key.Contains("eeloo"))
            {
                return new Color(0.74f, 0.75f, 0.77f);
            }
            if (key.Contains("kerbin"))
            {
                return new Color(0.67f, 0.61f, 0.53f);
            }

            return new Color(0.70f, 0.66f, 0.58f);
        }

        private static Color BlendWithColliderColor(Color baseColor, Color colliderColor)
        {
            float h;
            float s;
            float v;
            Color.RGBToHSV(colliderColor, out h, out s, out v);

            s = Mathf.Clamp(s, 0.05f, 0.35f);
            v = Mathf.Clamp(v, 0.20f, 0.86f);

            if (h > 0.20f && h < 0.45f)
            {
                h = Mathf.Lerp(h, 0.11f, 0.45f);
                s *= 0.45f;
                v *= 0.92f;
            }

            Color tunedCollider = Color.HSVToRGB(h, s, v);
            return Color.Lerp(baseColor, tunedCollider, 0.16f);
        }

        private static Color NormalizeDustTone(Color input)
        {
            float h;
            float s;
            float v;
            Color.RGBToHSV(input, out h, out s, out v);

            s = Mathf.Clamp(s, 0.12f, 0.40f);
            v = Mathf.Clamp(v, 0.24f, 0.88f);

            if (h > 0.20f && h < 0.45f)
            {
                h = Mathf.Lerp(h, 0.12f, 0.30f);
                s *= 0.72f;
            }

            return Color.HSVToRGB(h, s, v);
        }

        private static Material GetSharedMaterial()
        {
            if (sharedMaterial != null)
            {
                return sharedMaterial;
            }

            Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            if (shader == null)
            {
                return null;
            }

            sharedMaterial = new Material(shader);
            sharedMaterial.name = "RoverDustFXMaterial";
            sharedMaterial.color = Color.white;
            sharedMaterial.mainTexture = GetOrCreateDustTexture();
            return sharedMaterial;
        }

        private static Texture2D GetOrCreateDustTexture()
        {
            if (sharedDustTexture != null)
            {
                return sharedDustTexture;
            }

            const int size = 64;
            sharedDustTexture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            sharedDustTexture.wrapMode = TextureWrapMode.Clamp;
            sharedDustTexture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size) * 2f - 1f;
                    float ny = ((y + 0.5f) / size) * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float t = Mathf.Clamp01(1f - r);
                    float soft = Mathf.Pow(t, 1.35f);
                    float noise = Mathf.PerlinNoise(x * 0.11f, y * 0.11f);
                    float alpha = Mathf.Clamp01(soft * (0.90f + 0.10f * noise));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            sharedDustTexture.SetPixels(pixels);
            sharedDustTexture.Apply(false, true);
            return sharedDustTexture;
        }
    }
}
