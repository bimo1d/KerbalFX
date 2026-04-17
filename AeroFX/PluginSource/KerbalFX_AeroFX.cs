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
        public const string UiSectionExtras = "#LOC_KerbalFX_UI_SectionExtras";
        public const string UiTitle = "#LOC_KerbalFX_AeroFX_UI_Title";
        public const string UiEnable = "#LOC_KerbalFX_AeroFX_UI_Enable";
        public const string UiEnableTip = "#LOC_KerbalFX_AeroFX_UI_Enable_TT";
        public const string UiRibbonCap = "#LOC_KerbalFX_AeroFX_UI_RibbonCap";
        public const string UiRibbonCapTip = "#LOC_KerbalFX_AeroFX_UI_RibbonCap_TT";
        public const string UiLightAware = "#LOC_KerbalFX_AeroFX_UI_LightAware";
        public const string UiLightAwareTip = "#LOC_KerbalFX_AeroFX_UI_LightAware_TT";
        public const string UiManeuverOnly = "#LOC_KerbalFX_AeroFX_UI_ManeuverOnly";
        public const string UiManeuverOnlyTip = "#LOC_KerbalFX_AeroFX_UI_ManeuverOnly_TT";
        public const string UiDebug = "#LOC_KerbalFX_AeroFX_UI_Debug";
        public const string UiDebugTip = "#LOC_KerbalFX_AeroFX_UI_Debug_TT";

        public const string LogSettingsUpdated = "#LOC_KerbalFX_AeroFX_Log_SettingsUpdated";
        public const string LogBootstrapStart = "#LOC_KerbalFX_AeroFX_Log_BootstrapStart";
        public const string LogBootstrapStop = "#LOC_KerbalFX_AeroFX_Log_BootstrapStop";
        public const string LogHeartbeat = "#LOC_KerbalFX_AeroFX_Log_Heartbeat";
        public const string LogAttached = "#LOC_KerbalFX_AeroFX_Log_Attached";
        public const string LogVesselScan = "#LOC_KerbalFX_AeroFX_Log_VesselScan";
        public const string LogAnchorScan = "#LOC_KerbalFX_AeroFX_Log_AnchorScan";
        public const string LogAnchorCandidates = "#LOC_KerbalFX_AeroFX_Log_AnchorCandidates";
        public const string LogEmitter = "#LOC_KerbalFX_AeroFX_Log_Emitter";
        public const string LogConfig = "#LOC_KerbalFX_AeroFX_Log_Config";
        public const string LogHotReloadFailed = "#LOC_KerbalFX_AeroFX_Log_HotReloadFailed";
        public const string LogCenterResult = "#LOC_KerbalFX_AeroFX_Log_CenterResult";
        public const string LogCenterNone = "#LOC_KerbalFX_AeroFX_Log_CenterNone";
        public const string LogSecondaryResult = "#LOC_KerbalFX_AeroFX_Log_SecondaryResult";
        public const string LogSecondaryNone = "#LOC_KerbalFX_AeroFX_Log_SecondaryNone";
        public const string LogTailScan = "#LOC_KerbalFX_AeroFX_Log_TailScan";
    }

    public class AeroFxParameters : GameParameters.CustomParameterNode
    {
        [GameParameters.CustomParameterUI(AeroFxLoc.UiEnable, toolTip = AeroFxLoc.UiEnableTip)]
        public bool enableAeroFx = true;

        [GameParameters.CustomIntParameterUI(
            AeroFxLoc.UiRibbonCap,
            toolTip = AeroFxLoc.UiRibbonCapTip,
            minValue = 2,
            maxValue = 6,
            stepSize = 1,
            displayFormat = "N0"
        )]
        public int maxRibbonCount = 4;

        [GameParameters.CustomParameterUI(AeroFxLoc.UiLightAware, toolTip = AeroFxLoc.UiLightAwareTip)]
        public bool useLightAware = true;

        [GameParameters.CustomParameterUI(AeroFxLoc.UiManeuverOnly, toolTip = AeroFxLoc.UiManeuverOnlyTip)]
        public bool maneuverOnly = false;

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
        public static int MaxRibbonCount = 4;
        public static bool UseLightAware = true;
        public static bool UseManeuverOnly;
        public static bool DebugLogging;
        public static int Revision;

        private static bool initialized;

        public static void Refresh()
        {
            bool newEnabled = true;
            int newMaxRibbonCount = 4;
            bool newUseLightAware = true;
            bool newUseManeuverOnly = false;
            bool newDebug = false;

            if (HighLogic.CurrentGame != null && HighLogic.CurrentGame.Parameters != null)
            {
                AeroFxParameters p = HighLogic.CurrentGame.Parameters.CustomParams<AeroFxParameters>();
                if (p != null)
                {
                    newEnabled = p.enableAeroFx;
                    newMaxRibbonCount = Mathf.Clamp(p.maxRibbonCount, 2, 6);
                    newUseLightAware = p.useLightAware;
                    newUseManeuverOnly = p.maneuverOnly;
                    newDebug = p.debugLogging;
                }
            }

            bool changed = !initialized
                || newEnabled != Enabled
                || newMaxRibbonCount != MaxRibbonCount
                || newUseLightAware != UseLightAware
                || newUseManeuverOnly != UseManeuverOnly
                || newDebug != DebugLogging;

            Enabled = newEnabled;
            MaxRibbonCount = newMaxRibbonCount;
            UseLightAware = newUseLightAware;
            UseManeuverOnly = newUseManeuverOnly;
            DebugLogging = newDebug;

            if (changed)
            {
                initialized = true;
                Revision++;
                AeroFxLog.Info(Localizer.Format(
                    AeroFxLoc.LogSettingsUpdated,
                    Enabled,
                    MaxRibbonCount,
                    UseLightAware,
                    UseManeuverOnly,
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
        public const float FadeOutSpeed = 0.55f;
        public const float LightDaylightFloor = 0.82f;
        public const float LightShadowFloor = 0.08f;
        public const float AnchorRefreshInterval = 4.0f;

        private static readonly KerbalFxBodyVisibilityProfile bodyProfile =
            new KerbalFxBodyVisibilityProfile("KERBALFX_AERO_FX", GetConfigPath, SeedDefaultBodyVisibility, 0.20f, 2.50f);

        public static void Refresh()
        {
            bodyProfile.Refresh();
            Revision++;
            AeroFxLog.Info(Localizer.Format(
                AeroFxLoc.LogConfig,
                "GameDatabase",
                "BodyVisibility=" + bodyProfile.Count.ToString(CultureInfo.InvariantCulture)));
        }

        public static void TryHotReloadFromDisk()
        {
            string failure;
            if (!bodyProfile.TryHotReloadFromDisk(out failure))
                return;

            if (failure != null)
                AeroFxLog.Info(Localizer.Format(AeroFxLoc.LogHotReloadFailed, failure));

            Revision++;
            AeroFxLog.Info(Localizer.Format(
                AeroFxLoc.LogConfig,
                "HotReload",
                "BodyVisibility=" + bodyProfile.Count.ToString(CultureInfo.InvariantCulture)));
        }

        public static float GetBodyVisibilityMultiplier(string bodyName)
        {
            return bodyProfile.Get(bodyName);
        }

        private static void SeedDefaultBodyVisibility(Dictionary<string, float> dict)
        {
            dict["Kerbin"] = 1.00f;
            dict["Laythe"] = 1.08f;
            dict["Eve"] = 1.16f;
            dict["Jool"] = 0.95f;
            dict["Duna"] = 0.42f;
        }

        private static string GetConfigPath()
        {
            return Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "KerbalFX", "AeroFX", "KerbalFX_AeroFX.cfg");
        }
    }

    internal static class AeroFxLog
    {
        private static readonly KerbalFxLog impl = new KerbalFxLog(() => AeroFxConfig.DebugLogging);
        public static void Info(string message) { impl.Info(message); }
        public static void DebugLog(string message) { impl.DebugLog(message); }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    internal class AeroFxBootstrap : KerbalFxVesselControllerBootstrap<VesselAeroController>
    {
        protected override bool IsModuleEnabled { get { return AeroFxConfig.Enabled; } }
        protected override bool IsDebugLogging { get { return AeroFxConfig.DebugLogging; } }

        protected override void RefreshSettings()
        {
            AeroFxConfig.Refresh();
            AeroFxRuntimeConfig.TryHotReloadFromDisk();
        }

        protected override void OnBeforeStart()
        {
            AeroFxRuntimeConfig.Refresh();
        }

        protected override bool IsSupportedVessel(Vessel vessel)
        {
            return KerbalFxVesselUtil.IsSupportedFlightVessel(vessel)
                && vessel.mainBody != null
                && vessel.mainBody.atmosphere;
        }

        protected override VesselAeroController CreateController(Vessel vessel)
        {
            return new VesselAeroController(vessel);
        }

        protected override bool ControllerHasEmitters(VesselAeroController controller)
        {
            return controller.HasAnyEmitters;
        }

        protected override int ControllerEmitterCount(VesselAeroController controller)
        {
            return controller.EmitterCount;
        }

        protected override void TryRebuildController(VesselAeroController controller, float refreshElapsed)
        {
            controller.TryRebuild(refreshElapsed);
        }

        protected override void LogBootstrapStart()
        {
            AeroFxLog.Info(Localizer.Format(AeroFxLoc.LogBootstrapStart));
        }

        protected override void LogBootstrapStop()
        {
            AeroFxLog.Info(Localizer.Format(AeroFxLoc.LogBootstrapStop));
        }

        protected override void LogHeartbeat(int controllerCount)
        {
            AeroFxLog.DebugLog(Localizer.Format(AeroFxLoc.LogHeartbeat, controllerCount));
        }

        protected override void LogAttached(int emitterCount, string vesselName)
        {
            AeroFxLog.DebugLog(Localizer.Format(AeroFxLoc.LogAttached, emitterCount, vesselName));
        }
    }
}
