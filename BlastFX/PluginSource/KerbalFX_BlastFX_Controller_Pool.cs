using System;
using System.Collections;
using System.Collections.Generic;
using KSP.Localization;
using UnityEngine;

namespace KerbalFX.BlastFX
{
    internal partial class BlastFxController
    {
        private void DestroyPools()
        {
            for (int i = 0; i < SizeClassCount; i++)
            {
                DestroyPoolList(pyroSlots[i]);
                DestroyPoolList(puffSlots[i]);
                DestroyPoolList(dockSlots[i]);
                DestroyPoolList(vacuumSlots[i]);
            }
        }

        private static void DestroyPoolList(List<PoolSlot> pool)
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && pool[i].Root != null)
                    Destroy(pool[i].Root);
            }
            pool.Clear();
        }

        private void ReturnExpiredSlots()
        {
            if (activePoolSlotCount <= 0)
                return;

            float now = Time.time;
            if (now < nextPoolReturnAt)
                return;

            nextPoolReturnAt = float.MaxValue;
            for (int c = 0; c < SizeClassCount; c++)
            {
                ReturnExpiredInList(pyroSlots[c], now);
                ReturnExpiredInList(puffSlots[c], now);
                ReturnExpiredInList(dockSlots[c], now);
                ReturnExpiredInList(vacuumSlots[c], now);
            }
        }

        private void ReturnExpiredInList(List<PoolSlot> pool, float now)
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Count; i++)
            {
                PoolSlot slot = pool[i];
                if (!slot.Busy) continue;
                if (now < slot.ReturnTime)
                {
                    if (slot.ReturnTime < nextPoolReturnAt)
                        nextPoolReturnAt = slot.ReturnTime;
                    continue;
                }

                if (slot.Sparks != null) slot.Sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (slot.Chunks != null) slot.Chunks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (slot.Smoke != null)  slot.Smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                slot.Root.SetActive(false);
                slot.Busy = false;
                activePoolSlotCount = Math.Max(0, activePoolSlotCount - 1);
            }
        }

        private void StartPoolPrewarmIfNeeded()
        {
            if (poolPrewarmStarted || poolPrewarmComplete) return;
            if (!BlastFxConfig.Enabled || !BlastFxRuntimeConfig.EnableModule) return;
            poolPrewarmStarted = true;
            StartCoroutine(PrewarmPools());
        }

        private IEnumerator PrewarmPools()
        {
            for (int i = 0; i < SizeClassCount; i++)
            {
                FxSizeClass sizeClass = (FxSizeClass)i;
                while (pyroSlots[i].Count < PrewarmPyroSlotsPerSize)
                {
                    PoolSlot slot = BuildPyroSlot(sizeClass);
                    if (slot == null) break;
                    pyroSlots[i].Add(slot);
                    yield return null;
                }

                while (puffSlots[i].Count < PrewarmPuffSlotsPerSize)
                {
                    PoolSlot slot = BuildPuffSlot(sizeClass);
                    if (slot == null) break;
                    puffSlots[i].Add(slot);
                    yield return null;
                }

                while (dockSlots[i].Count < PrewarmDockSlotsPerSize)
                {
                    PoolSlot slot = BuildDockSlot(sizeClass);
                    if (slot == null) break;
                    dockSlots[i].Add(slot);
                    yield return null;
                }
            }

            poolPrewarmComplete = true;
            BlastFxLog.DebugLog(Localizer.Format(
                BlastFxLoc.LogPoolPrewarm,
                CountPoolSlots(pyroSlots),
                CountPoolSlots(puffSlots)));
        }

        private static int CountPoolSlots(List<PoolSlot>[] pools)
        {
            if (pools == null) return 0;
            int count = 0;
            for (int i = 0; i < pools.Length; i++)
            {
                if (pools[i] != null) count += pools[i].Count;
            }
            return count;
        }

        private PoolSlot AcquirePyro(FxSizeClass sc)
        {
            List<PoolSlot> pool = pyroSlots[(int)sc];
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].Busy) return pool[i];
            }
            PoolSlot fresh = BuildPyroSlot(sc);
            if (fresh == null) return null;
            pool.Add(fresh);
            BlastFxLog.DebugLog(Localizer.Format(
                BlastFxLoc.LogPoolGrow,
                "pyro",
                sc,
                pool.Count));
            return fresh;
        }

        private PoolSlot AcquirePuff(FxSizeClass sc)
        {
            List<PoolSlot> pool = puffSlots[(int)sc];
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].Busy) return pool[i];
            }
            PoolSlot fresh = BuildPuffSlot(sc);
            if (fresh == null) return null;
            pool.Add(fresh);
            BlastFxLog.DebugLog(Localizer.Format(
                BlastFxLoc.LogPoolGrow,
                "puff",
                sc,
                pool.Count));
            return fresh;
        }

        private PoolSlot AcquireDock(FxSizeClass sc)
        {
            List<PoolSlot> pool = dockSlots[(int)sc];
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].Busy) return pool[i];
            }
            PoolSlot fresh = BuildDockSlot(sc);
            if (fresh == null) return null;
            pool.Add(fresh);
            BlastFxLog.DebugLog(Localizer.Format(
                BlastFxLoc.LogPoolGrow,
                "dock",
                sc,
                pool.Count));
            return fresh;
        }

        private PoolSlot AcquireVacuum(FxSizeClass sc)
        {
            List<PoolSlot> pool = vacuumSlots[(int)sc];
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].Busy) return pool[i];
            }
            PoolSlot fresh = BuildVacuumSlot(sc);
            if (fresh == null) return null;
            pool.Add(fresh);
            BlastFxLog.DebugLog(Localizer.Format(
                BlastFxLoc.LogPoolGrow,
                "vacuum",
                sc,
                pool.Count));
            return fresh;
        }

        private static PoolSlot BuildPyroSlot(FxSizeClass sc)
        {
            int idx = (int)sc;
            float size01 = SizeToSize01[idx];
            float partRadius = SizeToRadius[idx];
            float ringScale = Mathf.Lerp(0.98f, 1.16f, size01);
            float rr = Mathf.Max(0.08f,
                (BlastFxRuntimeConfig.BaseRadius + partRadius * BlastFxRuntimeConfig.RadiusFromPart)
                * ringScale);

            float sparkSpeedScale = Mathf.Lerp(0.92f, 1.68f, size01);
            float smokeSpeedScale = Mathf.Lerp(0.92f, 1.44f, size01);
            float chunkSpeedScale = Mathf.Lerp(1.26f, 1.92f, size01)
                * BlastFxRuntimeConfig.FragmentSpeedMultiplier;
            float sparkSizeScale = Mathf.Lerp(0.90f, 1.34f, size01);
            float smokeSizeScale = Mathf.Lerp(0.96f, 1.48f, size01);
            float chunkSizeScale = Mathf.Lerp(0.84f, 1.85f, size01);

            int maxSparks = PyroSparkCounts[idx];
            int maxChunks = Mathf.Clamp(
                Mathf.RoundToInt(PyroChunkCounts[idx] * BlastFxRuntimeConfig.FragmentCountMultiplier),
                2, 320);
            int maxSmoke  = PyroSmokeCounts[idx];

            GameObject root = new GameObject("KerbalFX_BlastFX_PyroPool");
            root.SetActive(false);

            ParticleSystem sparks = CreateSparks(root.transform, 0, rr,
                maxSparks, sparkSpeedScale, sparkSizeScale);
            ParticleSystem chunks = CreateChunks(root.transform, 0, rr,
                maxChunks, chunkSpeedScale, chunkSizeScale);
            ParticleSystem smoke  = CreateSmoke(root.transform, 0, rr,
                maxSmoke, smokeSpeedScale, smokeSizeScale);

            if (sparks == null && chunks == null && smoke == null)
            {
                Destroy(root);
                return null;
            }

            return new PoolSlot
            {
                Root = root,
                Sparks = sparks,
                Chunks = chunks,
                Smoke = smoke,
                Busy = false,
                ReturnTime = 0f
            };
        }

        private static PoolSlot BuildPuffSlot(FxSizeClass sc)
        {
            int idx = (int)sc;
            float size01 = SizeToSize01[idx];
            float partRadius = SizeToRadius[idx];
            float rr = Mathf.Max(0.07f,
                (BlastFxRuntimeConfig.BaseRadius * 0.58f + partRadius * 0.62f)
                * Mathf.Lerp(1.00f, 1.14f, size01));

            int maxSmoke = PuffSmokeCounts[idx];
            float smokeSpeedScale = Mathf.Lerp(2.10f, 2.70f, size01);
            float smokeSizeScale  = Mathf.Lerp(0.74f, 0.98f, size01);

            GameObject root = new GameObject("KerbalFX_BlastFX_PuffPool");
            root.SetActive(false);

            ParticleSystem smoke = CreateSoftPuffSmoke(root.transform, 0, rr,
                maxSmoke, smokeSpeedScale, smokeSizeScale);

            if (smoke == null)
            {
                Destroy(root);
                return null;
            }

            return new PoolSlot
            {
                Root = root,
                Sparks = null,
                Chunks = null,
                Smoke = smoke,
                Busy = false,
                ReturnTime = 0f
            };
        }

        private static PoolSlot BuildDockSlot(FxSizeClass sc)
        {
            int idx = (int)sc;
            float size01 = SizeToSize01[idx];
            float rr = DockingRingRadii[idx];

            int maxSmoke = DockGasCounts[idx];
            float smokeSpeedScale = Mathf.Lerp(0.665f, 0.91f, size01);
            float smokeSizeScale  = Mathf.Lerp(0.30f, 0.86f, size01);

            GameObject root = new GameObject("KerbalFX_BlastFX_DockPool");
            root.SetActive(false);

            ParticleSystem smoke = CreateUndockGasSmoke(root.transform, 0, rr,
                maxSmoke, smokeSpeedScale, smokeSizeScale);

            if (smoke == null)
            {
                Destroy(root);
                return null;
            }

            return new PoolSlot
            {
                Root = root,
                Sparks = null,
                Chunks = null,
                Smoke = smoke,
                Busy = false,
                ReturnTime = 0f
            };
        }

        private static PoolSlot BuildVacuumSlot(FxSizeClass sc)
        {
            int idx = (int)sc;
            float size01 = SizeToSize01[idx];
            float partRadius = SizeToRadius[idx];
            float rr = Mathf.Max(0.06f,
                (BlastFxRuntimeConfig.BaseRadius * 0.55f + partRadius * 0.70f)
                * Mathf.Lerp(0.92f, 1.16f, size01));

            int maxDebris = VacuumDebrisCounts[idx];
            float debrisSpeedScale = Mathf.Lerp(0.765f, 1.08f, size01);
            float debrisSizeScale  = Mathf.Lerp(0.55f, 0.90f, size01);

            GameObject root = new GameObject("KerbalFX_BlastFX_VacuumPool");
            root.SetActive(false);

            ParticleSystem debris = CreateVacuumDebris(root.transform, 0, rr,
                maxDebris, debrisSpeedScale, debrisSizeScale);

            if (debris == null)
            {
                Destroy(root);
                return null;
            }

            return new PoolSlot
            {
                Root = root,
                Sparks = null,
                Chunks = debris,
                Smoke = null,
                Busy = false,
                ReturnTime = 0f
            };
        }

        private static void ActivateSlot(PoolSlot slot, Vector3 position,
            Quaternion rotation, int layer)
        {
            slot.Root.transform.position = position;
            slot.Root.transform.rotation = rotation;
            SetSlotLayer(slot, layer);
            slot.Root.SetActive(true);

            if (slot.Sparks != null)
            {
                slot.Sparks.Clear();
                slot.Sparks.Play(true);
            }
            if (slot.Chunks != null)
            {
                slot.Chunks.Clear();
                slot.Chunks.Play(true);
            }
            if (slot.Smoke != null)
            {
                slot.Smoke.Clear();
                slot.Smoke.Play(true);
            }
        }

        private static void SetSlotLayer(PoolSlot slot, int layer)
        {
            slot.Root.layer = layer;
            if (slot.Sparks != null) slot.Sparks.gameObject.layer = layer;
            if (slot.Chunks != null) slot.Chunks.gameObject.layer = layer;
            if (slot.Smoke  != null) slot.Smoke.gameObject.layer  = layer;
        }
    }
}
