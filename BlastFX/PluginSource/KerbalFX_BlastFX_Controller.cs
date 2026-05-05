using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using KSP.Localization;
using UnityEngine;

namespace KerbalFX.BlastFX
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    internal partial class BlastFxController : MonoBehaviour
    {
        private enum FxKind
        {
            None = 0,
            PyroRing,
            SoftPuff,
            UndockGas
        }

        private enum FxSizeClass
        {
            Tiny   = 0,   // size0  (0.625 m)
            Small  = 1,   // size1  (1.25 m)
            Medium = 2,   // size2  (2.5 m), size1p5, mk2
            Large  = 3    // size3+ (3.75 m+), mk3
        }

        private const int SizeClassCount = 4;

        private static readonly int[] PyroSparkCounts  = { 35,  80,  150, 240 };
        private static readonly int[] PyroChunkCounts  = { 8,   22,  55,  100 };
        private static readonly int[] PyroSmokeCounts  = { 14,  28,  48,  72  };
        private static readonly int[] PuffSmokeCounts  = { 18,  35,  62,  90  };
        private static readonly int[] DockGasCounts    = { 147, 230, 361, 527 };
        private static readonly int[] VacuumDebrisCounts = { 16, 24, 36, 52 };
        private static readonly float[] SizeToSize01   = { 0.05f, 0.30f, 0.75f, 1.00f };
        private static readonly float[] SizeToRadius   = { 0.31f, 0.63f, 1.25f, 1.60f };
        private static readonly float[] DockingRingRadii = { 0.13f, 0.24f, 0.48f, 0.72f };

        private sealed class State
        {
            public bool Init;
            public uint ParentId;
            public int ChildCount;
            public Vector3 Axis = Vector3.up;
            public double LastFxUt = -999d;
            public double LastJettisonNeighborUt = -999d;
            public double LastJettisonProbeUt = -999d;
            public bool HasJettisonNeighbor;
            public bool HasSnapshot;
            public Vector3 SnapshotPosition = Vector3.zero;
            public int SnapshotLayer;
            public bool SnapshotInVacuum;
            public FxSizeClass SizeClass = FxSizeClass.Small;
            public bool SizeClassResolved;
        }

        private sealed class HiddenRingState
        {
            public Part Part;
            public double HiddenAtUt;
            public double EarliestCleanupUt;
        }

        private sealed class PoolSlot
        {
            public GameObject Root;
            public ParticleSystem Sparks;
            public ParticleSystem Chunks;
            public ParticleSystem Smoke;
            public bool Busy;
            public float ReturnTime;
        }

        private static readonly Dictionary<string, bool> decouplerModuleCache =
            new Dictionary<string, bool>(64, StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> dockingModuleCache =
            new Dictionary<string, bool>(64, StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> tokenMatchCache =
            new Dictionary<string, bool>(128, StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> stockSeparatorCache =
            new Dictionary<string, bool>(64, StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, FxSizeClass> sizeClassCache =
            new Dictionary<string, FxSizeClass>(64, StringComparer.OrdinalIgnoreCase);
        private static int partTargetCacheRevision = -1;

        private readonly Dictionary<uint, State> byPart = new Dictionary<uint, State>(128);
        private readonly Dictionary<uint, double> suppressedFxUntilByPart = new Dictionary<uint, double>(64);
        private readonly Dictionary<uint, HiddenRingState> hiddenRings = new Dictionary<uint, HiddenRingState>(32);
        private readonly List<Part> trackedTargets = new List<Part>(256);
        private readonly HashSet<uint> seenIds = new HashSet<uint>(128);
        private readonly List<uint> staleIds = new List<uint>(128);
        private readonly List<uint> hiddenRingRemoveIds = new List<uint>(32);

        // Pool: indexed by (int)FxSizeClass
        private readonly List<PoolSlot>[] pyroSlots = new List<PoolSlot>[SizeClassCount];
        private readonly List<PoolSlot>[] puffSlots = new List<PoolSlot>[SizeClassCount];
        private readonly List<PoolSlot>[] dockSlots = new List<PoolSlot>[SizeClassCount];
        private readonly List<PoolSlot>[] vacuumSlots = new List<PoolSlot>[SizeClassCount];

        private float scanTimer;
        private float targetRefreshTimer;
        private float cfgTimer;
        private float hotReloadTimer;
        private float hiddenRingCleanupTimer;
        private float boostedScanUntil;
        private int activePoolSlotCount;
        private float nextPoolReturnAt = float.MaxValue;
        private bool poolPrewarmStarted;
        private bool poolPrewarmComplete;

        private const float ScanDt = 2.50f;
        private const float BoostedScanDt = 0.15f;
        private const float TargetRefreshDt = 3.00f;
        private const float TargetRefreshBoostedDt = 0.25f;
        private const float CfgDt = 0.5f;
        private const float HotReloadDt = 2.0f;
        private const double HiddenRingMinKeepSeconds = 6.0d;
        private const double JettisonNeighborCacheSeconds = 5.0d;
        private const double JettisonNeighborProbeDt = 0.75d;

        private const float PyroReturnDelay = 3.2f;
        private const float PuffReturnDelay = 2.2f;
        private const float DockReturnDelay = 3.4f;
        private const float VacuumDebrisReturnDelay = 18.5f;
        private const int PrewarmPyroSlotsPerSize = 3;
        private const int PrewarmPuffSlotsPerSize = 2;
        private const int PrewarmDockSlotsPerSize = 1;
        private const double VacuumDensityThreshold = 0.001d;

        // LOD distance thresholds (squared)
        private const float LodFullDistSqr    = 100f  * 100f;
        private const float LodHalfDistSqr    = 300f  * 300f;
        private const float LodQuarterDistSqr = 800f  * 800f;
        private const float LodCullDistSqr    = 2000f * 2000f;

        private void Start()
        {
            BlastFxConfig.Refresh();
            BlastFxRuntimeConfig.Refresh();
            for (int i = 0; i < SizeClassCount; i++)
            {
                pyroSlots[i] = new List<PoolSlot>(3);
                puffSlots[i] = new List<PoolSlot>(3);
                dockSlots[i] = new List<PoolSlot>(2);
                vacuumSlots[i] = new List<PoolSlot>(2);
            }
            SubscribeEvents();
            StartPoolPrewarmIfNeeded();
            RequestBoostedScan(4.0f);
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            StopAllCoroutines();
            DestroyPools();
            byPart.Clear();
            suppressedFxUntilByPart.Clear();
            trackedTargets.Clear();
            hiddenRings.Clear();
        }

        private void SubscribeEvents()
        {
            GameEvents.onPartDeCouple.Add(OnPartDecouple);
            GameEvents.onPartDeCoupleNewVesselComplete.Add(OnPartDecoupleNewVesselComplete);
            GameEvents.onPartDie.Add(OnPartDie);
            GameEvents.onPartUndock.Add(OnPartUndock);
            GameEvents.onVesselCreate.Add(OnVesselCreate);
        }

        private void UnsubscribeEvents()
        {
            GameEvents.onPartDeCouple.Remove(OnPartDecouple);
            GameEvents.onPartDeCoupleNewVesselComplete.Remove(OnPartDecoupleNewVesselComplete);
            GameEvents.onPartDie.Remove(OnPartDie);
            GameEvents.onPartUndock.Remove(OnPartUndock);
            GameEvents.onVesselCreate.Remove(OnVesselCreate);
        }

        private void OnPartDecouple(Part part)
        {
            TryTriggerFromEvent(part, "onPartDeCouple");
            RequestBoostedScan(2.5f);
        }

        private void OnPartDie(Part part)
        {
            TryTriggerFromEvent(part, "onPartDie");
            RequestBoostedScan(2.5f);
        }

        private void OnPartUndock(Part part)
        {
            TryTriggerFromEvent(part, "onPartUndock");
            RequestBoostedScan(2.5f);
        }

        private void OnPartDecoupleNewVesselComplete(Vessel fromVessel, Vessel toVessel)
        {
            RequestBoostedScan(3.0f);
            if (!BlastFxConfig.Enabled) return;
            if (!BlastFxRuntimeConfig.EnableModule) return;
            if (!BlastFxRuntimeConfig.DespawnDetachedRingVessel) return;
            TryDespawnSeparatedRingVessel(fromVessel);
            TryDespawnSeparatedRingVessel(toVessel);
        }

        private void OnVesselCreate(Vessel vessel)
        {
            RequestBoostedScan(2.0f);
        }

        private void RequestBoostedScan(float seconds)
        {
            float now = Time.time;
            boostedScanUntil = Mathf.Max(boostedScanUntil, now + Mathf.Max(0.5f, seconds));
            scanTimer = 0f;
            targetRefreshTimer = 0f;
        }

        private void TryDespawnSeparatedRingVessel(Vessel vessel)
        {
            Part ringPart = TryGetSinglePart(vessel);
            if (ringPart == null) return;
            if (!IsPyroTarget(ringPart)) return;

            SuppressFx(ringPart, BlastFxRuntimeConfig.DespawnDelay + 2.0f);
            if (BlastFxRuntimeConfig.HideDetachedRingVisualImmediately)
            {
                HidePartVisuals(ringPart);
            }

            if (ShouldSkipDespawnToPreserveShroud(ringPart))
            {
                TrackHiddenRing(ringPart);
                BlastFxLog.DebugLog(Localizer.Format(
                    BlastFxLoc.LogSkipDespawnPreserveShroud,
                    ringPart.partInfo != null ? ringPart.partInfo.name : "unknown"));
                return;
            }

            float despawnDelay = Mathf.Max(0f, BlastFxRuntimeConfig.DespawnDelay);
            if (despawnDelay <= 0.0001f)
            {
                if (ringPart != null && ringPart.vessel != null
                    && ringPart.vessel.parts != null && ringPart.vessel.parts.Count == 1)
                {
                    ringPart.Die();
                }
                return;
            }

            StartCoroutine(DestroySeparatedRingAfterDelay(ringPart, despawnDelay));
        }

        private static IEnumerator DestroySeparatedRingAfterDelay(Part ringPart, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!IsSinglePartVessel(ringPart != null ? ringPart.vessel : null)) yield break;
            ringPart.Die();
        }

        private static bool IsSinglePartTrackedVessel(Vessel vessel)
        {
            return vessel != null && vessel.parts != null && vessel.parts.Count == 1;
        }

        private static bool IsSinglePartVessel(Vessel vessel)
        {
            return vessel != null && vessel.loaded && vessel.parts != null && vessel.parts.Count == 1;
        }

        private static Part TryGetSinglePart(Vessel vessel)
        {
            return IsSinglePartVessel(vessel) ? vessel.parts[0] : null;
        }

        private static void HidePartVisuals(Part part)
        {
            if (part == null) return;
            DisableRenderers(part.FindModelComponents<Renderer>());
            StopParticles(part.FindModelComponents<ParticleSystem>());
            DisableLights(part.FindModelComponents<Light>());
        }

        private static void DisableRenderers(List<Renderer> renderers)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null) renderer.enabled = false;
            }
        }

        private static void StopParticles(List<ParticleSystem> particleSystems)
        {
            if (particleSystems == null) return;
            for (int i = 0; i < particleSystems.Count; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static void DisableLights(List<Light> lights)
        {
            if (lights == null) return;
            for (int i = 0; i < lights.Count; i++)
            {
                Light light = lights[i];
                if (light != null) light.enabled = false;
            }
        }

        private void TryTriggerFromEvent(Part part, string source)
        {
            FxKind fxKind = GetEventFxKind(part, source);
            if (!BlastFxConfig.Enabled || !BlastFxRuntimeConfig.EnableModule
                || part == null || fxKind == FxKind.None)
            {
                return;
            }
            if (BlastFxConfig.DebugLogging)
            {
                string probePart = part.partInfo != null ? part.partInfo.name : "null";
                BlastFxLog.DebugLog(Localizer.Format(
                    BlastFxLoc.LogTriggerProbe, source, probePart, fxKind.ToString()));
            }
            if (IsFxSuppressed(part)) return;

            State state = GetOrCreateState(part.flightID);
            double ut = Planetarium.GetUniversalTime();
            if (ut - state.LastFxUt < BlastFxRuntimeConfig.TriggerCooldown) return;

            state.LastFxUt = ut;
            ResolveSizeClassIfNeeded(part, state);
            Vector3 axis = GetAxis(part, state.Axis);
            Vector3 spawnPos = part.transform != null ? part.transform.position : Vector3.zero;
            if (fxKind == FxKind.UndockGas
                && TryGetDockingPose(part, out Vector3 dockPos, out Vector3 dockAxis))
            {
                spawnPos = dockPos;
                axis = dockAxis;
            }

            if (TrySpawnFromLivePart(part, state, spawnPos, axis, source, fxKind)) return;
            TrySpawnFromSnapshot(part, state, axis, source, fxKind);
        }

        private State GetOrCreateState(uint flightId)
        {
            if (!byPart.TryGetValue(flightId, out var state))
            {
                state = new State();
                byPart[flightId] = state;
            }
            return state;
        }

        private bool TrySpawnFromLivePart(Part part, State state, Vector3 spawnPos,
            Vector3 axis, string source, FxKind fxKind)
        {
            if (part == null || state == null || part.transform == null) return false;

            state.HasSnapshot = true;
            state.SnapshotPosition = spawnPos;
            state.SnapshotLayer = part.gameObject != null ? part.gameObject.layer : 0;
            state.SnapshotInVacuum = IsPartInVacuumFreefall(part);

            SpawnFx(fxKind, spawnPos, axis, state.SnapshotLayer,
                state.SizeClass, state.SnapshotInVacuum);

            string partName = part.partInfo != null ? part.partInfo.name : "unknown";
            BlastFxLog.DebugLog(Localizer.Format(BlastFxLoc.LogTriggerVia, source, partName));
            return true;
        }

        private void TrySpawnFromSnapshot(Part part, State state, Vector3 axis,
            string source, FxKind fxKind)
        {
            if (part == null || state == null || !state.HasSnapshot) return;

            SpawnFx(fxKind, state.SnapshotPosition, axis, state.SnapshotLayer,
                state.SizeClass, state.SnapshotInVacuum);

            string partName = part.partInfo != null ? part.partInfo.name : "unknown";
            BlastFxLog.DebugLog(Localizer.Format(BlastFxLoc.LogTriggerViaSnapshot, source, partName));
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            cfgTimer -= dt;
            if (cfgTimer <= 0f)
            {
                cfgTimer = CfgDt;
                BlastFxConfig.Refresh();
                StartPoolPrewarmIfNeeded();
            }

            hotReloadTimer -= dt;
            if (hotReloadTimer <= 0f)
            {
                hotReloadTimer = HotReloadDt;
                BlastFxRuntimeConfig.TryHotReload();
            }

            hiddenRingCleanupTimer -= dt;
            if (hiddenRingCleanupTimer <= 0f)
            {
                hiddenRingCleanupTimer = BlastFxRuntimeConfig.HiddenRingCleanupInterval;
                CleanupHiddenRings();
            }

            ReturnExpiredSlots();

            if (!BlastFxConfig.Enabled || !BlastFxRuntimeConfig.EnableModule) return;

            RefreshTrackedTargetsIfNeeded(dt);

            scanTimer -= dt;
            if (scanTimer > 0f) return;
            float scanInterval = Time.time < boostedScanUntil ? BoostedScanDt : ScanDt;
            scanTimer = scanInterval;
            Scan();
        }

        private void Scan()
        {
            CleanupSuppression();

            if (trackedTargets.Count == 0)
            {
                byPart.Clear();
                return;
            }

            seenIds.Clear();
            double scanUt = Planetarium.GetUniversalTime();

            for (int i = 0; i < trackedTargets.Count; i++)
            {
                ProcessTargetPartFromScan(trackedTargets[i], scanUt);
            }

            PruneMissingPartStates();
        }

        private void RefreshTrackedTargetsIfNeeded(float dt)
        {
            targetRefreshTimer -= dt;
            if (targetRefreshTimer > 0f) return;

            targetRefreshTimer = Time.time < boostedScanUntil
                ? TargetRefreshBoostedDt
                : TargetRefreshDt;

            trackedTargets.Clear();
            List<Vessel> loaded = FlightGlobals.VesselsLoaded;
            if (loaded == null || loaded.Count == 0) return;

            for (int v = 0; v < loaded.Count; v++)
            {
                Vessel vessel = loaded[v];
                if (vessel == null || !vessel.loaded || vessel.packed || vessel.parts == null)
                    continue;

                for (int i = 0; i < vessel.parts.Count; i++)
                {
                    Part part = vessel.parts[i];
                    if (IsPyroTarget(part))
                    {
                        trackedTargets.Add(part);
                    }
                }
            }
        }

        private void ProcessTargetPartFromScan(Part part, double scanUt)
        {
            if (!IsPyroTarget(part)) return;

            seenIds.Add(part.flightID);
            State state = GetOrCreateState(part.flightID);
            uint parentId = part.parent != null ? part.parent.flightID : 0u;
            int childCount = part.children != null ? part.children.Count : 0;

            UpdateJettisonNeighborCache(part, state, scanUt);
            Vector3 axis = GetAxis(part, state.Axis);
            UpdateScanSnapshot(part, state);
            ResolveSizeClassIfNeeded(part, state);
            TryTriggerFromStructureBreak(part, state, parentId, childCount, axis, scanUt);
            UpdateScanState(state, parentId, childCount, axis);
        }

        private static void UpdateScanSnapshot(Part part, State state)
        {
            state.HasSnapshot = true;
            state.SnapshotPosition = part.transform != null
                ? part.transform.position
                : state.SnapshotPosition;
            state.SnapshotLayer = part.gameObject != null
                ? part.gameObject.layer
                : state.SnapshotLayer;
            state.SnapshotInVacuum = IsPartInVacuumFreefall(part);
        }

        private void UpdateJettisonNeighborCache(Part part, State state, double scanUt)
        {
            if (!state.Init || (scanUt - state.LastJettisonProbeUt) >= JettisonNeighborProbeDt)
            {
                state.HasJettisonNeighbor = HasJettisonNeighbor(part);
                state.LastJettisonProbeUt = scanUt;
            }
            if (state.HasJettisonNeighbor)
            {
                state.LastJettisonNeighborUt = scanUt;
            }
        }

        private static void UpdateScanState(State state, uint parentId, int childCount,
            Vector3 axis)
        {
            state.Init = true;
            state.ParentId = parentId;
            state.ChildCount = childCount;
            state.Axis = axis;
        }

        private static bool HasStructureBreak(State state, uint parentId, int childCount)
        {
            bool parentBreak = state.ParentId != 0u && parentId == 0u;
            bool childBreak = state.ChildCount > childCount;
            return parentBreak || childBreak;
        }

        private void TryTriggerFromStructureBreak(Part part, State state,
            uint parentId, int childCount, Vector3 axis, double scanUt)
        {
            if (!state.Init || !HasStructureBreak(state, parentId, childCount)) return;
            if (scanUt - state.LastFxUt < BlastFxRuntimeConfig.TriggerCooldown) return;

            state.LastFxUt = scanUt;
            SpawnFx(FxKind.PyroRing, part.transform.position, axis,
                part.gameObject != null ? part.gameObject.layer : 0,
                state.SizeClass, state.SnapshotInVacuum);

            string partName = part.partInfo != null ? part.partInfo.name : "unknown";
            BlastFxLog.DebugLog(Localizer.Format(BlastFxLoc.LogTriggerViaScan, partName));
        }

        private void PruneMissingPartStates()
        {
            staleIds.Clear();
            var e = byPart.GetEnumerator();
            while (e.MoveNext())
            {
                if (!seenIds.Contains(e.Current.Key))
                    staleIds.Add(e.Current.Key);
            }
            e.Dispose();
            for (int i = 0; i < staleIds.Count; i++)
                byPart.Remove(staleIds[i]);
        }

        private static FxKind GetEventFxKind(Part part, string source)
        {
            if (part == null || part.partInfo == null || part.Modules == null)
                return FxKind.None;

            bool isUndockSource =
                string.Equals(source, "onPartUndock", StringComparison.Ordinal)
                || string.Equals(source, "onPartDeCouple", StringComparison.Ordinal);

            if (isUndockSource && IsDockingTarget(part))
            {
                return FxKind.UndockGas;
            }

            if (!HasDecouplerModule(part)) return FxKind.None;

            if (IsPyroTarget(part)) return FxKind.PyroRing;

            if (string.Equals(source, "onPartDeCouple", StringComparison.Ordinal)
                && IsSoftDecouplerTarget(part))
            {
                return FxKind.SoftPuff;
            }

            return FxKind.None;
        }

        private static bool IsPyroTarget(Part p)
        {
            if (p == null || p.partInfo == null || p.Modules == null) return false;
            if (!HasDecouplerModule(p)) return false;
            if (MatchesTargetToken(p)) return true;
            return IsLikelyStockTsSeparator(p);
        }

        private static bool IsSoftDecouplerTarget(Part p)
        {
            return p != null && HasDecouplerModule(p) && !IsPyroTarget(p);
        }

        private static bool IsDockingTarget(Part p)
        {
            if (p == null || p.Modules == null) return false;

            string cacheKey = p.partInfo != null ? p.partInfo.name
                : (p != null ? p.name : string.Empty);
            if (!string.IsNullOrEmpty(cacheKey)
                && dockingModuleCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            bool found = false;
            for (int i = 0; i < p.Modules.Count; i++)
            {
                var module = p.Modules[i];
                if (module == null) continue;
                string runtimeName = module.moduleName;
                string typeName = module.GetType().Name;
                if (NameLooksLikeDockingNode(runtimeName)
                    || NameLooksLikeDockingNode(typeName))
                {
                    found = true;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(cacheKey))
                dockingModuleCache[cacheKey] = found;

            return found;
        }

        private static bool NameLooksLikeDockingNode(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("DockingNode", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("DockingPort", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsPartInVacuumFreefall(Part part)
        {
            if (part == null) return false;
            Vessel vessel = part.vessel;
            if (vessel == null || vessel.mainBody == null) return false;

            bool atmosphereless = !vessel.mainBody.atmosphere
                || vessel.atmDensity < VacuumDensityThreshold;
            if (!atmosphereless) return false;

            if (vessel.LandedOrSplashed) return false;

            Vessel.Situations s = vessel.situation;
            if (s == Vessel.Situations.LANDED
                || s == Vessel.Situations.SPLASHED
                || s == Vessel.Situations.PRELAUNCH)
            {
                return false;
            }

            return true;
        }

        private static bool HasDecouplerModule(Part p)
        {
            if (p == null || p.Modules == null) return false;

            string cacheKey = p.partInfo != null ? p.partInfo.name
                : (p != null ? p.name : string.Empty);
            if (!string.IsNullOrEmpty(cacheKey)
                && decouplerModuleCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            cached = false;
            for (int i = 0; i < p.Modules.Count; i++)
            {
                PartModule m = p.Modules[i];
                if (m == null) continue;
                string tn = m.GetType().Name;
                if (!string.IsNullOrEmpty(tn)
                    && (KerbalFxUtil.ContainsIgnoreCase(tn, "Decouple")
                        || KerbalFxUtil.ContainsIgnoreCase(tn, "Decoupler")))
                {
                    cached = true;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(cacheKey))
                decouplerModuleCache[cacheKey] = cached;

            return cached;
        }

        private static bool MatchesTargetToken(Part p)
        {
            EnsurePartTargetCachesCurrent();
            string target = BlastFxRuntimeConfig.TargetPrefix;
            if (string.IsNullOrEmpty(target) || target.Trim().Length == 0) return false;

            string name = p.partInfo != null ? p.partInfo.name : string.Empty;
            string rawTitle = p.partInfo != null ? p.partInfo.title : string.Empty;
            string cacheKey = name + "|" + rawTitle;
            if (tokenMatchCache.TryGetValue(cacheKey, out var cached)) return cached;

            string localizedTitle = null;
            bool hasLocalizedKey = !string.IsNullOrEmpty(rawTitle)
                && rawTitle.StartsWith("#", StringComparison.Ordinal);

            string[] tokens = BlastFxRuntimeConfig.TargetTokens;
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (string.IsNullOrEmpty(token)) continue;
                if (KerbalFxUtil.ContainsIgnoreCase(name, token)
                    || KerbalFxUtil.ContainsIgnoreCase(rawTitle, token))
                {
                    tokenMatchCache[cacheKey] = true;
                    return true;
                }

                if (!hasLocalizedKey) continue;
                if (localizedTitle == null)
                    localizedTitle = Localizer.Format(rawTitle);

                if (KerbalFxUtil.ContainsIgnoreCase(localizedTitle, token))
                {
                    tokenMatchCache[cacheKey] = true;
                    return true;
                }
            }

            tokenMatchCache[cacheKey] = false;
            return false;
        }

        private static bool IsLikelyStockTsSeparator(Part p)
        {
            EnsurePartTargetCachesCurrent();
            string cfgToken = BlastFxRuntimeConfig.TargetPrefix;
            if (string.IsNullOrWhiteSpace(cfgToken)
                || cfgToken.IndexOf("TS", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            string name = p.partInfo != null ? p.partInfo.name : string.Empty;
            if (string.IsNullOrEmpty(name)) return false;

            if (stockSeparatorCache.TryGetValue(name, out var cached)) return cached;

            cached = name.StartsWith("Separator_", StringComparison.OrdinalIgnoreCase)
                || name.IndexOf("restock-separator-", StringComparison.OrdinalIgnoreCase) >= 0;
            stockSeparatorCache[name] = cached;
            return cached;
        }

        private static bool HasJettisonNeighbor(Part part)
        {
            if (part == null) return false;
            if (KerbalFxUtil.PartHasModule(part.parent, "ModuleJettison")) return true;
            if (part.children == null) return false;
            for (int i = 0; i < part.children.Count; i++)
            {
                if (KerbalFxUtil.PartHasModule(part.children[i], "ModuleJettison"))
                    return true;
            }
            return false;
        }

        private static void EnsurePartTargetCachesCurrent()
        {
            if (partTargetCacheRevision == BlastFxRuntimeConfig.Revision) return;
            tokenMatchCache.Clear();
            stockSeparatorCache.Clear();
            sizeClassCache.Clear();
            partTargetCacheRevision = BlastFxRuntimeConfig.Revision;
        }

        private static void ResolveSizeClassIfNeeded(Part part, State state)
        {
            if (state.SizeClassResolved) return;
            state.SizeClass = GetSizeClass(part);
            state.SizeClassResolved = true;
        }

        private static bool TryGetDockingPose(Part part, out Vector3 worldPos, out Vector3 worldAxis)
        {
            worldPos = Vector3.zero;
            worldAxis = Vector3.zero;
            if (part == null || part.Modules == null || part.transform == null) return false;
            for (int i = 0; i < part.Modules.Count; i++)
            {
                ModuleDockingNode dock = part.Modules[i] as ModuleDockingNode;
                if (dock == null) continue;

                AttachNode an = !string.IsNullOrEmpty(dock.referenceAttachNode)
                    ? part.FindAttachNode(dock.referenceAttachNode)
                    : null;
                if (an != null && an.orientation.sqrMagnitude > 0.0001f)
                {
                    worldAxis = part.transform.TransformDirection(an.orientation).normalized;
                    worldPos = part.transform.TransformPoint(an.position);
                    return true;
                }

                if (dock.nodeTransform != null
                    && dock.nodeTransform.forward.sqrMagnitude > 0.0001f)
                {
                    worldAxis = dock.nodeTransform.forward.normalized;
                    worldPos = dock.nodeTransform.position;
                    return true;
                }
            }
            return false;
        }

        private static FxSizeClass GetSizeClass(Part part)
        {
            if (part == null || part.partInfo == null) return FxSizeClass.Small;

            string partName = part.partInfo.name;
            if (!string.IsNullOrEmpty(partName))
            {
                if (sizeClassCache.TryGetValue(partName, out var cached)) return cached;
            }

            FxSizeClass result = ClassifyPart(part);

            if (!string.IsNullOrEmpty(partName))
                sizeClassCache[partName] = result;

            return result;
        }

        private static FxSizeClass ClassifyPart(Part part)
        {
            FxSizeClass fromProfile;
            if (TryClassifyByBulkheadProfile(part, out fromProfile))
                return fromProfile;

            FxSizeClass fromNodes = ClassifyByAttachNodes(part);
            if (fromNodes > FxSizeClass.Tiny) return fromNodes;

            return ClassifyByColliderRadius(part);
        }

        private static bool TryClassifyByBulkheadProfile(Part part, out FxSizeClass sizeClass)
        {
            sizeClass = FxSizeClass.Tiny;
            if (part == null || part.partInfo == null) return false;
            string profiles = part.partInfo.bulkheadProfiles;
            if (string.IsNullOrEmpty(profiles)) return false;

            bool found = false;
            int start = 0;
            for (int i = 0; i <= profiles.Length; i++)
            {
                if (i == profiles.Length || profiles[i] == ',')
                {
                    if (i > start)
                    {
                        string token = profiles.Substring(start, i - start).Trim();
                        FxSizeClass tokenSize;
                        if (TryMapBulkheadToken(token, out tokenSize))
                        {
                            found = true;
                            if (tokenSize > sizeClass) sizeClass = tokenSize;
                        }
                    }
                    start = i + 1;
                }
            }
            return found;
        }

        private static bool TryMapBulkheadToken(string token, out FxSizeClass sizeClass)
        {
            sizeClass = FxSizeClass.Tiny;
            if (string.IsNullOrEmpty(token)) return false;
            if (string.Equals(token, "size4", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "size3", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "mk3", StringComparison.OrdinalIgnoreCase))
            {
                sizeClass = FxSizeClass.Large;
                return true;
            }
            if (string.Equals(token, "size2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "size1p5", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "mk2", StringComparison.OrdinalIgnoreCase))
            {
                sizeClass = FxSizeClass.Medium;
                return true;
            }
            if (string.Equals(token, "size1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "srf", StringComparison.OrdinalIgnoreCase))
            {
                sizeClass = FxSizeClass.Small;
                return true;
            }
            if (string.Equals(token, "size0", StringComparison.OrdinalIgnoreCase))
                return true;

            if (token.StartsWith("size", StringComparison.OrdinalIgnoreCase))
            {
                string numeric = token.Substring(4).Replace('p', '.');
                if (float.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out var size))
                {
                    if (size >= 3.0f) sizeClass = FxSizeClass.Large;
                    else if (size >= 1.5f) sizeClass = FxSizeClass.Medium;
                    else if (size >= 1.0f) sizeClass = FxSizeClass.Small;
                    else sizeClass = FxSizeClass.Tiny;
                    return true;
                }
            }

            return false;
        }

        private static FxSizeClass ClassifyByAttachNodes(Part part)
        {
            if (part == null) return FxSizeClass.Tiny;
            int maxSize = -1;
            AttachNode top = part.FindAttachNode("top");
            if (top != null && top.size > maxSize) maxSize = top.size;
            AttachNode bottom = part.FindAttachNode("bottom");
            if (bottom != null && bottom.size > maxSize) maxSize = bottom.size;

            if (maxSize >= 3) return FxSizeClass.Large;
            if (maxSize == 2) return FxSizeClass.Medium;
            if (maxSize == 1) return FxSizeClass.Small;
            if (maxSize == 0) return FxSizeClass.Tiny;
            return FxSizeClass.Tiny;
        }

        private static FxSizeClass ClassifyByColliderRadius(Part part)
        {
            float radius = EstimatePartRadius(part);
            if (radius > 1.40f) return FxSizeClass.Large;
            if (radius > 0.80f) return FxSizeClass.Medium;
            if (radius > 0.40f) return FxSizeClass.Small;
            return FxSizeClass.Tiny;
        }

        private static float GetLodMultiplier(Vector3 worldPosition)
        {
            Camera cam = null;
            if (FlightCamera.fetch != null)
                cam = FlightCamera.fetch.mainCamera;
            if (cam == null) return 1.0f;

            float distSqr = (cam.transform.position - worldPosition).sqrMagnitude;
            if (distSqr < LodFullDistSqr)    return 1.00f;
            if (distSqr < LodHalfDistSqr)    return 0.50f;
            if (distSqr < LodQuarterDistSqr) return 0.25f;
            if (distSqr < LodCullDistSqr)    return 0.12f;
            return 0f;
        }

        private void SpawnFx(FxKind fxKind, Vector3 position, Vector3 axis,
            int layer, FxSizeClass sizeClass, bool inVacuum)
        {
            float lodMul = GetLodMultiplier(position);
            if (lodMul <= 0f) return;

            switch (fxKind)
            {
                case FxKind.SoftPuff:
                    SpawnSoftPuff(position, axis, layer, sizeClass, lodMul);
                    break;
                case FxKind.UndockGas:
                    SpawnUndockGas(position, axis, layer, sizeClass, lodMul);
                    break;
                default:
                    SpawnPyroBurst(position, axis, layer, sizeClass, lodMul);
                    break;
            }

            if (inVacuum && fxKind != FxKind.UndockGas)
                SpawnVacuumDebris(position, axis, layer, sizeClass, lodMul);
        }

        private void SpawnPyroBurst(Vector3 position, Vector3 axis, int layer,
            FxSizeClass sizeClass, float lodMul)
        {
            PoolSlot slot = AcquirePyro(sizeClass);
            if (slot == null) return;

            Vector3 n = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up;
            Quaternion rot = Quaternion.LookRotation(n);
            ActivateSlot(slot, position, rot, layer);

            int idx = (int)sizeClass;
            int sparkCount = Mathf.Max(6, Mathf.RoundToInt(PyroSparkCounts[idx] * lodMul));
            int chunkCount = Mathf.Max(2, Mathf.RoundToInt(
                PyroChunkCounts[idx] * BlastFxRuntimeConfig.FragmentCountMultiplier * lodMul));
            int smokeCount = Mathf.Max(4, Mathf.RoundToInt(PyroSmokeCounts[idx] * lodMul));

            if (slot.Sparks != null) slot.Sparks.Emit(sparkCount);
            if (slot.Chunks != null) slot.Chunks.Emit(chunkCount);
            if (slot.Smoke  != null) slot.Smoke.Emit(smokeCount);

            if (!slot.Busy)
                activePoolSlotCount++;
            slot.Busy = true;
            slot.ReturnTime = Time.time + PyroReturnDelay;
            if (slot.ReturnTime < nextPoolReturnAt)
                nextPoolReturnAt = slot.ReturnTime;

            if (BlastFxConfig.DebugLogging)
            {
                BlastFxLog.DebugLog(Localizer.Format(
                    BlastFxLoc.LogPyroRing,
                    sizeClass,
                    lodMul.ToString("0.00", CultureInfo.InvariantCulture),
                    sparkCount,
                    chunkCount,
                    smokeCount));
            }
        }

        private void SpawnSoftPuff(Vector3 position, Vector3 axis, int layer,
            FxSizeClass sizeClass, float lodMul)
        {
            PoolSlot slot = AcquirePuff(sizeClass);
            if (slot == null) return;

            int idx = (int)sizeClass;
            float size01 = SizeToSize01[idx];
            float partRadius = SizeToRadius[idx];
            float rr = Mathf.Max(0.07f,
                (BlastFxRuntimeConfig.BaseRadius * 0.58f + partRadius * 0.62f)
                * Mathf.Lerp(1.00f, 1.14f, size01));

            Vector3 n = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up;
            Vector3 puffPos = position + n * (rr * Mathf.Lerp(0.50f, 0.70f, size01));
            Quaternion rot = Quaternion.LookRotation(n);
            ActivateSlot(slot, puffPos, rot, layer);

            int smokeCount = Mathf.Max(6, Mathf.RoundToInt(PuffSmokeCounts[idx] * lodMul));
            if (slot.Smoke != null) slot.Smoke.Emit(smokeCount);

            if (!slot.Busy)
                activePoolSlotCount++;
            slot.Busy = true;
            slot.ReturnTime = Time.time + PuffReturnDelay;
            if (slot.ReturnTime < nextPoolReturnAt)
                nextPoolReturnAt = slot.ReturnTime;

            if (BlastFxConfig.DebugLogging)
            {
                BlastFxLog.DebugLog(Localizer.Format(
                    BlastFxLoc.LogSoftPuff,
                    sizeClass,
                    lodMul.ToString("0.00", CultureInfo.InvariantCulture),
                    smokeCount));
            }
        }

        private void SpawnUndockGas(Vector3 position, Vector3 axis, int layer,
            FxSizeClass sizeClass, float lodMul)
        {
            PoolSlot slot = AcquireDock(sizeClass);
            if (slot == null) return;

            Vector3 n = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up;
            Quaternion rot = Quaternion.LookRotation(n);
            ActivateSlot(slot, position, rot, layer);

            int idx = (int)sizeClass;
            int smokeCount = Mathf.Max(4, Mathf.RoundToInt(DockGasCounts[idx] * lodMul));
            if (slot.Smoke != null) slot.Smoke.Emit(smokeCount);

            if (!slot.Busy)
                activePoolSlotCount++;
            slot.Busy = true;
            slot.ReturnTime = Time.time + DockReturnDelay;
            if (slot.ReturnTime < nextPoolReturnAt)
                nextPoolReturnAt = slot.ReturnTime;

            if (BlastFxConfig.DebugLogging)
            {
                BlastFxLog.DebugLog(Localizer.Format(
                    BlastFxLoc.LogUndockGas,
                    sizeClass,
                    lodMul.ToString("0.00", CultureInfo.InvariantCulture),
                    smokeCount));
            }
        }

        private void SpawnVacuumDebris(Vector3 position, Vector3 axis, int layer,
            FxSizeClass sizeClass, float lodMul)
        {
            PoolSlot slot = AcquireVacuum(sizeClass);
            if (slot == null) return;

            int idx = (int)sizeClass;
            Vector3 n = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, n);
            ActivateSlot(slot, position, rot, layer);

            int debrisCount = Mathf.Max(4, Mathf.RoundToInt(VacuumDebrisCounts[idx] * lodMul));
            if (slot.Chunks != null) slot.Chunks.Emit(debrisCount);

            if (!slot.Busy)
                activePoolSlotCount++;
            slot.Busy = true;
            slot.ReturnTime = Time.time + VacuumDebrisReturnDelay;
            if (slot.ReturnTime < nextPoolReturnAt)
                nextPoolReturnAt = slot.ReturnTime;

            if (BlastFxConfig.DebugLogging)
            {
                BlastFxLog.DebugLog(Localizer.Format(
                    BlastFxLoc.LogVacuumDebris,
                    sizeClass,
                    debrisCount));
            }
        }

        private bool ShouldSkipDespawnToPreserveShroud(Part ringPart)
        {
            if (ringPart == null || ringPart.flightID == 0u)
                return false;

            if (!byPart.TryGetValue(ringPart.flightID, out var state))
                return HasJettisonNeighbor(ringPart);

            double now = Planetarium.GetUniversalTime();
            if (state.HasJettisonNeighbor) return true;
            if (now - state.LastJettisonNeighborUt <= JettisonNeighborCacheSeconds) return true;
            return HasJettisonNeighbor(ringPart);
        }

        private void TrackHiddenRing(Part ringPart)
        {
            if (ringPart == null || ringPart.flightID == 0u) return;
            double now = Planetarium.GetUniversalTime();
            double keepSeconds = Math.Max(HiddenRingMinKeepSeconds,
                BlastFxRuntimeConfig.DespawnDelay + 3.0f);
            hiddenRings[ringPart.flightID] = new HiddenRingState
            {
                Part = ringPart,
                HiddenAtUt = now,
                EarliestCleanupUt = now + keepSeconds
            };
        }

        private void CleanupHiddenRings()
        {
            if (hiddenRings.Count == 0) return;

            if (!BlastFxRuntimeConfig.SmartHiddenRingCleanup)
            {
                hiddenRings.Clear();
                return;
            }

            double now = Planetarium.GetUniversalTime();
            Vessel activeVessel = FlightGlobals.ActiveVessel;
            bool hasActivePosition = activeVessel != null && activeVessel.transform != null;
            Vector3 activePosition = hasActivePosition
                ? activeVessel.transform.position : Vector3.zero;
            float cleanupDistanceSqr = BlastFxRuntimeConfig.HiddenRingCleanupDistance
                * BlastFxRuntimeConfig.HiddenRingCleanupDistance;

            hiddenRingRemoveIds.Clear();
            var e = hiddenRings.GetEnumerator();
            while (e.MoveNext())
            {
                HiddenRingState state = e.Current.Value;
                Part part = state != null ? state.Part : null;
                if (part == null || part.vessel == null)
                {
                    hiddenRingRemoveIds.Add(e.Current.Key);
                    continue;
                }

                if (part.vessel == activeVessel) continue;
                if (!IsPyroTarget(part))
                {
                    hiddenRingRemoveIds.Add(e.Current.Key);
                    continue;
                }

                if (now < state.EarliestCleanupUt) continue;
                if (!ShouldCleanupHiddenRing(part, state, now,
                    hasActivePosition, activePosition, cleanupDistanceSqr))
                    continue;

                if (TryDestroyHiddenRing(part))
                    hiddenRingRemoveIds.Add(e.Current.Key);
            }
            e.Dispose();

            for (int i = 0; i < hiddenRingRemoveIds.Count; i++)
                hiddenRings.Remove(hiddenRingRemoveIds[i]);
        }

        private static bool ShouldCleanupHiddenRing(Part part, HiddenRingState state,
            double now, bool hasActivePosition, Vector3 activePosition,
            float cleanupDistanceSqr)
        {
            if (part == null || state == null) return true;
            if (now - state.HiddenAtUt >= BlastFxRuntimeConfig.HiddenRingMaxLifetime) return true;

            Vessel vessel = part.vessel;
            if (vessel == null) return true;
            if (!vessel.loaded || part.transform == null)
                return now >= state.EarliestCleanupUt;

            if (!hasActivePosition) return false;
            Vector3 delta = part.transform.position - activePosition;
            return delta.sqrMagnitude >= cleanupDistanceSqr;
        }

        private bool TryDestroyHiddenRing(Part part)
        {
            if (part == null) return true;
            Vessel vessel = part.vessel;
            if (vessel == null) return true;
            if (!IsSinglePartTrackedVessel(vessel)) return false;

            SuppressFx(part, 8.0d);
            if (vessel.loaded) part.Die();
            else vessel.Die();
            return true;
        }

        private void SuppressFx(Part part, double seconds)
        {
            if (part == null || part.flightID == 0u) return;
            double now = Planetarium.GetUniversalTime();
            double until = now + Math.Max(0.05d, seconds);
            suppressedFxUntilByPart[part.flightID] = until;
        }

        private bool IsFxSuppressed(Part part)
        {
            if (part == null || part.flightID == 0u) return false;
            if (!suppressedFxUntilByPart.TryGetValue(part.flightID, out var until)) return false;
            double now = Planetarium.GetUniversalTime();
            if (now <= until) return true;
            suppressedFxUntilByPart.Remove(part.flightID);
            return false;
        }

        private void CleanupSuppression()
        {
            if (suppressedFxUntilByPart.Count == 0) return;
            double now = Planetarium.GetUniversalTime();
            staleIds.Clear();
            var e = suppressedFxUntilByPart.GetEnumerator();
            while (e.MoveNext())
            {
                if (e.Current.Value < now)
                    staleIds.Add(e.Current.Key);
            }
            e.Dispose();
            for (int i = 0; i < staleIds.Count; i++)
                suppressedFxUntilByPart.Remove(staleIds[i]);
        }

        private static Vector3 GetAxis(Part p, Vector3 fallback)
        {
            if (p == null || p.transform == null)
                return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.up;

            if (p.parent != null && p.parent.transform != null)
            {
                Vector3 d = p.transform.position - p.parent.transform.position;
                if (d.sqrMagnitude > 0.0001f) return d.normalized;
            }
            if (p.children != null)
            {
                for (int i = 0; i < p.children.Count; i++)
                {
                    Part c = p.children[i];
                    if (c == null || c.transform == null) continue;
                    Vector3 d = c.transform.position - p.transform.position;
                    if (d.sqrMagnitude > 0.0001f) return d.normalized;
                }
            }

            Vector3 up = p.transform.up;
            if (up.sqrMagnitude > 0.0001f) return up.normalized;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.up;
        }

        private static float EstimatePartRadius(Part p)
        {
            if (p == null) return 0.30f;
            try
            {
                Bounds bounds;
                if (KerbalFxUtil.TryGetPartColliderBounds(p, out bounds, true))
                    return Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z), 0.08f, 2.0f);
            }
            catch (Exception ex)
            {
                BlastFxLog.DebugException("estimate-part-radius", ex);
            }
            return 0.30f;
        }

    }
}
