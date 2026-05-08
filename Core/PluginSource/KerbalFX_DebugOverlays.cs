using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace KerbalFX
{
    internal struct KerbalFxLightDebugReport
    {
        public string Module;
        public string Emitter;
        public KerbalFxLightSample Sample;
        public float AlphaMultiplier;
        public float Strength;
        public float ReportTime;
    }

    internal static class KerbalFxLightDebugReporter
    {
        public const float StaleAfter = 2.0f;
        private const int MaxEntries = 32;
        private static readonly Dictionary<string, KerbalFxLightDebugReport> entries =
            new Dictionary<string, KerbalFxLightDebugReport>(32, StringComparer.Ordinal);

        public static void Report(string module, string emitter, KerbalFxLightSample sample, float alphaMul, float strength)
        {
            if (string.IsNullOrEmpty(module) || string.IsNullOrEmpty(emitter))
                return;

            string key = module + "/" + emitter;
            if (entries.Count >= MaxEntries && !entries.ContainsKey(key))
                PruneStale(Time.time);
            if (entries.Count >= MaxEntries && !entries.ContainsKey(key))
                return;

            entries[key] = new KerbalFxLightDebugReport
            {
                Module = module,
                Emitter = emitter,
                Sample = sample,
                AlphaMultiplier = alphaMul,
                Strength = strength,
                ReportTime = Time.time
            };
        }

        public static IEnumerable<KerbalFxLightDebugReport> Snapshot()
        {
            return entries.Values;
        }

        public static int CountFresh(float now)
        {
            int count = 0;
            foreach (var pair in entries)
                if (now - pair.Value.ReportTime <= StaleAfter)
                    count++;
            return count;
        }

        public static void Clear()
        {
            entries.Clear();
        }

        private static void PruneStale(float now)
        {
            List<string> remove = null;
            foreach (var pair in entries)
            {
                if (now - pair.Value.ReportTime > StaleAfter)
                {
                    if (remove == null) remove = new List<string>();
                    remove.Add(pair.Key);
                }
            }
            if (remove != null)
                for (int i = 0; i < remove.Count; i++)
                    entries.Remove(remove[i]);
        }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    internal class KerbalFxLightDebugOverlay : MonoBehaviour
    {
        private const int PanelWidth = 720;
        private const int PanelMaxHeight = 540;
        private GUIStyle headerStyle;
        private GUIStyle rowStyle;
        private Vector2 scroll;
        private Rect panelRect = new Rect(12f, 60f, PanelWidth, 240f);

        private void OnGUI()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            float now = Time.time;
            int fresh = KerbalFxLightDebugReporter.CountFresh(now);
            if (fresh <= 0)
                return;

            EnsureStyles();
            panelRect.height = Mathf.Min(PanelMaxHeight, 60f + fresh * 22f + 30f);
            panelRect = GUILayout.Window(0x4B46_4C44, panelRect, DrawWindow, "KerbalFX Light-aware Debug");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("Module/Emitter | PB | sun | cos | sLm | amb | loc | aMul | flags", headerStyle);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(PanelMaxHeight - 60f));
            float now = Time.time;
            foreach (KerbalFxLightDebugReport r in KerbalFxLightDebugReporter.Snapshot())
            {
                if (now - r.ReportTime > KerbalFxLightDebugReporter.StaleAfter)
                    continue;

                string flags = (r.Sample.IsShadowed ? "shd " : "")
                    + (r.Sample.IsTwilight ? "twi" : "");
                string row = string.Format(CultureInfo.InvariantCulture,
                    "{0}/{1}  PB={2:F2} sun={3:F2} cos={4:F2} sLm={5:F2} amb={6:F2} loc={7:F2} aMul={8:F2} {9}",
                    r.Module,
                    Truncate(r.Emitter, 28),
                    r.Sample.PerceivedBrightness,
                    r.Sample.DirectSun,
                    r.Sample.CosSun,
                    r.Sample.SunLuma,
                    r.Sample.Ambient,
                    r.Sample.LocalLights,
                    r.AlphaMultiplier,
                    flags);
                GUILayout.Label(row, rowStyle);
            }
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void EnsureStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label);
                headerStyle.fontStyle = FontStyle.Bold;
                headerStyle.normal.textColor = new Color(0.95f, 0.95f, 0.70f);
            }
            if (rowStyle == null)
            {
                rowStyle = new GUIStyle(GUI.skin.label);
                rowStyle.normal.textColor = Color.white;
                rowStyle.fontSize = 11;
                rowStyle.wordWrap = false;
            }
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
                return value;
            return value.Substring(0, max);
        }
    }

    internal struct KerbalFxLineDebugReport
    {
        public string Module;
        public string RowId;
        public string Line;
        public float ReportTime;
    }

    internal static class KerbalFxLineDebugReporter
    {
        public const float StaleAfter = 4.0f;
        private const int MaxEntries = 24;
        private static readonly Dictionary<string, KerbalFxLineDebugReport> entries =
            new Dictionary<string, KerbalFxLineDebugReport>(MaxEntries, StringComparer.Ordinal);

        public static void Report(string module, string rowId, string line)
        {
            if (string.IsNullOrEmpty(module) || string.IsNullOrEmpty(rowId))
                return;

            string key = module + "/" + rowId;
            float now = Time.time;
            if (entries.Count >= MaxEntries && !entries.ContainsKey(key))
                PruneStale(now);
            if (entries.Count >= MaxEntries && !entries.ContainsKey(key))
                return;

            entries[key] = new KerbalFxLineDebugReport
            {
                Module = module,
                RowId = rowId,
                Line = line ?? string.Empty,
                ReportTime = now
            };
        }

        public static IEnumerable<KerbalFxLineDebugReport> Snapshot()
        {
            return entries.Values;
        }

        public static int CountFresh(float now)
        {
            int count = 0;
            foreach (var pair in entries)
            {
                if (now - pair.Value.ReportTime <= StaleAfter)
                    count++;
            }
            return count;
        }

        public static void Clear()
        {
            entries.Clear();
        }

        private static void PruneStale(float now)
        {
            List<string> remove = null;
            foreach (var pair in entries)
            {
                if (now - pair.Value.ReportTime > StaleAfter)
                {
                    if (remove == null) remove = new List<string>();
                    remove.Add(pair.Key);
                }
            }
            if (remove != null)
            {
                for (int i = 0; i < remove.Count; i++)
                    entries.Remove(remove[i]);
            }
        }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    internal class KerbalFxLineDebugOverlay : MonoBehaviour
    {
        private const float PanelPreferredWidth = 900f;
        private const float PanelHeightFixed = 324f;
        private const string LineDebugColGap = "  ";
        private GUIStyle headerStyle;
        private GUIStyle rowStyle;
        private Rect panelRect = new Rect(12f, 300f, PanelPreferredWidth, PanelHeightFixed);

        private void OnGUI()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            float now = Time.time;
            int fresh = KerbalFxLineDebugReporter.CountFresh(now);
            if (fresh <= 0)
                return;

            EnsureStyles();
            panelRect.width = Mathf.Min(PanelPreferredWidth, Mathf.Max(640f, Screen.width - 80f));
            panelRect.height = PanelHeightFixed;
            panelRect = GUILayout.Window(0x4B46_4C4E, panelRect, DrawWindow, "KerbalFX FX Debug");
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(14f);
            GUILayout.BeginVertical();
            GUILayout.Space(8f);
            GUILayout.Label(
                "Module/Channel | Busy  ChunkPc  Emit  Lod  Class  Queue  ParticlePc  Session  Flags",
                headerStyle,
                GUILayout.ExpandWidth(true));
            GUILayout.Space(6f);
            float now = Time.time;
            foreach (KerbalFxLineDebugReport r in KerbalFxLineDebugReporter.Snapshot())
            {
                if (now - r.ReportTime > KerbalFxLineDebugReporter.StaleAfter)
                    continue;

                GUILayout.Label(FormatBlastFxLineRow(r), rowStyle, GUILayout.MinHeight(22f), GUILayout.ExpandWidth(true));
                GUILayout.Space(4f);
            }

            GUILayout.Space(8f);
            GUILayout.EndVertical();
            GUILayout.Space(14f);
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        private void EnsureStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label);
                headerStyle.fontStyle = FontStyle.Bold;
                headerStyle.normal.textColor = new Color(0.95f, 0.95f, 0.70f);
                headerStyle.fontSize = 13;
                headerStyle.wordWrap = false;
            }
            if (rowStyle == null)
            {
                rowStyle = new GUIStyle(GUI.skin.label);
                rowStyle.normal.textColor = Color.white;
                rowStyle.fontSize = 13;
                rowStyle.wordWrap = false;
            }
        }

        private static string FormatBlastFxLineRow(KerbalFxLineDebugReport r)
        {
            string mod = string.IsNullOrEmpty(r.Module) ? "BlastFX" : r.Module;
            string id = TruncateRowId(r.RowId ?? string.Empty, 32);
            return mod + "/" + id + LineDebugColGap + (r.Line ?? string.Empty);
        }

        private static string TruncateRowId(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
                return value;
            return value.Substring(0, max);
        }
    }
}
