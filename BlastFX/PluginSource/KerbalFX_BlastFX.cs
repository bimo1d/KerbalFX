using System;
using System.IO;
using KSP.Localization;
using UnityEngine;

namespace KerbalFX.BlastFX
{
    internal static class BlastFxLoc
    {
        public const string UiSection = "#LOC_KerbalFX_UI_Section";
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
        public const string LogPyroRing = "#LOC_KerbalFX_BlastFX_Log_PyroRing";
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
            get { return 5; }
        }

        public override bool HasPresets
        {
            get { return false; }
        }
    }

    internal static class BlastFxConfig
    {
        public static bool Enabled = true;
        public static bool Debug;
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

            bool changed = !initialized || en != Enabled || dbg != Debug;
            Enabled = en;
            Debug = dbg;
            if (changed)
            {
                initialized = true;
                Revision++;
                BlastFxLog.Info(Localizer.Format(BlastFxLoc.LogSettings, Enabled, Debug));
            }
        }
    }

    internal static class BlastFxRuntimeConfig
    {
        public static bool EnableModule = true;
        public static string TargetPrefix = "TS-";
        public static string[] TargetTokens = new[] { "TS-" };
        public const float TriggerCooldown = 0.35f;
        public const float BaseRadius = 0.20f;
        public const float RadiusFromPart = 0.95f;
        public const int SparkCount = 150;
        public const int SmokeCount = 48;
        public const int SoftPuffSmokeCount = 28;
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
        public const float Cleanup = 3.6f;
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
        }

        private static string ConfigPath()
        {
            return Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "KerbalFX", "BlastFX", "KerbalFX_BlastFX.cfg");
        }

    }

    internal static class BlastFxLog
    {
        public static void Info(string msg)
        {
            Debug.Log("[KerbalFX] " + msg);
        }

        public static void DebugLog(string msg)
        {
            if (!BlastFxConfig.Debug) return;
            Debug.Log("[KerbalFX] " + msg);
        }

        public static void DebugException(string scope, Exception ex)
        {
            if (!BlastFxConfig.Debug || ex == null) return;
            Debug.Log("[KerbalFX] " + scope + " failed: " + ex.Message);
        }
    }

}
