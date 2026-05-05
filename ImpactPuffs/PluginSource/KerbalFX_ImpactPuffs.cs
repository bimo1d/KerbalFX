using System;
using System.Collections.Generic;
using System.Globalization;
using KSP.Localization;
using UnityEngine;

namespace KerbalFX.ImpactPuffs
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    internal class ImpactPuffsBootstrap : KerbalFxVesselControllerBootstrap<VesselImpactController>
    {
        protected override bool IsModuleEnabled { get { return ImpactPuffsConfig.Enabled; } }
        protected override bool IsDebugLogging { get { return ImpactPuffsConfig.DebugLogging; } }

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
        private readonly Vessel vessel;
        private readonly List<EngineGroundPuffEmitter> engineEmitters = new List<EngineGroundPuffEmitter>();
        private readonly HashSet<ModuleEngines> activeEngineModules = new HashSet<ModuleEngines>();
        private KerbalFxVesselPartSnapshot partSnapshot;
        private TouchdownBurstEmitter touchdownEmitter;
        private int acceptedEngineCount = 0;
        private static readonly string[] EngineTypeRejectTokens = { "mono", "rcs", "turbine", "jet", "scram", "airbreathing" };
        private static readonly string[] EngineIdRejectTokens = { "rcs", "monoprop", "mono", "vernier" };
        private static readonly string[] PartNameRejectTokens = { "jet", "turbine", "airbreathing", "air breathing", "rcs", "monoprop" };

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
                if (engineModule == null || !activeEngineModules.Add(engineModule))
                {
                    continue;
                }
            }

            int activeCluster = Mathf.Max(1, activeEngineModules.Count);
            for (int i = 0; i < engineEmitters.Count; i++)
            {
                engineEmitters[i].SetEngineClusterCount(activeCluster);
            }

            for (int i = 0; i < engineEmitters.Count; i++)
            {
                engineEmitters[i].Tick(vessel, dt);
            }

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
