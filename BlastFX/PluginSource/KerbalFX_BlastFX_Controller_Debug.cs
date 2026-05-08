using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using KerbalFX;
using KSP.Localization;
using UnityEngine;

namespace KerbalFX.BlastFX
{
    internal partial class BlastFxController
    {
        private float debugLogTimer = 2.5f;
        private float debugOverlayTimer;
        private int debugVacuumSpawns;
        private long debugVacuumEmitted;
        private float debugLastVacuumTime;
        private int debugLastVacuumEmit;
        private int debugLastVacuumProbePc = -1;
        private int debugLastVacuumProbeLatePc = -1;
        private bool debugLastVacuumBundle;
        private FxSizeClass debugLastVacuumClass;
        private float debugLastVacuumLod;
        private int debugKcsSpawns;
        private float debugLastKcsTime;
        private int debugLastKcsSystems;
        private float debugLastPyroTime;
        private int debugLastPyroSparks;
        private int debugLastPyroChunks;
        private int debugLastPyroSmoke;
        private FxSizeClass debugLastPyroClass;
        private float debugLastPyroLod;

        private static bool BlastFxWantFxProbe()
        {
            return BlastFxConfig.DebugLogging;
        }

        private static string JoinDebugFields(params string[] fields)
        {
            return string.Join("  ", fields);
        }

        private static string FormatDebugAge(float lastTime, float now)
        {
            if (lastTime <= 0f)
                return "-";
            return (now - lastTime).ToString("F1", CultureInfo.InvariantCulture) + "s";
        }

        private void UpdateDebugTelemetry(float dt)
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            if (!BlastFxConfig.Enabled || !BlastFxRuntimeConfig.EnableModule)
                return;

            bool writeLog = BlastFxConfig.DebugLogging;
            bool writeOverlay = BlastFxConfig.DebugLogging;
            if (!writeLog && !writeOverlay)
                return;

            if (writeLog)
                debugLogTimer -= dt;
            if (writeOverlay)
                debugOverlayTimer -= dt;

            bool logNow = writeLog && debugLogTimer <= 0f;
            bool overlayNow = writeOverlay && debugOverlayTimer <= 0f;
            if (!logNow && !overlayNow)
                return;

            if (logNow)
                debugLogTimer = 2.5f;
            if (overlayNow)
                debugOverlayTimer = 0.5f;

            CollectVacuumLive(out int vacBusy, out int vacChunksPc, out int vacBundledBusy);
            CollectPyroLive(out int pyBusy, out int pySpPc, out int pyChPc, out int pySmPc);
            CollectKcsLive(out int kcsBusy, out int kcsLivePc);

            int vacTotSlots = CountPoolSlots(vacuumSlots);
            Vessel active = FlightGlobals.ActiveVessel;
            string sit = active != null ? active.situation.ToString() : "-";
            string atm = active != null
                ? active.atmDensity.ToString("F7", CultureInfo.InvariantCulture)
                : "-";

            float now = Time.time;
            string vacAge = FormatDebugAge(debugLastVacuumTime, now);
            string pyAge = FormatDebugAge(debugLastPyroTime, now);
            string kcsAge = FormatDebugAge(debugLastKcsTime, now);
            string vacProbe = FormatProbeCount(debugLastVacuumProbePc);
            string vacProbeLate = FormatProbeCount(debugLastVacuumProbeLatePc);

            if (logNow)
            {
                BlastFxLog.DebugLog(Localizer.Format(
                    BlastFxLoc.LogFxHeartbeat,
                    vacBusy + "/" + vacTotSlots,
                    vacChunksPc,
                    vacBundledBusy,
                    debugLastVacuumEmit,
                    vacProbe + "/" + vacProbeLate,
                    debugVacuumSpawns + "/" + debugVacuumEmitted,
                    pyBusy + " " + pySpPc + "/" + pyChPc + "/" + pySmPc,
                    kcsQueue.Count + "/" + kcsRecent.Count + " " + kcsBusy + "/" + kcsLivePc,
                    activePoolSlotCount,
                    trackedTargets.Count,
                    sit,
                    atm));
            }

            if (overlayNow)
                ReportDebugOverlay(vacBusy, vacTotSlots, vacChunksPc, vacBundledBusy,
                    pyBusy, pySpPc, pyChPc, pySmPc, kcsBusy, kcsLivePc,
                    vacProbe, vacProbeLate, vacAge, pyAge, kcsAge);
        }

        private static string FormatProbeCount(int count)
        {
            return count < 0 ? "?" : count.ToString(CultureInfo.InvariantCulture);
        }

        private void ReportDebugOverlay(int vacBusy, int vacTotSlots,
            int vacChunksPc, int vacBundledBusy, int pyBusy, int pySpPc,
            int pyChPc, int pySmPc, int kcsBusy, int kcsLivePc,
            string vacProbe, string vacProbeLate, string vacAge,
            string pyAge, string kcsAge)
        {
            KerbalFxLineDebugReporter.Report("BlastFX", "VacuumLive",
                JoinDebugFields(
                    "Busy=" + vacBusy + "/" + vacTotSlots,
                    "ChunkPcSum=" + vacChunksPc,
                    "PrefabBundleBusy=" + vacBundledBusy));
            KerbalFxLineDebugReporter.Report("BlastFX", "VacuumSpawn",
                JoinDebugFields(
                    "Emit=" + debugLastVacuumEmit,
                    "PcLag2Frm=" + vacProbe,
                    "PcLag3Frm=" + vacProbeLate,
                    "PrefabBundle=" + (debugLastVacuumBundle ? "Yes" : "No"),
                    "Class=" + debugLastVacuumClass,
                    "Lod=" + debugLastVacuumLod.ToString("F2", CultureInfo.InvariantCulture),
                    "Age=" + vacAge));
            KerbalFxLineDebugReporter.Report("BlastFX", "VacuumSession",
                JoinDebugFields(
                    "Spawns=" + debugVacuumSpawns,
                    "EmitSum=" + debugVacuumEmitted));
            KerbalFxLineDebugReporter.Report("BlastFX", "PyroLive",
                JoinDebugFields(
                    "Busy=" + pyBusy,
                    "SparksPc=" + pySpPc,
                    "ChunksPc=" + pyChPc,
                    "SmokePc=" + pySmPc));
            KerbalFxLineDebugReporter.Report("BlastFX", "PyroLast",
                JoinDebugFields(
                    "Emit_Sparks=" + debugLastPyroSparks,
                    "Emit_Chunks=" + debugLastPyroChunks,
                    "Emit_Smoke=" + debugLastPyroSmoke,
                    "Class=" + debugLastPyroClass,
                    "Lod=" + debugLastPyroLod.ToString("F2", CultureInfo.InvariantCulture),
                    "Age=" + pyAge));
            KerbalFxLineDebugReporter.Report("BlastFX", "KcsVacuumFx",
                JoinDebugFields(
                    "Queue=" + kcsQueue.Count,
                    "RecentWnd=" + kcsRecent.Count,
                    "PoolSlots=" + kcsSlots.Count,
                    "Busy=" + kcsBusy,
                    "ParticlePcSum=" + kcsLivePc,
                    "Prefab_ParticleSystems=" + debugLastKcsSystems,
                    "SessionSpawns=" + debugKcsSpawns,
                    "Age=" + kcsAge));
            KerbalFxLineDebugReporter.Report("BlastFX", "Flags",
                JoinDebugFields(
                    "KcsVac_Runtime=" + kcsVacuumExplosionsEnabled,
                    "KcsVac_Cfg=" + BlastFxRuntimeConfig.EnableVacuumExplosions,
                    "BundleDebrisCfg=" + BlastFxRuntimeConfig.UseBundleVacuumDebrisMaterial,
                    "PoolPrewarmOk=" + poolPrewarmComplete));
        }

        private void CollectVacuumLive(out int busySlots,
            out int chunksParticleSum, out int bundledBusySlots)
        {
            busySlots = 0;
            chunksParticleSum = 0;
            bundledBusySlots = 0;
            for (int i = 0; i < SizeClassCount; i++)
            {
                List<PoolSlot> list = vacuumSlots[i];
                if (list == null)
                    continue;
                for (int j = 0; j < list.Count; j++)
                {
                    PoolSlot slot = list[j];
                    if (slot == null || !slot.Busy)
                        continue;
                    busySlots++;
                    if (slot.VacuumDebrisFromBundledPrefab)
                        bundledBusySlots++;
                    if (slot.Chunks != null)
                        chunksParticleSum += slot.Chunks.particleCount;
                }
            }
        }

        private void CollectPyroLive(out int busySlots,
            out int sparksPc, out int chunksPc, out int smokePc)
        {
            busySlots = 0;
            sparksPc = 0;
            chunksPc = 0;
            smokePc = 0;
            for (int i = 0; i < SizeClassCount; i++)
            {
                List<PoolSlot> list = pyroSlots[i];
                if (list == null)
                    continue;
                for (int j = 0; j < list.Count; j++)
                {
                    PoolSlot slot = list[j];
                    if (slot == null || !slot.Busy)
                        continue;
                    busySlots++;
                    if (slot.Sparks != null) sparksPc += slot.Sparks.particleCount;
                    if (slot.Chunks != null) chunksPc += slot.Chunks.particleCount;
                    if (slot.Smoke != null) smokePc += slot.Smoke.particleCount;
                }
            }
        }

        private void CollectKcsLive(out int busySlots, out int particleSum)
        {
            busySlots = 0;
            particleSum = 0;
            for (int i = 0; i < kcsSlots.Count; i++)
            {
                KcsExplosionSlot slot = kcsSlots[i];
                if (slot == null || !slot.Busy)
                    continue;
                busySlots++;
                if (slot.Systems == null)
                    continue;
                for (int j = 0; j < slot.Systems.Length; j++)
                {
                    ParticleSystem ps = slot.Systems[j];
                    if (ps != null)
                        particleSum += ps.particleCount;
                }
            }
        }

        private static readonly float[] VacuumDebrisAnchorProbeCheckpoints = { 1.0f, 5.0f };

        private IEnumerator BlastFxVacuumDebrisAnchorProbe(PoolSlot slot)
        {
            if (slot == null || slot.Root == null) yield break;

            Transform root = slot.Root.transform;
            Transform anchor = slot.AnchorTransform;

            Vessel activeAtSpawn = FlightGlobals.ActiveVessel;
            Vessel anchorVesselAtSpawn = ResolveProbeVesselFromTransform(anchor);
            bool anchorIsActive = activeAtSpawn != null && anchorVesselAtSpawn != null
                && activeAtSpawn.id == anchorVesselAtSpawn.id;

            BlastFxLog.DebugLog(Localizer.Format(
                BlastFxLoc.LogVacuumAnchorProbeSpawn,
                anchor != null ? anchor.name : "null",
                anchorVesselAtSpawn != null ? anchorVesselAtSpawn.vesselName : "null",
                activeAtSpawn != null ? activeAtSpawn.vesselName : "null",
                anchorIsActive));

            Vector3 rootSpawnWorld = root.position;
            Vector3 vesselSpawnWorld = activeAtSpawn != null && activeAtSpawn.transform != null
                ? activeAtSpawn.transform.position
                : Vector3.zero;

            float spawnTime = Time.time;
            for (int idx = 0; idx < VacuumDebrisAnchorProbeCheckpoints.Length; idx++)
            {
                float target = VacuumDebrisAnchorProbeCheckpoints[idx];
                while (Time.time - spawnTime < target)
                    yield return null;

                if (slot == null || slot.Root == null || !slot.Busy)
                    yield break;

                Transform anchorNow = slot.AnchorTransform;
                bool anchorAlive = anchorNow != null;

                Vector3 rootNow = root.position;
                Vector3 anchorPosNow = anchorAlive ? anchorNow.position : Vector3.zero;
                Vessel act = FlightGlobals.ActiveVessel;
                Vector3 vesselNow = act != null && act.transform != null
                    ? act.transform.position
                    : Vector3.zero;

                float rootMinusAnchor = anchorAlive ? (rootNow - anchorPosNow).magnitude : 0f;
                float anchorMinusVessel = anchorAlive ? (anchorPosNow - vesselNow).magnitude : 0f;
                float rootDrift = (rootNow - rootSpawnWorld).magnitude;
                float vesselDrift = (vesselNow - vesselSpawnWorld).magnitude;

                BlastFxLog.DebugLog(Localizer.Format(
                    BlastFxLoc.LogVacuumAnchorProbeStep,
                    target.ToString("0.0", CultureInfo.InvariantCulture),
                    anchorAlive,
                    rootDrift.ToString("0.00", CultureInfo.InvariantCulture),
                    vesselDrift.ToString("0.00", CultureInfo.InvariantCulture),
                    rootMinusAnchor.ToString("0.000", CultureInfo.InvariantCulture),
                    anchorMinusVessel.ToString("0.00", CultureInfo.InvariantCulture)));
            }
        }

        private static Vessel ResolveProbeVesselFromTransform(Transform tr)
        {
            if (tr == null) return null;
            Part part = tr.GetComponentInParent<Part>();
            if (part != null) return part.vessel;
            return tr.GetComponentInParent<Vessel>();
        }

        private IEnumerator BlastFxVacuumDebrisDebugFrames(ParticleSystem ps,
            int emitted, FxSizeClass sizeClass, float lodMul)
        {
            yield return null;
            yield return null;
            if (ps == null)
                yield break;

            debugLastVacuumProbePc = ps.particleCount;

            var ren = ps.GetComponent<ParticleSystemRenderer>();
            string mat = ren != null && ren.sharedMaterial != null
                ? ren.sharedMaterial.name
                : "?";
            float shRad = 0f;
            try { shRad = ps.shape.radius; } catch { }

            if (BlastFxConfig.DebugLogging)
            {
                BlastFxLog.DebugLog(Localizer.Format(
                    BlastFxLoc.LogVacuumDebrisProbe,
                    sizeClass,
                    emitted,
                    debugLastVacuumProbePc,
                    ps.isPlaying,
                    lodMul.ToString("0.00", CultureInfo.InvariantCulture),
                    shRad.ToString("0.000", CultureInfo.InvariantCulture),
                    mat));
            }

            yield return null;
            if (ps != null)
                debugLastVacuumProbeLatePc = ps.particleCount;

            if (BlastFxConfig.DebugLogging && ps != null)
            {
                BlastFxLog.DebugLog(Localizer.Format(
                    BlastFxLoc.LogVacuumDebrisProbeLate,
                    debugLastVacuumProbeLatePc));
            }
        }
    }
}
