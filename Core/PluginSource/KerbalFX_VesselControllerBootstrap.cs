using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace KerbalFX
{
    internal interface IVesselFxController
    {
        bool IsStillValid();
        void Tick(float dt);
        void StopAll();
        void Dispose();
    }

    internal abstract class KerbalFxVesselControllerBootstrap<TController> : MonoBehaviour
        where TController : class, IVesselFxController
    {
        private struct FailedProbe
        {
            public int PartCount;
            public uint PartSignature;
            public int ConfigRevision;
            public float RetryAt;
        }

        private readonly Dictionary<Guid, TController> controllers = new Dictionary<Guid, TController>();
        private readonly List<TController> controllerList = new List<TController>();
        private readonly Dictionary<Guid, float> invalidTimers = new Dictionary<Guid, float>();
        private readonly Dictionary<Guid, FailedProbe> failedProbes = new Dictionary<Guid, FailedProbe>();
        private readonly Queue<Vessel> refreshQueue = new Queue<Vessel>(16);
        private readonly HashSet<Guid> queuedRefreshIds = new HashSet<Guid>();
        private readonly List<Guid> removeIds = new List<Guid>(32);
        private bool controllerListDirty = true;

        private float controllerRefreshTimer;
        private float settingsRefreshTimer;
        private float debugHeartbeatTimer;
        private float queuedRefreshElapsed;
        private int failedProbeConfigRevision = int.MinValue;
        private bool emittersStoppedWhileDisabled;

        private const float FailedProbeRetrySeconds = 15.0f;
        private const int RefreshVesselBudgetPerFrame = 2;

        private static readonly ProfilerMarker RefreshSettingsMarker =
            new ProfilerMarker("KerbalFX.Bootstrap.RefreshSettings");
        private static readonly ProfilerMarker RefreshControllersMarker =
            new ProfilerMarker("KerbalFX.Bootstrap.RefreshControllers");
        private static readonly ProfilerMarker TryAttachControllerMarker =
            new ProfilerMarker("KerbalFX.Bootstrap.TryAttachController");
        private static readonly ProfilerMarker TryRebuildControllerMarker =
            new ProfilerMarker("KerbalFX.Bootstrap.TryRebuildController");

        protected virtual float ControllerRefreshInterval { get { return 1.0f; } }
        protected virtual float SettingsRefreshInterval { get { return 5.0f; } }
        protected virtual float ControllerInvalidGraceSeconds { get { return 4.0f; } }
        protected virtual float HeartbeatInterval { get { return 2.5f; } }
        protected virtual int CurrentConfigRevision { get { return 0; } }

        protected abstract bool IsModuleEnabled { get; }
        protected abstract bool IsDebugLogging { get; }
        protected abstract void RefreshSettings();
        protected abstract bool IsSupportedVessel(Vessel vessel);
        protected abstract TController CreateController(Vessel vessel);
        protected abstract bool ControllerHasEmitters(TController controller);
        protected abstract int ControllerEmitterCount(TController controller);
        protected abstract void TryRebuildController(TController controller, float refreshElapsed);

        protected abstract void LogBootstrapStart();
        protected abstract void LogBootstrapStop();
        protected abstract void LogHeartbeat(int controllerCount);
        protected abstract void LogAttached(int emitterCount, string vesselName);

        protected virtual void OnBeforeStart() { }
        protected virtual void OnFrameStart(float dt) { }
        protected virtual void OnFrameDisabled(float dt) { }
        protected virtual void OnFrameEnabled(float dt) { }
        protected virtual void OnBeforeDestroy() { }

        protected int ControllerCount { get { return controllers.Count; } }

        private void Start()
        {
            OnBeforeStart();
            RefreshSettingsMarker.Begin();
            try
            {
                RefreshSettings();
                failedProbeConfigRevision = CurrentConfigRevision;
            }
            finally
            {
                RefreshSettingsMarker.End();
            }

            RefreshControllers(0f);
            ApplyInitialRefreshJitter();
            LogBootstrapStart();
        }

        private void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            float dt = Time.deltaTime;
            OnFrameStart(dt);
            RefreshSettingsIfNeeded(dt);

            if (!IsModuleEnabled)
            {
                if (!emittersStoppedWhileDisabled)
                {
                    StopAllEmitters();
                    emittersStoppedWhileDisabled = true;
                }
                OnFrameDisabled(dt);
                return;
            }

            emittersStoppedWhileDisabled = false;
            RefreshControllersIfNeeded(dt);
            ProcessQueuedRefreshVessels();
            TickControllers(dt);
            LogHeartbeatIfNeeded(dt);
            OnFrameEnabled(dt);
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
            invalidTimers.Clear();
            failedProbes.Clear();
            refreshQueue.Clear();
            queuedRefreshIds.Clear();
            OnBeforeDestroy();
            LogBootstrapStop();
        }

        private void RefreshSettingsIfNeeded(float dt)
        {
            settingsRefreshTimer -= dt;
            if (settingsRefreshTimer > 0f)
                return;

            settingsRefreshTimer = SettingsRefreshInterval;
            RefreshSettingsMarker.Begin();
            try
            {
                RefreshSettings();
                ClearFailedProbesIfConfigChanged();
            }
            finally
            {
                RefreshSettingsMarker.End();
            }
        }

        private void RefreshControllersIfNeeded(float dt)
        {
            controllerRefreshTimer -= dt;
            if (controllerRefreshTimer > 0f)
                return;

            float refreshElapsed = ControllerRefreshInterval - controllerRefreshTimer;
            controllerRefreshTimer = ControllerRefreshInterval;
            RefreshControllers(Mathf.Max(0f, refreshElapsed));
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
            if (!IsDebugLogging)
                return;

            debugHeartbeatTimer -= dt;
            if (debugHeartbeatTimer > 0f)
                return;

            debugHeartbeatTimer = HeartbeatInterval;
            LogHeartbeat(controllers.Count);
        }

        private void StopAllEmitters()
        {
            var e = controllers.GetEnumerator();
            while (e.MoveNext())
                e.Current.Value.StopAll();
            e.Dispose();
        }

        private void RefreshControllers(float refreshElapsed)
        {
            RefreshControllersMarker.Begin();
            try
            {
                RemoveInvalidControllers(refreshElapsed);
                AttachOrRefreshLoadedVessels(refreshElapsed);
            }
            finally
            {
                RefreshControllersMarker.End();
            }
        }

        private void RemoveInvalidControllers(float refreshElapsed)
        {
            removeIds.Clear();
            float grace = ControllerInvalidGraceSeconds;

            var e = controllers.GetEnumerator();
            while (e.MoveNext())
            {
                if (!e.Current.Value.IsStillValid())
                {
                    invalidTimers.TryGetValue(e.Current.Key, out var invalidTimer);
                    invalidTimer += refreshElapsed;
                    invalidTimers[e.Current.Key] = invalidTimer;

                    if (invalidTimer >= grace)
                    {
                        e.Current.Value.Dispose();
                        removeIds.Add(e.Current.Key);
                    }
                }
                else
                {
                    invalidTimers.Remove(e.Current.Key);
                }
            }
            e.Dispose();

            for (int i = 0; i < removeIds.Count; i++)
                RemoveController(removeIds[i]);
        }

        private void RemoveController(Guid vesselId)
        {
            controllers.Remove(vesselId);
            invalidTimers.Remove(vesselId);
            failedProbes.Remove(vesselId);
            controllerListDirty = true;
        }

        private void AttachOrRefreshLoadedVessels(float refreshElapsed)
        {
            List<Vessel> loaded = FlightGlobals.VesselsLoaded;
            if (loaded == null)
                return;

            refreshQueue.Clear();
            queuedRefreshIds.Clear();
            queuedRefreshElapsed = refreshElapsed;

            Vessel active = FlightGlobals.ActiveVessel;
            if (active != null && IsSupportedVessel(active))
                RefreshSingleVessel(active, refreshElapsed);

            for (int i = 0; i < loaded.Count; i++)
            {
                Vessel vessel = loaded[i];
                if (!IsSupportedVessel(vessel))
                    continue;

                if (vessel == active)
                    continue;

                if (queuedRefreshIds.Add(vessel.id))
                    refreshQueue.Enqueue(vessel);
            }

            ProcessQueuedRefreshVessels();
        }

        private void ProcessQueuedRefreshVessels()
        {
            int budget = RefreshVesselBudgetPerFrame;
            while (budget > 0 && refreshQueue.Count > 0)
            {
                Vessel vessel = refreshQueue.Dequeue();
                if (vessel != null)
                    queuedRefreshIds.Remove(vessel.id);
                if (IsSupportedVessel(vessel))
                    RefreshSingleVessel(vessel, queuedRefreshElapsed);
                budget--;
            }
        }

        private void RefreshSingleVessel(Vessel vessel, float refreshElapsed)
        {
            if (vessel == null)
                return;

            if (controllers.TryGetValue(vessel.id, out var controller))
            {
                TryRebuildControllerMarker.Begin();
                try
                {
                    TryRebuildController(controller, refreshElapsed);
                }
                finally
                {
                    TryRebuildControllerMarker.End();
                }
                return;
            }

            TryAttachController(vessel);
        }

        private void TryAttachController(Vessel vessel)
        {
            TryAttachControllerMarker.Begin();
            try
            {
                if (ShouldSkipFailedProbe(vessel))
                    return;

                TController controller = CreateController(vessel);
                if (controller == null || !ControllerHasEmitters(controller))
                {
                    if (controller != null)
                        controller.Dispose();
                    RecordFailedProbe(vessel);
                    return;
                }

                controllers.Add(vessel.id, controller);
                failedProbes.Remove(vessel.id);
                invalidTimers.Remove(vessel.id);
                controllerListDirty = true;
                LogAttached(ControllerEmitterCount(controller), vessel.vesselName);
            }
            finally
            {
                TryAttachControllerMarker.End();
            }
        }

        private void ApplyInitialRefreshJitter()
        {
            float jitter = GetDeterministicJitter01();
            settingsRefreshTimer = SettingsRefreshInterval * jitter;
            controllerRefreshTimer = ControllerRefreshInterval * (0.35f + 0.65f * jitter);
        }

        private float GetDeterministicJitter01()
        {
            string name = GetType().FullName;
            if (string.IsNullOrEmpty(name))
                name = GetType().Name;

            unchecked
            {
                int hash = 23;
                for (int i = 0; i < name.Length; i++)
                    hash = hash * 31 + name[i];
                return (Mathf.Abs(hash) % 1000) / 1000f;
            }
        }

        private void ClearFailedProbesIfConfigChanged()
        {
            int revision = CurrentConfigRevision;
            if (revision == failedProbeConfigRevision)
                return;

            failedProbeConfigRevision = revision;
            failedProbes.Clear();
        }

        private bool ShouldSkipFailedProbe(Vessel vessel)
        {
            if (vessel == null || vessel.parts == null)
                return true;

            FailedProbe failed;
            if (!failedProbes.TryGetValue(vessel.id, out failed))
                return false;

            int configRevision = CurrentConfigRevision;
            if (failed.ConfigRevision != configRevision)
                return false;

            int partCount = vessel.parts.Count;
            uint signature = KerbalFxUtil.ComputeVesselPartSignature(vessel);
            return failed.PartCount == partCount
                && failed.PartSignature == signature
                && Time.time < failed.RetryAt;
        }

        private void RecordFailedProbe(Vessel vessel)
        {
            if (vessel == null)
                return;

            failedProbes[vessel.id] = new FailedProbe
            {
                PartCount = vessel.parts != null ? vessel.parts.Count : -1,
                PartSignature = KerbalFxUtil.ComputeVesselPartSignature(vessel),
                ConfigRevision = CurrentConfigRevision,
                RetryAt = Time.time + FailedProbeRetrySeconds
            };
        }
    }
}
