using System;
using System.IO;
using KSP.Localization;
using UnityEngine;

namespace KerbalFX.BlastFX
{
    internal static class BlastFxLoc
    {
        public const string UiSectionExtras = "#LOC_KerbalFX_UI_SectionExtras";
        public const string UiTitle = "#LOC_KerbalFX_BlastFX_UI_Title";
        public const string UiEnable = "#LOC_KerbalFX_BlastFX_UI_Enable";
        public const string UiEnableTip = "#LOC_KerbalFX_BlastFX_UI_Enable_TT";
        public const string UiDebug = "#LOC_KerbalFX_BlastFX_UI_Debug";
        public const string UiDebugTip = "#LOC_KerbalFX_BlastFX_UI_Debug_TT";
        public const string LogSettings = "#LOC_KerbalFX_BlastFX_Log_SettingsUpdated";
        public const string LogHotReloadFailed = "#LOC_KerbalFX_BlastFX_Log_HotReloadFailed";
        public const string LogSkipDespawnPreserveShroud = "#LOC_KerbalFX_BlastFX_Log_SkipDespawnPreserveShroud";
        public const string LogTriggerVia = "#LOC_KerbalFX_BlastFX_Log_TriggerVia";
        public const string LogTriggerViaSnapshot = "#LOC_KerbalFX_BlastFX_Log_TriggerViaSnapshot";
        public const string LogTriggerViaScan = "#LOC_KerbalFX_BlastFX_Log_TriggerViaScan";
        public const string LogTriggerProbe = "#LOC_KerbalFX_BlastFX_Log_TriggerProbe";
        public const string LogPyroRing = "#LOC_KerbalFX_BlastFX_Log_PyroRing";
        public const string LogSoftPuff = "#LOC_KerbalFX_BlastFX_Log_SoftPuff";
        public const string LogUndockGas = "#LOC_KerbalFX_BlastFX_Log_UndockGas";
        public const string LogVacuumDebris = "#LOC_KerbalFX_BlastFX_Log_VacuumDebris";
        public const string LogPoolPrewarm = "#LOC_KerbalFX_BlastFX_Log_PoolPrewarm";
        public const string LogPoolGrow = "#LOC_KerbalFX_BlastFX_Log_PoolGrow";
        public const string LogHarmonyPatchFailed = "#LOC_KerbalFX_BlastFX_Log_HarmonyPatchFailed";
        public const string LogVacuumAssetBundleFailed = "#LOC_KerbalFX_BlastFX_Log_VacuumAssetBundleFailed";
        public const string LogVacuumPrefabMissing = "#LOC_KerbalFX_BlastFX_Log_VacuumPrefabMissing";
        public const string LogVacuumPrefabLoaded = "#LOC_KerbalFX_BlastFX_Log_VacuumPrefabLoaded";
        public const string LogVacuumPreload = "#LOC_KerbalFX_BlastFX_Log_VacuumPreload";
        public const string LogVacuumPrewarmSlots = "#LOC_KerbalFX_BlastFX_Log_VacuumPrewarmSlots";
        public const string LogVacuumWarmupComplete = "#LOC_KerbalFX_BlastFX_Log_VacuumWarmupComplete";
        public const string LogVacuumSkipFuelGate = "#LOC_KerbalFX_BlastFX_Log_VacuumSkipFuelGate";
        public const string LogVacuumSkipNotVacuum = "#LOC_KerbalFX_BlastFX_Log_VacuumSkipNotVacuum";
        public const string LogVacuumSkipLod = "#LOC_KerbalFX_BlastFX_Log_VacuumSkipLod";
        public const string LogVacuumQueued = "#LOC_KerbalFX_BlastFX_Log_VacuumQueued";
        public const string LogVacuumRateLimitDrop = "#LOC_KerbalFX_BlastFX_Log_VacuumRateLimitDrop";
        public const string LogVacuumBatch = "#LOC_KerbalFX_BlastFX_Log_VacuumBatch";
        public const string LogVacuumSpawn = "#LOC_KerbalFX_BlastFX_Log_VacuumSpawn";
        public const string LogVacuumBuildSlot = "#LOC_KerbalFX_BlastFX_Log_VacuumBuildSlot";
        public const string LogFxHeartbeat = "#LOC_KerbalFX_BlastFX_Log_FxHeartbeat";
        public const string LogVacuumDebrisProbe = "#LOC_KerbalFX_BlastFX_Log_VacuumDebrisProbe";
        public const string LogVacuumDebrisProbeLate = "#LOC_KerbalFX_BlastFX_Log_VacuumDebrisProbeLate";
        public const string LogVacuumAnchorRetarget = "#LOC_KerbalFX_BlastFX_Log_VacuumAnchorRetarget";
        public const string LogVacuumAnchorProbeSpawn = "#LOC_KerbalFX_BlastFX_Log_VacuumAnchorProbeSpawn";
        public const string LogVacuumAnchorProbeStep = "#LOC_KerbalFX_BlastFX_Log_VacuumAnchorProbeStep";
    }

    public class BlastFxParameters : GameParameters.CustomParameterNode
    {
        [GameParameters.CustomParameterUI(BlastFxLoc.UiEnable, toolTip = BlastFxLoc.UiEnableTip)]
        public bool enableBlastFx = true;

        [GameParameters.CustomParameterUI(BlastFxLoc.UiDebug, toolTip = BlastFxLoc.UiDebugTip)]
        public bool debugLogging;

        public override string Title
        {
            get { return Localizer.Format(BlastFxLoc.UiTitle); }
        }

        public override GameParameters.GameMode GameMode
        {
            get { return GameParameters.GameMode.ANY; }
        }

        public override string Section
        {
            get { return "KerbalFX_02_Extras"; }
        }

        public override string DisplaySection
        {
            get { return Localizer.Format(BlastFxLoc.UiSectionExtras); }
        }

        public override int SectionOrder
        {
            get { return 6; }
        }

        public override bool HasPresets
        {
            get { return false; }
        }
    }

    internal static class BlastFxConfig
    {
        public static bool Enabled = true;
        public static bool DebugLogging;
        public static int Revision;
        private static bool initialized;

        public static void Refresh()
        {
            bool en = true;
            bool dbg = false;
            if (HighLogic.CurrentGame != null && HighLogic.CurrentGame.Parameters != null)
            {
                BlastFxParameters p = HighLogic.CurrentGame.Parameters.CustomParams<BlastFxParameters>();
                if (p != null)
                {
                    en = p.enableBlastFx;
                    dbg = p.debugLogging;
                }
            }

            bool changed = !initialized || en != Enabled || dbg != DebugLogging;
            Enabled = en;
            DebugLogging = dbg;
            if (changed)
            {
                initialized = true;
                Revision++;
                BlastFxLog.Info(Localizer.Format(BlastFxLoc.LogSettings, Enabled, DebugLogging));
            }
        }
    }

    internal static class BlastFxRuntimeConfig
    {
        public static bool EnableModule = true;
        public static string TargetPrefix = "TS-";
        public static string[] TargetTokens = new[] { "TS-" };
        public static bool EnableVacuumExplosions = false;
        public static string VacuumExplosionBundle = "kcseffects";
        public static string VacuumExplosionPrefab = "Explosion";
        public static float VacuumExplosionReturnDelay = 15.0f;
        public static bool VacuumExplosionPreload = true;
        public static int VacuumExplosionPrewarmSlots = 2;
        public static int VacuumExplosionRateLimit = 3;
        public static float VacuumExplosionLimitRadius = 3.0f;
        public static float VacuumExplosionLimitTime = 0.9f;
        public static float VacuumExplosionMergeMaxSpeedDiff = 160f;
        public static bool VacuumExplosionWarmupDraw = true;
        public static bool VacuumExplosionRequireEligibleFuelPart = true;
        public static float VacuumExplosionMinFuelReserve = 0.01f;
        public static bool UseBundleVacuumDebrisMaterial = true;
        public static string VacuumDebrisMaterialTransform = "Debris";
        public const float TriggerCooldown = 0.35f;
        public const float BaseRadius = 0.20f;
        public const float RadiusFromPart = 0.95f;
        public const float SparkLife = 0.40f;
        public const float SmokeLife = 1.00f;
        public const float SparkSpeed = 5.4f;
        public const float SmokeSpeed = 1.8f;
        public const float SoftPuffLife = 1.45f;
        public const float SoftPuffSpeed = 1.18f;
        public const float FragmentCountMultiplier = 1.00f;
        public const float FragmentSpeedMultiplier = 1.00f;
        public static bool DespawnDetachedRingVessel = true;
        public static bool HideDetachedRingVisualImmediately = true;
        public const float DespawnDelay = 1.00f;
        public static bool SmartHiddenRingCleanup = true;
        public const float HiddenRingCleanupInterval = 2.0f;
        public const float HiddenRingCleanupDistance = 1200f;
        public const float HiddenRingMaxLifetime = 180f;
        public static int Revision;
        private static DateTime stamp = DateTime.MinValue;

        public static void Refresh()
        {
            ReloadFromGameDb();
            KerbalFxUtil.PrimeConfigFileStamp(ConfigPath(), ref stamp);
        }

        public static void TryHotReload()
        {
            if (!KerbalFxUtil.HasConfigFileChanged(ConfigPath(), ref stamp)) return;
            ReloadFromDisk();
        }

        private static void SeedDefaults()
        {
            EnableModule = true;
            TargetPrefix = "TS-";
            TargetTokens = new[] { "TS-" };
            DespawnDetachedRingVessel = true;
            HideDetachedRingVisualImmediately = true;
            SmartHiddenRingCleanup = true;
            EnableVacuumExplosions = true;
            VacuumExplosionBundle = "kcseffects";
            VacuumExplosionPrefab = "Explosion";
            VacuumExplosionReturnDelay = 15.0f;
            VacuumExplosionPreload = true;
            VacuumExplosionPrewarmSlots = 2;
            VacuumExplosionRateLimit = 3;
            VacuumExplosionLimitRadius = 3.0f;
            VacuumExplosionLimitTime = 0.9f;
            VacuumExplosionMergeMaxSpeedDiff = 160f;
            VacuumExplosionWarmupDraw = true;
            VacuumExplosionRequireEligibleFuelPart = true;
            VacuumExplosionMinFuelReserve = 0.01f;
            UseBundleVacuumDebrisMaterial = true;
            VacuumDebrisMaterialTransform = "Debris";
        }

        private static void ReloadFromGameDb()
        {
            SeedDefaults();
            if (GameDatabase.Instance != null)
            {
                ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("KERBALFX_BLAST_FX");
                if (nodes != null)
                {
                    for (int i = 0; i < nodes.Length; i++) Apply(nodes[i]);
                }
            }
            Revision++;
        }

        private static void ReloadFromDisk()
        {
            SeedDefaults();
            try
            {
                string path = ConfigPath();
                if (File.Exists(path))
                {
                    ConfigNode root = ConfigNode.Load(path);
                    if (root != null)
                    {
                        ConfigNode[] nodes = root.GetNodes("KERBALFX_BLAST_FX");
                        if (nodes != null)
                        {
                            for (int i = 0; i < nodes.Length; i++) Apply(nodes[i]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                BlastFxLog.Info(Localizer.Format(BlastFxLoc.LogHotReloadFailed, ex.Message));
            }
            Revision++;
        }

        private static void Apply(ConfigNode node)
        {
            if (node == null) return;
            EnableModule = KerbalFxUtil.ReadBool(node, "EnableModule", EnableModule);
            string prefix = KerbalFxUtil.ReadString(node, "TargetPartNamePrefix", TargetPrefix);
            if (prefix != TargetPrefix)
            {
                TargetPrefix = prefix;
                TargetTokens = new[] { prefix };
            }
            DespawnDetachedRingVessel = KerbalFxUtil.ReadBool(node, "DespawnDetachedRingVessel", DespawnDetachedRingVessel);
            HideDetachedRingVisualImmediately = KerbalFxUtil.ReadBool(node, "HideDetachedRingVisualImmediately", HideDetachedRingVisualImmediately);
            SmartHiddenRingCleanup = KerbalFxUtil.ReadBool(node, "SmartHiddenRingCleanup", SmartHiddenRingCleanup);
            EnableVacuumExplosions = KerbalFxUtil.ReadBool(node, "EnableVacuumExplosions", EnableVacuumExplosions);
            VacuumExplosionBundle = KerbalFxUtil.ReadString(node, "VacuumExplosionBundle", VacuumExplosionBundle);
            VacuumExplosionPrefab = KerbalFxUtil.ReadString(node, "VacuumExplosionPrefab", VacuumExplosionPrefab);
            VacuumExplosionReturnDelay = KerbalFxUtil.ReadFloat(node, "VacuumExplosionReturnDelay", VacuumExplosionReturnDelay, 1.0f, 60.0f);
            VacuumExplosionPreload = KerbalFxUtil.ReadBool(node, "VacuumExplosionPreload", VacuumExplosionPreload);
            VacuumExplosionPrewarmSlots = Mathf.Clamp(
                Mathf.RoundToInt(KerbalFxUtil.ReadFloat(node, "VacuumExplosionPrewarmSlots", VacuumExplosionPrewarmSlots, 0.0f, 12.0f)),
                0, 12);
            VacuumExplosionRateLimit = Mathf.Clamp(
                Mathf.RoundToInt(KerbalFxUtil.ReadFloat(node, "VacuumExplosionRateLimit", VacuumExplosionRateLimit, 0.0f, 20.0f)),
                0, 20);
            VacuumExplosionLimitRadius = KerbalFxUtil.ReadFloat(node, "VacuumExplosionLimitRadius", VacuumExplosionLimitRadius, 0.5f, 50.0f);
            VacuumExplosionLimitTime = KerbalFxUtil.ReadFloat(node, "VacuumExplosionLimitTime", VacuumExplosionLimitTime, 0.1f, 5.0f);
            VacuumExplosionMergeMaxSpeedDiff = KerbalFxUtil.ReadFloat(node, "VacuumExplosionMergeMaxSpeedDiff", VacuumExplosionMergeMaxSpeedDiff, 0.0f, 1000.0f);
            VacuumExplosionWarmupDraw = KerbalFxUtil.ReadBool(node, "VacuumExplosionWarmupDraw", VacuumExplosionWarmupDraw);
            VacuumExplosionRequireEligibleFuelPart = KerbalFxUtil.ReadBool(node, "VacuumExplosionRequireEligibleFuelPart", VacuumExplosionRequireEligibleFuelPart);
            VacuumExplosionMinFuelReserve = KerbalFxUtil.ReadFloat(node, "VacuumExplosionMinFuelReserve", VacuumExplosionMinFuelReserve, 0f, 10000f);
            UseBundleVacuumDebrisMaterial = KerbalFxUtil.ReadBool(node, "UseBundleVacuumDebrisMaterial", UseBundleVacuumDebrisMaterial);
            VacuumDebrisMaterialTransform = KerbalFxUtil.ReadString(node, "VacuumDebrisMaterialTransform", VacuumDebrisMaterialTransform);
        }

        private static string ConfigPath()
        {
            return Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "KerbalFX", "BlastFX", "KerbalFX_BlastFX.cfg");
        }

    }

    internal static class BlastFxLog
    {
        private static readonly KerbalFxLog impl = new KerbalFxLog(() => BlastFxConfig.DebugLogging);
        public static void Info(string msg) { impl.Info(msg); }
        public static void DebugLog(string msg) { impl.DebugLog(msg); }
        public static void DebugException(string scope, Exception ex) { impl.DebugException(scope, ex); }
    }

}
