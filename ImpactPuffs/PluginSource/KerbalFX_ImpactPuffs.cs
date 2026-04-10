using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using KSP.Localization;
using UnityEngine;

namespace KerbalFX.ImpactPuffs
{
    internal static class ImpactPuffsLoc
    {
        public const string UiSection = "#LOC_KerbalFX_UI_Section";
        public const string UiTitle = "#LOC_KerbalFX_ImpactPuffs_UI_Title";

        public const string UiEnable = "#LOC_KerbalFX_ImpactPuffs_UI_Enable";
        public const string UiEnableTip = "#LOC_KerbalFX_ImpactPuffs_UI_Enable_TT";
        public const string UiSimplified = "#LOC_KerbalFX_ImpactPuffs_UI_Simplified";
        public const string UiSimplifiedTip = "#LOC_KerbalFX_ImpactPuffs_UI_Simplified_TT";
        public const string UiLightAware = "#LOC_KerbalFX_ImpactPuffs_UI_LightAware";
        public const string UiLightAwareTip = "#LOC_KerbalFX_ImpactPuffs_UI_LightAware_TT";
        public const string UiDebug = "#LOC_KerbalFX_ImpactPuffs_UI_Debug";
        public const string UiDebugTip = "#LOC_KerbalFX_ImpactPuffs_UI_Debug_TT";

        public const string LogSettingsUpdated = "#LOC_KerbalFX_ImpactPuffs_Log_SettingsUpdated";
        public const string LogBootstrapStart = "#LOC_KerbalFX_ImpactPuffs_Log_BootstrapStart";
        public const string LogBootstrapStop = "#LOC_KerbalFX_ImpactPuffs_Log_BootstrapStop";
        public const string LogHeartbeat = "#LOC_KerbalFX_ImpactPuffs_Log_Heartbeat";
        public const string LogAttached = "#LOC_KerbalFX_ImpactPuffs_Log_Attached";
        public const string LogEngineEmitter = "#LOC_KerbalFX_ImpactPuffs_Log_EngineEmitter";
        public const string LogBurst = "#LOC_KerbalFX_ImpactPuffs_Log_Burst";
        public const string LogConfig = "#LOC_KerbalFX_ImpactPuffs_Log_Config";
        public const string LogHotReloadFailed = "#LOC_KerbalFX_ImpactPuffs_Log_HotReloadFailed";
        public const string LogVesselScan = "#LOC_KerbalFX_ImpactPuffs_Log_VesselScan";
        public const string LogLaunchsiteSuppression = "#LOC_KerbalFX_ImpactPuffs_Log_LaunchsiteSuppression";
        public const string LogRingShockDebug = "#LOC_KerbalFX_ImpactPuffs_Log_RingShockDebug";
        public const string LogBurstDebug = "#LOC_KerbalFX_ImpactPuffs_Log_BurstDebug";
    }

    public class ImpactPuffsParameters : GameParameters.CustomParameterNode
    {
        [GameParameters.CustomParameterUI(ImpactPuffsLoc.UiEnable, toolTip = ImpactPuffsLoc.UiEnableTip)]
        public bool enableImpactPuffs = true;

        [GameParameters.CustomParameterUI(ImpactPuffsLoc.UiSimplified, toolTip = ImpactPuffsLoc.UiSimplifiedTip)]
        public bool useSimplifiedEffects;

        [GameParameters.CustomParameterUI(ImpactPuffsLoc.UiLightAware, toolTip = ImpactPuffsLoc.UiLightAwareTip)]
        public bool useLightAware = true;

        [GameParameters.CustomParameterUI(ImpactPuffsLoc.UiDebug, toolTip = ImpactPuffsLoc.UiDebugTip)]
        public bool debugLogging;

        public override string Title
        {
            get { return Localizer.Format(ImpactPuffsLoc.UiTitle); }
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
            get { return Localizer.Format(ImpactPuffsLoc.UiSection); }
        }

        public override int SectionOrder
        {
            get { return 5; }
        }

        public override bool HasPresets
        {
            get { return false; }
        }
    }

    internal static class ImpactPuffsConfig
    {
        public static bool Enabled = true;
        public static bool UseSimplifiedEffects = false;
        public static bool UseLightAware = true;
        public static bool DebugLogging;
        public static int Revision;

        private static bool initialized;

        public static void Refresh()
        {
            bool newEnabled = true;
            bool newUseSimplifiedEffects = false;
            bool newUseLightAware = true;
            bool newDebug = false;

            if (HighLogic.CurrentGame != null && HighLogic.CurrentGame.Parameters != null)
            {
                ImpactPuffsParameters p = HighLogic.CurrentGame.Parameters.CustomParams<ImpactPuffsParameters>();
                if (p != null)
                {
                    newEnabled = p.enableImpactPuffs;
                    newUseSimplifiedEffects = p.useSimplifiedEffects;
                    newUseLightAware = p.useLightAware;
                    newDebug = p.debugLogging;
                }
            }

            bool changed = !initialized
                || newEnabled != Enabled
                || newUseSimplifiedEffects != UseSimplifiedEffects
                || newUseLightAware != UseLightAware
                || newDebug != DebugLogging;

            Enabled = newEnabled;
            UseSimplifiedEffects = newUseSimplifiedEffects;
            UseLightAware = newUseLightAware;
            DebugLogging = newDebug;

            if (changed)
            {
                initialized = true;
                Revision++;
                ImpactPuffsLog.Info(Localizer.Format(
                    ImpactPuffsLoc.LogSettingsUpdated,
                    Enabled,
                    UseSimplifiedEffects,
                    UseLightAware,
                    DebugLogging
                ));
            }
        }
    }

    internal static class ImpactPuffsRuntimeConfig
    {
        public static bool Loaded;
        public static int Revision;

        public static float EmissionMultiplier = 1.00f;
        public static float MaxParticlesMultiplier = 1.00f;
        public static float RadiusScaleMultiplier = 1.00f;
        public static float SharedEmissionMultiplier = 2.30f;
        public static float SharedMaxParticlesMultiplier = 1.45f;
        public static float SharedRadiusScaleMultiplier = 1.22f;

        public static float MaxRayDistance = 42f;
        public static float MinNormalizedThrust = 0.05f;
        public static float ShadowLightFactor = 0.28f;
        public static float LateralSpreadMultiplier = 2.40f;
        public static float VerticalLiftMultiplier = 0.60f;
        public static float TurbulenceMultiplier = 1.45f;
        public static float RingExpansionMultiplier = 1.55f;
        public static float DynamicSwayMultiplier = 1.00f;

        public static float EngineCountExponent = 0.72f;
        public static float EngineCountMinScale = 0.22f;
        public static float MinExhaustToGroundAlignment = 0.18f;
        public static float MinRayDirectionToBodyDown = 0.22f;
        public static float MinExhaustToBodyDown = 0.20f;

        public static float ThrustPowerReference = 180f;
        public static float ThrustPowerExponent = 0.60f;
        public static float ThrustPowerMinScale = 0.35f;
        public static float ThrustPowerMaxScale = 3.80f;

        public static float MaxDistanceAtLowThrust = 9.0f;
        public static float MaxDistanceAtHighThrust = 42.0f;

        public static float TouchdownMinSpeed = 2.2f;
        public static float TouchdownBurstMultiplier = 1.00f;

        private static readonly Dictionary<string, float> BodyVisibilityMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static DateTime lastConfigWriteUtc = DateTime.MinValue;

        public static void Refresh()
        {
            ReloadFromGameDatabase();
            PrimeConfigFileStamp(GetConfigPath(), ref lastConfigWriteUtc);
        }

        public static void TryHotReloadFromDisk()
        {
            if (!HasFileChanged(GetConfigPath(), ref lastConfigWriteUtc))
            {
                return;
            }

            ReloadFromDisk();
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
                return Mathf.Clamp(multiplier, 0.40f, 3.00f);
            }

            return 1f;
        }

        private static void ReloadFromGameDatabase()
        {
            SeedDefaults();

            if (GameDatabase.Instance != null)
            {
                ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("KERBALFX_IMPACT_PUFFS");
                if (nodes != null)
                {
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        if (nodes[i] != null)
                        {
                            ApplyFromNode(nodes[i]);
                        }
                    }
                }
            }

            Loaded = true;
            Revision++;
            LogCurrentConfig("GameDatabase");
        }

        private static void ReloadFromDisk()
        {
            SeedDefaults();

            try
            {
                string path = GetConfigPath();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    ConfigNode root = ConfigNode.Load(path);
                    if (root != null)
                    {
                        ConfigNode[] nodes = root.GetNodes("KERBALFX_IMPACT_PUFFS");
                        if (nodes != null)
                        {
                            for (int i = 0; i < nodes.Length; i++)
                            {
                                if (nodes[i] != null)
                                {
                                    ApplyFromNode(nodes[i]);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ImpactPuffsLog.Info(Localizer.Format(ImpactPuffsLoc.LogHotReloadFailed, ex.Message));
            }

            Loaded = true;
            Revision++;
            LogCurrentConfig("HotReload");
        }

        private static void SeedDefaults()
        {
            EmissionMultiplier = 1.00f;
            MaxParticlesMultiplier = 1.00f;
            RadiusScaleMultiplier = 1.00f;
            SharedEmissionMultiplier = 2.30f;
            SharedMaxParticlesMultiplier = 1.45f;
            SharedRadiusScaleMultiplier = 1.22f;

            MaxRayDistance = 42f;
            MinNormalizedThrust = 0.05f;
            ShadowLightFactor = 0.28f;
            LateralSpreadMultiplier = 2.40f;
            VerticalLiftMultiplier = 0.60f;
            TurbulenceMultiplier = 1.45f;
            RingExpansionMultiplier = 1.55f;
            DynamicSwayMultiplier = 1.00f;

            EngineCountExponent = 0.72f;
            EngineCountMinScale = 0.22f;
            MinExhaustToGroundAlignment = 0.18f;
            MinRayDirectionToBodyDown = 0.22f;
            MinExhaustToBodyDown = 0.20f;

            ThrustPowerReference = 180f;
            ThrustPowerExponent = 0.60f;
            ThrustPowerMinScale = 0.35f;
            ThrustPowerMaxScale = 3.80f;

            MaxDistanceAtLowThrust = 9.0f;
            MaxDistanceAtHighThrust = 42.0f;

            TouchdownMinSpeed = 2.2f;
            TouchdownBurstMultiplier = 1.00f;

            BodyVisibilityMultipliers.Clear();
            BodyVisibilityMultipliers["Kerbin"] = 1.00f;
            BodyVisibilityMultipliers["Mun"] = 1.22f;
            BodyVisibilityMultipliers["Minmus"] = 1.18f;
            BodyVisibilityMultipliers["Duna"] = 1.00f;
            BodyVisibilityMultipliers["Ike"] = 1.06f;
            BodyVisibilityMultipliers["Eve"] = 1.08f;
            BodyVisibilityMultipliers["Moho"] = 1.10f;
            BodyVisibilityMultipliers["Dres"] = 1.06f;
            BodyVisibilityMultipliers["Vall"] = 1.06f;
            BodyVisibilityMultipliers["Tylo"] = 1.00f;
            BodyVisibilityMultipliers["Bop"] = 1.08f;
            BodyVisibilityMultipliers["Pol"] = 1.08f;
            BodyVisibilityMultipliers["Eeloo"] = 1.12f;
        }

        private static void ApplyFromNode(ConfigNode node)
        {
            EmissionMultiplier = ReadFloat(node, "EmissionMultiplier", EmissionMultiplier, 0.10f, 8.00f);
            MaxParticlesMultiplier = ReadFloat(node, "MaxParticlesMultiplier", MaxParticlesMultiplier, 0.25f, 6.00f);
            RadiusScaleMultiplier = ReadFloat(node, "RadiusScaleMultiplier", RadiusScaleMultiplier, 0.25f, 4.00f);
            SharedEmissionMultiplier = ReadFloat(node, "SharedEmissionMultiplier", SharedEmissionMultiplier, 0.10f, 8.00f);
            SharedMaxParticlesMultiplier = ReadFloat(node, "SharedMaxParticlesMultiplier", SharedMaxParticlesMultiplier, 0.25f, 6.00f);
            SharedRadiusScaleMultiplier = ReadFloat(node, "SharedRadiusScaleMultiplier", SharedRadiusScaleMultiplier, 0.25f, 4.00f);

            MaxRayDistance = ReadFloat(node, "MaxRayDistance", MaxRayDistance, 8f, 120f);
            MinNormalizedThrust = ReadFloat(node, "MinNormalizedThrust", MinNormalizedThrust, 0.01f, 1f);
            ShadowLightFactor = ReadFloat(node, "ShadowLightFactor", ShadowLightFactor, 0.0f, 1f);
            LateralSpreadMultiplier = ReadFloat(node, "LateralSpreadMultiplier", LateralSpreadMultiplier, 0.30f, 8f);
            VerticalLiftMultiplier = ReadFloat(node, "VerticalLiftMultiplier", VerticalLiftMultiplier, 0.10f, 3f);
            TurbulenceMultiplier = ReadFloat(node, "TurbulenceMultiplier", TurbulenceMultiplier, 0.10f, 4f);
            RingExpansionMultiplier = ReadFloat(node, "RingExpansionMultiplier", RingExpansionMultiplier, 0.30f, 6f);
            DynamicSwayMultiplier = ReadFloat(node, "DynamicSwayMultiplier", DynamicSwayMultiplier, 0.10f, 4f);

            EngineCountExponent = ReadFloat(node, "EngineCountExponent", EngineCountExponent, 0.15f, 2.20f);
            EngineCountMinScale = ReadFloat(node, "EngineCountMinScale", EngineCountMinScale, 0.05f, 1.00f);
            MinExhaustToGroundAlignment = ReadFloat(node, "MinExhaustToGroundAlignment", MinExhaustToGroundAlignment, 0.00f, 0.95f);
            MinRayDirectionToBodyDown = ReadFloat(node, "MinRayDirectionToBodyDown", MinRayDirectionToBodyDown, -0.20f, 0.95f);
            MinExhaustToBodyDown = ReadFloat(node, "MinExhaustToBodyDown", MinExhaustToBodyDown, 0.00f, 0.98f);

            ThrustPowerReference = ReadFloat(node, "ThrustPowerReference", ThrustPowerReference, 10f, 900f);
            ThrustPowerExponent = ReadFloat(node, "ThrustPowerExponent", ThrustPowerExponent, 0.10f, 2.50f);
            ThrustPowerMinScale = ReadFloat(node, "ThrustPowerMinScale", ThrustPowerMinScale, 0.05f, 4.00f);
            ThrustPowerMaxScale = ReadFloat(node, "ThrustPowerMaxScale", ThrustPowerMaxScale, 0.20f, 8.00f);
            if (ThrustPowerMaxScale < ThrustPowerMinScale)
            {
                ThrustPowerMaxScale = ThrustPowerMinScale;
            }

            MaxDistanceAtLowThrust = ReadFloat(node, "MaxDistanceAtLowThrust", MaxDistanceAtLowThrust, 2f, 80f);
            MaxDistanceAtHighThrust = ReadFloat(node, "MaxDistanceAtHighThrust", MaxDistanceAtHighThrust, 3f, 120f);
            if (MaxDistanceAtHighThrust < MaxDistanceAtLowThrust)
            {
                MaxDistanceAtHighThrust = MaxDistanceAtLowThrust;
            }

            TouchdownMinSpeed = ReadFloat(node, "TouchdownMinSpeed", TouchdownMinSpeed, 0.5f, 35f);
            TouchdownBurstMultiplier = ReadFloat(node, "TouchdownBurstMultiplier", TouchdownBurstMultiplier, 0.20f, 5f);

            ConfigNode[] bodyNodes = node.GetNodes("BODY_VISIBILITY");
            if (bodyNodes != null)
            {
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

                    float value = ReadFloat(bodyNode, "multiplier", 1f, 0.40f, 3.00f);
                    BodyVisibilityMultipliers[name.Trim()] = value;
                }
            }
        }

        private static float ReadFloat(ConfigNode node, string key, float fallback, float minValue, float maxValue)
        {
            if (node == null || string.IsNullOrEmpty(key) || !node.HasValue(key))
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

        private static string GetConfigPath()
        {
            return Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "KerbalFX", "ImpactPuffs", "KerbalFX_ImpactPuffs.cfg");
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

        private static void LogCurrentConfig(string source)
        {
            string details =
                "EmissionMultiplier=" + EmissionMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " MaxParticlesMultiplier=" + MaxParticlesMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " RadiusScaleMultiplier=" + RadiusScaleMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " SharedEmissionMultiplier=" + SharedEmissionMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " SharedMaxParticlesMultiplier=" + SharedMaxParticlesMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " SharedRadiusScaleMultiplier=" + SharedRadiusScaleMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " MaxRayDistance=" + MaxRayDistance.ToString("F1", CultureInfo.InvariantCulture)
                + " MinNormalizedThrust=" + MinNormalizedThrust.ToString("F2", CultureInfo.InvariantCulture)
                + " LateralSpreadMultiplier=" + LateralSpreadMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " VerticalLiftMultiplier=" + VerticalLiftMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " TurbulenceMultiplier=" + TurbulenceMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " RingExpansionMultiplier=" + RingExpansionMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " DynamicSwayMultiplier=" + DynamicSwayMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " EngineCountExponent=" + EngineCountExponent.ToString("F2", CultureInfo.InvariantCulture)
                + " EngineCountMinScale=" + EngineCountMinScale.ToString("F2", CultureInfo.InvariantCulture)
                + " MinExhaustToGroundAlignment=" + MinExhaustToGroundAlignment.ToString("F2", CultureInfo.InvariantCulture)
                + " MinRayDirectionToBodyDown=" + MinRayDirectionToBodyDown.ToString("F2", CultureInfo.InvariantCulture)
                + " MinExhaustToBodyDown=" + MinExhaustToBodyDown.ToString("F2", CultureInfo.InvariantCulture)
                + " ThrustPowerReference=" + ThrustPowerReference.ToString("F1", CultureInfo.InvariantCulture)
                + " ThrustPowerExponent=" + ThrustPowerExponent.ToString("F2", CultureInfo.InvariantCulture)
                + " ThrustPowerMinScale=" + ThrustPowerMinScale.ToString("F2", CultureInfo.InvariantCulture)
                + " ThrustPowerMaxScale=" + ThrustPowerMaxScale.ToString("F2", CultureInfo.InvariantCulture)
                + " MaxDistanceAtLowThrust=" + MaxDistanceAtLowThrust.ToString("F1", CultureInfo.InvariantCulture)
                + " MaxDistanceAtHighThrust=" + MaxDistanceAtHighThrust.ToString("F1", CultureInfo.InvariantCulture)
                + " TouchdownMinSpeed=" + TouchdownMinSpeed.ToString("F2", CultureInfo.InvariantCulture)
                + " TouchdownBurstMultiplier=" + TouchdownBurstMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " BodyVisibilityEntries=" + BodyVisibilityMultipliers.Count.ToString(CultureInfo.InvariantCulture);

            ImpactPuffsLog.Info(
                Localizer.Format(ImpactPuffsLoc.LogConfig, source, details));
        }
    }

    internal static class ImpactPuffsLog
    {
        public static void Info(string message)
        {
            Debug.Log("[KerbalFX] " + message);
        }

        public static void DebugLog(string message)
        {
            if (ImpactPuffsConfig.DebugLogging)
            {
                Debug.Log("[KerbalFX] " + message);
            }
        }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ImpactPuffsBootstrap : MonoBehaviour
    {
        private readonly Dictionary<Guid, VesselImpactController> controllers = new Dictionary<Guid, VesselImpactController>();
        private readonly Dictionary<Guid, float> invalidControllerTimers = new Dictionary<Guid, float>();
        private readonly List<Guid> removeControllerIds = new List<Guid>(32);

        private float controllerRefreshTimer;
        private float settingsRefreshTimer;
        private float debugHeartbeatTimer;
        private bool emittersStoppedWhileDisabled;

        private const float ControllerRefreshInterval = 1.0f;
        private const float SettingsRefreshInterval = 0.5f;
        private const float ControllerInvalidGraceSeconds = 4.0f;

        private void Start()
        {
            ImpactPuffsConfig.Refresh();
            ImpactPuffsRuntimeConfig.Refresh();
            EngineGroundPuffEmitter.CleanupSunOcclusionCache(true);
            ImpactPuffsLog.Info(Localizer.Format(ImpactPuffsLoc.LogBootstrapStart));
        }

        private void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight)
            {
                return;
            }

            float dt = Time.deltaTime;
            RefreshSettingsIfNeeded(dt);
            if (!ImpactPuffsConfig.Enabled)
            {
                if (!emittersStoppedWhileDisabled)
                {
                    StopAllEmitters();
                    emittersStoppedWhileDisabled = true;
                }

                EngineGroundPuffEmitter.CleanupSunOcclusionCache(false);
                return;
            }

            emittersStoppedWhileDisabled = false;
            RefreshControllersIfNeeded(dt);
            TickControllers(dt);
            LogHeartbeatIfNeeded(dt);
            EngineGroundPuffEmitter.CleanupSunOcclusionCache(false);
        }

        private void RefreshSettingsIfNeeded(float dt)
        {
            settingsRefreshTimer -= dt;
            if (settingsRefreshTimer > 0f)
            {
                return;
            }

            settingsRefreshTimer = SettingsRefreshInterval;
            ImpactPuffsConfig.Refresh();
            ImpactPuffsRuntimeConfig.TryHotReloadFromDisk();
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
            foreach (KeyValuePair<Guid, VesselImpactController> pair in controllers)
            {
                pair.Value.Tick(dt);
            }
        }

        private void LogHeartbeatIfNeeded(float dt)
        {
            if (!ImpactPuffsConfig.DebugLogging)
            {
                return;
            }

            debugHeartbeatTimer -= dt;
            if (debugHeartbeatTimer > 0f)
            {
                return;
            }

            debugHeartbeatTimer = 2.5f;
            ImpactPuffsLog.DebugLog(Localizer.Format(ImpactPuffsLoc.LogHeartbeat, controllers.Count));
        }

        private void StopAllEmitters()
        {
            foreach (KeyValuePair<Guid, VesselImpactController> pair in controllers)
            {
                pair.Value.StopAll();
            }
        }

        private void RefreshControllers()
        {
            RemoveInvalidControllers();
            AttachOrRefreshLoadedVessels();
        }

        private void RemoveInvalidControllers()
        {
            removeControllerIds.Clear();
            foreach (KeyValuePair<Guid, VesselImpactController> pair in controllers)
            {
                if (!pair.Value.IsStillValid())
                {
                    float invalidTimer;
                    invalidControllerTimers.TryGetValue(pair.Key, out invalidTimer);
                    invalidTimer += ControllerRefreshInterval;
                    invalidControllerTimers[pair.Key] = invalidTimer;

                    if (invalidTimer >= ControllerInvalidGraceSeconds)
                    {
                        pair.Value.Dispose();
                        removeControllerIds.Add(pair.Key);
                    }
                }
                else
                {
                    invalidControllerTimers.Remove(pair.Key);
                }
            }

            for (int i = 0; i < removeControllerIds.Count; i++)
            {
                RemoveController(removeControllerIds[i]);
            }
        }

        private void RemoveController(Guid vesselId)
        {
            controllers.Remove(vesselId);
            invalidControllerTimers.Remove(vesselId);
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

                VesselImpactController controller;
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
            VesselImpactController controller = new VesselImpactController(vessel);
            if (!controller.HasAnyEmitters)
            {
                controller.Dispose();
                return;
            }

            controllers.Add(vessel.id, controller);
            invalidControllerTimers.Remove(vessel.id);
            ImpactPuffsLog.DebugLog(Localizer.Format(ImpactPuffsLoc.LogAttached, controller.EngineEmitterCount, vessel.vesselName));
        }

        private static bool IsSupportedVessel(Vessel vessel)
        {
            if (vessel == null || !vessel.loaded || vessel.packed || vessel.isEVA)
            {
                return false;
            }

            return vessel.vesselType != VesselType.Flag && vessel.vesselType != VesselType.Debris;
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<Guid, VesselImpactController> pair in controllers)
            {
                pair.Value.Dispose();
            }

            controllers.Clear();
            invalidControllerTimers.Clear();
            EngineGroundPuffEmitter.CleanupSunOcclusionCache(true);
            ImpactPuffsLog.Info(Localizer.Format(ImpactPuffsLoc.LogBootstrapStop));
        }
    }

    internal sealed class VesselImpactController
    {
        private readonly Vessel vessel;
        private readonly List<EngineGroundPuffEmitter> engineEmitters = new List<EngineGroundPuffEmitter>();
        private TouchdownBurstEmitter touchdownEmitter;
        private int cachedPartCount = -1;
        private int acceptedEngineCount = 0;
        private static readonly string[] EngineTypeRejectTokens = { "mono", "rcs", "turbine", "jet", "scram", "airbreathing" };
        private static readonly string[] EngineIdRejectTokens = { "rcs", "monoprop", "mono", "vernier" };
        private static readonly string[] PartNameRejectTokens = { "jet", "turbine", "airbreathing", "air breathing", "rcs", "monoprop" };

        public int EngineEmitterCount
        {
            get { return engineEmitters.Count; }
        }

        public bool HasAnyEmitters
        {
            get { return engineEmitters.Count > 0 || touchdownEmitter != null; }
        }

        public VesselImpactController(Vessel vessel)
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
            if (vessel == null || !vessel.loaded || vessel.packed)
            {
                StopAll();
                return;
            }

            for (int i = 0; i < engineEmitters.Count; i++)
            {
                engineEmitters[i].Tick(vessel, dt);
            }

            if (touchdownEmitter != null)
            {
                touchdownEmitter.Tick(dt);
            }
        }

        public void StopAll()
        {
            for (int i = 0; i < engineEmitters.Count; i++)
            {
                engineEmitters[i].StopEmission();
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
            acceptedEngineCount = 0;

            if (vessel == null || vessel.parts == null)
            {
                return;
            }

            int engineModuleCount = 0;
            int skippedEngineCount = 0;
            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part part = vessel.parts[i];
                ScanPartForEngines(part, ref engineModuleCount, ref skippedEngineCount);
            }

            ApplyEmitterClusterSize();
            touchdownEmitter = new TouchdownBurstEmitter(vessel);
            LogVesselScan(engineModuleCount, skippedEngineCount);
        }

        private void DisposeEmitters()
        {
            for (int i = 0; i < engineEmitters.Count; i++)
            {
                engineEmitters[i].Dispose();
            }

            engineEmitters.Clear();

            if (touchdownEmitter != null)
            {
                touchdownEmitter.Dispose();
                touchdownEmitter = null;
            }
        }

        private void ScanPartForEngines(Part part, ref int engineModuleCount, ref int skippedEngineCount)
        {
            if (part == null || part.Modules == null)
            {
                return;
            }

            for (int i = 0; i < part.Modules.Count; i++)
            {
                ModuleEngines engine = part.Modules[i] as ModuleEngines;
                if (engine == null)
                {
                    continue;
                }

                engineModuleCount++;
                if (!ShouldUseEngineForGroundPuffs(part, engine))
                {
                    skippedEngineCount++;
                    continue;
                }

                acceptedEngineCount++;
                AddEngineEmittersForTransforms(part, engine);
            }
        }

        private void AddEngineEmittersForTransforms(Part part, ModuleEngines engine)
        {
            List<Transform> transforms = ResolveThrustTransforms(engine, part);
            if (transforms.Count == 0)
            {
                engineEmitters.Add(new EngineGroundPuffEmitter(part, engine, null));
                return;
            }

            for (int i = 0; i < transforms.Count; i++)
            {
                engineEmitters.Add(new EngineGroundPuffEmitter(part, engine, transforms[i]));
            }
        }

        private void ApplyEmitterClusterSize()
        {
            int clusterSize = Mathf.Max(1, acceptedEngineCount);
            for (int i = 0; i < engineEmitters.Count; i++)
            {
                engineEmitters[i].SetEngineClusterCount(clusterSize);
            }
        }

        private void LogVesselScan(int engineModuleCount, int skippedEngineCount)
        {
            if (!ImpactPuffsConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel)
            {
                return;
            }

            ImpactPuffsLog.DebugLog(Localizer.Format(
                ImpactPuffsLoc.LogVesselScan,
                vessel.vesselName,
                engineModuleCount.ToString(CultureInfo.InvariantCulture),
                acceptedEngineCount.ToString(CultureInfo.InvariantCulture),
                skippedEngineCount.ToString(CultureInfo.InvariantCulture),
                engineEmitters.Count.ToString(CultureInfo.InvariantCulture)));
        }

        private static List<Transform> ResolveThrustTransforms(ModuleEngines engine, Part part)
        {
            List<Transform> raw = new List<Transform>();

            if (engine != null && engine.thrustTransforms != null)
            {
                for (int i = 0; i < engine.thrustTransforms.Count; i++)
                {
                    Transform transform = engine.thrustTransforms[i];
                    if (transform != null)
                    {
                        raw.Add(transform);
                    }
                }
            }

            if (raw.Count == 0 && engine != null && part != null && !string.IsNullOrEmpty(engine.thrustVectorTransformName))
            {
                Transform[] named = part.FindModelTransforms(engine.thrustVectorTransformName);
                if (named != null)
                {
                    for (int i = 0; i < named.Length; i++)
                    {
                        if (named[i] != null)
                        {
                            raw.Add(named[i]);
                        }
                    }
                }
            }

            if (raw.Count <= 1)
            {
                return raw;
            }

            List<Transform> filtered = new List<Transform>();
            for (int i = 0; i < raw.Count; i++)
            {
                if (IsLikelyNozzleTransform(part, raw[i]))
                {
                    filtered.Add(raw[i]);
                }
            }

            return filtered.Count > 0 ? filtered : raw;
        }

        private static bool IsLikelyNozzleTransform(Part part, Transform transform)
        {
            if (part == null || part.transform == null || transform == null)
            {
                return false;
            }

            Vector3 localPos = part.transform.InverseTransformPoint(transform.position);
            Vector3 localExhaustDir = part.transform.InverseTransformDirection(-transform.forward);
            if (localExhaustDir.sqrMagnitude < 0.0001f)
            {
                return true;
            }

            localExhaustDir.Normalize();
            float sideScore = Vector3.Dot(localPos, localExhaustDir);
            return sideScore >= -0.02f;
        }

        private static bool ShouldUseEngineForGroundPuffs(Part part, ModuleEngines engine)
        {
            if (engine == null)
            {
                return false;
            }

            string engineType = ReadEngineTypeName(engine);
            if (ContainsAny(engineType, EngineTypeRejectTokens))
            {
                return false;
            }

            if (HasPropellant(engine, "MonoPropellant") || HasPropellant(engine, "IntakeAir"))
            {
                return false;
            }

            string engineId = SafeLower(engine.engineID);
            if (ContainsAny(engineId, EngineIdRejectTokens))
            {
                return false;
            }

            string partName = string.Empty;
            if (part != null && part.partInfo != null)
            {
                partName = (part.partInfo.name + " " + part.partInfo.title).ToLowerInvariant();
            }

            if (ContainsAny(partName, PartNameRejectTokens))
            {
                return false;
            }

            return true;
        }

        private static bool HasPropellant(ModuleEngines engine, string propellantName)
        {
            if (engine == null || engine.propellants == null || string.IsNullOrEmpty(propellantName))
            {
                return false;
            }

            for (int i = 0; i < engine.propellants.Count; i++)
            {
                Propellant propellant = engine.propellants[i];
                if (propellant != null && string.Equals(propellant.name, propellantName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ReadEngineTypeName(ModuleEngines engine)
        {
            if (engine == null)
            {
                return string.Empty;
            }

            Type type = engine.GetType();
            if (type == null)
            {
                return string.Empty;
            }

            try
            {
                System.Reflection.FieldInfo field = type.GetField("engineType");
                if (field != null)
                {
                    object fieldValue = field.GetValue(engine);
                    if (fieldValue != null)
                    {
                        return fieldValue.ToString().ToLowerInvariant();
                    }
                }

                System.Reflection.PropertyInfo property = type.GetProperty("engineType");
                if (property != null)
                {
                    object propertyValue = property.GetValue(engine, null);
                    if (propertyValue != null)
                    {
                        return propertyValue.ToString().ToLowerInvariant();
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static string SafeLower(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.ToLowerInvariant();
        }

        private static bool ContainsAny(string source, string[] tokens)
        {
            if (string.IsNullOrEmpty(source) || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token) && source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class EngineGroundPuffEmitter
    {
        private readonly Part part;
        private readonly ModuleEngines engine;
        private readonly Transform thrustTransform;
        private readonly GameObject root;
        private readonly ParticleSystem particleSystem;
        private readonly VolumetricPlumeField volumetricField;
        private readonly string debugId;

        private bool disposed;
        private float smoothedRate;
        private float profileRefreshTimer;
        private float colorRefreshTimer;
        private float debugTimer;
        private float suppressionLogTimer;
        private float dynamicSwayTime;
        private float startupRampTimer;
        private float ignitionPrimeTimer;
        private bool wasEngineIgnited;

        private int appliedUiRevision = -1;
        private int appliedRuntimeRevision = -1;
        private int engineClusterCount = 1;

        private Color currentColor = new Color(0.72f, 0.68f, 0.61f, 1f);
        private float cachedLightFactor = 1f;
        private float cachedBodyVisibility = 1f;

        private const float BaseAlpha = 0.60f;
        private const float StartupRampDuration = 1.65f;
        private const float IgnitionPrimeDuration = 1.20f;
        private static readonly string[] LaunchsiteSurfaceTokens =
        {
            "launchpad",
            "launch_pad",
            "launch pad",
            "launchsite",
            "launch_site",
            "ksc",
            "runway"
        };
        private static readonly RaycastHit[] SharedHits = new RaycastHit[24];
        private static readonly RaycastHit[] SunOcclusionHits = new RaycastHit[16];
        private static readonly Dictionary<Guid, SunOcclusionCacheEntry> SunOcclusionCache = new Dictionary<Guid, SunOcclusionCacheEntry>();
        private static readonly List<Guid> SunOcclusionRemoveIds = new List<Guid>(32);
        private const float SunOcclusionPurgeIntervalSeconds = 900f;
        private static float nextSunOcclusionPurgeAt;

        private struct SunOcclusionCacheEntry
        {
            public bool Occluded;
            public float ValidUntil;
            public Vector3 SamplePoint;
            public Vector3 SunDirection;
        }

        public static void CleanupSunOcclusionCache(bool force)
        {
            if (force)
            {
                SunOcclusionCache.Clear();
                SunOcclusionRemoveIds.Clear();
                nextSunOcclusionPurgeAt = Time.time + SunOcclusionPurgeIntervalSeconds;
                return;
            }

            float now = Time.time;
            if (now < nextSunOcclusionPurgeAt)
            {
                return;
            }

            nextSunOcclusionPurgeAt = now + SunOcclusionPurgeIntervalSeconds;
            if (SunOcclusionCache.Count == 0)
            {
                return;
            }

            SunOcclusionRemoveIds.Clear();
            foreach (KeyValuePair<Guid, SunOcclusionCacheEntry> pair in SunOcclusionCache)
            {
                SunOcclusionCacheEntry entry = pair.Value;
                bool cacheExpired = entry.ValidUntil + 2f < now;
                Vessel vessel = FlightGlobals.FindVessel(pair.Key);
                bool vesselInvalid = vessel == null || !vessel.loaded || vessel.packed;
                if (cacheExpired || vesselInvalid)
                {
                    SunOcclusionRemoveIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < SunOcclusionRemoveIds.Count; i++)
            {
                SunOcclusionCache.Remove(SunOcclusionRemoveIds[i]);
            }
        }

        public EngineGroundPuffEmitter(Part part, ModuleEngines engine, Transform thrustTransform)
        {
            this.part = part;
            this.engine = engine;
            this.thrustTransform = thrustTransform;

            string transformName = thrustTransform != null ? thrustTransform.name : "part";
            debugId = part.partInfo.name + ":" + transformName;

            root = new GameObject("KerbalFX_EngineGroundPuff");
            root.transform.SetParent(null, false);
            root.transform.position = part.transform.position;
            root.layer = part.gameObject.layer;

            particleSystem = root.AddComponent<ParticleSystem>();
            ConfigureParticleSystemBase();
            ApplyRuntimeVisualProfile(true);
            volumetricField = new VolumetricPlumeField(root.transform, part.gameObject.layer);
        }

        public void SetEngineClusterCount(int count)
        {
            engineClusterCount = Mathf.Max(1, count);
        }

        public void Tick(Vessel vessel, float dt)
        {
            if (disposed || part == null || engine == null)
            {
                return;
            }

            RefreshRuntimeProfileIfNeeded(dt);

            if (ShouldSkipForVesselState(vessel, dt))
            {
                return;
            }

            float currentThrust;
            float normalizedThrust;
            if (!TryResolveThrustInputs(vessel, dt, out currentThrust, out normalizedThrust))
            {
                return;
            }

            RaycastHit groundHit;
            Vector3 exhaustDirection;
            float terrainHeight;
            float exhaustToBodyDown;
            float alignment;
            float distanceFactor;
            if (!TryResolveGroundInteraction(
                vessel,
                normalizedThrust,
                dt,
                out groundHit,
                out exhaustDirection,
                out terrainHeight,
                out exhaustToBodyDown,
                out alignment,
                out distanceFactor))
            {
                return;
            }

            float qualityNorm;
            float pressure;
            float thrustPowerNorm;
            float targetRate = ComputeTargetRate(
                vessel,
                currentThrust,
                normalizedThrust,
                distanceFactor,
                out qualityNorm,
                out pressure,
                out thrustPowerNorm);

            targetRate = ApplyLightAwarenessRate(vessel, groundHit, normalizedThrust, targetRate);

            Vector3 stableNormal;
            Vector3 outwardDirForCoreClamp;
            Vector3 finalPosition = ComputeEmitterPosition(
                vessel,
                groundHit,
                pressure,
                out stableNormal,
                out outwardDirForCoreClamp);

            root.transform.position = finalPosition;

            if (ImpactPuffsConfig.UseSimplifiedEffects)
            {
                UpdateDynamicGroundFlow(vessel, groundHit.point, stableNormal, exhaustDirection, pressure, qualityNorm, distanceFactor, thrustPowerNorm);
            }
            else
            {
                UpdateVolumetricFrame(vessel, finalPosition, stableNormal, outwardDirForCoreClamp, exhaustDirection, pressure, dt);
            }

            colorRefreshTimer -= dt;
            if (colorRefreshTimer <= 0f)
            {
                colorRefreshTimer = 0.25f;
                UpdateSurfaceColor(vessel, groundHit.collider);
                ApplyCurrentStartColor();
            }

            if (ImpactPuffsConfig.UseSimplifiedEffects)
            {
                SetTargetRate(targetRate, dt);
                if (volumetricField != null)
                {
                    volumetricField.StopSoft(dt);
                }
            }
            else
            {
                SetTargetRate(0f, dt);
                if (volumetricField != null)
                {
                    volumetricField.Update(
                        root.transform.position,
                        root.transform.rotation,
                        targetRate,
                        pressure,
                        qualityNorm,
                        currentColor,
                        cachedLightFactor,
                        dt
                    );
                }
            }

            LogEmitterDebug(
                vessel,
                dt,
                normalizedThrust,
                groundHit.distance,
                ImpactPuffsConfig.UseSimplifiedEffects ? smoothedRate : targetRate,
                pressure,
                alignment,
                exhaustToBodyDown,
                terrainHeight,
                currentThrust);
        }

        private bool ShouldSkipForVesselState(Vessel vessel, float dt)
        {
            if (vessel == null || !vessel.loaded || vessel.packed || vessel.Splashed)
            {
                StopAllEmission(dt, false);
                return true;
            }

            if (vessel.situation == Vessel.Situations.PRELAUNCH)
            {
                StopAllEmission(dt, true);
                return true;
            }

            if (IsInKerbinLaunchsiteZone(vessel))
            {
                LogLaunchsiteSuppression("zone", vessel, null);
                StopAllEmission(dt, true);
                return true;
            }

            return false;
        }

        private bool TryResolveThrustInputs(Vessel vessel, float dt, out float currentThrust, out float normalizedThrust)
        {
            currentThrust = 0f;
            normalizedThrust = 0f;

            bool engineIgnited = engine.EngineIgnited;
            if (engineIgnited && !wasEngineIgnited)
            {
                ignitionPrimeTimer = IgnitionPrimeDuration;
            }

            wasEngineIgnited = engineIgnited;
            ignitionPrimeTimer = Mathf.Max(0f, ignitionPrimeTimer - dt);
            if (!engineIgnited)
            {
                StopAllEmission(dt, false);
                return false;
            }

            currentThrust = Mathf.Max(0f, (float)engine.finalThrust);
            normalizedThrust = GetNormalizedLiveThrust(engine);

            float throttleCommand = 0f;
            if (vessel != null && vessel.ctrlState != null)
            {
                throttleCommand = Mathf.Clamp01(vessel.ctrlState.mainThrottle);
            }

            if (ignitionPrimeTimer > 0f)
            {
                float primeT = ignitionPrimeTimer / IgnitionPrimeDuration;
                float ignitionAssist = throttleCommand * Mathf.Lerp(0.30f, 0.88f, primeT);
                normalizedThrust = Mathf.Max(normalizedThrust, ignitionAssist);
            }

            if (normalizedThrust < ImpactPuffsRuntimeConfig.MinNormalizedThrust)
            {
                StopAllEmission(dt, false);
                return false;
            }

            return true;
        }

        private bool TryResolveGroundInteraction(
            Vessel vessel,
            float normalizedThrust,
            float dt,
            out RaycastHit groundHit,
            out Vector3 exhaustDirection,
            out float terrainHeight,
            out float exhaustToBodyDown,
            out float alignment,
            out float distanceFactor)
        {
            groundHit = default(RaycastHit);
            exhaustDirection = Vector3.down;
            terrainHeight = -1f;
            exhaustToBodyDown = 0f;
            alignment = 0f;
            distanceFactor = 0f;

            float maxEffectiveDistance = Mathf.Lerp(
                ImpactPuffsRuntimeConfig.MaxDistanceAtLowThrust,
                ImpactPuffsRuntimeConfig.MaxDistanceAtHighThrust,
                Mathf.Clamp01(normalizedThrust)
            );
            maxEffectiveDistance = Mathf.Clamp(maxEffectiveDistance, 2f, ImpactPuffsRuntimeConfig.MaxRayDistance);

            terrainHeight = GetSafeTerrainHeightAgl(vessel);
            float terrainGate = maxEffectiveDistance * 1.35f + 3f;
            if (terrainHeight >= 0f && terrainHeight > terrainGate)
            {
                StopAllEmission(dt, false);
                return false;
            }

            Vector3 origin = thrustTransform != null ? thrustTransform.position : part.transform.position;
            exhaustDirection = GetPrimaryExhaustDirection(origin);

            Vector3 bodyDown = Vector3.down;
            if (vessel != null && vessel.mainBody != null)
            {
                Vector3 toBody = vessel.mainBody.position - origin;
                if (toBody.sqrMagnitude > 0.0001f)
                {
                    bodyDown = toBody.normalized;
                }
            }

            exhaustToBodyDown = Vector3.Dot(exhaustDirection, bodyDown);
            if (exhaustToBodyDown < ImpactPuffsRuntimeConfig.MinExhaustToBodyDown)
            {
                StopAllEmission(dt, false);
                return false;
            }

            if (!TryFindGroundHit(origin, exhaustDirection, vessel, ImpactPuffsRuntimeConfig.MaxRayDistance, out groundHit))
            {
                StopAllEmission(dt, false);
                return false;
            }

            if (IsLaunchsiteExcludedSurface(groundHit.collider))
            {
                LogLaunchsiteSuppression("surface", vessel, groundHit.collider);
                StopAllEmission(dt, true);
                return false;
            }

            Vector3 toGround = groundHit.point - origin;
            if (toGround.sqrMagnitude < 0.0001f)
            {
                StopAllEmission(dt, false);
                return false;
            }

            Vector3 toGroundDirection = toGround.normalized;
            alignment = Vector3.Dot(exhaustDirection, toGroundDirection);
            float minAlignment = Mathf.Lerp(0.42f, ImpactPuffsRuntimeConfig.MinExhaustToGroundAlignment, normalizedThrust);
            if (alignment < minAlignment)
            {
                StopAllEmission(dt, false);
                return false;
            }

            if (groundHit.distance > maxEffectiveDistance)
            {
                StopAllEmission(dt, false);
                return false;
            }

            distanceFactor = Mathf.Clamp01(1f - (groundHit.distance / maxEffectiveDistance));
            distanceFactor = Mathf.Pow(distanceFactor, 1.00f);
            if (distanceFactor <= 0.001f)
            {
                StopAllEmission(dt, false);
                return false;
            }

            return true;
        }

        private float ComputeTargetRate(
            Vessel vessel,
            float currentThrust,
            float normalizedThrust,
            float distanceFactor,
            out float qualityNorm,
            out float pressure,
            out float thrustPowerNorm)
        {
            float quality = GetModeQualityScale();
            qualityNorm = Mathf.InverseLerp(0.25f, 2.0f, quality);
            float qualityRateScale = 1f + (Mathf.Pow(quality, 1.50f) - 1f) * 1.35f;

            cachedBodyVisibility = ImpactPuffsRuntimeConfig.GetBodyVisibilityMultiplier(
                vessel.mainBody != null ? vessel.mainBody.bodyName : string.Empty);

            float thrustPowerScale = ComputeThrustPowerScale(currentThrust);
            thrustPowerNorm = Mathf.Clamp01(
                (thrustPowerScale - ImpactPuffsRuntimeConfig.ThrustPowerMinScale)
                / Mathf.Max(0.01f, ImpactPuffsRuntimeConfig.ThrustPowerMaxScale - ImpactPuffsRuntimeConfig.ThrustPowerMinScale));

            float lowThrustBoost = Mathf.Lerp(1.42f, 1.00f, normalizedThrust);
            pressure = Mathf.Clamp01(
                normalizedThrust
                * lowThrustBoost
                * Mathf.Lerp(0.52f, 1.95f, distanceFactor)
                * Mathf.Lerp(0.78f, 1.46f, thrustPowerNorm));

            float baseRate = (1320f + 14800f * pressure * pressure) * Mathf.Lerp(0.42f, 1.36f, distanceFactor);
            float engineClusterScale = ComputeEngineClusterScale(engineClusterCount);
            float modeDensityScale = ImpactPuffsConfig.UseSimplifiedEffects ? 1.00f : 1.48f;

            float targetRate = baseRate
                * qualityRateScale
                * thrustPowerScale
                * engineClusterScale
                * ImpactPuffsRuntimeConfig.EmissionMultiplier
                * ImpactPuffsRuntimeConfig.SharedEmissionMultiplier
                * cachedBodyVisibility
                * modeDensityScale;

            targetRate *= 1.56f;
            return Mathf.Clamp(targetRate, 0f, 42000f);
        }

        private float ApplyLightAwarenessRate(Vessel vessel, RaycastHit groundHit, float normalizedThrust, float targetRate)
        {
            if (ImpactPuffsConfig.UseLightAware)
            {
                cachedLightFactor = ImpactPuffsConfig.UseSimplifiedEffects
                    ? EvaluateEngineAwareLightFactor(vessel, groundHit.point, groundHit.normal, normalizedThrust)
                    : EvaluateVolumetricLightFactor(vessel, groundHit.point, groundHit.normal, normalizedThrust);

                if (!ImpactPuffsConfig.UseSimplifiedEffects)
                {
                    float volumetricLightRate = Mathf.Pow(Mathf.Clamp01(cachedLightFactor), 0.65f);
                    volumetricLightRate = Mathf.Lerp(0.28f, 1f, volumetricLightRate);
                    targetRate *= volumetricLightRate;
                }
            }
            else
            {
                cachedLightFactor = 1f;
            }

            return targetRate;
        }

        private Vector3 ComputeEmitterPosition(
            Vessel vessel,
            RaycastHit groundHit,
            float pressure,
            out Vector3 stableNormal,
            out Vector3 outwardDirForCoreClamp)
        {
            stableNormal = groundHit.normal.sqrMagnitude > 0.0001f ? groundHit.normal.normalized : Vector3.up;

            float surfaceOffset = -0.05f;
            Vector3 lateralOffset = Vector3.zero;
            outwardDirForCoreClamp = Vector3.zero;

            Vector3 centerPlane = Vector3.ProjectOnPlane(groundHit.point - vessel.CoM, stableNormal);
            float centerPlaneMag = centerPlane.magnitude;
            float underCenter = Mathf.Clamp01(1f - (centerPlaneMag / 0.85f));

            Vector3 outwardDir = centerPlane;
            if (outwardDir.sqrMagnitude < 0.0001f)
            {
                outwardDir = Vector3.ProjectOnPlane((Vector3)vessel.srf_velocity, stableNormal);
            }
            if (outwardDir.sqrMagnitude < 0.0001f)
            {
                outwardDir = Vector3.ProjectOnPlane(part.transform.right, stableNormal);
            }

            if (outwardDir.sqrMagnitude > 0.0001f)
            {
                outwardDir.Normalize();
                outwardDirForCoreClamp = outwardDir;
                float outwardShift = ImpactPuffsConfig.UseSimplifiedEffects
                    ? (Mathf.Lerp(0.40f, 1.35f, pressure) * (0.80f + 0.90f * underCenter))
                    : (Mathf.Lerp(0.08f, 0.42f, pressure) * Mathf.Lerp(0.30f, 0.92f, underCenter));
                lateralOffset = outwardDir * outwardShift;
            }

            Vector3 finalPosition = groundHit.point + stableNormal * surfaceOffset + lateralOffset;
            Vector3 finalPlane = Vector3.ProjectOnPlane(finalPosition - vessel.CoM, stableNormal);
            float finalRadius = finalPlane.magnitude;
            float minCoreRadius = ImpactPuffsConfig.UseSimplifiedEffects
                ? Mathf.Lerp(1.10f, 2.85f, pressure)
                : Mathf.Lerp(0.55f, 1.95f, pressure);
            if (finalRadius < minCoreRadius)
            {
                Vector3 pushDir = outwardDirForCoreClamp;
                if (pushDir.sqrMagnitude < 0.0001f && finalPlane.sqrMagnitude > 0.0001f)
                {
                    pushDir = finalPlane.normalized;
                }
                if (pushDir.sqrMagnitude > 0.0001f)
                {
                    finalPosition += pushDir * (minCoreRadius - finalRadius);
                }
            }

            return finalPosition;
        }

        private void RefreshRuntimeProfileIfNeeded(float dt)
        {
            profileRefreshTimer -= dt;
            if (profileRefreshTimer <= 0f
                || appliedUiRevision != ImpactPuffsConfig.Revision
                || appliedRuntimeRevision != ImpactPuffsRuntimeConfig.Revision)
            {
                profileRefreshTimer = 0.33f;
                ApplyRuntimeVisualProfile(false);
            }
        }

        private void LogEmitterDebug(
            Vessel vessel,
            float dt,
            float normalizedThrust,
            float groundDistance,
            float displayedRate,
            float pressure,
            float alignment,
            float exhaustToBodyDown,
            float terrainHeight,
            float currentThrust)
        {
            if (!ImpactPuffsConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel)
            {
                return;
            }

            debugTimer -= dt;
            if (debugTimer > 0f)
            {
                return;
            }

            debugTimer = 1.2f;
            ImpactPuffsLog.DebugLog(Localizer.Format(
                ImpactPuffsLoc.LogEngineEmitter,
                debugId,
                normalizedThrust.ToString("F2", CultureInfo.InvariantCulture),
                groundDistance.ToString("F2", CultureInfo.InvariantCulture),
                displayedRate.ToString("F1", CultureInfo.InvariantCulture),
                cachedLightFactor.ToString("F2", CultureInfo.InvariantCulture)
                + " pressure=" + pressure.ToString("F2", CultureInfo.InvariantCulture)
                + " align=" + alignment.ToString("F2", CultureInfo.InvariantCulture)
                + " bodyAlign=" + exhaustToBodyDown.ToString("F2", CultureInfo.InvariantCulture)
                + " terrainH=" + terrainHeight.ToString("F1", CultureInfo.InvariantCulture)
                + " thrust=" + currentThrust.ToString("F0", CultureInfo.InvariantCulture)
                + " mode=" + (ImpactPuffsConfig.UseSimplifiedEffects ? "simplified" : "volumetric")
            ));
        }

        public void StopEmission()
        {
            StopAllEmission(0.12f, false);
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
            if (volumetricField != null)
            {
                volumetricField.Dispose();
            }

            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        private void StopAllEmission(float dt, bool immediateVolumetric)
        {
            SetTargetRate(0f, dt);
            if (volumetricField == null)
            {
                return;
            }

            if (immediateVolumetric)
            {
                volumetricField.StopImmediate();
            }
            else
            {
                volumetricField.StopSoft(dt);
            }
        }

        private void LogLaunchsiteSuppression(string reason, Vessel vessel, Collider collider)
        {
            if (!ImpactPuffsConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel)
            {
                return;
            }

            suppressionLogTimer -= Time.deltaTime;
            if (suppressionLogTimer > 0f)
            {
                return;
            }

            suppressionLogTimer = 1.5f;
            string colliderName = collider != null ? GetSurfaceNameChain(collider, 2) : "n/a";
            ImpactPuffsLog.DebugLog(Localizer.Format(
                ImpactPuffsLoc.LogLaunchsiteSuppression,
                reason,
                vessel.vesselName,
                vessel.latitude.ToString("F3", CultureInfo.InvariantCulture),
                vessel.longitude.ToString("F3", CultureInfo.InvariantCulture),
                vessel.altitude.ToString("F1", CultureInfo.InvariantCulture),
                colliderName));
        }

        private void ConfigureParticleSystemBase()
        {
            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(currentColor.r, currentColor.g, currentColor.b, BaseAlpha);
            main.startRotation = new ParticleSystem.MinMaxCurve(-3.14159f, 3.14159f);

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 68f;
            shape.radius = 1.10f;

            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-2.20f, 2.40f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.10f, 0.75f);
            velocity.z = new ParticleSystem.MinMaxCurve(-2.00f, 2.00f);
            velocity.radial = new ParticleSystem.MinMaxCurve(0.45f, 1.20f);

            ParticleSystem.NoiseModule noise = particleSystem.noise;
            noise.enabled = true;
            noise.strength = 0.90f;
            noise.frequency = 0.55f;
            noise.scrollSpeed = 0.35f;
            noise.damping = true;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 2f;

            Material material = ImpactPuffsAssets.GetSharedMaterial();
            if (material != null)
            {
                renderer.material = material;
            }
        }

        private void ApplyRuntimeVisualProfile(bool force)
        {
            if (!force
                && appliedUiRevision == ImpactPuffsConfig.Revision
                && appliedRuntimeRevision == ImpactPuffsRuntimeConfig.Revision)
            {
                return;
            }

            appliedUiRevision = ImpactPuffsConfig.Revision;
            appliedRuntimeRevision = ImpactPuffsRuntimeConfig.Revision;

            float quality = GetModeQualityScale();
            float qualityNorm = Mathf.InverseLerp(0.25f, 2.0f, quality);
            float volumetricBoost = ImpactPuffsConfig.UseSimplifiedEffects ? 1.00f : 1.30f;

            ParticleSystem.MainModule main = particleSystem.main;
            float maxParticles = 2200f
                * (1f + (Mathf.Pow(quality, 1.25f) - 1f) * 1.15f)
                * volumetricBoost
                * ImpactPuffsRuntimeConfig.MaxParticlesMultiplier
                * ImpactPuffsRuntimeConfig.SharedMaxParticlesMultiplier;
            main.maxParticles = Mathf.RoundToInt(Mathf.Clamp(maxParticles, 480f, 22000f));

            float minSize = 0.22f * Mathf.Lerp(0.72f, 1.70f, qualityNorm) * ImpactPuffsRuntimeConfig.RadiusScaleMultiplier * ImpactPuffsRuntimeConfig.SharedRadiusScaleMultiplier;
            float maxSize = 0.72f * Mathf.Lerp(0.72f, 1.70f, qualityNorm) * ImpactPuffsRuntimeConfig.RadiusScaleMultiplier * ImpactPuffsRuntimeConfig.SharedRadiusScaleMultiplier;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);

            main.startLifetime = new ParticleSystem.MinMaxCurve(
                0.78f * Mathf.Lerp(0.90f, 1.38f, qualityNorm),
                3.35f * Mathf.Lerp(0.90f, 1.45f, qualityNorm) * volumetricBoost
            );
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, Mathf.Lerp(0.45f, 1.20f, qualityNorm));
            main.gravityModifier = Mathf.Lerp(0.004f, 0.018f, qualityNorm);

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.angle = Mathf.Lerp(58f, 86f, qualityNorm);
            shape.radius = Mathf.Lerp(0.75f, 2.60f, qualityNorm) * ImpactPuffsRuntimeConfig.RadiusScaleMultiplier * ImpactPuffsRuntimeConfig.SharedRadiusScaleMultiplier;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.55f);
            curve.AddKey(0.48f, 1.95f);
            curve.AddKey(1f, 0.25f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

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
                    new GradientAlphaKey(Mathf.Lerp(0.30f, 0.56f, qualityNorm) * volumetricBoost, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.maxParticleSize = Mathf.Lerp(0.24f, 0.62f, qualityNorm);

            ApplyCurrentStartColor();

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            if (smoothedRate < 0.01f)
            {
                emission.rateOverTime = 0f;
            }
        }

        private void UpdateVolumetricFrame(Vessel vessel, Vector3 plumePosition, Vector3 surfaceNormal, Vector3 outwardHint, Vector3 exhaustDirection, float pressure, float dt)
        {
            Vector3 tangentForward = Vector3.zero;
            if (vessel != null)
            {
                tangentForward = Vector3.ProjectOnPlane(plumePosition - vessel.CoM, surfaceNormal);
            }

            if (tangentForward.sqrMagnitude < 0.0001f && outwardHint.sqrMagnitude > 0.0001f)
            {
                tangentForward = Vector3.ProjectOnPlane(outwardHint, surfaceNormal);
            }
            if (tangentForward.sqrMagnitude < 0.0001f)
            {
                tangentForward = Vector3.ProjectOnPlane(exhaustDirection, surfaceNormal);
            }
            if (tangentForward.sqrMagnitude < 0.0001f)
            {
                tangentForward = Vector3.ProjectOnPlane(part.transform.forward, surfaceNormal);
            }
            if (tangentForward.sqrMagnitude < 0.0001f)
            {
                tangentForward = Vector3.Cross(surfaceNormal, Vector3.right);
            }

            tangentForward.Normalize();
            Vector3 tangentRight = Vector3.Cross(surfaceNormal, tangentForward);
            if (tangentRight.sqrMagnitude < 0.0001f)
            {
                tangentRight = Vector3.Cross(surfaceNormal, Vector3.forward);
            }
            tangentRight.Normalize();

            Vector3 awayFromVessel = tangentForward;
            if (awayFromVessel.sqrMagnitude < 0.0001f)
            {
                awayFromVessel = tangentRight;
            }
            awayFromVessel.Normalize();

            root.transform.position = plumePosition;
            root.transform.rotation = Quaternion.LookRotation(awayFromVessel, surfaceNormal);
        }

        private void UpdateDynamicGroundFlow(Vessel vessel, Vector3 groundPoint, Vector3 surfaceNormal, Vector3 exhaustDirection, float pressure, float qualityNorm, float distanceFactor, float thrustPowerNorm)
        {
            float volumetricBoost = ImpactPuffsConfig.UseSimplifiedEffects ? 1.00f : 1.28f;
            Vector3 tangentForward = Vector3.zero;
            if (vessel != null)
            {
                tangentForward = Vector3.ProjectOnPlane(groundPoint - vessel.CoM, surfaceNormal);
            }

            if (tangentForward.sqrMagnitude < 0.0001f)
            {
                tangentForward = Vector3.ProjectOnPlane(exhaustDirection, surfaceNormal);
            }
            if (tangentForward.sqrMagnitude < 0.0001f)
            {
                tangentForward = Vector3.ProjectOnPlane(part.transform.forward, surfaceNormal);
            }
            if (tangentForward.sqrMagnitude < 0.0001f)
            {
                tangentForward = Vector3.Cross(surfaceNormal, Vector3.right);
            }

            tangentForward.Normalize();
            root.transform.rotation = Quaternion.LookRotation(tangentForward, surfaceNormal);

            dynamicSwayTime += Time.deltaTime * Mathf.Lerp(1.35f, 7.60f, pressure) * ImpactPuffsRuntimeConfig.DynamicSwayMultiplier;
            float swayA = Mathf.Sin(dynamicSwayTime * 1.75f + part.flightID * 0.013f);
            float swayB = Mathf.Cos(dynamicSwayTime * 2.30f + part.flightID * 0.017f);
            float swayC = Mathf.Sin(dynamicSwayTime * 0.78f + part.flightID * 0.051f);
            float sway = (swayA + swayB + swayC) / 3f;

            float lateral = Mathf.Lerp(2.7f, 14.2f, pressure)
                * ImpactPuffsRuntimeConfig.LateralSpreadMultiplier
                * Mathf.Lerp(0.86f, 1.24f, qualityNorm)
                * Mathf.Lerp(0.82f, 1.35f, thrustPowerNorm)
                * volumetricBoost;
            float lift = Mathf.Lerp(0.28f, 1.45f, pressure)
                * ImpactPuffsRuntimeConfig.VerticalLiftMultiplier
                * Mathf.Lerp(0.84f, 1.08f, qualityNorm)
                * volumetricBoost;
            float ring = Mathf.Lerp(0.64f, 2.52f, pressure)
                * ImpactPuffsRuntimeConfig.RingExpansionMultiplier
                * Mathf.Lerp(0.84f, 1.15f, qualityNorm)
                * Mathf.Lerp(0.72f, 1.22f, distanceFactor)
                * Mathf.Lerp(0.86f, 1.28f, thrustPowerNorm)
                * volumetricBoost;

            Vector3 centerOffsetVector = Vector3.ProjectOnPlane(groundPoint - vessel.CoM, surfaceNormal);
            float centerOffset = centerOffsetVector.magnitude;
            float directionalBias = Mathf.Clamp01(centerOffset / Mathf.Lerp(0.45f, 1.30f, pressure));
            lift *= Mathf.Lerp(0.30f, 1f, directionalBias);
            ring *= Mathf.Lerp(1.75f, 1.00f, directionalBias);
            lateral *= Mathf.Lerp(1.32f, 1.00f, directionalBias);
            sway *= Mathf.Lerp(0.32f, 1.00f, directionalBias);

            float swayScale = 1.00f;
            float swayOffset = sway * lateral * 0.95f * swayScale * directionalBias;
            float sideJitter = Mathf.Sin(dynamicSwayTime * 1.07f + part.flightID * 0.023f) * lateral * 0.76f * swayScale * Mathf.Lerp(0.45f, 1f, directionalBias);

            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve((-lateral * 0.92f) + swayOffset - sideJitter, (lateral * 0.92f) + swayOffset + sideJitter);
            float yMin = lift * 0.18f;
            float yMax = lift * 1.08f;
            velocity.y = new ParticleSystem.MinMaxCurve(yMin, yMax);
            float zBias = sideJitter * 0.28f;
            float zMin = (lateral * 0.08f) + zBias;
            float zMax = (lateral * 1.34f) + zBias;
            zMin = Mathf.Max(zMin, lateral * 0.20f);
            zMax = Mathf.Max(zMax, lateral * 1.62f);
            velocity.z = new ParticleSystem.MinMaxCurve(zMin, zMax);
            velocity.radial = new ParticleSystem.MinMaxCurve(ring * 0.72f, ring * 1.28f);

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            float radius = Mathf.Lerp(0.62f, 2.65f, pressure);
            radius *= 1.14f;
            shape.radius = radius * ImpactPuffsRuntimeConfig.RadiusScaleMultiplier * ImpactPuffsRuntimeConfig.SharedRadiusScaleMultiplier;
            shape.angle = Mathf.Lerp(50f, 80f, pressure);

            ParticleSystem.NoiseModule noise = particleSystem.noise;
            noise.enabled = true;
            noise.strength = Mathf.Lerp(0.30f, 2.25f, pressure) * ImpactPuffsRuntimeConfig.TurbulenceMultiplier;
            noise.frequency = Mathf.Lerp(0.30f, 1.05f, pressure);
            noise.scrollSpeed = Mathf.Lerp(0.18f, 1.25f, pressure);
            noise.damping = true;
        }

        private void SetTargetRate(float targetRate, float dt)
        {
            if (targetRate > 0.25f)
            {
                startupRampTimer = Mathf.Min(StartupRampDuration, startupRampTimer + Mathf.Max(0f, dt));
            }
            else
            {
                startupRampTimer = Mathf.Max(0f, startupRampTimer - Mathf.Max(0f, dt) * 3.0f);
            }
            float startupFactor = Mathf.Lerp(0.12f, 1f, Mathf.Clamp01(startupRampTimer / StartupRampDuration));
            targetRate *= startupFactor;

            float smoothingSpeed = targetRate > smoothedRate ? 1.65f : 7.50f;
            float lerpSpeed = Mathf.Clamp01(dt * smoothingSpeed);
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

        private Vector3 GetPrimaryExhaustDirection(Vector3 origin)
        {
            if (thrustTransform != null)
            {
                Vector3 dirA = -thrustTransform.forward;
                Vector3 dirB = thrustTransform.forward;
                if (dirA.sqrMagnitude > 0.0001f && dirB.sqrMagnitude > 0.0001f)
                {
                    dirA.Normalize();
                    dirB.Normalize();

                    float scoreA = 0f;
                    float scoreB = 0f;

                    if (part != null && part.transform != null)
                    {
                        Vector3 fromPartCenter = origin - part.transform.position;
                        if (fromPartCenter.sqrMagnitude > 0.0001f)
                        {
                            fromPartCenter.Normalize();
                            scoreA += Vector3.Dot(dirA, fromPartCenter) * 1.45f;
                            scoreB += Vector3.Dot(dirB, fromPartCenter) * 1.45f;
                        }

                        Vector3 partDown = -part.transform.up;
                        if (partDown.sqrMagnitude > 0.0001f)
                        {
                            partDown.Normalize();
                            scoreA += Vector3.Dot(dirA, partDown) * 0.85f;
                            scoreB += Vector3.Dot(dirB, partDown) * 0.85f;
                        }
                    }

                    return scoreB > scoreA ? dirB : dirA;
                }
            }

            if (part != null && part.transform != null)
            {
                Vector3 fallback = -part.transform.up;
                if (fallback.sqrMagnitude > 0.0001f)
                {
                    return fallback.normalized;
                }
            }

            return Vector3.down;
        }

        private static bool TryFindGroundHit(Vector3 origin, Vector3 primaryDir, Vessel vessel, float maxDistance, out RaycastHit hit)
        {
            hit = new RaycastHit();
            if (primaryDir.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Vector3 primary = primaryDir.normalized;

            Vector3 bodyDown = Vector3.down;
            if (vessel != null && vessel.mainBody != null)
            {
                Vector3 toBody = vessel.mainBody.position - origin;
                if (toBody.sqrMagnitude > 0.0001f)
                {
                    bodyDown = toBody.normalized;
                }
            }

            float dotPrimary = Vector3.Dot(primary, bodyDown);
            float minDot = ImpactPuffsRuntimeConfig.MinRayDirectionToBodyDown;

            bool allowPrimary = dotPrimary >= minDot;
            if (!allowPrimary)
            {
                return false;
            }

            RaycastHit primaryHit;
            if (!TryRay(origin, primary, vessel, maxDistance, out primaryHit))
            {
                return false;
            }

            hit = primaryHit;
            return true;
        }

        private static bool TryRay(Vector3 origin, Vector3 direction, Vessel vessel, float maxDistance, out RaycastHit bestHit)
        {
            bestHit = new RaycastHit();
            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction.normalized,
                SharedHits,
                maxDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );

            if (hitCount <= 0)
            {
                return false;
            }

            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = SharedHits[i];
                if (candidate.collider == null)
                {
                    continue;
                }

                Part hitPart = candidate.collider.GetComponentInParent<Part>();
                if (hitPart != null)
                {
                    continue;
                }

                if (vessel != null && vessel.mainBody != null)
                {
                    Vector3 upFromBody = candidate.point - vessel.mainBody.position;
                    if (upFromBody.sqrMagnitude > 0.0001f)
                    {
                        upFromBody.Normalize();
                        Vector3 hitNormal = candidate.normal.sqrMagnitude > 0.0001f ? candidate.normal.normalized : upFromBody;
                        float normalToUp = Vector3.Dot(hitNormal, upFromBody);
                        if (normalToUp < 0.30f)
                        {
                            continue;
                        }
                    }
                }

                if (candidate.distance < bestDistance)
                {
                    bestDistance = candidate.distance;
                    bestHit = candidate;
                    found = true;
                }
            }

            return found;
        }

        private static float GetSafeTerrainHeightAgl(Vessel vessel)
        {
            if (vessel == null)
            {
                return -1f;
            }

            if (vessel.Landed || vessel.situation == Vessel.Situations.PRELAUNCH)
            {
                return 0f;
            }

            double agl = vessel.heightFromTerrain;
            if (!double.IsNaN(agl) && !double.IsInfinity(agl) && agl >= 0.0)
            {
                return (float)agl;
            }

            double radar = vessel.radarAltitude;
            if (!double.IsNaN(radar) && !double.IsInfinity(radar) && radar >= 0.0)
            {
                return (float)radar;
            }

            return -1f;
        }

        internal static float GetModeQualityScale()
        {
            return ImpactPuffsConfig.UseSimplifiedEffects ? 1.00f : 1.70f;
        }

        private static float GetNormalizedLiveThrust(ModuleEngines module)
        {
            if (module == null)
            {
                return 0f;
            }

            float finalThrust = Mathf.Max(0f, (float)module.finalThrust);
            if (finalThrust <= 0.06f)
            {
                return 0f;
            }

            float thrustPercent = Mathf.Clamp01(module.thrustPercentage / 100f);
            float maxPossible = Mathf.Max(0.01f, module.maxThrust * thrustPercent);
            if (maxPossible > 0.01f)
            {
                return Mathf.Clamp01(finalThrust / maxPossible);
            }

            return 0f;
        }

        private static float ComputeThrustPowerScale(float currentThrust)
        {
            float safeThrust = Mathf.Max(0f, currentThrust);
            float normalized = safeThrust / Mathf.Max(10f, ImpactPuffsRuntimeConfig.ThrustPowerReference);
            float scaled = Mathf.Pow(Mathf.Max(0.001f, normalized), ImpactPuffsRuntimeConfig.ThrustPowerExponent);
            return Mathf.Clamp(scaled, ImpactPuffsRuntimeConfig.ThrustPowerMinScale, ImpactPuffsRuntimeConfig.ThrustPowerMaxScale);
        }

        private static float ComputeEngineClusterScale(int clusterCount)
        {
            float count = Mathf.Max(1f, clusterCount);
            float scale = 1f / Mathf.Pow(count, ImpactPuffsRuntimeConfig.EngineCountExponent);
            return Mathf.Clamp(scale, ImpactPuffsRuntimeConfig.EngineCountMinScale, 1f);
        }

        internal static bool IsLaunchsiteExcludedSurface(Collider collider)
        {
            if (collider == null || collider.transform == null)
            {
                return false;
            }

            Transform cursor = collider.transform;
            int depth = 0;
            while (cursor != null && depth < 6)
            {
                if (ContainsToken(cursor.name, LaunchsiteSurfaceTokens))
                {
                    return true;
                }

                GameObject go = cursor.gameObject;
                if (go != null && ContainsToken(go.name, LaunchsiteSurfaceTokens))
                {
                    return true;
                }

                cursor = cursor.parent;
                depth++;
            }

            return false;
        }

        internal static bool IsInKerbinLaunchsiteZone(Vessel vessel)
        {
            if (vessel == null || vessel.mainBody == null)
            {
                return false;
            }

            if (!string.Equals(vessel.mainBody.bodyName, "Kerbin", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (vessel.altitude > 3500.0)
            {
                return false;
            }

            double lat = vessel.latitude;
            double lon = NormalizeLongitudeSigned(vessel.longitude);

            return lat >= -0.28 && lat <= 0.18 && lon >= -74.95 && lon <= -74.20;
        }

        private static bool ContainsToken(string source, string[] tokens)
        {
            if (string.IsNullOrEmpty(source) || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token) && source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static double NormalizeLongitudeSigned(double lon)
        {
            while (lon > 180.0)
            {
                lon -= 360.0;
            }
            while (lon < -180.0)
            {
                lon += 360.0;
            }
            return lon;
        }

        private static string GetSurfaceNameChain(Collider collider, int depth)
        {
            if (collider == null)
            {
                return string.Empty;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder(128);
            Transform cursor = collider.transform;
            int remaining = Mathf.Max(1, depth);
            while (cursor != null && remaining > 0)
            {
                if (!string.IsNullOrEmpty(cursor.name))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append('/');
                    }
                    sb.Append(cursor.name.ToLowerInvariant());
                }

                cursor = cursor.parent;
                remaining--;
            }

            return sb.ToString();
        }

        private void UpdateSurfaceColor(Vessel vessel, Collider collider)
        {
            Color bodyColor = ImpactPuffsSurfaceColor.GetBaseDustColor(vessel);
            Color finalColor = bodyColor;

            Color colliderColor;
            if (ImpactPuffsSurfaceColor.TryGetColliderColor(collider, out colliderColor))
            {
                finalColor = ImpactPuffsSurfaceColor.BlendWithColliderColor(bodyColor, colliderColor);
            }

            Color toned = ImpactPuffsSurfaceColor.NormalizeDustTone(finalColor);
            Color softWhite = new Color(0.90f, 0.90f, 0.89f);
            currentColor = Color.Lerp(toned, softWhite, 0.18f);
        }

        private void ApplyCurrentStartColor()
        {
            if (particleSystem == null)
            {
                return;
            }

            float volumetricBoost = ImpactPuffsConfig.UseSimplifiedEffects ? 1.00f : 1.15f;
            float baseAlpha = BaseAlpha * 0.68f * volumetricBoost;
            float lightMin = ImpactPuffsConfig.UseSimplifiedEffects ? 0.18f : 0.26f;
            float lightMax = ImpactPuffsConfig.UseSimplifiedEffects ? 0.84f : 0.92f;
            float alpha = baseAlpha * Mathf.Lerp(lightMin, lightMax, Mathf.Clamp01(cachedLightFactor));
            alpha *= Mathf.Lerp(0.92f, 1.12f, Mathf.Clamp01((cachedBodyVisibility - 1f) / 0.75f));
            float alphaCap = 0.60f + (ImpactPuffsConfig.UseSimplifiedEffects ? 0.00f : 0.08f);
            alpha = Mathf.Clamp(alpha, 0f, alphaCap);

            ParticleSystem.MainModule main = particleSystem.main;
            main.startColor = new Color(currentColor.r, currentColor.g, currentColor.b, alpha);
        }

        private static float EvaluateEngineAwareLightFactor(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal, float normalizedThrust)
        {
            if (!ImpactPuffsConfig.UseLightAware)
            {
                return 1f;
            }

            float sunLight = EvaluateSimplifiedSunLighting(vessel, worldPoint, surfaceNormal);
            float thrust01 = Mathf.Clamp01(normalizedThrust);
            float engineGlow = Mathf.Lerp(0.01f, 0.11f, Mathf.Pow(thrust01, 0.80f));
            return Mathf.Clamp01(Mathf.Max(sunLight, engineGlow));
        }

        private static float EvaluateVolumetricLightFactor(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal, float normalizedThrust)
        {
            if (!ImpactPuffsConfig.UseLightAware)
            {
                return 1f;
            }

            float sunLight = EvaluateNonSimplifiedLighting(vessel, worldPoint, surfaceNormal, true);
            float thrust01 = Mathf.Clamp01(normalizedThrust);
            float engineGlow = Mathf.Lerp(0.0f, 0.020f, Mathf.Pow(thrust01, 2.20f));
            return Mathf.Clamp01(Mathf.Max(sunLight, engineGlow));
        }

        internal static float GetSunLightFactor(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal)
        {
            if (!ImpactPuffsConfig.UseLightAware)
            {
                return 1f;
            }

            return EvaluateSimplifiedSunLighting(vessel, worldPoint, surfaceNormal);
        }

        internal static float GetTouchdownLightFactor(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal)
        {
            if (!ImpactPuffsConfig.UseLightAware)
            {
                return 1f;
            }

            return EvaluateNonSimplifiedLighting(vessel, worldPoint, surfaceNormal, true);
        }

        private static float EvaluateNonSimplifiedLighting(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal, bool includeTerrainOcclusion)
        {
            if (surfaceNormal.sqrMagnitude < 0.0001f)
            {
                surfaceNormal = Vector3.up;
            }
            surfaceNormal.Normalize();

            Vector3 sunDirection;
            if (!TryGetSunDirection(worldPoint, out sunDirection))
            {
                return 0f;
            }

            float ndotl = Mathf.Clamp01(Vector3.Dot(surfaceNormal, sunDirection));
            if (ndotl <= 0f)
            {
                return 0f;
            }

            bool bodyOccluded = vessel != null
                && vessel.mainBody != null
                && IsSunOccluded(vessel.mainBody, worldPoint, sunDirection);
            bool terrainOccluded = includeTerrainOcclusion
                && !bodyOccluded
                && IsLocallySunOccluded(vessel, worldPoint, surfaceNormal, sunDirection);

            float litFactor = Mathf.Lerp(0.05f, 1f, Mathf.Pow(ndotl, 0.58f));
            if (bodyOccluded || terrainOccluded)
            {
                float shadowStrength = Mathf.Clamp01(ImpactPuffsRuntimeConfig.ShadowLightFactor);
                float shadowMul = terrainOccluded
                    ? Mathf.Lerp(0.05f, 0.12f, shadowStrength)
                    : Mathf.Lerp(0.06f, 0.14f, shadowStrength);
                if (terrainOccluded)
                {
                    shadowMul *= 0.92f;
                }
                litFactor *= shadowMul;
                float shadowCap = terrainOccluded ? 0.10f : 0.18f;
                litFactor = Mathf.Min(litFactor, shadowCap);
            }

            return Mathf.Clamp01(litFactor);
        }

        private static float EvaluateSimplifiedSunLighting(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal)
        {
            if (surfaceNormal.sqrMagnitude < 0.0001f)
            {
                surfaceNormal = Vector3.up;
            }
            surfaceNormal.Normalize();

            Vector3 sunDirection;
            if (!TryGetSunDirection(worldPoint, out sunDirection))
            {
                return 0f;
            }

            float ndotl = Mathf.Clamp01(Vector3.Dot(surfaceNormal, sunDirection));
            if (ndotl <= 0f)
            {
                return 0f;
            }

            float litFactor = Mathf.Lerp(0.05f, 1f, Mathf.Pow(ndotl, 0.58f));
            bool bodyOccluded = vessel != null
                && vessel.mainBody != null
                && IsSunOccluded(vessel.mainBody, worldPoint, sunDirection);
            if (!bodyOccluded)
            {
                return Mathf.Clamp01(litFactor);
            }

            float shadowStrength = Mathf.Clamp01(ImpactPuffsRuntimeConfig.ShadowLightFactor);
            float shadowMul = Mathf.Lerp(0.06f, 0.16f, shadowStrength);
            return Mathf.Clamp01(Mathf.Min(litFactor * shadowMul, 0.18f));
        }

        private static bool IsLocallySunOccluded(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal, Vector3 sunDirection)
        {
            if (vessel == null || vessel.packed || !vessel.loaded || sunDirection.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Guid vesselId = vessel.id;
            float now = Time.time;
            SunOcclusionCacheEntry cacheEntry;
            if (SunOcclusionCache.TryGetValue(vesselId, out cacheEntry))
            {
                float pointDeltaSq = (cacheEntry.SamplePoint - worldPoint).sqrMagnitude;
                float directionDot = Vector3.Dot(cacheEntry.SunDirection, sunDirection);
                if (now <= cacheEntry.ValidUntil && pointDeltaSq <= 9f && directionDot >= 0.995f)
                {
                    return cacheEntry.Occluded;
                }
            }

            bool occluded = ComputeLocalSunOcclusion(vessel, worldPoint, surfaceNormal, sunDirection);
            SunOcclusionCacheEntry updatedEntry = new SunOcclusionCacheEntry
            {
                Occluded = occluded,
                ValidUntil = now + (ImpactPuffsConfig.UseSimplifiedEffects ? 0.30f : 0.14f),
                SamplePoint = worldPoint,
                SunDirection = sunDirection
            };
            SunOcclusionCache[vesselId] = updatedEntry;
            return occluded;
        }

        private static bool ComputeLocalSunOcclusion(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal, Vector3 sunDirection)
        {
            Vector3 normal = surfaceNormal;
            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = Vector3.up;
            }
            normal.Normalize();

            Vector3 direction = sunDirection.normalized;
            Vector3 origin = worldPoint + normal * 0.20f + direction * 0.05f;
            float maxDistance = 1200f;

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                SunOcclusionHits,
                maxDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );
            if (hitCount <= 0)
            {
                return false;
            }

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = SunOcclusionHits[i];
                if (hit.collider == null || hit.distance <= 0.03f)
                {
                    continue;
                }

                Part hitPart = hit.collider.GetComponentInParent<Part>();
                if (hitPart != null && hitPart.vessel == vessel)
                {
                    continue;
                }

                if (vessel.rootPart != null && hit.collider.transform != null && hit.collider.transform.IsChildOf(vessel.rootPart.transform))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool TryGetSunDirection(Vector3 worldPoint, out Vector3 sunDirection)
        {
            sunDirection = Vector3.up;

            CelestialBody sunBody = Planetarium.fetch != null ? Planetarium.fetch.Sun : null;
            if (sunBody == null)
            {
                return false;
            }

            Vector3 toSun = sunBody.position - worldPoint;
            if (toSun.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            sunDirection = toSun.normalized;
            return true;
        }

        private static bool IsSunOccluded(CelestialBody body, Vector3 worldPoint, Vector3 sunDirection)
        {
            if (body == null || sunDirection.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Vector3 toCenter = body.position - worldPoint;
            float projection = Vector3.Dot(toCenter, sunDirection.normalized);
            if (projection <= 0f)
            {
                return false;
            }

            double radius = body.Radius;
            double perpendicularSq = toCenter.sqrMagnitude - projection * projection;
            return perpendicularSq <= radius * radius;
        }
    }



}


