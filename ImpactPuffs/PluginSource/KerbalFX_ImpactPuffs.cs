using System;
using System.Collections.Generic;
using System.Globalization;
using KSP.Localization;
using UnityEngine;

namespace KerbalFX.ImpactPuffs
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ImpactPuffsBootstrap : MonoBehaviour
    {
        private readonly Dictionary<Guid, VesselImpactController> controllers = new Dictionary<Guid, VesselImpactController>();
        private readonly List<VesselImpactController> controllerList = new List<VesselImpactController>();
        private bool controllerListDirty = true;
        private readonly Dictionary<Guid, float> invalidControllerTimers = new Dictionary<Guid, float>();
        private readonly List<Guid> removeControllerIds = new List<Guid>(32);

        private float controllerRefreshTimer;
        private float settingsRefreshTimer;
        private float debugHeartbeatTimer;
        private bool emittersStoppedWhileDisabled;

        private const float ControllerRefreshInterval = 1.0f;
        private const float SettingsRefreshInterval = 0.5f;
        private const float ControllerInvalidGraceSeconds = 4.0f;
        private const float HeartbeatInterval = 2.5f;

        private void Start()
        {
            ImpactPuffsConfig.Refresh();
            ImpactPuffsRuntimeConfig.Refresh();
            EngineGroundPuffEmitter.CleanupSunOcclusionCache(true);
            ImpactPuffsLog.Info(Localizer.Format(ImpactPuffsLoc.LogBootstrapStart));
        }

        private void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight)
            {
                return;
            }

            float dt = Time.deltaTime;
            RefreshSettingsIfNeeded(dt);
            if (!ImpactPuffsConfig.Enabled)
            {
                if (!emittersStoppedWhileDisabled)
                {
                    StopAllEmitters();
                    emittersStoppedWhileDisabled = true;
                }

                EngineGroundPuffEmitter.CleanupSunOcclusionCache(false);
                return;
            }

            emittersStoppedWhileDisabled = false;
            RefreshControllersIfNeeded(dt);
            TickControllers(dt);
            LogHeartbeatIfNeeded(dt);
            EngineGroundPuffEmitter.CleanupSunOcclusionCache(false);
        }

        private void RefreshSettingsIfNeeded(float dt)
        {
            settingsRefreshTimer -= dt;
            if (settingsRefreshTimer > 0f)
            {
                return;
            }

            settingsRefreshTimer = SettingsRefreshInterval;
            ImpactPuffsConfig.Refresh();
            ImpactPuffsRuntimeConfig.TryHotReloadFromDisk();
        }

        private void RefreshControllersIfNeeded(float dt)
        {
            controllerRefreshTimer -= dt;
            if (controllerRefreshTimer > 0f)
            {
                return;
            }

            controllerRefreshTimer = ControllerRefreshInterval;
            RefreshControllers();
        }

        private void TickControllers(float dt)
        {
            if (controllerListDirty)
            {
                controllerListDirty = false;
                controllerList.Clear();
                var e = controllers.GetEnumerator();
                while (e.MoveNext())
                    controllerList.Add(e.Current.Value);
                e.Dispose();
            }
            for (int i = 0; i < controllerList.Count; i++)
                controllerList[i].Tick(dt);
        }

        private void LogHeartbeatIfNeeded(float dt)
        {
            if (!ImpactPuffsConfig.DebugLogging)
            {
                return;
            }

            debugHeartbeatTimer -= dt;
            if (debugHeartbeatTimer > 0f)
            {
                return;
            }

            debugHeartbeatTimer = HeartbeatInterval;
            ImpactPuffsLog.DebugLog(Localizer.Format(ImpactPuffsLoc.LogHeartbeat, controllers.Count));
        }

        private void StopAllEmitters()
        {
            var e = controllers.GetEnumerator();
            while (e.MoveNext())
                e.Current.Value.StopAll();
            e.Dispose();
        }

        private void RefreshControllers()
        {
            RemoveInvalidControllers();
            AttachOrRefreshLoadedVessels();
        }

        private void RemoveInvalidControllers()
        {
            removeControllerIds.Clear();
            var e = controllers.GetEnumerator();
            while (e.MoveNext())
            {
                if (!e.Current.Value.IsStillValid())
                {
                    float invalidTimer;
                    invalidControllerTimers.TryGetValue(e.Current.Key, out invalidTimer);
                    invalidTimer += ControllerRefreshInterval;
                    invalidControllerTimers[e.Current.Key] = invalidTimer;

                    if (invalidTimer >= ControllerInvalidGraceSeconds)
                    {
                        e.Current.Value.Dispose();
                        removeControllerIds.Add(e.Current.Key);
                    }
                }
                else
                {
                    invalidControllerTimers.Remove(e.Current.Key);
                }
            }
            e.Dispose();

            for (int i = 0; i < removeControllerIds.Count; i++)
            {
                RemoveController(removeControllerIds[i]);
            }
        }

        private void RemoveController(Guid vesselId)
        {
            controllers.Remove(vesselId);
            invalidControllerTimers.Remove(vesselId);
            controllerListDirty = true;
        }

        private void AttachOrRefreshLoadedVessels()
        {
            List<Vessel> loaded = FlightGlobals.VesselsLoaded;
            if (loaded == null)
            {
                return;
            }

            for (int i = 0; i < loaded.Count; i++)
            {
                Vessel vessel = loaded[i];
                if (!IsSupportedVessel(vessel))
                {
                    continue;
                }

                VesselImpactController controller;
                if (controllers.TryGetValue(vessel.id, out controller))
                {
                    controller.TryRebuild();
                    continue;
                }

                TryAttachController(vessel);
            }
        }

        private void TryAttachController(Vessel vessel)
        {
            VesselImpactController controller = new VesselImpactController(vessel);
            if (!controller.HasAnyEmitters)
            {
                controller.Dispose();
                return;
            }

            controllers.Add(vessel.id, controller);
            invalidControllerTimers.Remove(vessel.id);
            controllerListDirty = true;
            ImpactPuffsLog.DebugLog(Localizer.Format(ImpactPuffsLoc.LogAttached, controller.EngineEmitterCount, vessel.vesselName));
        }

        private static bool IsSupportedVessel(Vessel vessel)
        {
            if (vessel == null || !vessel.loaded || vessel.packed || vessel.isEVA)
            {
                return false;
            }

            return vessel.vesselType != VesselType.Flag && vessel.vesselType != VesselType.Debris;
        }

        private void OnDestroy()
        {
            var e = controllers.GetEnumerator();
            while (e.MoveNext())
                e.Current.Value.Dispose();
            e.Dispose();

            controllers.Clear();
            controllerList.Clear();
            controllerListDirty = true;
            invalidControllerTimers.Clear();
            EngineGroundPuffEmitter.CleanupSunOcclusionCache(true);
            ImpactPuffsLog.Info(Localizer.Format(ImpactPuffsLoc.LogBootstrapStop));
        }
    }

    internal sealed class VesselImpactController
    {
        private readonly Vessel vessel;
        private readonly List<EngineGroundPuffEmitter> engineEmitters = new List<EngineGroundPuffEmitter>();
        private readonly List<ModuleEngines> activeEngineModules = new List<ModuleEngines>(8);
        private TouchdownBurstEmitter touchdownEmitter;
        private int cachedPartCount = -1;
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

            if (vessel.parts.Count != cachedPartCount)
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
                if (engineModule == null || activeEngineModules.Contains(engineModule))
                {
                    continue;
                }

                activeEngineModules.Add(engineModule);
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
            cachedPartCount = vessel != null && vessel.parts != null ? vessel.parts.Count : -1;
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
                ModuleEngines engine = part.Modules[i] as ModuleEngines;
                if (engine == null)
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
            if (transforms.Count == 0)
            {
                engineEmitters.Add(new EngineGroundPuffEmitter(part, engine, null));
                return;
            }

            for (int i = 0; i < transforms.Count; i++)
            {
                engineEmitters.Add(new EngineGroundPuffEmitter(part, engine, transforms[i]));
            }
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
            if (engine == null)
            {
                return string.Empty;
            }

            Type type = engine.GetType();
            if (type == null)
            {
                return string.Empty;
            }

            try
            {
                System.Reflection.FieldInfo field = type.GetField("engineType");
                if (field != null)
                {
                    object fieldValue = field.GetValue(engine);
                    if (fieldValue != null)
                    {
                        return fieldValue.ToString().ToLowerInvariant();
                    }
                }

                System.Reflection.PropertyInfo property = type.GetProperty("engineType");
                if (property != null)
                {
                    object propertyValue = property.GetValue(engine, null);
                    if (propertyValue != null)
                    {
                        return propertyValue.ToString().ToLowerInvariant();
                    }
                }
            }
            catch (Exception ex)
            {
                ImpactPuffsLog.DebugLog("ReadEngineTypeName reflection failed: " + ex.Message);
            }

            return string.Empty;
        }

        private static string SafeLower(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.ToLowerInvariant();
        }

    }

}

