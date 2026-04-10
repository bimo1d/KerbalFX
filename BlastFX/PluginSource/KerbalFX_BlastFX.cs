using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using KSP.Localization;
using UnityEngine;

namespace KerbalFX.BlastFX
{
    internal static class BlastFxLoc
    {
        public const string UiSection = "#LOC_KerbalFX_UI_Section";
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
            get { return "KerbalFX"; }
        }

        public override string DisplaySection
        {
            get { return Localizer.Format(BlastFxLoc.UiSection); }
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
                Log.Settings(Localizer.Format(BlastFxLoc.LogSettings, Enabled, Debug));
            }
        }
    }

    internal static class BlastFxRuntime
    {
        public static bool EnableModule = true;
        public static string TargetPrefix = "TS-";
        public static string[] TargetTokens = new[] { "TS-" };
        public static float TriggerCooldown = 0.35f;
        public static float BaseRadius = 0.20f;
        public static float RadiusFromPart = 0.95f;
        public static int SparkCount = 150;
        public static int SmokeCount = 48;
        public static float SparkLife = 0.40f;
        public static float SmokeLife = 1.00f;
        public static float SparkSpeed = 5.4f;
        public static float SmokeSpeed = 1.8f;
        public static float FragmentCountMultiplier = 1.00f;
        public static float FragmentSpeedMultiplier = 1.00f;
        public static bool DespawnDetachedRingVessel = true;
        public static bool HideDetachedRingVisualImmediately = true;
        public static float DespawnDelay = 0.35f;
        public static bool SmartHiddenRingCleanup = true;
        public static float HiddenRingCleanupInterval = 2.0f;
        public static float HiddenRingCleanupDistance = 1200f;
        public static float HiddenRingMaxLifetime = 180f;
        public static float Cleanup = 3.6f;
        public static int Revision;
        private static DateTime stamp = DateTime.MinValue;

        public static void Refresh()
        {
            ReloadFromGameDb();
            PrimeStamp();
        }

        public static void TryHotReload()
        {
            if (!HasChanged()) return;
            ReloadFromDisk();
        }

        private static void SeedDefaults()
        {
            EnableModule = true;
            TargetPrefix = "TS-";
            TargetTokens = new[] { "TS-" };
            TriggerCooldown = 0.35f;
            BaseRadius = 0.20f;
            RadiusFromPart = 0.95f;
            SparkCount = 150;
            SmokeCount = 48;
            SparkLife = 0.40f;
            SmokeLife = 1.00f;
            SparkSpeed = 5.4f;
            SmokeSpeed = 1.8f;
            FragmentCountMultiplier = 1.00f;
            FragmentSpeedMultiplier = 1.00f;
            DespawnDetachedRingVessel = true;
            HideDetachedRingVisualImmediately = true;
            DespawnDelay = 0.35f;
            SmartHiddenRingCleanup = true;
            HiddenRingCleanupInterval = 2.0f;
            HiddenRingCleanupDistance = 1200f;
            HiddenRingMaxLifetime = 180f;
            Cleanup = 3.6f;
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
            RefreshTargetTokens();
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
                Log.Info(Localizer.Format(BlastFxLoc.LogHotReloadFailed, ex.Message));
            }
            RefreshTargetTokens();
            Revision++;
        }

        private static void Apply(ConfigNode node)
        {
            if (node == null) return;
            EnableModule = ReadBool(node, "EnableModule", EnableModule);
            TargetPrefix = ReadString(node, "TargetPartNamePrefix", TargetPrefix);
            TriggerCooldown = ReadFloat(node, "TriggerCooldownSeconds", TriggerCooldown, 0.05f, 5f);
            BaseRadius = ReadFloat(node, "BaseRingRadius", BaseRadius, 0.05f, 2f);
            RadiusFromPart = ReadFloat(node, "RingRadiusPerPartRadius", RadiusFromPart, 0.2f, 3f);
            SparkCount = ReadInt(node, "SparkCount", SparkCount, 6, 4000);
            SmokeCount = ReadInt(node, "SmokeCount", SmokeCount, 0, 4000);
            SparkLife = ReadFloat(node, "SparkLifetime", SparkLife, 0.05f, 4f);
            SmokeLife = ReadFloat(node, "SmokeLifetime", SmokeLife, 0.05f, 8f);
            SparkSpeed = ReadFloat(node, "SparkSpeed", SparkSpeed, 0.1f, 60f);
            SmokeSpeed = ReadFloat(node, "SmokeSpeed", SmokeSpeed, 0.1f, 30f);
            FragmentCountMultiplier = ReadFloat(node, "FragmentCountMultiplier", FragmentCountMultiplier, 0.2f, 4.0f);
            FragmentSpeedMultiplier = ReadFloat(node, "FragmentSpeedMultiplier", FragmentSpeedMultiplier, 0.2f, 4.0f);
            DespawnDetachedRingVessel = ReadBool(node, "DespawnDetachedRingVessel", DespawnDetachedRingVessel);
            HideDetachedRingVisualImmediately = ReadBool(node, "HideDetachedRingVisualImmediately", HideDetachedRingVisualImmediately);
            DespawnDelay = ReadFloat(node, "DespawnDelaySeconds", DespawnDelay, 0.0f, 8.0f);
            SmartHiddenRingCleanup = ReadBool(node, "SmartHiddenRingCleanup", SmartHiddenRingCleanup);
            HiddenRingCleanupInterval = ReadFloat(node, "HiddenRingCleanupIntervalSeconds", HiddenRingCleanupInterval, 0.5f, 15f);
            HiddenRingCleanupDistance = ReadFloat(node, "HiddenRingCleanupDistanceMeters", HiddenRingCleanupDistance, 50f, 8000f);
            HiddenRingMaxLifetime = ReadFloat(node, "HiddenRingMaxLifetimeSeconds", HiddenRingMaxLifetime, 10f, 1800f);
            Cleanup = ReadFloat(node, "CleanupSeconds", Cleanup, 0.3f, 15f);
        }

        private static void RefreshTargetTokens()
        {
            if (string.IsNullOrWhiteSpace(TargetPrefix))
            {
                TargetTokens = new string[0];
                return;
            }

            string[] parsed = TargetPrefix.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            if (parsed == null || parsed.Length == 0)
            {
                TargetTokens = new string[0];
                return;
            }

            int kept = 0;
            for (int i = 0; i < parsed.Length; i++)
            {
                string token = parsed[i] != null ? parsed[i].Trim() : string.Empty;
                if (token.Length == 0)
                {
                    continue;
                }

                parsed[kept++] = token;
            }

            if (kept == 0)
            {
                TargetTokens = new string[0];
                return;
            }

            if (kept != parsed.Length)
            {
                string[] compact = new string[kept];
                Array.Copy(parsed, compact, kept);
                TargetTokens = compact;
                return;
            }

            TargetTokens = parsed;
        }

        private static string ConfigPath()
        {
            return Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "KerbalFX", "BlastFX", "KerbalFX_BlastFX.cfg");
        }

        private static bool HasChanged()
        {
            try
            {
                string path = ConfigPath();
                if (!File.Exists(path)) return false;
                DateTime t = File.GetLastWriteTimeUtc(path);
                if (t > stamp)
                {
                    stamp = t;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.DebugException("runtime-stamp-check", ex);
            }
            return false;
        }

        private static void PrimeStamp()
        {
            try
            {
                string path = ConfigPath();
                if (File.Exists(path)) stamp = File.GetLastWriteTimeUtc(path);
            }
            catch (Exception ex)
            {
                Log.DebugException("runtime-stamp-prime", ex);
            }
        }

        private static bool ReadBool(ConfigNode n, string k, bool f) { bool v; return bool.TryParse(n.GetValue(k), out v) ? v : f; }
        private static float ReadFloat(ConfigNode n, string k, float f, float mn, float mx) { float v; return float.TryParse(n.GetValue(k), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? Mathf.Clamp(v, mn, mx) : f; }
        private static int ReadInt(ConfigNode n, string k, int f, int mn, int mx) { int v; return int.TryParse(n.GetValue(k), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? Mathf.Clamp(v, mn, mx) : f; }
        private static string ReadString(ConfigNode n, string k, string f) { string v = n.GetValue(k); return string.IsNullOrEmpty(v) ? f : v.Trim(); }
    }

    internal static class Log
    {
        public static void Info(string msg)
        {
            if (!BlastFxConfig.Debug) return;
            Debug.Log("[KerbalFX][BlastFX] " + msg);
        }

        public static void Settings(string msg)
        {
            Debug.Log("[KerbalFX][BlastFX] " + msg);
        }

        public static void DebugException(string scope, Exception ex)
        {
            if (!BlastFxConfig.Debug || ex == null) return;
            Debug.Log("[KerbalFX][BlastFX] " + scope + " failed: " + ex.Message);
        }
    }

}

