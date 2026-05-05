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
        public const string UiQualityScale = "#LOC_KerbalFX_ImpactPuffs_UI_QualityScale";
        public const string UiQualityScaleTip = "#LOC_KerbalFX_ImpactPuffs_UI_QualityScale_TT";
        public const string UiSurfaceTint = "#LOC_KerbalFX_ImpactPuffs_UI_SurfaceTint";
        public const string UiSurfaceTintTip = "#LOC_KerbalFX_ImpactPuffs_UI_SurfaceTint_TT";
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
        public const string LogSurfaceSample = "#LOC_KerbalFX_ImpactPuffs_Log_SurfaceSample";
        public const string LogTouchdownSample = "#LOC_KerbalFX_ImpactPuffs_Log_TouchdownSample";
        public const string LogLightSample = "#LOC_KerbalFX_ImpactPuffs_Log_LightSample";
        public const string LogTouchdownLightSample = "#LOC_KerbalFX_ImpactPuffs_Log_TouchdownLightSample";
    }

    public class ImpactPuffsParameters : GameParameters.CustomParameterNode
    {
        [GameParameters.CustomParameterUI(ImpactPuffsLoc.UiEnable, toolTip = ImpactPuffsLoc.UiEnableTip)]
        public bool enableImpactPuffs = true;

        [GameParameters.CustomIntParameterUI(
            ImpactPuffsLoc.UiQualityScale,
            toolTip = ImpactPuffsLoc.UiQualityScaleTip,
            minValue = 25,
            maxValue = 200,
            stepSize = 25,
            displayFormat = "N0"
        )]
        public int qualityScale = 100;

        [GameParameters.CustomParameterUI(ImpactPuffsLoc.UiSurfaceTint, toolTip = ImpactPuffsLoc.UiSurfaceTintTip)]
        public bool adaptSurfaceColor = true;

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
        public static bool DebugLogging;
        public static bool AdaptSurfaceColor = true;
        public static bool UseLightAware = true;
        public static int QualityPercent = 100;
        public static int Revision;

        private static bool initialized;

        public static void Refresh()
        {
            bool newEnabled = true;
            bool newAdaptSurfaceColor = true;
            bool newUseLightAware = true;
            bool newDebug = false;
            int newQualityPercent = 100;

            if (HighLogic.CurrentGame != null && HighLogic.CurrentGame.Parameters != null)
            {
                ImpactPuffsParameters p = HighLogic.CurrentGame.Parameters.CustomParams<ImpactPuffsParameters>();
                if (p != null)
                {
                    newEnabled = p.enableImpactPuffs;
                    newAdaptSurfaceColor = p.adaptSurfaceColor;
                    newUseLightAware = p.useLightAware;
                    newDebug = p.debugLogging;
                    newQualityPercent = Mathf.Clamp(p.qualityScale, 25, 200);
                }
            }
            bool changed = !initialized
                || newEnabled != Enabled
                || newAdaptSurfaceColor != AdaptSurfaceColor
                || newUseLightAware != UseLightAware
                || newDebug != DebugLogging
                || newQualityPercent != QualityPercent;

            Enabled = newEnabled;
            AdaptSurfaceColor = newAdaptSurfaceColor;
            UseLightAware = newUseLightAware;
            DebugLogging = newDebug;
            QualityPercent = newQualityPercent;

            if (changed)
            {
                initialized = true;
                Revision++;
                ImpactPuffsLog.Info(Localizer.Format(
                    ImpactPuffsLoc.LogSettingsUpdated,
                    Enabled,
                    QualityPercent,
                    AdaptSurfaceColor,
                    UseLightAware,
                    DebugLogging
                ));
            }
        }

        public static float GetQualityScaleMultiplier()
        {
            int quality = Mathf.Clamp(QualityPercent, 25, 200);
            if (quality <= 100)
            {
                return quality / 100f;
            }

            return Mathf.Lerp(1f, 1.5f, (quality - 100f) / 100f);
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
        public const float LateralSpreadMultiplier = 2.40f;
        public const float VerticalLiftMultiplier = 0.88f;
        public const float TurbulenceMultiplier = 1.45f;
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

        private static readonly KerbalFxBodyVisibilityProfile bodyProfile =
            new KerbalFxBodyVisibilityProfile("KERBALFX_IMPACT_PUFFS", GetConfigPath, SeedDefaultBodyVisibility, 0.40f, 3.00f);

        private static readonly KerbalFxSurfaceTintProfile tintProfile =
            new KerbalFxSurfaceTintProfile("KERBALFX_IMPACT_PUFFS", GetConfigPath, SeedDefaultBodyTints);

        private static readonly KerbalFxSurfaceTintProfile touchdownTintProfile =
            new KerbalFxSurfaceTintProfile("KERBALFX_IMPACT_PUFFS", GetConfigPath, SeedDefaultTouchdownBodyTints);

        private static readonly KerbalFxLightAwareProfile lightAwareProfile =
            new KerbalFxLightAwareProfile(
                "KERBALFX_IMPACT_PUFFS",
                GetConfigPath,
                SeedDefaultLightAware,
                new KerbalFxLightAwareEntry
                {
                    DarkScale = 0.10f,
                    BrightScale = 1f,
                    TwilightFloor = 0.22f,
                    MinPerceived = 0.06f,
                    ColorTintStrength = 0.32f
                });

        public static KerbalFxSurfaceTintProfile TintProfile { get { return tintProfile; } }

        public static KerbalFxSurfaceTintProfile TouchdownTintProfile { get { return touchdownTintProfile; } }

        public static KerbalFxLightAwareProfile LightAwareProfile { get { return lightAwareProfile; } }

        public static void Refresh()
        {
            bodyProfile.Refresh();
            tintProfile.Refresh();
            touchdownTintProfile.Refresh();
            lightAwareProfile.Refresh();
            Revision++;
            ImpactPuffsLog.Info(Localizer.Format(
                ImpactPuffsLoc.LogConfig,
                "GameDatabase",
                "BodyVisibilityEntries=" + bodyProfile.Count.ToString(CultureInfo.InvariantCulture)
                + " BodyTintEntries=" + tintProfile.Count.ToString(CultureInfo.InvariantCulture)
                + " TouchdownTintEntries=" + touchdownTintProfile.Count.ToString(CultureInfo.InvariantCulture)
                + " LightAwareEntries=" + lightAwareProfile.Count.ToString(CultureInfo.InvariantCulture)));
        }

        public static void TryHotReloadFromDisk()
        {
            string visibilityFailure;
            string tintFailure;
            string touchdownTintFailure;
            string lightAwareFailure;
            bool visibilityChanged = bodyProfile.TryHotReloadFromDisk(out visibilityFailure);
            bool tintChanged = tintProfile.TryHotReloadFromDisk(out tintFailure);
            bool touchdownTintChanged = touchdownTintProfile.TryHotReloadFromDisk(out touchdownTintFailure);
            bool lightAwareChanged = lightAwareProfile.TryHotReloadFromDisk(out lightAwareFailure);
            if (!visibilityChanged && !tintChanged && !touchdownTintChanged && !lightAwareChanged)
                return;

            if (visibilityFailure != null)
                ImpactPuffsLog.Info(Localizer.Format(ImpactPuffsLoc.LogHotReloadFailed, visibilityFailure));
            if (tintFailure != null)
                ImpactPuffsLog.Info(Localizer.Format(ImpactPuffsLoc.LogHotReloadFailed, tintFailure));
            if (touchdownTintFailure != null)
                ImpactPuffsLog.Info(Localizer.Format(ImpactPuffsLoc.LogHotReloadFailed, touchdownTintFailure));
            if (lightAwareFailure != null)
                ImpactPuffsLog.Info(Localizer.Format(ImpactPuffsLoc.LogHotReloadFailed, lightAwareFailure));

            Revision++;
            ImpactPuffsLog.Info(Localizer.Format(
                ImpactPuffsLoc.LogConfig,
                "HotReload",
                "BodyVisibilityEntries=" + bodyProfile.Count.ToString(CultureInfo.InvariantCulture)
                + " BodyTintEntries=" + tintProfile.Count.ToString(CultureInfo.InvariantCulture)
                + " TouchdownTintEntries=" + touchdownTintProfile.Count.ToString(CultureInfo.InvariantCulture)
                + " LightAwareEntries=" + lightAwareProfile.Count.ToString(CultureInfo.InvariantCulture)));
        }

        public static float GetBodyVisibilityMultiplier(string bodyName)
        {
            return bodyProfile.Get(bodyName);
        }

        private static string GetConfigPath()
        {
            return Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "KerbalFX", "ImpactPuffs", "KerbalFX_ImpactPuffs.cfg");
        }

        private static void SeedDefaultBodyVisibility(Dictionary<string, float> dict)
        {
            dict["Kerbin"] = 1.00f;
            dict["Mun"] = 1.22f;
            dict["Minmus"] = 1.18f;
            dict["Duna"] = 1.00f;
            dict["Ike"] = 1.06f;
            dict["Eve"] = 1.08f;
            dict["Moho"] = 1.10f;
            dict["Dres"] = 1.06f;
            dict["Vall"] = 1.06f;
            dict["Tylo"] = 1.00f;
            dict["Bop"] = 1.08f;
            dict["Pol"] = 1.08f;
            dict["Eeloo"] = 1.12f;
        }

        private static void SeedDefaultBodyTints(Dictionary<string, KerbalFxBodyTintEntry> dict)
        {
            SeedCommonBodyTints(dict);
            dict["Gilly"] = KerbalFxBodyTintEntry.FromColor(new Color(0.54f, 0.48f, 0.40f), 1.45f);
            dict["Dres"] = KerbalFxBodyTintEntry.FromColor(new Color(0.62f, 0.58f, 0.52f), 0.85f);
            dict["Vall"] = KerbalFxBodyTintEntry.FromColor(new Color(0.66f, 0.70f, 0.74f), 1.15f);
        }

        private static void SeedCommonBodyTints(Dictionary<string, KerbalFxBodyTintEntry> dict)
        {
            dict["Kerbin"] = KerbalFxBodyTintEntry.FromColor(new Color(0.62f, 0.55f, 0.42f));
            dict["Mun"] = KerbalFxBodyTintEntry.FromColor(new Color(0.62f, 0.62f, 0.60f));
            dict["Minmus"] = KerbalFxBodyTintEntry.FromColor(new Color(0.74f, 0.86f, 0.78f));
            dict["Duna"] = KerbalFxBodyTintEntry.FromColor(new Color(0.78f, 0.46f, 0.32f), 0.90f);
            dict["Ike"] = KerbalFxBodyTintEntry.FromColor(new Color(0.55f, 0.52f, 0.50f));
            dict["Eve"] = KerbalFxBodyTintEntry.FromColor(new Color(0.58f, 0.42f, 0.62f));
            dict["Laythe"] = KerbalFxBodyTintEntry.FromColor(new Color(0.52f, 0.60f, 0.56f), 1.30f);
            dict["Moho"] = KerbalFxBodyTintEntry.FromColor(new Color(0.58f, 0.40f, 0.30f));
            dict["Tylo"] = KerbalFxBodyTintEntry.FromColor(new Color(0.74f, 0.70f, 0.62f));
            dict["Bop"] = KerbalFxBodyTintEntry.FromColor(new Color(0.50f, 0.46f, 0.40f), 1.25f);
            dict["Pol"] = KerbalFxBodyTintEntry.FromColor(new Color(0.78f, 0.72f, 0.50f));
            dict["Eeloo"] = KerbalFxBodyTintEntry.FromColor(new Color(0.55f, 0.62f, 0.59f), 0.45f);
        }

        private static void SeedDefaultLightAware(Dictionary<string, KerbalFxLightAwareEntry> dict)
        {
            dict["Kerbin"] = new KerbalFxLightAwareEntry { DarkScale = 0.16f, BrightScale = 1f, TwilightFloor = 0.34f, MinPerceived = 0.08f, ColorTintStrength = 0.30f };
            dict["Laythe"] = new KerbalFxLightAwareEntry { DarkScale = 0.16f, BrightScale = 1f, TwilightFloor = 0.34f, MinPerceived = 0.08f, ColorTintStrength = 0.34f };
            dict["Eve"] = new KerbalFxLightAwareEntry { DarkScale = 0.22f, BrightScale = 1f, TwilightFloor = 0.42f, MinPerceived = 0.10f, ColorTintStrength = 0.40f };
            dict["Duna"] = new KerbalFxLightAwareEntry { DarkScale = 0.14f, BrightScale = 1f, TwilightFloor = 0.30f, MinPerceived = 0.06f, ColorTintStrength = 0.36f };
            dict["Jool"] = new KerbalFxLightAwareEntry { DarkScale = 0.16f, BrightScale = 1f, TwilightFloor = 0.32f, MinPerceived = 0.07f, ColorTintStrength = 0.35f };
            dict["Mun"] = new KerbalFxLightAwareEntry { DarkScale = 0.06f, BrightScale = 1f, TwilightFloor = 0.06f, MinPerceived = 0.04f, ColorTintStrength = 0.28f };
            dict["Minmus"] = new KerbalFxLightAwareEntry { DarkScale = 0.06f, BrightScale = 1f, TwilightFloor = 0.06f, MinPerceived = 0.04f, ColorTintStrength = 0.28f };
            dict["Ike"] = new KerbalFxLightAwareEntry { DarkScale = 0.07f, BrightScale = 1f, TwilightFloor = 0.07f, MinPerceived = 0.04f, ColorTintStrength = 0.28f };
            dict["Gilly"] = new KerbalFxLightAwareEntry { DarkScale = 0.06f, BrightScale = 1f, TwilightFloor = 0.06f, MinPerceived = 0.04f, ColorTintStrength = 0.26f };
            dict["Vall"] = new KerbalFxLightAwareEntry { DarkScale = 0.06f, BrightScale = 1f, TwilightFloor = 0.06f, MinPerceived = 0.04f, ColorTintStrength = 0.26f };
            dict["Tylo"] = new KerbalFxLightAwareEntry { DarkScale = 0.07f, BrightScale = 1f, TwilightFloor = 0.07f, MinPerceived = 0.04f, ColorTintStrength = 0.26f };
            dict["Bop"] = new KerbalFxLightAwareEntry { DarkScale = 0.06f, BrightScale = 1f, TwilightFloor = 0.06f, MinPerceived = 0.04f, ColorTintStrength = 0.26f };
            dict["Pol"] = new KerbalFxLightAwareEntry { DarkScale = 0.06f, BrightScale = 1f, TwilightFloor = 0.06f, MinPerceived = 0.04f, ColorTintStrength = 0.28f };
            dict["Dres"] = new KerbalFxLightAwareEntry { DarkScale = 0.06f, BrightScale = 1f, TwilightFloor = 0.06f, MinPerceived = 0.04f, ColorTintStrength = 0.26f };
            dict["Eeloo"] = new KerbalFxLightAwareEntry { DarkScale = 0.06f, BrightScale = 1f, TwilightFloor = 0.06f, MinPerceived = 0.04f, ColorTintStrength = 0.28f };
            dict["Moho"] = new KerbalFxLightAwareEntry { DarkScale = 0.07f, BrightScale = 1f, TwilightFloor = 0.07f, MinPerceived = 0.04f, ColorTintStrength = 0.30f };
        }

        private static void SeedDefaultTouchdownBodyTints(Dictionary<string, KerbalFxBodyTintEntry> dict)
        {
            SeedCommonBodyTints(dict);
            dict["Gilly"] = KerbalFxBodyTintEntry.FromColor(new Color(0.54f, 0.50f, 0.44f), 0.65f);
            dict["Dres"] = KerbalFxBodyTintEntry.FromColor(new Color(0.62f, 0.58f, 0.52f), 0.45f);
            dict["Vall"] = KerbalFxBodyTintEntry.FromColor(new Color(0.66f, 0.70f, 0.74f), 1.08f);
        }
    }

    internal static class ImpactPuffsLog
    {
        private static readonly KerbalFxLog impl = new KerbalFxLog(() => ImpactPuffsConfig.DebugLogging);
        public static void Info(string message) { impl.Info(message); }
        public static void DebugLog(string message) { impl.DebugLog(message); }
    }
}
