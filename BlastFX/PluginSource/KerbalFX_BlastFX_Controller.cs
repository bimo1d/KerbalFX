using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using KSP.Localization;
using UnityEngine;

namespace KerbalFX.BlastFX
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    internal class BlastFxController : MonoBehaviour
    {
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
        }

        private sealed class HiddenRingState
        {
            public Part Part;
            public double HiddenAtUt;
            public double EarliestCleanupUt;
        }

        private readonly Dictionary<uint, State> byPart = new Dictionary<uint, State>(128);
        private readonly Dictionary<uint, double> suppressedFxUntilByPart = new Dictionary<uint, double>(64);
        private readonly Dictionary<uint, HiddenRingState> hiddenRings = new Dictionary<uint, HiddenRingState>(32);
        private readonly List<Part> trackedTargets = new List<Part>(256);
        private readonly HashSet<uint> seenIds = new HashSet<uint>(128);
        private readonly List<uint> staleIds = new List<uint>(128);
        private readonly List<uint> hiddenRingRemoveIds = new List<uint>(32);
        private float scanTimer;
        private float targetRefreshTimer;
        private float cfgTimer;
        private float hiddenRingCleanupTimer;
        private float boostedScanUntil;
        private const float ScanDt = 0.30f;
        private const float BoostedScanDt = 0.08f;
        private const float TargetRefreshDt = 0.75f;
        private const float TargetRefreshBoostedDt = 0.15f;
        private const float CfgDt = 0.5f;
        private const double HiddenRingMinKeepSeconds = 6.0d;
        private const double JettisonNeighborCacheSeconds = 5.0d;
        private const double JettisonNeighborProbeDt = 0.75d;

        private void Start()
        {
            BlastFxConfig.Refresh();
            BlastFxRuntime.Refresh();
            SubscribeEvents();
            RequestBoostedScan(4.0f);
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            trackedTargets.Clear();
            hiddenRings.Clear();
        }

        private void SubscribeEvents()
        {
            GameEvents.onPartDeCouple.Add(OnPartDecouple);
            GameEvents.onPartDeCoupleNewVesselComplete.Add(OnPartDecoupleNewVesselComplete);
            GameEvents.onPartDie.Add(OnPartDie);
            GameEvents.onPartUndock.Add(OnPartUndock);
        }

        private void UnsubscribeEvents()
        {
            GameEvents.onPartDeCouple.Remove(OnPartDecouple);
            GameEvents.onPartDeCoupleNewVesselComplete.Remove(OnPartDecoupleNewVesselComplete);
            GameEvents.onPartDie.Remove(OnPartDie);
            GameEvents.onPartUndock.Remove(OnPartUndock);
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
            if (!BlastFxRuntime.EnableModule) return;
            if (!BlastFxRuntime.DespawnDetachedRingVessel) return;
            TryDespawnSeparatedRingVessel(fromVessel);
            TryDespawnSeparatedRingVessel(toVessel);
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
            if (!IsTarget(ringPart)) return;

            SuppressFx(ringPart, BlastFxRuntime.DespawnDelay + 2.0f);
            if (BlastFxRuntime.HideDetachedRingVisualImmediately)
            {
                HidePartVisuals(ringPart);
            }

            if (ShouldSkipDespawnToPreserveShroud(ringPart))
            {
                TrackHiddenRing(ringPart);
                Log.Info(Localizer.Format(
                    BlastFxLoc.LogSkipDespawnPreserveShroud,
                    ringPart.partInfo != null ? ringPart.partInfo.name : "unknown"));
                return;
            }

            float despawnDelay = Mathf.Max(0f, BlastFxRuntime.DespawnDelay);
            if (despawnDelay <= 0.0001f)
            {
                if (ringPart != null && ringPart.vessel != null && ringPart.vessel.parts != null && ringPart.vessel.parts.Count == 1)
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
            if (part == null)
            {
                return;
            }

            DisableRenderers(part.FindModelComponents<Renderer>());
            StopParticles(part.FindModelComponents<ParticleSystem>());
            DisableLights(part.FindModelComponents<Light>());
            DisableColliders(part.FindModelComponents<Collider>());
        }

        private static void DisableRenderers(List<Renderer> renderers)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }
        }

        private static void StopParticles(List<ParticleSystem> particleSystems)
        {
            if (particleSystems == null) return;
            for (int i = 0; i < particleSystems.Count; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private static void DisableLights(List<Light> lights)
        {
            if (lights == null) return;
            for (int i = 0; i < lights.Count; i++)
            {
                Light light = lights[i];
                if (light != null)
                {
                    light.enabled = false;
                }
            }
        }

        private static void DisableColliders(List<Collider> colliders)
        {
            if (colliders == null) return;
            for (int i = 0; i < colliders.Count; i++)
            {
                Collider col = colliders[i];
                if (col != null)
                {
                    col.enabled = false;
                }
            }
        }

        private void TryTriggerFromEvent(Part part, string source)
        {
            if (!BlastFxConfig.Enabled || !BlastFxRuntime.EnableModule || part == null || !IsTarget(part))
            {
                return;
            }
            if (IsFxSuppressed(part))
            {
                return;
            }

            State state = GetOrCreateState(part.flightID);
            double ut = Planetarium.GetUniversalTime();
            if (ut - state.LastFxUt < BlastFxRuntime.TriggerCooldown)
            {
                return;
            }

            state.LastFxUt = ut;
            Vector3 axis = GetAxis(part, state.Axis);
            if (TrySpawnFromLivePart(part, state, axis, source))
            {
                return;
            }

            TrySpawnFromSnapshot(part, state, axis, source);
        }

        private State GetOrCreateState(uint flightId)
        {
            State state;
            if (!byPart.TryGetValue(flightId, out state))
            {
                state = new State();
                byPart[flightId] = state;
            }
            return state;
        }

        private static bool TrySpawnFromLivePart(Part part, State state, Vector3 axis, string source)
        {
            if (part == null || state == null || part.transform == null)
            {
                return false;
            }

            state.HasSnapshot = true;
            state.SnapshotPosition = part.transform.position;
            state.SnapshotLayer = part.gameObject != null ? part.gameObject.layer : 0;
            SpawnBurst(part, axis);
            string partName = part.partInfo != null ? part.partInfo.name : "unknown";
            Log.Info(Localizer.Format(BlastFxLoc.LogTriggerVia, source, partName));
            return true;
        }

        private static void TrySpawnFromSnapshot(Part part, State state, Vector3 axis, string source)
        {
            if (part == null || state == null || !state.HasSnapshot)
            {
                return;
            }

            float partRadius = EstimatePartRadius(part);
            string partName = part.partInfo != null ? part.partInfo.name : "unknown";
            SpawnBurst(state.SnapshotPosition, axis, state.SnapshotLayer, partRadius, partName);
            Log.Info(Localizer.Format(BlastFxLoc.LogTriggerViaSnapshot, source, partName));
        }

        private void Update()
        {
            cfgTimer -= Time.deltaTime;
            if (cfgTimer <= 0f)
            {
                cfgTimer = CfgDt;
                BlastFxConfig.Refresh();
                BlastFxRuntime.TryHotReload();
            }

            hiddenRingCleanupTimer -= Time.deltaTime;
            if (hiddenRingCleanupTimer <= 0f)
            {
                hiddenRingCleanupTimer = BlastFxRuntime.HiddenRingCleanupInterval;
                CleanupHiddenRings();
            }

            if (!BlastFxConfig.Enabled || !BlastFxRuntime.EnableModule) return;

            RefreshTrackedTargetsIfNeeded(Time.deltaTime);

            scanTimer -= Time.deltaTime;
            if (scanTimer > 0f) return;
            float scanInterval = Time.time < boostedScanUntil ? BoostedScanDt : ScanDt;
            scanTimer = scanInterval;
            Scan();
        }

        private void Scan()
        {
            if (trackedTargets.Count == 0)
            {
                byPart.Clear();
                return;
            }

            CleanupSuppression();
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
            if (targetRefreshTimer > 0f)
            {
                return;
            }

            targetRefreshTimer = Time.time < boostedScanUntil ? TargetRefreshBoostedDt : TargetRefreshDt;

            trackedTargets.Clear();
            List<Vessel> loaded = FlightGlobals.VesselsLoaded;
            if (loaded == null || loaded.Count == 0)
            {
                return;
            }

            for (int v = 0; v < loaded.Count; v++)
            {
                Vessel vessel = loaded[v];
                if (vessel == null || !vessel.loaded || vessel.packed || vessel.parts == null)
                {
                    continue;
                }

                for (int i = 0; i < vessel.parts.Count; i++)
                {
                    Part part = vessel.parts[i];
                    if (IsTarget(part))
                    {
                        trackedTargets.Add(part);
                    }
                }
            }
        }

        private void ProcessTargetPartFromScan(Part part, double scanUt)
        {
            if (!IsTarget(part))
            {
                return;
            }

            seenIds.Add(part.flightID);
            State state = GetOrCreateState(part.flightID);
            uint parentId = part.parent != null ? part.parent.flightID : 0u;
            int childCount = part.children != null ? part.children.Count : 0;

            UpdateJettisonNeighborCache(part, state, scanUt);
            Vector3 axis = GetAxis(part, state.Axis);
            UpdateScanSnapshot(part, state);
            TryTriggerFromStructureBreak(part, state, parentId, childCount, axis, scanUt);
            UpdateScanState(state, parentId, childCount, axis);
        }

        private static void UpdateScanSnapshot(Part part, State state)
        {
            state.HasSnapshot = true;
            state.SnapshotPosition = part.transform != null ? part.transform.position : state.SnapshotPosition;
            state.SnapshotLayer = part.gameObject != null ? part.gameObject.layer : state.SnapshotLayer;
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

        private static void UpdateScanState(State state, uint parentId, int childCount, Vector3 axis)
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

        private void TryTriggerFromStructureBreak(Part part, State state, uint parentId, int childCount, Vector3 axis, double scanUt)
        {
            if (!state.Init || !HasStructureBreak(state, parentId, childCount))
            {
                return;
            }

            if (scanUt - state.LastFxUt < BlastFxRuntime.TriggerCooldown)
            {
                return;
            }

            state.LastFxUt = scanUt;
            SpawnBurst(part, axis);
            string partName = part.partInfo != null ? part.partInfo.name : "unknown";
            Log.Info(Localizer.Format(BlastFxLoc.LogTriggerViaScan, partName));
        }

        private void PruneMissingPartStates()
        {
            staleIds.Clear();
            foreach (KeyValuePair<uint, State> kv in byPart)
            {
                if (!seenIds.Contains(kv.Key))
                {
                    staleIds.Add(kv.Key);
                }
            }
            for (int i = 0; i < staleIds.Count; i++)
            {
                byPart.Remove(staleIds[i]);
            }
        }

        private static bool IsTarget(Part p)
        {
            if (p == null || p.partInfo == null || p.Modules == null) return false;
            if (!HasDecouplerModule(p)) return false;
            if (MatchesTargetToken(p)) return true;
            return IsLikelyStockTsSeparator(p);
        }

        private static bool HasDecouplerModule(Part p)
        {
            for (int i = 0; i < p.Modules.Count; i++)
            {
                PartModule m = p.Modules[i];
                if (m == null) continue;
                string tn = m.GetType().Name;
                if (!string.IsNullOrEmpty(tn) && (tn.IndexOf("Decouple", StringComparison.OrdinalIgnoreCase) >= 0 || tn.IndexOf("Decoupler", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool MatchesTargetToken(Part p)
        {
            string target = BlastFxRuntime.TargetPrefix;
            if (string.IsNullOrWhiteSpace(target))
            {
                return true;
            }

            string name = p.partInfo != null ? p.partInfo.name : string.Empty;
            string rawTitle = p.partInfo != null ? p.partInfo.title : string.Empty;
            string localizedTitle = null;
            bool hasLocalizedKey = !string.IsNullOrEmpty(rawTitle) && rawTitle.StartsWith("#", StringComparison.Ordinal);

            string[] tokens = BlastFxRuntime.TargetTokens;
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (string.IsNullOrEmpty(token)) continue;
                if (ContainsIgnoreCase(name, token) || ContainsIgnoreCase(rawTitle, token))
                {
                    return true;
                }

                if (!hasLocalizedKey)
                {
                    continue;
                }

                if (localizedTitle == null)
                {
                    localizedTitle = Localizer.Format(rawTitle);
                }

                if (ContainsIgnoreCase(localizedTitle, token))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsLikelyStockTsSeparator(Part p)
        {
            string cfgToken = BlastFxRuntime.TargetPrefix;
            if (string.IsNullOrWhiteSpace(cfgToken) || cfgToken.IndexOf("TS", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            string name = p.partInfo != null ? p.partInfo.name : string.Empty;
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (name.StartsWith("Separator_", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return name.IndexOf("restock-separator-", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasJettisonNeighbor(Part part)
        {
            if (part == null)
            {
                return false;
            }

            if (PartHasModule(part.parent, "ModuleJettison"))
            {
                return true;
            }

            if (part.children == null)
            {
                return false;
            }

            for (int i = 0; i < part.children.Count; i++)
            {
                if (PartHasModule(part.children[i], "ModuleJettison"))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PartHasModule(Part part, string moduleName)
        {
            if (part == null || part.Modules == null || string.IsNullOrEmpty(moduleName))
            {
                return false;
            }

            for (int i = 0; i < part.Modules.Count; i++)
            {
                PartModule module = part.Modules[i];
                if (module == null)
                {
                    continue;
                }

                string runtimeName = module.moduleName;
                if (!string.IsNullOrEmpty(runtimeName) && string.Equals(runtimeName, moduleName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                string typeName = module.GetType().Name;
                if (!string.IsNullOrEmpty(typeName) && string.Equals(typeName, moduleName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return !string.IsNullOrEmpty(source)
                && !string.IsNullOrEmpty(value)
                && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool ShouldSkipDespawnToPreserveShroud(Part ringPart)
        {
            if (ringPart == null || ringPart.flightID == 0u)
            {
                return false;
            }

            State state;
            if (!byPart.TryGetValue(ringPart.flightID, out state))
            {
                return HasJettisonNeighbor(ringPart);
            }

            double now = Planetarium.GetUniversalTime();
            if (state.HasJettisonNeighbor)
            {
                return true;
            }

            if (now - state.LastJettisonNeighborUt <= JettisonNeighborCacheSeconds)
            {
                return true;
            }

            return HasJettisonNeighbor(ringPart);
        }

        private void TrackHiddenRing(Part ringPart)
        {
            if (ringPart == null || ringPart.flightID == 0u)
            {
                return;
            }

            double now = Planetarium.GetUniversalTime();
            double keepSeconds = Math.Max(HiddenRingMinKeepSeconds, BlastFxRuntime.DespawnDelay + 3.0f);
            hiddenRings[ringPart.flightID] = new HiddenRingState
            {
                Part = ringPart,
                HiddenAtUt = now,
                EarliestCleanupUt = now + keepSeconds
            };
        }

        private void CleanupHiddenRings()
        {
            if (hiddenRings.Count == 0)
            {
                return;
            }

            if (!BlastFxRuntime.SmartHiddenRingCleanup)
            {
                hiddenRings.Clear();
                return;
            }

            double now = Planetarium.GetUniversalTime();
            Vessel activeVessel = FlightGlobals.ActiveVessel;
            bool hasActivePosition = activeVessel != null && activeVessel.transform != null;
            Vector3 activePosition = hasActivePosition ? activeVessel.transform.position : Vector3.zero;
            float cleanupDistanceSqr = BlastFxRuntime.HiddenRingCleanupDistance * BlastFxRuntime.HiddenRingCleanupDistance;

            hiddenRingRemoveIds.Clear();
            foreach (KeyValuePair<uint, HiddenRingState> pair in hiddenRings)
            {
                HiddenRingState state = pair.Value;
                Part part = state != null ? state.Part : null;
                if (part == null || !IsSinglePartVessel(part.vessel))
                {
                    hiddenRingRemoveIds.Add(pair.Key);
                    continue;
                }

                if (part.vessel == activeVessel)
                {
                    continue;
                }

                if (!IsTarget(part))
                {
                    hiddenRingRemoveIds.Add(pair.Key);
                    continue;
                }

                if (now < state.EarliestCleanupUt)
                {
                    continue;
                }

                if (!ShouldCleanupHiddenRing(part, state, now, hasActivePosition, activePosition, cleanupDistanceSqr))
                {
                    continue;
                }

                SuppressFx(part, 8.0d);
                part.Die();
                hiddenRingRemoveIds.Add(pair.Key);
            }

            for (int i = 0; i < hiddenRingRemoveIds.Count; i++)
            {
                hiddenRings.Remove(hiddenRingRemoveIds[i]);
            }
        }

        private static bool ShouldCleanupHiddenRing(
            Part part,
            HiddenRingState state,
            double now,
            bool hasActivePosition,
            Vector3 activePosition,
            float cleanupDistanceSqr)
        {
            if (part == null || state == null)
            {
                return true;
            }

            if (now - state.HiddenAtUt >= BlastFxRuntime.HiddenRingMaxLifetime)
            {
                return true;
            }

            if (!hasActivePosition || part.transform == null)
            {
                return false;
            }

            Vector3 delta = part.transform.position - activePosition;
            return delta.sqrMagnitude >= cleanupDistanceSqr;
        }

        private void SuppressFx(Part part, double seconds)
        {
            if (part == null || part.flightID == 0u)
            {
                return;
            }

            double now = Planetarium.GetUniversalTime();
            double until = now + Math.Max(0.05d, seconds);
            suppressedFxUntilByPart[part.flightID] = until;
        }

        private bool IsFxSuppressed(Part part)
        {
            if (part == null || part.flightID == 0u)
            {
                return false;
            }

            double until;
            if (!suppressedFxUntilByPart.TryGetValue(part.flightID, out until))
            {
                return false;
            }

            double now = Planetarium.GetUniversalTime();
            if (now <= until)
            {
                return true;
            }

            suppressedFxUntilByPart.Remove(part.flightID);
            return false;
        }

        private void CleanupSuppression()
        {
            if (suppressedFxUntilByPart.Count == 0)
            {
                return;
            }

            double now = Planetarium.GetUniversalTime();
            staleIds.Clear();
            foreach (KeyValuePair<uint, double> kv in suppressedFxUntilByPart)
            {
                if (kv.Value < now)
                {
                    staleIds.Add(kv.Key);
                }
            }

            for (int i = 0; i < staleIds.Count; i++)
            {
                suppressedFxUntilByPart.Remove(staleIds[i]);
            }
        }

        private static Vector3 GetAxis(Part p, Vector3 fallback)
        {
            if (p == null || p.transform == null) return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.up;
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

        private static void SpawnBurst(Part part, Vector3 axis)
        {
            if (part == null || part.transform == null) return;
            int layer = part.gameObject != null ? part.gameObject.layer : 0;
            float partRadius = EstimatePartRadius(part);
            string partName = part.partInfo != null ? part.partInfo.name : "unknown";
            SpawnBurst(part.transform.position, axis, layer, partRadius, partName);
        }

        private static void SpawnBurst(Vector3 position, Vector3 axis, int layer, float partRadius, string partName)
        {
            Vector3 n = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up;
            float size01 = Mathf.InverseLerp(0.10f, 1.60f, Mathf.Max(0.05f, partRadius));
            float detonationScale = Mathf.Lerp(0.90f, 1.90f, Mathf.Pow(size01, 0.85f));
            float ringScale = Mathf.Lerp(0.98f, 1.16f, size01);
            float rr = Mathf.Max(0.08f, (BlastFxRuntime.BaseRadius + partRadius * BlastFxRuntime.RadiusFromPart) * ringScale);
            int sparkCount = Mathf.Clamp(Mathf.RoundToInt(BlastFxRuntime.SparkCount * detonationScale), 1, 8000);
            int smokeCount = Mathf.Clamp(Mathf.RoundToInt(BlastFxRuntime.SmokeCount * Mathf.Lerp(0.92f, 1.65f, size01)), 0, 6000);
            int chunkCount = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(24f, 132f, size01)), 8, 220);
            float sparkSpeedScale = Mathf.Lerp(0.92f, 1.68f, size01);
            float smokeSpeedScale = Mathf.Lerp(0.92f, 1.44f, size01);
            float chunkSpeedScale = Mathf.Lerp(0.95f, 1.55f, size01);
            float sparkSizeScale = Mathf.Lerp(0.90f, 1.34f, size01);
            float smokeSizeScale = Mathf.Lerp(0.96f, 1.48f, size01);
            float chunkSizeScale = Mathf.Lerp(1.00f, 2.30f, size01);
            chunkCount = Mathf.Clamp(Mathf.RoundToInt(chunkCount * BlastFxRuntime.FragmentCountMultiplier), 6, 320);
            chunkSpeedScale *= BlastFxRuntime.FragmentSpeedMultiplier;

            GameObject root = new GameObject("KerbalFX_BlastFX_PyroRing");
            root.layer = layer;
            root.transform.position = position;
            root.transform.rotation = Quaternion.LookRotation(n);

            ParticleSystem sparks = CreateSparks(root.transform, root.layer, rr, sparkCount, sparkSpeedScale, sparkSizeScale);
            ParticleSystem chunks = CreateChunks(root.transform, root.layer, rr, chunkCount, chunkSpeedScale, chunkSizeScale);
            ParticleSystem smoke = CreateSmoke(root.transform, root.layer, rr, smokeCount, smokeSpeedScale, smokeSizeScale);
            if (sparks != null)
            {
                sparks.Play(true);
                sparks.Emit(sparkCount);
            }
            if (chunks != null)
            {
                chunks.Play(true);
                chunks.Emit(chunkCount);
            }
            if (smoke != null)
            {
                smoke.Play(true);
                if (smokeCount > 0)
                {
                    smoke.Emit(smokeCount);
                }
            }

            Log.Info(Localizer.Format(
                BlastFxLoc.LogPyroRing,
                partName,
                rr.ToString("0.00", CultureInfo.InvariantCulture),
                size01.ToString("0.00", CultureInfo.InvariantCulture),
                sparkCount.ToString(CultureInfo.InvariantCulture),
                chunkCount.ToString(CultureInfo.InvariantCulture),
                smokeCount.ToString(CultureInfo.InvariantCulture)));
            Destroy(root, BlastFxRuntime.Cleanup);
        }

        private static ParticleSystem CreateSparks(Transform parent, int layer, float ringRadius, int sparkCount, float speedScale, float sizeScale)
        {
            GameObject go = new GameObject("BlastFX_Sparks");
            go.transform.SetParent(parent, false);
            go.layer = layer;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Clamp(sparkCount * 4, 64, 8192);
            main.startLifetime = new ParticleSystem.MinMaxCurve(BlastFxRuntime.SparkLife * 0.55f, BlastFxRuntime.SparkLife * 1.20f * Mathf.Lerp(0.95f, 1.12f, sizeScale - 0.9f));
            main.startSpeed = new ParticleSystem.MinMaxCurve(BlastFxRuntime.SparkSpeed * 0.70f * speedScale, BlastFxRuntime.SparkSpeed * 1.55f * speedScale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f * sizeScale, 0.055f * sizeScale);
            main.startRotation = new ParticleSystem.MinMaxCurve(-3.14159f, 3.14159f);

            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;

            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = ringRadius;
            sh.radiusThickness = 0.06f;
            sh.arc = 360f;

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.radial = new ParticleSystem.MinMaxCurve(BlastFxRuntime.SparkSpeed * 0.40f * speedScale, BlastFxRuntime.SparkSpeed * 1.00f * speedScale);
            vel.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.28f, 0.28f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1.00f, 0.96f, 0.84f), 0.00f),
                    new GradientColorKey(new Color(1.00f, 0.68f, 0.20f), 0.12f),
                    new GradientColorKey(new Color(0.96f, 0.26f, 0.10f), 0.36f),
                    new GradientColorKey(new Color(0.25f, 0.25f, 0.25f), 1.00f)
                },
                new[] {
                    new GradientAlphaKey(1.00f, 0.00f),
                    new GradientAlphaKey(0.92f, 0.12f),
                    new GradientAlphaKey(0.35f, 0.55f),
                    new GradientAlphaKey(0.00f, 1.00f)
                });
            col.color = g;

            ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.22f;
            r.lengthScale = 1.18f;
            r.material = BlastFxAssets.GetSparkMaterial();
            return ps;
        }

        private static ParticleSystem CreateChunks(Transform parent, int layer, float ringRadius, int chunkCount, float speedScale, float sizeScale)
        {
            GameObject go = new GameObject("BlastFX_Chunks");
            go.transform.SetParent(parent, false);
            go.layer = layer;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Clamp(chunkCount * 4, 24, 2048);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.72f, 1.92f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(BlastFxRuntime.SparkSpeed * 0.26f * speedScale, BlastFxRuntime.SparkSpeed * 0.72f * speedScale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f * sizeScale, 0.22f * sizeScale);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.04f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-3.14159f, 3.14159f);

            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;

            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = ringRadius;
            sh.radiusThickness = 0.04f;
            sh.arc = 360f;

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.radial = new ParticleSystem.MinMaxCurve(BlastFxRuntime.SparkSpeed * 0.55f * speedScale, BlastFxRuntime.SparkSpeed * 1.35f * speedScale);
            vel.x = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.14f, 0.14f);

            ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.separateAxes = false;
            limit.space = ParticleSystemSimulationSpace.Local;
            limit.limit = new ParticleSystem.MinMaxCurve(Mathf.Lerp(3.2f, 8.6f, Mathf.Clamp01(sizeScale / 2.2f)));
            limit.dampen = 0.62f;

            ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-16.0f, 16.0f);

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1.00f, 0.84f, 0.46f), 0.00f),
                    new GradientColorKey(new Color(0.98f, 0.46f, 0.18f), 0.20f),
                    new GradientColorKey(new Color(0.36f, 0.36f, 0.36f), 0.66f),
                    new GradientColorKey(new Color(0.16f, 0.16f, 0.16f), 1.00f)
                },
                new[] {
                    new GradientAlphaKey(1.00f, 0.00f),
                    new GradientAlphaKey(0.90f, 0.22f),
                    new GradientAlphaKey(0.62f, 0.65f),
                    new GradientAlphaKey(0.18f, 0.90f),
                    new GradientAlphaKey(0.00f, 1.00f)
                });
            col.color = g;

            ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = BlastFxAssets.GetChunkMesh();
            r.material = BlastFxAssets.GetChunkMaterial();
            return ps;
        }

        private static ParticleSystem CreateSmoke(Transform parent, int layer, float ringRadius, int smokeCount, float speedScale, float sizeScale)
        {
            GameObject go = new GameObject("BlastFX_Smoke");
            go.transform.SetParent(parent, false);
            go.layer = layer;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Clamp(smokeCount * 6, 64, 8192);
            main.startLifetime = new ParticleSystem.MinMaxCurve(BlastFxRuntime.SmokeLife * 0.75f, BlastFxRuntime.SmokeLife * 1.30f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(BlastFxRuntime.SmokeSpeed * 0.55f * speedScale, BlastFxRuntime.SmokeSpeed * 1.25f * speedScale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f * sizeScale, 0.42f * sizeScale);
            main.startRotation = new ParticleSystem.MinMaxCurve(-3.14159f, 3.14159f);

            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = 0f;

            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = ringRadius * 1.02f;
            sh.radiusThickness = 0.34f;
            sh.arc = 360f;

            ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.radial = new ParticleSystem.MinMaxCurve(BlastFxRuntime.SmokeSpeed * 0.35f * speedScale, BlastFxRuntime.SmokeSpeed * 1.05f * speedScale);
            vel.x = new ParticleSystem.MinMaxCurve(-0.32f, 0.32f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.16f, 0.16f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.22f, 0.22f);

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.50f, 0.50f, 0.50f), 0.00f),
                    new GradientColorKey(new Color(0.36f, 0.36f, 0.36f), 0.45f),
                    new GradientColorKey(new Color(0.28f, 0.28f, 0.28f), 1.00f)
                },
                new[] {
                    new GradientAlphaKey(0.42f, 0.00f),
                    new GradientAlphaKey(0.26f, 0.35f),
                    new GradientAlphaKey(0.08f, 0.75f),
                    new GradientAlphaKey(0.00f, 1.00f)
                });
            col.color = g;

            ParticleSystemRenderer r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.material = BlastFxAssets.GetSmokeMaterial();
            return ps;
        }

        private static float EstimatePartRadius(Part p)
        {
            if (p == null) return 0.30f;
            try
            {
                Bounds b = new Bounds(p.transform.position, Vector3.zero);
                bool has = false;
                List<Collider> cs = p.FindModelComponents<Collider>();
                if (cs != null)
                {
                    for (int i = 0; i < cs.Count; i++)
                    {
                        Collider c = cs[i];
                        if (c == null || !c.enabled) continue;
                        if (!has) { b = c.bounds; has = true; }
                        else b.Encapsulate(c.bounds);
                    }
                }
                if (has) return Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z), 0.08f, 2.0f);
            }
            catch (Exception ex)
            {
                Log.DebugException("estimate-part-radius", ex);
            }
            return 0.30f;
        }
    }
}

