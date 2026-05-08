using System;
using System.Collections.Generic;
using System.Globalization;
using KSP.Localization;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine;

namespace KerbalFX.ImpactPuffs
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    internal class ImpactPuffsBootstrap : KerbalFxVesselControllerBootstrap<VesselImpactController>
    {
        protected override bool IsModuleEnabled { get { return ImpactPuffsConfig.Enabled; } }
        protected override bool IsDebugLogging { get { return ImpactPuffsConfig.DebugLogging; } }
        protected override int CurrentConfigRevision
        {
            get
            {
                unchecked
                {
                    return ImpactPuffsConfig.Revision * 397 ^ ImpactPuffsRuntimeConfig.Revision;
                }
            }
        }

        protected override void RefreshSettings()
        {
            ImpactPuffsConfig.Refresh();
            ImpactPuffsRuntimeConfig.TryHotReloadFromDisk();
        }

        protected override void OnBeforeStart()
        {
            ImpactPuffsRuntimeConfig.Refresh();
        }

        protected override bool IsSupportedVessel(Vessel vessel)
        {
            return KerbalFxVesselUtil.IsSupportedFlightVessel(vessel);
        }

        protected override VesselImpactController CreateController(Vessel vessel)
        {
            return new VesselImpactController(vessel);
        }

        protected override bool ControllerHasEmitters(VesselImpactController controller)
        {
            return controller.HasAnyEmitters;
        }

        protected override int ControllerEmitterCount(VesselImpactController controller)
        {
            return controller.EngineEmitterCount;
        }

        protected override void TryRebuildController(VesselImpactController controller, float refreshElapsed)
        {
            controller.TryRebuild();
        }

        protected override void LogBootstrapStart()
        {
            ImpactPuffsLog.Info(Localizer.Format(ImpactPuffsLoc.LogBootstrapStart));
        }

        protected override void LogBootstrapStop()
        {
            ImpactPuffsLog.Info(Localizer.Format(ImpactPuffsLoc.LogBootstrapStop));
        }

        protected override void LogHeartbeat(int controllerCount)
        {
            ImpactPuffsLog.DebugLog(Localizer.Format(ImpactPuffsLoc.LogHeartbeat, controllerCount));
        }

        protected override void LogAttached(int emitterCount, string vesselName)
        {
            ImpactPuffsLog.DebugLog(Localizer.Format(ImpactPuffsLoc.LogAttached, emitterCount, vesselName));
        }
    }

    internal sealed class VesselImpactController : IVesselFxController
    {
        private struct EnginePuffCluster
        {
            public int LeaderIndex;
            public int Count;
            public float Weight;
            public float TargetRate;
            public Vector3 PositionSum;
            public Vector3 ClusterPositionSum;
            public float PressureSum;

            public Vector3 Center
            {
                get { return Weight > 0.0001f ? PositionSum / Weight : Vector3.zero; }
            }

            public Vector3 LinkCenter
            {
                get { return Weight > 0.0001f ? ClusterPositionSum / Weight : Vector3.zero; }
            }

            public float AveragePressure
            {
                get { return Weight > 0.0001f ? PressureSum / Weight : 0f; }
            }

            public void Reset(int leaderIndex, EngineGroundPuffFrame frame)
            {
                LeaderIndex = leaderIndex;
                Count = 0;
                Weight = 0f;
                TargetRate = 0f;
                PositionSum = Vector3.zero;
                ClusterPositionSum = Vector3.zero;
                PressureSum = 0f;
                Add(frame);
            }

            public void Add(EngineGroundPuffFrame frame)
            {
                float weight = Mathf.Max(1f, frame.TargetRate);
                Count++;
                Weight += weight;
                TargetRate += frame.TargetRate;
                PositionSum += frame.Position * weight;
                ClusterPositionSum += frame.ClusterPosition * weight;
                PressureSum += frame.Pressure * weight;
            }

            public EngineGroundPuffFrame BuildFrame(EngineGroundPuffFrame leader)
            {
                float inv = Weight > 0.0001f ? 1f / Weight : 1f;
                leader.Position = PositionSum * inv;
                leader.ClusterPosition = ClusterPositionSum * inv;
                leader.TargetRate = TargetRate;
                leader.Pressure = AveragePressure;
                return leader;
            }
        }

        private readonly Vessel vessel;
        private readonly List<EngineGroundPuffEmitter> engineEmitters = new List<EngineGroundPuffEmitter>();
        private readonly List<EngineGroundPuffFrame> engineFrames = new List<EngineGroundPuffFrame>(8);
        private readonly List<EnginePuffCluster> engineClusters = new List<EnginePuffCluster>(4);
        private readonly List<int> engineFrameClusterIds = new List<int>(8);
        private readonly HashSet<ModuleEngines> activeEngineModules = new HashSet<ModuleEngines>();
        private NativeArray<ImpactPuffFrameInput> engineFrameJobInput;
        private NativeArray<ImpactPuffFrameOutput> engineFrameJobOutput;
        private NativeArray<ImpactPuffClusterInput> engineClusterJobInput;
        private NativeArray<int> engineClusterJobIds;
        private NativeArray<int> engineClusterJobCount;
        private KerbalFxVesselPartSnapshot partSnapshot;
        private TouchdownBurstEmitter touchdownEmitter;
        private float engineClusterDebugTimer;
        private int acceptedEngineCount = 0;
        private static readonly string[] EngineTypeRejectTokens = { "mono", "rcs", "turbine", "jet", "scram", "airbreathing" };
        private static readonly string[] EngineIdRejectTokens = { "rcs", "monoprop", "mono", "vernier" };
        private static readonly string[] PartNameRejectTokens = { "jet", "turbine", "airbreathing", "air breathing", "rcs", "monoprop" };
        private const float EngineClusterLinkRadius = 1.5f;
        private const float EngineClusterLinkRadiusSqr = EngineClusterLinkRadius * EngineClusterLinkRadius;
        private const float EngineClusterDebugInterval = 1.0f;
        private const int BurstFrameMinFrameCount = 4;
        private const int BurstClusterMinFrameCount = 4;
        private static readonly ProfilerMarker EngineClusterMarker =
            new ProfilerMarker("KerbalFX.ImpactPuffs.EngineCluster");

        public int EngineEmitterCount
        {
            get { return engineEmitters.Count; }
        }

        public bool HasAnyEmitters
        {
            get { return engineEmitters.Count > 0 || touchdownEmitter != null; }
        }

        public VesselImpactController(Vessel vessel)
        {
            this.vessel = vessel;
            RebuildEmitters();
        }

        public bool IsStillValid()
        {
            return vessel != null && vessel.loaded && !vessel.packed;
        }

        public void TryRebuild()
        {
            if (vessel == null || vessel.parts == null)
            {
                return;
            }

            if (partSnapshot.HasChanged(vessel))
            {
                RebuildEmitters();
            }
        }

        public void Tick(float dt)
        {
            if (vessel == null || !vessel.loaded || vessel.packed)
            {
                StopAll();
                return;
            }

            activeEngineModules.Clear();
            for (int i = 0; i < engineEmitters.Count; i++)
            {
                if (!engineEmitters[i].IsEngineActive)
                {
                    continue;
                }

                ModuleEngines engineModule = engineEmitters[i].EngineModule;
                if (engineModule != null)
                    activeEngineModules.Add(engineModule);
            }

            int activeCluster = Mathf.Max(1, activeEngineModules.Count);
            for (int i = 0; i < engineEmitters.Count; i++)
            {
                engineEmitters[i].SetEngineClusterCount(activeCluster);
            }

            TickEngineClusters(dt);

            if (touchdownEmitter != null)
            {
                touchdownEmitter.Tick(dt);
            }
        }

        public void StopAll()
        {
            for (int i = 0; i < engineEmitters.Count; i++)
            {
                engineEmitters[i].StopEmission();
            }
        }

        public void Dispose()
        {
            DisposeEmitters();
            DisposeFrameJobBuffers();
            DisposeClusterJobBuffers();
        }

        private void RebuildEmitters()
        {
            DisposeEmitters();
            partSnapshot.Capture(vessel);
            acceptedEngineCount = 0;

            if (vessel == null || vessel.parts == null)
            {
                return;
            }

            int engineModuleCount = 0;
            int skippedEngineCount = 0;
            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part part = vessel.parts[i];
                ScanPartForEngines(part, ref engineModuleCount, ref skippedEngineCount);
            }

            ApplyEmitterClusterSize();
            touchdownEmitter = new TouchdownBurstEmitter(vessel);
            LogVesselScan(engineModuleCount, skippedEngineCount);
        }

        private void DisposeEmitters()
        {
            for (int i = 0; i < engineEmitters.Count; i++)
            {
                engineEmitters[i].Dispose();
            }

            engineEmitters.Clear();

            if (touchdownEmitter != null)
            {
                touchdownEmitter.Dispose();
                touchdownEmitter = null;
            }
        }

        private void ScanPartForEngines(Part part, ref int engineModuleCount, ref int skippedEngineCount)
        {
            if (part == null || part.Modules == null)
            {
                return;
            }

            for (int i = 0; i < part.Modules.Count; i++)
            {
                if (!(part.Modules[i] is ModuleEngines engine))
                {
                    continue;
                }

                engineModuleCount++;
                if (!ShouldUseEngineForGroundPuffs(part, engine))
                {
                    skippedEngineCount++;
                    continue;
                }

                acceptedEngineCount++;
                AddEngineEmittersForTransforms(part, engine);
            }
        }

        private void AddEngineEmittersForTransforms(Part part, ModuleEngines engine)
        {
            List<Transform> transforms = ResolveThrustTransforms(engine, part);
            engineEmitters.Add(new EngineGroundPuffEmitter(part, engine, transforms));
        }

        private void ApplyEmitterClusterSize()
        {
            int clusterSize = Mathf.Max(1, acceptedEngineCount);
            for (int i = 0; i < engineEmitters.Count; i++)
            {
                engineEmitters[i].SetEngineClusterCount(clusterSize);
            }
        }

        private void TickEngineClusters(float dt)
        {
            engineFrames.Clear();
            bool collectFrameInputs = engineEmitters.Count >= BurstFrameMinFrameCount;
            if (collectFrameInputs)
                EnsureFrameJobCapacity(engineEmitters.Count);

            int frameCount = 0;
            for (int i = 0; i < engineEmitters.Count; i++)
            {
                EngineGroundPuffFrame frame;
                if (collectFrameInputs && engineFrameJobInput.IsCreated)
                {
                    ImpactPuffFrameInput input;
                    if (engineEmitters[i].TryBuildFrameInput(vessel, dt, out frame, out input))
                    {
                        engineFrames.Add(frame);
                        engineFrameJobInput[frameCount++] = input;
                    }
                }
                else if (engineEmitters[i].TryBuildFrame(vessel, dt, out frame))
                {
                    engineFrames.Add(frame);
                }
            }

            if (engineFrames.Count == 0)
                return;

            if (collectFrameInputs && frameCount > 0)
                ApplyFrameMath(frameCount);

            EngineClusterMarker.Begin();
            try
            {
                BuildEngineClusters();
                LogEngineClusters(dt);
                EmitEngineClusters(dt);
            }
            finally
            {
                EngineClusterMarker.End();
            }
        }

        private void BuildEngineClusters()
        {
            if (engineFrames.Count >= BurstClusterMinFrameCount && TryBuildEngineClustersWithBurst())
                return;

            BuildEngineClustersManaged();
        }

        private bool TryBuildEngineClustersWithBurst()
        {
            int frameCount = engineFrames.Count;
            if (frameCount <= 0)
                return false;

            EnsureClusterJobCapacity(frameCount);
            if (!engineClusterJobInput.IsCreated || !engineClusterJobIds.IsCreated || !engineClusterJobCount.IsCreated)
                return false;

            for (int i = 0; i < frameCount; i++)
            {
                engineClusterJobInput[i] = new ImpactPuffClusterInput
                {
                    ClusterPosition = ImpactPuffClusterJobs.ToFloat3(engineFrames[i].ClusterPosition)
                };
            }

            ImpactPuffClusterJobs.Build(
                engineClusterJobInput,
                frameCount,
                EngineClusterLinkRadiusSqr,
                engineClusterJobIds,
                engineClusterJobCount);

            engineClusters.Clear();
            engineFrameClusterIds.Clear();
            for (int i = 0; i < frameCount; i++)
                engineFrameClusterIds.Add(-1);

            int clusterCount = Mathf.Clamp(engineClusterJobCount[0], 0, frameCount);
            for (int i = 0; i < frameCount; i++)
            {
                int clusterIndex = engineClusterJobIds[i];
                if (clusterIndex < 0 || clusterIndex >= clusterCount)
                    return false;

                engineFrameClusterIds[i] = clusterIndex;
                while (engineClusters.Count <= clusterIndex)
                {
                    EnginePuffCluster empty = new EnginePuffCluster();
                    empty.LeaderIndex = -1;
                    engineClusters.Add(empty);
                }

                EnginePuffCluster cluster = engineClusters[clusterIndex];
                if (cluster.LeaderIndex < 0)
                    cluster.Reset(i, engineFrames[i]);
                else
                    cluster.Add(engineFrames[i]);
                engineClusters[clusterIndex] = cluster;
            }

            return engineClusters.Count > 0;
        }

        private void BuildEngineClustersManaged()
        {
            engineClusters.Clear();
            engineFrameClusterIds.Clear();
            for (int i = 0; i < engineFrames.Count; i++)
                engineFrameClusterIds.Add(-1);

            for (int i = 0; i < engineFrames.Count; i++)
            {
                if (engineFrameClusterIds[i] >= 0)
                    continue;

                EngineGroundPuffFrame frame = engineFrames[i];
                int clusterIndex = engineClusters.Count;
                EnginePuffCluster fresh = new EnginePuffCluster();
                fresh.Reset(i, frame);
                engineClusters.Add(fresh);
                engineFrameClusterIds[i] = clusterIndex;

                bool expanded;
                do
                {
                    expanded = false;
                    for (int j = 0; j < engineFrames.Count; j++)
                    {
                        if (engineFrameClusterIds[j] >= 0)
                            continue;
                        if (!IsLinkedToCluster(j, clusterIndex))
                            continue;

                        EnginePuffCluster cluster = engineClusters[clusterIndex];
                        cluster.Add(engineFrames[j]);
                        engineClusters[clusterIndex] = cluster;
                        engineFrameClusterIds[j] = clusterIndex;
                        expanded = true;
                    }
                }
                while (expanded);
            }
        }

        private bool IsLinkedToCluster(int frameIndex, int clusterIndex)
        {
            Vector3 position = engineFrames[frameIndex].ClusterPosition;
            for (int i = 0; i < engineFrames.Count; i++)
            {
                if (engineFrameClusterIds[i] != clusterIndex)
                    continue;

                Vector3 delta = position - engineFrames[i].ClusterPosition;
                if (delta.sqrMagnitude <= EngineClusterLinkRadiusSqr)
                    return true;
            }
            return false;
        }

        private void ApplyFrameMath(int frameCount)
        {
            if (frameCount >= BurstFrameMinFrameCount && engineFrameJobOutput.IsCreated)
            {
                ImpactPuffFrameJobs.Build(engineFrameJobInput, frameCount, engineFrameJobOutput);
                for (int i = 0; i < frameCount; i++)
                {
                    EngineGroundPuffFrame frame = engineFrames[i];
                    EngineGroundPuffEmitter.ApplyFrameOutput(ref frame, engineFrameJobOutput[i]);
                    engineFrames[i] = frame;
                }
                return;
            }

            for (int i = 0; i < frameCount; i++)
            {
                EngineGroundPuffFrame frame = engineFrames[i];
                EngineGroundPuffEmitter.ApplyFrameOutput(
                    ref frame,
                    ImpactPuffFrameJobs.BuildManaged(engineFrameJobInput[i]));
                engineFrames[i] = frame;
            }
        }

        private void EnsureFrameJobCapacity(int frameCount)
        {
            if (frameCount <= 0)
                return;
            if (engineFrameJobInput.IsCreated && engineFrameJobInput.Length >= frameCount)
                return;

            DisposeFrameJobBuffers();
            engineFrameJobInput = new NativeArray<ImpactPuffFrameInput>(
                frameCount,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            engineFrameJobOutput = new NativeArray<ImpactPuffFrameOutput>(
                frameCount,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
        }

        private void EnsureClusterJobCapacity(int frameCount)
        {
            if (frameCount <= 0)
                return;
            if (engineClusterJobInput.IsCreated && engineClusterJobInput.Length >= frameCount)
                return;

            DisposeClusterJobBuffers();
            engineClusterJobInput = new NativeArray<ImpactPuffClusterInput>(
                frameCount,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            engineClusterJobIds = new NativeArray<int>(
                frameCount,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            engineClusterJobCount = new NativeArray<int>(
                1,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
        }

        private void DisposeClusterJobBuffers()
        {
            if (engineClusterJobInput.IsCreated)
                engineClusterJobInput.Dispose();
            if (engineClusterJobIds.IsCreated)
                engineClusterJobIds.Dispose();
            if (engineClusterJobCount.IsCreated)
                engineClusterJobCount.Dispose();
        }

        private void DisposeFrameJobBuffers()
        {
            if (engineFrameJobInput.IsCreated)
                engineFrameJobInput.Dispose();
            if (engineFrameJobOutput.IsCreated)
                engineFrameJobOutput.Dispose();
        }

        private void EmitEngineClusters(float dt)
        {
            for (int i = 0; i < engineFrames.Count; i++)
            {
                if (!IsClusterLeader(i))
                    engineFrames[i].Emitter.FadeClusteredEmission(dt);
            }

            for (int i = 0; i < engineClusters.Count; i++)
            {
                EnginePuffCluster cluster = engineClusters[i];
                EngineGroundPuffFrame leader = engineFrames[cluster.LeaderIndex];
                EngineGroundPuffFrame merged = cluster.BuildFrame(leader);
                leader.Emitter.EmitFrame(vessel, merged, cluster.Count, dt);
            }
        }

        private void LogEngineClusters(float dt)
        {
            if (!ImpactPuffsConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel)
                return;

            engineClusterDebugTimer -= dt;
            if (engineClusterDebugTimer > 0f)
                return;

            engineClusterDebugTimer = EngineClusterDebugInterval;
            ImpactPuffsLog.DebugLog(Localizer.Format(
                ImpactPuffsLoc.LogEngineClusterSummary,
                engineFrames.Count.ToString(CultureInfo.InvariantCulture),
                engineClusters.Count.ToString(CultureInfo.InvariantCulture),
                EngineClusterLinkRadius.ToString("F2", CultureInfo.InvariantCulture),
                activeEngineModules.Count.ToString(CultureInfo.InvariantCulture),
                acceptedEngineCount.ToString(CultureInfo.InvariantCulture)));

            for (int i = 0; i < engineClusters.Count; i++)
            {
                EnginePuffCluster cluster = engineClusters[i];
                Vector3 centerOffset = cluster.Center - vessel.CoM;
                Vector3 linkCenterOffset = cluster.LinkCenter - vessel.CoM;
                float nearestOutside = ComputeClusterNearestOutsideDistance(i);
                ImpactPuffsLog.DebugLog(Localizer.Format(
                    ImpactPuffsLoc.LogEngineClusterItem,
                    i.ToString(CultureInfo.InvariantCulture),
                    cluster.LeaderIndex.ToString(CultureInfo.InvariantCulture),
                    cluster.Count.ToString(CultureInfo.InvariantCulture),
                    cluster.TargetRate.ToString("F1", CultureInfo.InvariantCulture),
                    cluster.AveragePressure.ToString("F2", CultureInfo.InvariantCulture),
                    FormatVector(centerOffset),
                    "linkCenter=" + FormatVector(linkCenterOffset)
                        + " nearestOutside=" + FormatDistance(nearestOutside)
                        + " members=" + FormatClusterMembers(i)));
            }
        }

        private bool IsClusterLeader(int sampleIndex)
        {
            for (int i = 0; i < engineClusters.Count; i++)
                if (engineClusters[i].LeaderIndex == sampleIndex)
                    return true;
            return false;
        }

        private float ComputeClusterNearestOutsideDistance(int clusterIndex)
        {
            float nearest = float.MaxValue;
            for (int i = 0; i < engineFrames.Count; i++)
            {
                if (engineFrameClusterIds[i] != clusterIndex)
                    continue;

                for (int j = 0; j < engineFrames.Count; j++)
                {
                    if (engineFrameClusterIds[j] == clusterIndex)
                        continue;

                    float distance = Vector3.Distance(engineFrames[i].ClusterPosition, engineFrames[j].ClusterPosition);
                    if (distance < nearest)
                        nearest = distance;
                }
            }

            return nearest;
        }

        private string FormatClusterMembers(int clusterIndex)
        {
            string result = string.Empty;
            for (int i = 0; i < engineFrames.Count; i++)
            {
                if (engineFrameClusterIds[i] != clusterIndex)
                    continue;

                if (result.Length > 0)
                    result += ",";

                Vector3 offset = engineFrames[i].Position - vessel.CoM;
                Vector3 linkOffset = engineFrames[i].ClusterPosition - vessel.CoM;
                result += i.ToString(CultureInfo.InvariantCulture)
                    + "@rate=" + engineFrames[i].TargetRate.ToString("F0", CultureInfo.InvariantCulture)
                    + "@d=" + Vector3.Distance(engineFrames[i].ClusterPosition, engineClusters[clusterIndex].LinkCenter).ToString("F2", CultureInfo.InvariantCulture)
                    + "@link=" + FormatVector(linkOffset)
                    + "@pos=" + FormatVector(offset);
            }

            return result;
        }

        private static string FormatDistance(float value)
        {
            if (value == float.MaxValue)
                return "none";
            return value.ToString("F2", CultureInfo.InvariantCulture);
        }

        private static string FormatVector(Vector3 value)
        {
            return "("
                + value.x.ToString("F2", CultureInfo.InvariantCulture)
                + ","
                + value.y.ToString("F2", CultureInfo.InvariantCulture)
                + ","
                + value.z.ToString("F2", CultureInfo.InvariantCulture)
                + ")";
        }

        private void LogVesselScan(int engineModuleCount, int skippedEngineCount)
        {
            if (!ImpactPuffsConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel)
            {
                return;
            }

            ImpactPuffsLog.DebugLog(Localizer.Format(
                ImpactPuffsLoc.LogVesselScan,
                vessel.vesselName,
                engineModuleCount.ToString(CultureInfo.InvariantCulture),
                acceptedEngineCount.ToString(CultureInfo.InvariantCulture),
                skippedEngineCount.ToString(CultureInfo.InvariantCulture),
                engineEmitters.Count.ToString(CultureInfo.InvariantCulture)));
        }

        private static List<Transform> ResolveThrustTransforms(ModuleEngines engine, Part part)
        {
            List<Transform> raw = new List<Transform>();

            if (engine != null && engine.thrustTransforms != null)
            {
                for (int i = 0; i < engine.thrustTransforms.Count; i++)
                {
                    Transform transform = engine.thrustTransforms[i];
                    if (transform != null)
                    {
                        raw.Add(transform);
                    }
                }
            }

            if (raw.Count == 0 && engine != null && part != null && !string.IsNullOrEmpty(engine.thrustVectorTransformName))
            {
                Transform[] named = part.FindModelTransforms(engine.thrustVectorTransformName);
                if (named != null)
                {
                    for (int i = 0; i < named.Length; i++)
                    {
                        if (named[i] != null)
                        {
                            raw.Add(named[i]);
                        }
                    }
                }
            }

            if (raw.Count <= 1)
            {
                return raw;
            }

            List<Transform> filtered = new List<Transform>();
            for (int i = 0; i < raw.Count; i++)
            {
                if (IsLikelyNozzleTransform(part, raw[i]))
                {
                    filtered.Add(raw[i]);
                }
            }

            return filtered.Count > 0 ? filtered : raw;
        }

        private static bool IsLikelyNozzleTransform(Part part, Transform transform)
        {
            if (part == null || part.transform == null || transform == null)
            {
                return false;
            }

            Vector3 localPos = part.transform.InverseTransformPoint(transform.position);
            Vector3 localExhaustDir = part.transform.InverseTransformDirection(-transform.forward);
            if (localExhaustDir.sqrMagnitude < 0.0001f)
            {
                return true;
            }

            localExhaustDir.Normalize();
            float sideScore = Vector3.Dot(localPos, localExhaustDir);
            return sideScore >= -0.02f;
        }

        private static bool ShouldUseEngineForGroundPuffs(Part part, ModuleEngines engine)
        {
            if (engine == null)
            {
                return false;
            }

            string engineType = ReadEngineTypeName(engine);
            if (KerbalFxUtil.ContainsAnyToken(engineType, EngineTypeRejectTokens))
            {
                return false;
            }

            if (HasPropellant(engine, "MonoPropellant") || HasPropellant(engine, "IntakeAir"))
            {
                return false;
            }

            string engineId = SafeLower(engine.engineID);
            if (KerbalFxUtil.ContainsAnyToken(engineId, EngineIdRejectTokens))
            {
                return false;
            }

            string partName = string.Empty;
            if (part != null && part.partInfo != null)
            {
                partName = (part.partInfo.name + " " + part.partInfo.title).ToLowerInvariant();
            }

            if (KerbalFxUtil.ContainsAnyToken(partName, PartNameRejectTokens))
            {
                return false;
            }

            return true;
        }

        private static bool HasPropellant(ModuleEngines engine, string propellantName)
        {
            if (engine == null || engine.propellants == null || string.IsNullOrEmpty(propellantName))
            {
                return false;
            }

            for (int i = 0; i < engine.propellants.Count; i++)
            {
                Propellant propellant = engine.propellants[i];
                if (propellant != null && string.Equals(propellant.name, propellantName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ReadEngineTypeName(ModuleEngines engine)
        {
            return KerbalFxUtil.ReadMemberStringLowerInvariant(engine, "engineType");
        }

        private static string SafeLower(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.ToLowerInvariant();
        }

    }

}
