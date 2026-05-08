using System;
using System.Collections;
using System.Collections.Generic;
using KSP.Localization;
using Unity.Profiling;
using UnityEngine;

namespace KerbalFX.BlastFX
{
    internal partial class BlastFxController
    {
        private sealed class KcsExplosionSlot
        {
            public GameObject Root;
            public Transform[] Transforms;
            public ParticleSystem[] Systems;
            public AudioSource[] AudioSources;
            public bool Busy;
            public float ReturnTime;
        }

        private struct KcsExplosionEvent
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public int Layer;
            public float Time;
            public bool StockFxSuppressed;
        }

        private const float VacuumDebrisSizeScale = 0.7f;
        private const float VacuumDebrisShellRadiusScale = 5.0f;
        private const float VacuumDebrisTravelScale = 0.165f;
        private const float VacuumDebrisLifetimeMin = 5f;
        private const float VacuumDebrisLifetimeMax = 10f;

        private static class KcsExplosionAssets
        {
            private const float VacuumDebrisCloneEmissionScale = 0.5f;
            private static bool attempted;
            private static GameObject cachedPrefab;
            private static int cachedRevision = -1;
            private static bool loggedLoaded;

            public static bool TryGetPrefab(out GameObject prefab)
            {
                if (cachedRevision != BlastFxRuntimeConfig.Revision)
                {
                    cachedRevision = BlastFxRuntimeConfig.Revision;
                    attempted = false;
                    cachedPrefab = null;
                    loggedLoaded = false;
                }

                prefab = cachedPrefab;
                if (prefab != null) return true;
                if (attempted) return false;

                attempted = true;
                try
                {
                    string bundleName = string.IsNullOrEmpty(BlastFxRuntimeConfig.VacuumExplosionBundle)
                        ? "kcseffects"
                        : BlastFxRuntimeConfig.VacuumExplosionBundle;
                    string bundlePath = System.IO.Path.Combine(
                        KSPUtil.ApplicationRootPath,
                        "GameData",
                        "KerbalFX",
                        "BlastFX",
                        "AssetBundles",
                        bundleName);

                    var bundle = AssetBundle.LoadFromFile(bundlePath);
                    if (bundle == null)
                    {
                        BlastFxLog.Info(Localizer.Format(
                            BlastFxLoc.LogVacuumAssetBundleFailed, bundlePath));
                        return false;
                    }

                    string prefabName = string.IsNullOrEmpty(BlastFxRuntimeConfig.VacuumExplosionPrefab)
                        ? "Explosion"
                        : BlastFxRuntimeConfig.VacuumExplosionPrefab;

                    cachedPrefab = bundle.LoadAsset<GameObject>(prefabName);
                    bundle.Unload(false);

                    if (cachedPrefab == null)
                    {
                        BlastFxLog.Info(Localizer.Format(
                            BlastFxLoc.LogVacuumPrefabMissing, prefabName));
                        return false;
                    }

                    if (!loggedLoaded)
                    {
                        loggedLoaded = true;
                        BlastFxLog.Info(Localizer.Format(
                            BlastFxLoc.LogVacuumPrefabLoaded, prefabName, bundleName));
                    }
                }
                catch (Exception ex)
                {
                    BlastFxLog.DebugException("vacuum-explosion-assets", ex);
                    return false;
                }

                prefab = cachedPrefab;
                return prefab != null;
            }

            private static Transform FindExplosionDepthChild(Transform root, string leafName)
            {
                if (root == null || string.IsNullOrEmpty(leafName)) return null;

                Transform direct = root.Find(leafName);
                if (direct != null) return direct;

                Transform[] all = root.GetComponentsInChildren<Transform>(true);
                if (all == null) return null;

                for (int i = 0; i < all.Length; i++)
                {
                    Transform node = all[i];
                    if (node == null) continue;
                    if (string.Equals(node.name, leafName, StringComparison.OrdinalIgnoreCase))
                        return node;
                }

                return null;
            }

            public static ParticleSystem TryCloneVacuumDebrisForPool(Transform parent, int layer)
            {
                if (!BlastFxRuntimeConfig.UseBundleVacuumDebrisMaterial)
                    return null;
                if (parent == null)
                    return null;
                if (!TryGetPrefab(out GameObject prefab) || prefab == null)
                    return null;

                string configured = BlastFxRuntimeConfig.VacuumDebrisMaterialTransform;
                if (string.IsNullOrEmpty(configured))
                    configured = "Debris";

                string[] candidates =
                {
                    configured,
                    "Debris",
                    "Sparks",
                    "Spark"
                };

                for (int c = 0; c < candidates.Length; c++)
                {
                    string nm = candidates[c];
                    if (string.IsNullOrEmpty(nm)) continue;

                    bool duplicate = false;
                    for (int p = 0; p < c; p++)
                    {
                        if (string.Equals(nm, candidates[p], StringComparison.OrdinalIgnoreCase))
                        {
                            duplicate = true;
                            break;
                        }
                    }
                    if (duplicate) continue;

                    Transform tr = FindExplosionDepthChild(prefab.transform, nm);
                    if (tr == null) continue;

                    ParticleSystem srcPs = tr.GetComponent<ParticleSystem>();
                    if (srcPs == null) continue;

                    GameObject cloneGO = UnityEngine.Object.Instantiate(tr.gameObject, parent, false);
                    cloneGO.name = "BlastFX_VacuumDebris_BundlePrefab";
                    cloneGO.transform.localPosition = Vector3.zero;
                    cloneGO.transform.localRotation = Quaternion.identity;
                    cloneGO.transform.localScale = Vector3.one;
                    SetLayerRecursive(cloneGO, layer);
                    ConfigureVacuumDebrisClone(cloneGO);

                    ParticleSystem clonePs = cloneGO.GetComponent<ParticleSystem>();
                    if (clonePs == null)
                    {
                        UnityEngine.Object.Destroy(cloneGO);
                        continue;
                    }

                    return clonePs;
                }

                return null;
            }

            private static void ConfigureVacuumDebrisClone(GameObject root)
            {
                if (root == null) return;

                ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
                if (systems == null) return;

                for (int i = 0; i < systems.Length; i++)
                {
                    ParticleSystem ps = systems[i];
                    if (ps == null) continue;

                    var main = ps.main;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    if (main.startSize3D)
                    {
                        main.startSizeXMultiplier *= VacuumDebrisSizeScale;
                        main.startSizeYMultiplier *= VacuumDebrisSizeScale;
                        main.startSizeZMultiplier *= VacuumDebrisSizeScale;
                    }
                    else
                    {
                        main.startSizeMultiplier *= VacuumDebrisSizeScale;
                    }
                    main.startSpeed = new ParticleSystem.MinMaxCurve(
                        BlastFxRuntimeConfig.SparkSpeed * 0.58f * VacuumDebrisTravelScale,
                        BlastFxRuntimeConfig.SparkSpeed * 1.28f * VacuumDebrisTravelScale);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(
                        VacuumDebrisLifetimeMin, VacuumDebrisLifetimeMax);
                    main.gravityModifier = new ParticleSystem.MinMaxCurve(0f);
                    main.maxParticles = Mathf.Max(1,
                        Mathf.RoundToInt(main.maxParticles * VacuumDebrisCloneEmissionScale));

                    var shape = ps.shape;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius *= VacuumDebrisShellRadiusScale;
                    shape.radiusThickness = 0f;
                    shape.randomDirectionAmount = 0.35f;
                    shape.sphericalDirectionAmount = 1.0f;

                    var velocity = ps.velocityOverLifetime;
                    velocity.enabled = true;
                    velocity.space = ParticleSystemSimulationSpace.Local;
                    velocity.radial = new ParticleSystem.MinMaxCurve(
                        BlastFxRuntimeConfig.SparkSpeed * 0.95f * VacuumDebrisTravelScale,
                        BlastFxRuntimeConfig.SparkSpeed * 1.95f * VacuumDebrisTravelScale);
                    velocity.x = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);
                    velocity.y = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);
                    velocity.z = new ParticleSystem.MinMaxCurve(-0.14f, 0.14f);

                    var inheritVelocity = ps.inheritVelocity;
                    inheritVelocity.enabled = false;

                    var externalForces = ps.externalForces;
                    externalForces.enabled = false;

                    var limitVelocity = ps.limitVelocityOverLifetime;
                    limitVelocity.enabled = false;

                    var forceOverLifetime = ps.forceOverLifetime;
                    forceOverLifetime.enabled = false;

                    var noise = ps.noise;
                    noise.enabled = false;

                    var emission = ps.emission;
                    emission.enabled = false;
                }
            }
        }

        private readonly List<KcsExplosionSlot> kcsSlots = new List<KcsExplosionSlot>(6);
        private readonly List<KcsExplosionEvent> kcsRecent = new List<KcsExplosionEvent>(10);
        private readonly List<KcsExplosionEvent> kcsQueue = new List<KcsExplosionEvent>(6);
        private readonly List<KcsExplosionEvent> kcsBatch = new List<KcsExplosionEvent>(6);
        private float kcsNextProcessAt;
        private float kcsTelemetryNextAt;
        private static int cachedCanSpawnKcsRevision = -1;
        private static bool cachedCanSpawnKcs;
        private static readonly ProfilerMarker KcsQueueMarker =
            new ProfilerMarker("KerbalFX.BlastFX.KCS.Queue");
        private static readonly ProfilerMarker KcsProcessMarker =
            new ProfilerMarker("KerbalFX.BlastFX.KCS.Process");
        private static readonly ProfilerMarker KcsSpawnMarker =
            new ProfilerMarker("KerbalFX.BlastFX.KCS.Spawn");
        private static readonly ProfilerMarker KcsBuildSlotMarker =
            new ProfilerMarker("KerbalFX.BlastFX.KCS.BuildSlot");

        private void KcsVacuumExplosionInit()
        {
            kcsNextProcessAt = 0f;
            kcsTelemetryNextAt = 0f;
            kcsRecent.Clear();
            kcsQueue.Clear();
            if (BlastFxRuntimeConfig.VacuumExplosionPreload)
                StartCoroutine(PreloadKcsAssetsAndPrewarm());
        }

        private System.Collections.IEnumerator PreloadKcsAssetsAndPrewarm()
        {
            yield return null;

            if (!kcsVacuumExplosionsEnabled)
                yield break;

            if (BlastFxRuntimeConfig.VacuumExplosionPreload)
            {
                if (KcsExplosionAssets.TryGetPrefab(out _))
                    BlastFxLog.Info(Localizer.Format(BlastFxLoc.LogVacuumPreload, "OK"));
                else
                    BlastFxLog.Info(Localizer.Format(BlastFxLoc.LogVacuumPreload, "FAIL"));
            }

            int target = Mathf.Max(0, BlastFxRuntimeConfig.VacuumExplosionPrewarmSlots);
            for (int i = 0; i < target; i++)
            {
                if (!kcsVacuumExplosionsEnabled)
                    yield break;
                KcsExplosionSlot slot = BuildKcsSlot();
                if (slot == null)
                    break;
                kcsSlots.Add(slot);
                yield return null;
            }

            if (BlastFxConfig.DebugLogging)
                BlastFxLog.DebugLog(Localizer.Format(
                    BlastFxLoc.LogVacuumPrewarmSlots, kcsSlots.Count));

            if (BlastFxRuntimeConfig.VacuumExplosionWarmupDraw && kcsVacuumExplosionsEnabled)
                yield return StartCoroutine(WarmupKcsExplosionInvisibleDraw());
        }

        private IEnumerator WarmupKcsExplosionInvisibleDraw()
        {
            yield return null;
            yield return null;

            if (!kcsVacuumExplosionsEnabled)
                yield break;
            if (!KcsExplosionAssets.TryGetPrefab(out GameObject tpl) || tpl == null)
                yield break;

            Camera cam = FlightCamera.fetch != null
                ? FlightCamera.fetch.mainCamera
                : Camera.main;

            GameObject warmup = Instantiate(tpl);
            warmup.name = "KerbalFX_BlastFX_KcsWarmupDiscard";
            warmup.hideFlags |= HideFlags.HideAndDontSave;

            warmup.transform.localScale = Vector3.one * 0.008f;

            Transform camTr = cam != null ? cam.transform : null;
            float distBehind = 320f;
            if (cam != null && cam.farClipPlane > 10f)
            {
                float fromFar = cam.farClipPlane * 0.012f;
                distBehind = Mathf.Clamp(fromFar, 220f, 1600f);
            }

            if (camTr != null)
            {
                warmup.transform.position = camTr.position - camTr.forward * distBehind;
            }
            else
            {
                warmup.transform.position = new Vector3(0f, 0f, -distBehind);
            }

            AudioSource[] warmAud = warmup.GetComponentsInChildren<AudioSource>(true);
            if (warmAud != null)
            {
                for (int a = 0; a < warmAud.Length; a++)
                {
                    AudioSource snd = warmAud[a];
                    if (snd == null) continue;
                    snd.mute = true;
                    snd.volume = 0f;
                    if (snd.spatialBlend < 0.98f)
                        snd.spatialBlend = 1f;
                }
            }

            ParticleSystem[] warmPs =
                warmup.GetComponentsInChildren<ParticleSystem>(true);

            warmup.SetActive(true);

            yield return null;

            if (warmPs != null)
            {
                for (int i = 0; i < warmPs.Length; i++)
                {
                    ParticleSystem ps = warmPs[i];
                    if (ps == null) continue;
                    ps.Clear(true);
                    ps.Play(true);
                    yield return null;
                }
            }

            if (warmAud != null)
            {
                for (int a = 0; a < warmAud.Length; a++)
                {
                    AudioSource snd = warmAud[a];
                    if (snd == null) continue;
                    if (snd.clip == null) continue;
                    snd.Play();
                }

                yield return null;
            }

            const int settleFrames = 6;
            for (int i = 0; i < settleFrames; i++)
                yield return null;

            if (warmPs != null)
            {
                for (int i = 0; i < warmPs.Length; i++)
                {
                    ParticleSystem ps = warmPs[i];
                    if (ps == null) continue;
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            if (warmAud != null)
            {
                for (int a = 0; a < warmAud.Length; a++)
                {
                    AudioSource snd = warmAud[a];
                    if (snd == null) continue;
                    snd.Stop();
                }
            }

            Destroy(warmup);
            BlastFxLog.Info(Localizer.Format(BlastFxLoc.LogVacuumWarmupComplete));
        }

        private void KcsVacuumExplosionDestroy()
        {
            StopKcsVacuumExplosions();
            for (int i = 0; i < kcsSlots.Count; i++)
            {
                if (kcsSlots[i] != null && kcsSlots[i].Root != null)
                    Destroy(kcsSlots[i].Root);
            }
            kcsSlots.Clear();
            kcsRecent.Clear();
            kcsQueue.Clear();
            kcsBatch.Clear();
        }

        private bool TryQueueKcsVacuumExplosion(Part part, bool stockFxSuppressed = false)
        {
            KcsQueueMarker.Begin();
            try
            {
                if (!BlastFxConfig.Enabled || !BlastFxRuntimeConfig.EnableModule)
                    return false;
                if (!kcsVacuumExplosionsEnabled)
                    return false;
                if (part == null) return false;
                if (!VacuumExplosionPartEligibleForFuelRule(part))
                {
                    if (BlastFxConfig.DebugLogging)
                        BlastFxLog.DebugLog(Localizer.Format(
                            BlastFxLoc.LogVacuumSkipFuelGate,
                            part.partInfo != null ? part.partInfo.name : "unknown"));
                    return false;
                }
                if (!IsPartInVacuumFreefall(part))
                {
                    if (BlastFxConfig.DebugLogging && part.vessel != null && part.vessel.mainBody != null)
                        BlastFxLog.DebugLog(Localizer.Format(
                            BlastFxLoc.LogVacuumSkipNotVacuum,
                            part.partInfo != null ? part.partInfo.name : "unknown"));
                    return false;
                }

                float lodMul = GetLodMultiplier(part.transform != null ? part.transform.position : Vector3.zero);
                if (lodMul <= 0f)
                {
                    if (BlastFxConfig.DebugLogging)
                        BlastFxLog.DebugLog(Localizer.Format(BlastFxLoc.LogVacuumSkipLod));
                    return false;
                }
                if (!CanSpawnKcsVacuumExplosionPrefab())
                    return false;

                var ev = new KcsExplosionEvent
                {
                    Position = part.transform != null ? part.transform.position : Vector3.zero,
                    Velocity = part.vessel != null ? (Vector3)part.vessel.rb_velocity : Vector3.zero,
                    Layer = part.gameObject != null ? part.gameObject.layer : 0,
                    Time = Time.time,
                    StockFxSuppressed = stockFxSuppressed
                };

                kcsQueue.Add(ev);
                if (BlastFxConfig.DebugLogging)
                    BlastFxLog.DebugLog(Localizer.Format(
                        BlastFxLoc.LogVacuumQueued, kcsQueue.Count));
                if (kcsNextProcessAt > Time.time)
                    return true;
                kcsNextProcessAt = Time.time + 0.02f;
                return true;
            }
            finally
            {
                KcsQueueMarker.End();
            }
        }

        private void UpdateKcsVacuumExplosions(float dt)
        {
            KcsProcessMarker.Begin();
            try
            {
                if (!kcsVacuumExplosionsEnabled) return;
                if (kcsQueue.Count == 0 && kcsRecent.Count == 0) return;

                float now = Time.time;

                if (kcsRecent.Count > 0)
                {
                    float maxAge = Mathf.Max(0.05f, BlastFxRuntimeConfig.VacuumExplosionLimitTime);
                    for (int i = kcsRecent.Count - 1; i >= 0; i--)
                    {
                        if (now - kcsRecent[i].Time > maxAge)
                            kcsRecent.RemoveAt(i);
                    }
                }

                if (kcsQueue.Count == 0 || now < kcsNextProcessAt)
                    return;

                float limitRadius = Mathf.Max(0.1f, BlastFxRuntimeConfig.VacuumExplosionLimitRadius);
                float limitRadiusSqr = limitRadius * limitRadius;
                int rateLimit = Mathf.Max(0, BlastFxRuntimeConfig.VacuumExplosionRateLimit);
                float mergeMaxSpeedDiff = Mathf.Max(0f, BlastFxRuntimeConfig.VacuumExplosionMergeMaxSpeedDiff);

                int processed = 0;
                int spawned = 0;
                int mergedCount = 0;
                int dropped = 0;

                kcsBatch.Clear();
                for (int i = 0; i < kcsQueue.Count; i++)
                {
                    KcsExplosionEvent ev = kcsQueue[i];
                    processed++;

                    int mergeIndex = FindKcsMergeCandidate(kcsBatch, ev,
                        limitRadiusSqr, mergeMaxSpeedDiff);
                    if (mergeIndex >= 0)
                    {
                        kcsBatch[mergeIndex] = MergeKcsEvents(kcsBatch[mergeIndex], ev, now);
                        mergedCount++;
                        continue;
                    }

                    int nearby = CountKcsNearby(kcsRecent, ev, limitRadiusSqr)
                        + CountKcsNearby(kcsBatch, ev, limitRadiusSqr);
                    if (!ev.StockFxSuppressed && rateLimit > 0 && nearby >= rateLimit)
                    {
                        if (BlastFxConfig.DebugLogging)
                            BlastFxLog.DebugLog(Localizer.Format(
                                BlastFxLoc.LogVacuumRateLimitDrop, nearby));
                        dropped++;
                        continue;
                    }

                    kcsBatch.Add(ev);
                }

                for (int i = 0; i < kcsBatch.Count; i++)
                {
                    KcsExplosionEvent ev = kcsBatch[i];
                    kcsRecent.Add(ev);
                    if (SpawnKcsVacuumExplosion(ev))
                        spawned++;
                }

                kcsQueue.Clear();
                kcsBatch.Clear();
                kcsNextProcessAt = now + 0.04f;

                if (BlastFxConfig.DebugLogging && now >= kcsTelemetryNextAt)
                {
                    kcsTelemetryNextAt = now + 0.50f;
                    int busy = 0;
                    for (int i = 0; i < kcsSlots.Count; i++)
                        if (kcsSlots[i] != null && kcsSlots[i].Busy) busy++;

                    BlastFxLog.DebugLog(Localizer.Format(
                        BlastFxLoc.LogVacuumBatch,
                        processed,
                        spawned,
                        mergedCount,
                        dropped,
                        kcsSlots.Count,
                        busy,
                        kcsRecent.Count));
                }
            }
            finally
            {
                KcsProcessMarker.End();
            }
        }

        private bool SpawnKcsVacuumExplosion(KcsExplosionEvent ev)
        {
            KcsSpawnMarker.Begin();
            try
            {
                float lodMul = GetLodMultiplier(ev.Position);
                if (lodMul <= 0f) return false;

                if (!KcsExplosionAssets.TryGetPrefab(out var prefab))
                    return false;

                KcsExplosionSlot slot = AcquireKcsSlot();
                if (slot == null) return false;

                slot.Root.transform.position = ev.Position;
                slot.Root.transform.rotation = Quaternion.identity;
                SetLayerRecursive(slot.Root, slot.Transforms, ev.Layer);

                slot.Root.SetActive(true);

                if (slot.Systems != null)
                {
                    for (int i = 0; i < slot.Systems.Length; i++)
                    {
                        var ps = slot.Systems[i];
                        if (ps == null) continue;
                        ps.Clear(true);
                        ps.Play(true);
                    }
                }

                if (!slot.Busy)
                    activePoolSlotCount++;
                slot.Busy = true;
                slot.ReturnTime = Time.time + Mathf.Max(1.0f, BlastFxRuntimeConfig.VacuumExplosionReturnDelay);
                if (slot.ReturnTime < nextPoolReturnAt)
                    nextPoolReturnAt = slot.ReturnTime;

                if (BlastFxWantFxProbe())
                {
                    debugLastKcsTime = Time.time;
                    debugLastKcsSystems = slot.Systems != null ? slot.Systems.Length : 0;
                    debugKcsSpawns++;
                }

                if (BlastFxConfig.DebugLogging)
                {
                    int sysCount = slot.Systems != null ? slot.Systems.Length : 0;
                    BlastFxLog.DebugLog(Localizer.Format(
                        BlastFxLoc.LogVacuumSpawn,
                        slot.Busy,
                        BlastFxRuntimeConfig.VacuumExplosionReturnDelay.ToString("0.0"),
                        sysCount));
                }

                return true;
            }
            finally
            {
                KcsSpawnMarker.End();
            }
        }

        private static int CountKcsNearby(List<KcsExplosionEvent> list,
            KcsExplosionEvent ev, float limitRadiusSqr)
        {
            if (list == null || list.Count == 0)
                return 0;

            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if ((list[i].Position - ev.Position).sqrMagnitude <= limitRadiusSqr)
                    count++;
            }
            return count;
        }

        private static int FindKcsMergeCandidate(List<KcsExplosionEvent> list,
            KcsExplosionEvent ev, float limitRadiusSqr, float mergeMaxSpeedDiff)
        {
            if (list == null || list.Count == 0)
                return -1;

            int mergeIndex = -1;
            float closestSqr = float.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                KcsExplosionEvent existing = list[i];
                float dSqr = (existing.Position - ev.Position).sqrMagnitude;
                if (dSqr > limitRadiusSqr || dSqr >= closestSqr)
                    continue;

                Vector3 speedDiff = existing.Velocity - ev.Velocity;
                if (speedDiff.magnitude > mergeMaxSpeedDiff)
                    continue;

                closestSqr = dSqr;
                mergeIndex = i;
            }
            return mergeIndex;
        }

        private static KcsExplosionEvent MergeKcsEvents(KcsExplosionEvent a,
            KcsExplosionEvent b, float now)
        {
            a.Position = (a.Position + b.Position) * 0.5f;
            a.Velocity = (a.Velocity + b.Velocity) * 0.5f;
            a.Time = now;
            a.StockFxSuppressed = a.StockFxSuppressed || b.StockFxSuppressed;
            return a;
        }

        private static bool CanSpawnKcsVacuumExplosionPrefab()
        {
            if (cachedCanSpawnKcsRevision == BlastFxRuntimeConfig.Revision)
                return cachedCanSpawnKcs;

            cachedCanSpawnKcsRevision = BlastFxRuntimeConfig.Revision;
            cachedCanSpawnKcs = false;

            if (!KcsExplosionAssets.TryGetPrefab(out GameObject prefab) || prefab == null)
                return false;

            ParticleSystem[] systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
            cachedCanSpawnKcs = systems != null && systems.Length > 0;
            return cachedCanSpawnKcs;
        }

        private void ReturnExpiredKcsSlots(float now)
        {
            for (int i = 0; i < kcsSlots.Count; i++)
            {
                KcsExplosionSlot slot = kcsSlots[i];
                if (slot == null || !slot.Busy) continue;
                if (now < slot.ReturnTime)
                {
                    if (slot.ReturnTime < nextPoolReturnAt)
                        nextPoolReturnAt = slot.ReturnTime;
                    continue;
                }

                if (slot.Systems != null)
                {
                    for (int p = 0; p < slot.Systems.Length; p++)
                    {
                        var ps = slot.Systems[p];
                        if (ps != null)
                            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
                slot.Root.SetActive(false);
                slot.Busy = false;
                activePoolSlotCount = Math.Max(0, activePoolSlotCount - 1);
            }
        }

        private void StopKcsVacuumExplosions()
        {
            kcsQueue.Clear();
            kcsBatch.Clear();
            kcsRecent.Clear();
            kcsNextProcessAt = 0f;

            for (int i = 0; i < kcsSlots.Count; i++)
            {
                KcsExplosionSlot slot = kcsSlots[i];
                if (slot == null) continue;
                if (slot.Systems != null)
                {
                    for (int p = 0; p < slot.Systems.Length; p++)
                    {
                        ParticleSystem ps = slot.Systems[p];
                        if (ps != null)
                            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
                if (slot.AudioSources != null)
                {
                    for (int a = 0; a < slot.AudioSources.Length; a++)
                    {
                        AudioSource snd = slot.AudioSources[a];
                        if (snd != null)
                            snd.Stop();
                    }
                }
                if (slot.Root != null)
                    slot.Root.SetActive(false);
                if (slot.Busy)
                    activePoolSlotCount = Math.Max(0, activePoolSlotCount - 1);
                slot.Busy = false;
                slot.ReturnTime = 0f;
            }
        }

        private static void SetLayerRecursive(GameObject root, int layer)
        {
            SetLayerRecursive(root, null, layer);
        }

        private static void SetLayerRecursive(GameObject root, Transform[] transforms, int layer)
        {
            if (root == null) return;
            root.layer = layer;
            if (transforms == null)
                transforms = root.GetComponentsInChildren<Transform>(true);
            if (transforms == null) return;
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].gameObject != null)
                    transforms[i].gameObject.layer = layer;
            }
        }

        private KcsExplosionSlot AcquireKcsSlot()
        {
            for (int i = 0; i < kcsSlots.Count; i++)
            {
                if (kcsSlots[i] != null && !kcsSlots[i].Busy)
                    return kcsSlots[i];
            }

            KcsExplosionSlot fresh = BuildKcsSlot();
            if (fresh == null) return null;
            kcsSlots.Add(fresh);
            return fresh;
        }

        private KcsExplosionSlot BuildKcsSlot()
        {
            KcsBuildSlotMarker.Begin();
            try
            {
                if (!KcsExplosionAssets.TryGetPrefab(out var prefab) || prefab == null)
                    return null;

                GameObject root = null;
                try
                {
                    root = Instantiate(prefab);
                    root.name = "KerbalFX_BlastFX_VacuumExplosionPool";
                    root.SetActive(false);

                    Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                    ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
                    AudioSource[] audios =
                        root.GetComponentsInChildren<AudioSource>(true);

                    if (BlastFxConfig.DebugLogging)
                    {
                        int worldCount = 0;
                        int localCount = 0;
                        for (int i = 0; i < systems.Length; i++)
                        {
                            var ps = systems[i];
                            if (ps == null) continue;
                            if (ps.main.simulationSpace == ParticleSystemSimulationSpace.World)
                                worldCount++;
                            else
                                localCount++;
                        }
                        BlastFxLog.DebugLog(Localizer.Format(
                            BlastFxLoc.LogVacuumBuildSlot,
                            systems.Length,
                            worldCount,
                            localCount));
                    }

                    return new KcsExplosionSlot
                    {
                        Root = root,
                        Transforms = transforms,
                        Systems = systems,
                        AudioSources = audios,
                        Busy = false,
                        ReturnTime = 0f
                    };
                }
                catch (Exception ex)
                {
                    BlastFxLog.DebugException("vacuum-explosion-build-slot", ex);
                    if (root != null) Destroy(root);
                    return null;
                }
            }
            finally
            {
                KcsBuildSlotMarker.End();
            }
        }
    }
}
