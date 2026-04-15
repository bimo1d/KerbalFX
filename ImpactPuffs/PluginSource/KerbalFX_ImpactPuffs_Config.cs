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
        public const string UiSectionMain = "#LOC_KerbalFX_UI_SectionMain";
        public const string UiTitle = "#LOC_KerbalFX_ImpactPuffs_UI_Title";

        public const string UiEnable = "#LOC_KerbalFX_ImpactPuffs_UI_Enable";
        public const string UiEnableTip = "#LOC_KerbalFX_ImpactPuffs_UI_Enable_TT";
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
    }

    public class ImpactPuffsParameters : GameParameters.CustomParameterNode
    {
        [GameParameters.CustomParameterUI(ImpactPuffsLoc.UiEnable, toolTip = ImpactPuffsLoc.UiEnableTip)]
        public bool enableImpactPuffs = true;

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
            get { return "KerbalFX_01_Main"; }
        }

        public override string DisplaySection
        {
            get { return Localizer.Format(ImpactPuffsLoc.UiSectionMain); }
        }

        public override int SectionOrder
        {
            get { return 10; }
        }

        public override bool HasPresets
        {
            get { return false; }
        }
    }

    internal static class ImpactPuffsConfig
    {
        public static bool Enabled = true;
        public static bool UseLightAware = true;
        public static bool DebugLogging;
        public static int Revision;

        private static bool initialized;

        public static void Refresh()
        {
            bool newEnabled = true;
            bool newUseLightAware = true;
            bool newDebug = false;

            if (HighLogic.CurrentGame != null && HighLogic.CurrentGame.Parameters != null)
            {
                ImpactPuffsParameters p = HighLogic.CurrentGame.Parameters.CustomParams<ImpactPuffsParameters>();
                if (p != null)
                {
                    newEnabled = p.enableImpactPuffs;
                    newUseLightAware = p.useLightAware;
                    newDebug = p.debugLogging;
                }
            }

            bool changed = !initialized
                || newEnabled != Enabled
                || newUseLightAware != UseLightAware
                || newDebug != DebugLogging;

            Enabled = newEnabled;
            UseLightAware = newUseLightAware;
            DebugLogging = newDebug;

            if (changed)
            {
                initialized = true;
                Revision++;
                ImpactPuffsLog.Info(Localizer.Format(
                    ImpactPuffsLoc.LogSettingsUpdated,
                    Enabled,
                    UseLightAware,
                    DebugLogging
                ));
            }
        }
    }

    internal static class ImpactPuffsRuntimeConfig
    {
        public static int Revision;

        public const float EmissionMultiplier = 1.95f;
        public const float MaxParticlesMultiplier = 2.35f;
        public const float RadiusScaleMultiplier = 1.72f;
        public const float SharedEmissionMultiplier = 2.30f;
        public const float SharedMaxParticlesMultiplier = 1.45f;
        public const float SharedRadiusScaleMultiplier = 1.22f;
        public const float MaxRayDistance = 42f;
        public const float MinNormalizedThrust = 0.005f;
        public const float ShadowLightFactor = 0.28f;
        public const float LateralSpreadMultiplier = 2.40f;
        public const float VerticalLiftMultiplier = 0.88f;
        public const float TurbulenceMultiplier = 1.45f;
        public const float RingExpansionMultiplier = 1.55f;
        public const float DynamicSwayMultiplier = 1.65f;
        public const float EngineCountExponent = 0.72f;
        public const float EngineCountMinScale = 0.22f;
        public const float MinExhaustToGroundAlignment = 0.18f;
        public const float MinRayDirectionToBodyDown = 0.05f;
        public const float MinExhaustToBodyDown = 0.24f;
        public const float ThrustPowerReference = 180f;
        public const float ThrustPowerExponent = 0.60f;
        public const float ThrustPowerMinScale = 0.35f;
        public const float ThrustPowerMaxScale = 3.80f;
        public const float MaxDistanceAtLowThrust = 10.0f;
        public const float MaxDistanceAtHighThrust = 28.0f;
        public const float TouchdownMinSpeed = 2.2f;
        public const float TouchdownBurstMultiplier = 3.00f;

        private static readonly Dictionary<string, float> BodyVisibilityMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static DateTime lastConfigWriteUtc = DateTime.MinValue;

        public static void Refresh()
        {
            SeedDefaultBodyVisibility();

            if (GameDatabase.Instance != null)
            {
                ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("KERBALFX_IMPACT_PUFFS");
                if (nodes != null)
                {
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        if (nodes[i] != null)
                        {
                            KerbalFxUtil.LoadBodyVisibility(nodes[i], BodyVisibilityMultipliers, 0.40f, 3.00f);
                        }
                    }
                }
            }

            KerbalFxUtil.PrimeConfigFileStamp(GetConfigPath(), ref lastConfigWriteUtc);
            Revision++;
            ImpactPuffsLog.Info(Localizer.Format(
                ImpactPuffsLoc.LogConfig,
                "GameDatabase",
                "BodyVisibilityEntries=" + BodyVisibilityMultipliers.Count.ToString(CultureInfo.InvariantCulture)));
        }

        public static void TryHotReloadFromDisk()
        {
            if (!KerbalFxUtil.HasConfigFileChanged(GetConfigPath(), ref lastConfigWriteUtc))
            {
                return;
            }

            SeedDefaultBodyVisibility();

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
                                    KerbalFxUtil.LoadBodyVisibility(nodes[i], BodyVisibilityMultipliers, 0.40f, 3.00f);
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

            Revision++;
            ImpactPuffsLog.Info(Localizer.Format(
                ImpactPuffsLoc.LogConfig,
                "HotReload",
                "BodyVisibilityEntries=" + BodyVisibilityMultipliers.Count.ToString(CultureInfo.InvariantCulture)));
        }

        public static float GetBodyVisibilityMultiplier(string bodyName)
        {
            return KerbalFxVesselUtil.GetBodyVisibilityMultiplier(bodyName, BodyVisibilityMultipliers, 0.40f, 3.00f);
        }

        private static string GetConfigPath()
        {
            return Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "KerbalFX", "ImpactPuffs", "KerbalFX_ImpactPuffs.cfg");
        }

        private static void SeedDefaultBodyVisibility()
        {
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
}
