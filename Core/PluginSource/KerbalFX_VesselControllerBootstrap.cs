using System;
using System.Collections.Generic;
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
        private readonly Dictionary<Guid, TController> controllers = new Dictionary<Guid, TController>();
        private readonly List<TController> controllerList = new List<TController>();
        private readonly Dictionary<Guid, float> invalidTimers = new Dictionary<Guid, float>();
        private readonly List<Guid> removeIds = new List<Guid>(32);
        private bool controllerListDirty = true;

        private float controllerRefreshTimer;
        private float settingsRefreshTimer;
        private float debugHeartbeatTimer;
        private bool emittersStoppedWhileDisabled;

        protected virtual float ControllerRefreshInterval { get { return 1.0f; } }
        protected virtual float SettingsRefreshInterval { get { return 0.5f; } }
        protected virtual float ControllerInvalidGraceSeconds { get { return 4.0f; } }
        protected virtual float HeartbeatInterval { get { return 2.5f; } }

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
            RefreshSettings();
            RefreshControllers(0f);
            controllerRefreshTimer = ControllerRefreshInterval;
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
            OnBeforeDestroy();
            LogBootstrapStop();
        }

        private void RefreshSettingsIfNeeded(float dt)
        {
            settingsRefreshTimer -= dt;
            if (settingsRefreshTimer > 0f)
                return;

            settingsRefreshTimer = SettingsRefreshInterval;
            RefreshSettings();
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
            RemoveInvalidControllers(refreshElapsed);
            AttachOrRefreshLoadedVessels(refreshElapsed);
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
                    float invalidTimer;
                    invalidTimers.TryGetValue(e.Current.Key, out invalidTimer);
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
            controllerListDirty = true;
        }

        private void AttachOrRefreshLoadedVessels(float refreshElapsed)
        {
            List<Vessel> loaded = FlightGlobals.VesselsLoaded;
            if (loaded == null)
                return;

            for (int i = 0; i < loaded.Count; i++)
            {
                Vessel vessel = loaded[i];
                if (!IsSupportedVessel(vessel))
                    continue;

                TController controller;
                if (controllers.TryGetValue(vessel.id, out controller))
                {
                    TryRebuildController(controller, refreshElapsed);
                    continue;
                }

                TryAttachController(vessel);
            }
        }

        private void TryAttachController(Vessel vessel)
        {
            TController controller = CreateController(vessel);
            if (controller == null || !ControllerHasEmitters(controller))
            {
                if (controller != null)
                    controller.Dispose();
                return;
            }

            controllers.Add(vessel.id, controller);
            invalidTimers.Remove(vessel.id);
            controllerListDirty = true;
            LogAttached(ControllerEmitterCount(controller), vessel.vesselName);
        }
    }
}
