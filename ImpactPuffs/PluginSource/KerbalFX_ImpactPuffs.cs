using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using KSP.Localization;
using UnityEngine;

namespace KerbalFX.ImpactPuffs
{
    internal static class ImpactPuffsLoc
    {
        public const string UiSection = "#LOC_KerbalFX_UI_Section";
        public const string UiTitle = "#LOC_KerbalFX_ImpactPuffs_UI_Title";

        public const string UiSimplified = "#LOC_KerbalFX_ImpactPuffs_UI_Simplified";
        public const string UiSimplifiedTip = "#LOC_KerbalFX_ImpactPuffs_UI_Simplified_TT";
        public const string UiDebug = "#LOC_KerbalFX_ImpactPuffs_UI_Debug";
        public const string UiDebugTip = "#LOC_KerbalFX_ImpactPuffs_UI_Debug_TT";

        public const string LogSettingsUpdated = "#LOC_KerbalFX_ImpactPuffs_Log_SettingsUpdated";
        public const string LogBootstrapStart = "#LOC_KerbalFX_ImpactPuffs_Log_BootstrapStart";
        public const string LogBootstrapStop = "#LOC_KerbalFX_ImpactPuffs_Log_BootstrapStop";
        public const string LogHeartbeat = "#LOC_KerbalFX_ImpactPuffs_Log_Heartbeat";
        public const string LogAttached = "#LOC_KerbalFX_ImpactPuffs_Log_Attached";
        public const string LogEngineEmitter = "#LOC_KerbalFX_ImpactPuffs_Log_EngineEmitter";
        public const string LogBurst = "#LOC_KerbalFX_ImpactPuffs_Log_Burst";
    }

    public class ImpactPuffsParameters : GameParameters.CustomParameterNode
    {
        [GameParameters.CustomParameterUI(ImpactPuffsLoc.UiSimplified, toolTip = ImpactPuffsLoc.UiSimplifiedTip)]
        public bool useSimplifiedEffects;

        [GameParameters.CustomParameterUI(ImpactPuffsLoc.UiDebug, toolTip = ImpactPuffsLoc.UiDebugTip)]
        public bool debugLogging;

        public override string Title
        {
            get { return Localizer.Format(ImpactPuffsLoc.UiTitle); }
        }

        public override GameParameters.GameMode GameMode
        {
            get { return GameParameters.GameMode.ANY; }
        }

        public override string Section
        {
            get { return "KerbalFX"; }
        }

        public override string DisplaySection
        {
            get { return Localizer.Format(ImpactPuffsLoc.UiSection); }
        }

        public override int SectionOrder
        {
            get { return 5; }
        }

        public override bool HasPresets
        {
            get { return false; }
        }
    }

    internal static class ImpactPuffsConfig
    {
        public static bool UseSimplifiedEffects = false;
        public static bool DebugLogging;
        public static int Revision;

        private static bool initialized;

        public static void Refresh()
        {
            bool newUseSimplifiedEffects = false;
            bool newDebug = false;

            if (HighLogic.CurrentGame != null && HighLogic.CurrentGame.Parameters != null)
            {
                ImpactPuffsParameters p = HighLogic.CurrentGame.Parameters.CustomParams<ImpactPuffsParameters>();
                if (p != null)
                {
                    newUseSimplifiedEffects = p.useSimplifiedEffects;
                    newDebug = p.debugLogging;
                }
            }

            bool changed = !initialized
                || newUseSimplifiedEffects != UseSimplifiedEffects
                || newDebug != DebugLogging;

            UseSimplifiedEffects = newUseSimplifiedEffects;
            DebugLogging = newDebug;

            if (changed)
            {
                initialized = true;
                Revision++;
                ImpactPuffsLog.Info(Localizer.Format(
                    ImpactPuffsLoc.LogSettingsUpdated,
                    UseSimplifiedEffects,
                    DebugLogging
                ));
            }
        }
    }

    internal static class ImpactPuffsRuntimeConfig
    {
        public static bool Loaded;
        public static int Revision;

        public static float EmissionMultiplier = 1.00f;
        public static float MaxParticlesMultiplier = 1.00f;
        public static float RadiusScaleMultiplier = 1.00f;

        public static float MaxRayDistance = 42f;
        public static float MinNormalizedThrust = 0.05f;
        public static float ShadowLightFactor = 0.28f;
        public static float LateralSpreadMultiplier = 2.40f;
        public static float VerticalLiftMultiplier = 0.60f;
        public static float TurbulenceMultiplier = 1.45f;
        public static float RingExpansionMultiplier = 1.55f;
        public static float DynamicSwayMultiplier = 1.00f;

        public static float EngineCountExponent = 0.72f;
        public static float EngineCountMinScale = 0.22f;
        public static float MinExhaustToGroundAlignment = 0.18f;
        public static float MinRayDirectionToBodyDown = 0.22f;
        public static float MinExhaustToBodyDown = 0.20f;

        public static float ThrustPowerReference = 180f;
        public static float ThrustPowerExponent = 0.60f;
        public static float ThrustPowerMinScale = 0.35f;
        public static float ThrustPowerMaxScale = 3.80f;

        public static float MaxDistanceAtLowThrust = 9.0f;
        public static float MaxDistanceAtHighThrust = 42.0f;

        public static float TouchdownMinSpeed = 2.2f;
        public static float TouchdownBurstMultiplier = 1.00f;

        private static readonly Dictionary<string, float> BodyVisibilityMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static DateTime lastConfigWriteUtc = DateTime.MinValue;

        public static void Refresh()
        {
            ReloadFromGameDatabase();
            PrimeConfigFileStamp(GetConfigPath(), ref lastConfigWriteUtc);
        }

        public static void TryHotReloadFromDisk()
        {
            if (!HasFileChanged(GetConfigPath(), ref lastConfigWriteUtc))
            {
                return;
            }

            ReloadFromDisk();
        }

        public static float GetBodyVisibilityMultiplier(string bodyName)
        {
            if (string.IsNullOrEmpty(bodyName))
            {
                return 1f;
            }

            float multiplier;
            if (BodyVisibilityMultipliers.TryGetValue(bodyName.Trim(), out multiplier))
            {
                return Mathf.Clamp(multiplier, 0.40f, 3.00f);
            }

            return 1f;
        }

        private static void ReloadFromGameDatabase()
        {
            SeedDefaults();

            if (GameDatabase.Instance != null)
            {
                ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("KERBALFX_IMPACT_PUFFS");
                if (nodes != null)
                {
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        if (nodes[i] != null)
                        {
                            ApplyFromNode(nodes[i]);
                        }
                    }
                }
            }

            Loaded = true;
            Revision++;
            LogCurrentConfig("GameDatabase");
        }

        private static void ReloadFromDisk()
        {
            SeedDefaults();

            try
            {
                string path = GetConfigPath();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    ConfigNode root = ConfigNode.Load(path);
                    if (root != null)
                    {
                        ConfigNode[] nodes = root.GetNodes("KERBALFX_IMPACT_PUFFS");
                        if (nodes != null)
                        {
                            for (int i = 0; i < nodes.Length; i++)
                            {
                                if (nodes[i] != null)
                                {
                                    ApplyFromNode(nodes[i]);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ImpactPuffsLog.Info("HotReload failed for ImpactPuffs config: " + ex.Message);
            }

            Loaded = true;
            Revision++;
            LogCurrentConfig("HotReload");
        }

        private static void SeedDefaults()
        {
            EmissionMultiplier = 1.00f;
            MaxParticlesMultiplier = 1.00f;
            RadiusScaleMultiplier = 1.00f;

            MaxRayDistance = 42f;
            MinNormalizedThrust = 0.05f;
            ShadowLightFactor = 0.28f;
            LateralSpreadMultiplier = 2.40f;
            VerticalLiftMultiplier = 0.60f;
            TurbulenceMultiplier = 1.45f;
            RingExpansionMultiplier = 1.55f;
            DynamicSwayMultiplier = 1.00f;

            EngineCountExponent = 0.72f;
            EngineCountMinScale = 0.22f;
            MinExhaustToGroundAlignment = 0.18f;
            MinRayDirectionToBodyDown = 0.22f;
            MinExhaustToBodyDown = 0.20f;

            ThrustPowerReference = 180f;
            ThrustPowerExponent = 0.60f;
            ThrustPowerMinScale = 0.35f;
            ThrustPowerMaxScale = 3.80f;

            MaxDistanceAtLowThrust = 9.0f;
            MaxDistanceAtHighThrust = 42.0f;

            TouchdownMinSpeed = 2.2f;
            TouchdownBurstMultiplier = 1.00f;

            BodyVisibilityMultipliers.Clear();
            BodyVisibilityMultipliers["Kerbin"] = 1.00f;
            BodyVisibilityMultipliers["Mun"] = 1.22f;
            BodyVisibilityMultipliers["Minmus"] = 1.18f;
            BodyVisibilityMultipliers["Duna"] = 1.00f;
            BodyVisibilityMultipliers["Ike"] = 1.06f;
            BodyVisibilityMultipliers["Eve"] = 1.08f;
            BodyVisibilityMultipliers["Moho"] = 1.10f;
            BodyVisibilityMultipliers["Dres"] = 1.06f;
            BodyVisibilityMultipliers["Vall"] = 1.06f;
            BodyVisibilityMultipliers["Tylo"] = 1.00f;
            BodyVisibilityMultipliers["Bop"] = 1.08f;
            BodyVisibilityMultipliers["Pol"] = 1.08f;
            BodyVisibilityMultipliers["Eeloo"] = 1.12f;
        }

        private static void ApplyFromNode(ConfigNode node)
        {
            EmissionMultiplier = ReadFloat(node, "EmissionMultiplier", EmissionMultiplier, 0.10f, 8.00f);
            MaxParticlesMultiplier = ReadFloat(node, "MaxParticlesMultiplier", MaxParticlesMultiplier, 0.25f, 6.00f);
            RadiusScaleMultiplier = ReadFloat(node, "RadiusScaleMultiplier", RadiusScaleMultiplier, 0.25f, 4.00f);

            MaxRayDistance = ReadFloat(node, "MaxRayDistance", MaxRayDistance, 8f, 120f);
            MinNormalizedThrust = ReadFloat(node, "MinNormalizedThrust", MinNormalizedThrust, 0.01f, 1f);
            ShadowLightFactor = ReadFloat(node, "ShadowLightFactor", ShadowLightFactor, 0.0f, 1f);
            LateralSpreadMultiplier = ReadFloat(node, "LateralSpreadMultiplier", LateralSpreadMultiplier, 0.30f, 8f);
            VerticalLiftMultiplier = ReadFloat(node, "VerticalLiftMultiplier", VerticalLiftMultiplier, 0.10f, 3f);
            TurbulenceMultiplier = ReadFloat(node, "TurbulenceMultiplier", TurbulenceMultiplier, 0.10f, 4f);
            RingExpansionMultiplier = ReadFloat(node, "RingExpansionMultiplier", RingExpansionMultiplier, 0.30f, 6f);
            DynamicSwayMultiplier = ReadFloat(node, "DynamicSwayMultiplier", DynamicSwayMultiplier, 0.10f, 4f);

            EngineCountExponent = ReadFloat(node, "EngineCountExponent", EngineCountExponent, 0.15f, 2.20f);
            EngineCountMinScale = ReadFloat(node, "EngineCountMinScale", EngineCountMinScale, 0.05f, 1.00f);
            MinExhaustToGroundAlignment = ReadFloat(node, "MinExhaustToGroundAlignment", MinExhaustToGroundAlignment, 0.00f, 0.95f);
            MinRayDirectionToBodyDown = ReadFloat(node, "MinRayDirectionToBodyDown", MinRayDirectionToBodyDown, -0.20f, 0.95f);
            MinExhaustToBodyDown = ReadFloat(node, "MinExhaustToBodyDown", MinExhaustToBodyDown, 0.00f, 0.98f);

            ThrustPowerReference = ReadFloat(node, "ThrustPowerReference", ThrustPowerReference, 10f, 900f);
            ThrustPowerExponent = ReadFloat(node, "ThrustPowerExponent", ThrustPowerExponent, 0.10f, 2.50f);
            ThrustPowerMinScale = ReadFloat(node, "ThrustPowerMinScale", ThrustPowerMinScale, 0.05f, 4.00f);
            ThrustPowerMaxScale = ReadFloat(node, "ThrustPowerMaxScale", ThrustPowerMaxScale, 0.20f, 8.00f);
            if (ThrustPowerMaxScale < ThrustPowerMinScale)
            {
                ThrustPowerMaxScale = ThrustPowerMinScale;
            }

            MaxDistanceAtLowThrust = ReadFloat(node, "MaxDistanceAtLowThrust", MaxDistanceAtLowThrust, 2f, 80f);
            MaxDistanceAtHighThrust = ReadFloat(node, "MaxDistanceAtHighThrust", MaxDistanceAtHighThrust, 3f, 120f);
            if (MaxDistanceAtHighThrust < MaxDistanceAtLowThrust)
            {
                MaxDistanceAtHighThrust = MaxDistanceAtLowThrust;
            }

            TouchdownMinSpeed = ReadFloat(node, "TouchdownMinSpeed", TouchdownMinSpeed, 0.5f, 35f);
            TouchdownBurstMultiplier = ReadFloat(node, "TouchdownBurstMultiplier", TouchdownBurstMultiplier, 0.20f, 5f);

            ConfigNode[] bodyNodes = node.GetNodes("BODY_VISIBILITY");
            if (bodyNodes != null)
            {
                for (int i = 0; i < bodyNodes.Length; i++)
                {
                    ConfigNode bodyNode = bodyNodes[i];
                    if (bodyNode == null)
                    {
                        continue;
                    }

                    string name = bodyNode.GetValue("name");
                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    float value = ReadFloat(bodyNode, "multiplier", 1f, 0.40f, 3.00f);
                    BodyVisibilityMultipliers[name.Trim()] = value;
                }
            }
        }

        private static float ReadFloat(ConfigNode node, string key, float fallback, float minValue, float maxValue)
        {
            if (node == null || string.IsNullOrEmpty(key) || !node.HasValue(key))
            {
                return fallback;
            }

            string raw = node.GetValue(key);
            if (string.IsNullOrEmpty(raw))
            {
                return fallback;
            }

            float value;
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return Mathf.Clamp(value, minValue, maxValue);
            }

            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return Mathf.Clamp(value, minValue, maxValue);
            }

            return fallback;
        }

        private static string GetConfigPath()
        {
            return Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "KerbalFX", "ImpactPuffs", "KerbalFX_ImpactPuffs.cfg");
        }

        private static void PrimeConfigFileStamp(string path, ref DateTime stamp)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            stamp = File.GetLastWriteTimeUtc(path);
        }

        private static bool HasFileChanged(string path, ref DateTime stamp)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            DateTime writeUtc = File.GetLastWriteTimeUtc(path);
            if (writeUtc <= DateTime.MinValue)
            {
                return false;
            }

            if (writeUtc <= stamp)
            {
                return false;
            }

            stamp = writeUtc;
            return true;
        }

        private static void LogCurrentConfig(string source)
        {
            ImpactPuffsLog.Info(
                "[ImpactPuffs] Config " + source
                + ": EmissionMultiplier=" + EmissionMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " MaxParticlesMultiplier=" + MaxParticlesMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " RadiusScaleMultiplier=" + RadiusScaleMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " MaxRayDistance=" + MaxRayDistance.ToString("F1", CultureInfo.InvariantCulture)
                + " MinNormalizedThrust=" + MinNormalizedThrust.ToString("F2", CultureInfo.InvariantCulture)
                + " LateralSpreadMultiplier=" + LateralSpreadMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " VerticalLiftMultiplier=" + VerticalLiftMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " TurbulenceMultiplier=" + TurbulenceMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " RingExpansionMultiplier=" + RingExpansionMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " DynamicSwayMultiplier=" + DynamicSwayMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " EngineCountExponent=" + EngineCountExponent.ToString("F2", CultureInfo.InvariantCulture)
                + " EngineCountMinScale=" + EngineCountMinScale.ToString("F2", CultureInfo.InvariantCulture)
                + " MinExhaustToGroundAlignment=" + MinExhaustToGroundAlignment.ToString("F2", CultureInfo.InvariantCulture)
                + " MinRayDirectionToBodyDown=" + MinRayDirectionToBodyDown.ToString("F2", CultureInfo.InvariantCulture)
                + " MinExhaustToBodyDown=" + MinExhaustToBodyDown.ToString("F2", CultureInfo.InvariantCulture)
                + " ThrustPowerReference=" + ThrustPowerReference.ToString("F1", CultureInfo.InvariantCulture)
                + " ThrustPowerExponent=" + ThrustPowerExponent.ToString("F2", CultureInfo.InvariantCulture)
                + " ThrustPowerMinScale=" + ThrustPowerMinScale.ToString("F2", CultureInfo.InvariantCulture)
                + " ThrustPowerMaxScale=" + ThrustPowerMaxScale.ToString("F2", CultureInfo.InvariantCulture)
                + " MaxDistanceAtLowThrust=" + MaxDistanceAtLowThrust.ToString("F1", CultureInfo.InvariantCulture)
                + " MaxDistanceAtHighThrust=" + MaxDistanceAtHighThrust.ToString("F1", CultureInfo.InvariantCulture)
                + " TouchdownMinSpeed=" + TouchdownMinSpeed.ToString("F2", CultureInfo.InvariantCulture)
                + " TouchdownBurstMultiplier=" + TouchdownBurstMultiplier.ToString("F2", CultureInfo.InvariantCulture)
                + " BodyVisibilityEntries=" + BodyVisibilityMultipliers.Count.ToString(CultureInfo.InvariantCulture)
            );
        }
    }

    internal static class ImpactPuffsLog
    {
        public static void Info(string message)
        {
            Debug.Log("[KerbalFX] " + message);
        }

        public static void DebugLog(string message)
        {
            if (ImpactPuffsConfig.DebugLogging)
            {
                Debug.Log("[KerbalFX] " + message);
            }
        }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ImpactPuffsBootstrap : MonoBehaviour
    {
        private readonly Dictionary<Guid, VesselImpactController> controllers = new Dictionary<Guid, VesselImpactController>();
        private readonly Dictionary<Guid, float> invalidControllerTimers = new Dictionary<Guid, float>();

        private float controllerRefreshTimer;
        private float settingsRefreshTimer;
        private float debugHeartbeatTimer;

        private const float ControllerRefreshInterval = 1.0f;
        private const float SettingsRefreshInterval = 0.5f;
        private const float ControllerInvalidGraceSeconds = 4.0f;

        private void Start()
        {
            ImpactPuffsConfig.Refresh();
            ImpactPuffsRuntimeConfig.Refresh();
            ImpactPuffsLog.Info(Localizer.Format(ImpactPuffsLoc.LogBootstrapStart));
        }

        private void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight)
            {
                return;
            }

            settingsRefreshTimer -= Time.deltaTime;
            if (settingsRefreshTimer <= 0f)
            {
                settingsRefreshTimer = SettingsRefreshInterval;
                ImpactPuffsConfig.Refresh();
                ImpactPuffsRuntimeConfig.TryHotReloadFromDisk();
            }

            controllerRefreshTimer -= Time.deltaTime;
            if (controllerRefreshTimer <= 0f)
            {
                controllerRefreshTimer = ControllerRefreshInterval;
                RefreshControllers();
            }

            float dt = Time.deltaTime;
            foreach (KeyValuePair<Guid, VesselImpactController> pair in controllers)
            {
                pair.Value.Tick(dt);
            }

            if (ImpactPuffsConfig.DebugLogging)
            {
                debugHeartbeatTimer -= dt;
                if (debugHeartbeatTimer <= 0f)
                {
                    debugHeartbeatTimer = 2.5f;
                    ImpactPuffsLog.DebugLog(Localizer.Format(ImpactPuffsLoc.LogHeartbeat, controllers.Count));
                }
            }
        }

        private void RefreshControllers()
        {
            List<Guid> removeIds = new List<Guid>();
            foreach (KeyValuePair<Guid, VesselImpactController> pair in controllers)
            {
                if (!pair.Value.IsStillValid())
                {
                    float invalidTimer;
                    invalidControllerTimers.TryGetValue(pair.Key, out invalidTimer);
                    invalidTimer += ControllerRefreshInterval;
                    invalidControllerTimers[pair.Key] = invalidTimer;

                    if (invalidTimer >= ControllerInvalidGraceSeconds)
                    {
                        pair.Value.Dispose();
                        removeIds.Add(pair.Key);
                    }
                }
                else if (invalidControllerTimers.ContainsKey(pair.Key))
                {
                    invalidControllerTimers.Remove(pair.Key);
                }
            }

            for (int i = 0; i < removeIds.Count; i++)
            {
                controllers.Remove(removeIds[i]);
                invalidControllerTimers.Remove(removeIds[i]);
            }

            List<Vessel> loaded = FlightGlobals.VesselsLoaded;
            if (loaded == null)
            {
                return;
            }

            for (int i = 0; i < loaded.Count; i++)
            {
                Vessel vessel = loaded[i];
                if (vessel == null || !vessel.loaded || vessel.packed || vessel.isEVA)
                {
                    continue;
                }

                if (vessel.vesselType == VesselType.Flag || vessel.vesselType == VesselType.Debris)
                {
                    continue;
                }

                VesselImpactController controller;
                if (!controllers.TryGetValue(vessel.id, out controller))
                {
                    controller = new VesselImpactController(vessel);
                    if (controller.HasAnyEmitters)
                    {
                        controllers.Add(vessel.id, controller);
                        invalidControllerTimers.Remove(vessel.id);
                        ImpactPuffsLog.DebugLog(Localizer.Format(ImpactPuffsLoc.LogAttached, controller.EngineEmitterCount, vessel.vesselName));
                    }
                    else
                    {
                        controller.Dispose();
                    }
                }
                else
                {
                    controller.TryRebuild();
                }
            }
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<Guid, VesselImpactController> pair in controllers)
            {
                pair.Value.Dispose();
            }

            controllers.Clear();
            invalidControllerTimers.Clear();
            ImpactPuffsLog.Info(Localizer.Format(ImpactPuffsLoc.LogBootstrapStop));
        }
    }

    internal sealed class VesselImpactController
    {
        private readonly Vessel vessel;
        private readonly List<EngineGroundPuffEmitter> engineEmitters = new List<EngineGroundPuffEmitter>();
        private TouchdownBurstEmitter touchdownEmitter;
        private int cachedPartCount = -1;
        private int acceptedEngineCount = 0;

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
                if (part == null || part.Modules == null)
                {
                    continue;
                }

                for (int m = 0; m < part.Modules.Count; m++)
                {
                    ModuleEngines engine = part.Modules[m] as ModuleEngines;
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
                    List<Transform> transforms = ResolveThrustTransforms(engine, part);
                    if (transforms.Count == 0)
                    {
                        engineEmitters.Add(new EngineGroundPuffEmitter(part, engine, null));
                        continue;
                    }

                    for (int t = 0; t < transforms.Count; t++)
                    {
                        engineEmitters.Add(new EngineGroundPuffEmitter(part, engine, transforms[t]));
                    }
                }
            }

            int clusterSize = Mathf.Max(1, acceptedEngineCount);
            for (int i = 0; i < engineEmitters.Count; i++)
            {
                engineEmitters[i].SetEngineClusterCount(clusterSize);
            }

            touchdownEmitter = new TouchdownBurstEmitter(vessel);

            if (ImpactPuffsConfig.DebugLogging && vessel == FlightGlobals.ActiveVessel)
            {
                ImpactPuffsLog.DebugLog("[ImpactPuffs] Vessel scan " + vessel.vesselName
                    + ": engines=" + engineModuleCount.ToString(CultureInfo.InvariantCulture)
                    + " acceptedEngines=" + acceptedEngineCount.ToString(CultureInfo.InvariantCulture)
                    + " skippedEngines=" + skippedEngineCount.ToString(CultureInfo.InvariantCulture)
                    + " emitters=" + engineEmitters.Count.ToString(CultureInfo.InvariantCulture));
            }
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
            if (ContainsAny(engineType, "mono", "rcs", "turbine", "jet", "scram", "airbreathing"))
            {
                return false;
            }

            if (HasPropellant(engine, "MonoPropellant") || HasPropellant(engine, "IntakeAir"))
            {
                return false;
            }

            string engineId = SafeLower(engine.engineID);
            if (ContainsAny(engineId, "rcs", "monoprop", "mono", "vernier"))
            {
                return false;
            }

            string partName = string.Empty;
            if (part != null && part.partInfo != null)
            {
                partName = (part.partInfo.name + " " + part.partInfo.title).ToLowerInvariant();
            }

            if (ContainsAny(partName, "jet", "turbine", "airbreathing", "air breathing", "rcs", "monoprop"))
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
            catch
            {
                // Safe fallback to keep the module running across different KSP API variants.
            }

            return string.Empty;
        }

        private static string SafeLower(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.ToLowerInvariant();
        }

        private static bool ContainsAny(string source, params string[] tokens)
        {
            if (string.IsNullOrEmpty(source) || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token) && source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class EngineGroundPuffEmitter
    {
        private readonly Part part;
        private readonly ModuleEngines engine;
        private readonly Transform thrustTransform;
        private readonly GameObject root;
        private readonly ParticleSystem particleSystem;
        private readonly VolumetricPlumeField volumetricField;
        private readonly string debugId;

        private bool disposed;
        private float smoothedRate;
        private float profileRefreshTimer;
        private float colorRefreshTimer;
        private float debugTimer;
        private float suppressionLogTimer;
        private float dynamicSwayTime;
        private float startupRampTimer;
        private float ignitionPrimeTimer;
        private bool wasEngineIgnited;

        private int appliedUiRevision = -1;
        private int appliedRuntimeRevision = -1;
        private int engineClusterCount = 1;

        private Color currentColor = new Color(0.72f, 0.68f, 0.61f, 1f);
        private float cachedLightFactor = 1f;
        private float cachedBodyVisibility = 1f;

        private const float BaseAlpha = 0.60f;
        private const float StartupRampDuration = 1.65f;
        private const float IgnitionPrimeDuration = 1.20f;
        private static readonly RaycastHit[] SharedHits = new RaycastHit[24];
        private static readonly RaycastHit[] SunOcclusionHits = new RaycastHit[16];
        private static readonly Dictionary<Guid, SunOcclusionCacheEntry> SunOcclusionCache = new Dictionary<Guid, SunOcclusionCacheEntry>();

        private struct SunOcclusionCacheEntry
        {
            public bool Occluded;
            public float ValidUntil;
            public Vector3 SamplePoint;
            public Vector3 SunDirection;
        }

        public EngineGroundPuffEmitter(Part part, ModuleEngines engine, Transform thrustTransform)
        {
            this.part = part;
            this.engine = engine;
            this.thrustTransform = thrustTransform;

            string transformName = thrustTransform != null ? thrustTransform.name : "part";
            debugId = part.partInfo.name + ":" + transformName;

            root = new GameObject("KerbalFX_EngineGroundPuff");
            root.transform.SetParent(null, false);
            root.transform.position = part.transform.position;
            root.layer = part.gameObject.layer;

            particleSystem = root.AddComponent<ParticleSystem>();
            ConfigureParticleSystemBase();
            ApplyRuntimeVisualProfile(true);
            volumetricField = new VolumetricPlumeField(root.transform, part.gameObject.layer);
        }

        public void SetEngineClusterCount(int count)
        {
            engineClusterCount = Mathf.Max(1, count);
        }

        public void Tick(Vessel vessel, float dt)
        {
            if (disposed || part == null || engine == null)
            {
                return;
            }

            profileRefreshTimer -= dt;
            if (profileRefreshTimer <= 0f
                || appliedUiRevision != ImpactPuffsConfig.Revision
                || appliedRuntimeRevision != ImpactPuffsRuntimeConfig.Revision)
            {
                profileRefreshTimer = 0.33f;
                ApplyRuntimeVisualProfile(false);
            }

            if (vessel == null || !vessel.loaded || vessel.packed || vessel.Splashed)
            {
                StopAllEmission(dt, false);
                return;
            }

            if (vessel.situation == Vessel.Situations.PRELAUNCH)
            {
                StopAllEmission(dt, true);
                return;
            }

            if (IsInKerbinLaunchsiteZone(vessel))
            {
                LogLaunchsiteSuppression("zone", vessel, null);
                StopAllEmission(dt, true);
                return;
            }

            bool engineIgnited = engine.EngineIgnited;
            if (engineIgnited && !wasEngineIgnited)
            {
                ignitionPrimeTimer = IgnitionPrimeDuration;
            }
            wasEngineIgnited = engineIgnited;
            ignitionPrimeTimer = Mathf.Max(0f, ignitionPrimeTimer - dt);

            if (!engineIgnited)
            {
                StopAllEmission(dt, false);
                return;
            }

            float currentThrust = Mathf.Max(0f, (float)engine.finalThrust);
            float normalizedThrust = GetNormalizedLiveThrust(engine);
            float throttleCommand = 0f;
            if (vessel != null && vessel.ctrlState != null)
            {
                throttleCommand = Mathf.Clamp01(vessel.ctrlState.mainThrottle);
            }
            if (ignitionPrimeTimer > 0f)
            {
                float primeT = ignitionPrimeTimer / IgnitionPrimeDuration;
                float ignitionAssist = throttleCommand * Mathf.Lerp(0.30f, 0.88f, primeT);
                normalizedThrust = Mathf.Max(normalizedThrust, ignitionAssist);
            }
            if (normalizedThrust < ImpactPuffsRuntimeConfig.MinNormalizedThrust)
            {
                StopAllEmission(dt, false);
                return;
            }

            float maxEffectiveDistance = Mathf.Lerp(
                ImpactPuffsRuntimeConfig.MaxDistanceAtLowThrust,
                ImpactPuffsRuntimeConfig.MaxDistanceAtHighThrust,
                Mathf.Clamp01(normalizedThrust)
            );
            maxEffectiveDistance = Mathf.Clamp(maxEffectiveDistance, 2f, ImpactPuffsRuntimeConfig.MaxRayDistance);

            float terrainHeight = GetSafeTerrainHeightAgl(vessel);
            float terrainGate = maxEffectiveDistance * 1.35f + 3f;
            if (terrainHeight >= 0f && terrainHeight > terrainGate)
            {
                StopAllEmission(dt, false);
                return;
            }

            Vector3 origin = thrustTransform != null ? thrustTransform.position : part.transform.position;
            Vector3 exhaustDirection = GetPrimaryExhaustDirection(origin);
            Vector3 bodyDown = Vector3.down;
            if (vessel != null && vessel.mainBody != null)
            {
                Vector3 toBody = vessel.mainBody.position - origin;
                if (toBody.sqrMagnitude > 0.0001f)
                {
                    bodyDown = toBody.normalized;
                }
            }

            float exhaustToBodyDown = Vector3.Dot(exhaustDirection, bodyDown);
            if (exhaustToBodyDown < ImpactPuffsRuntimeConfig.MinExhaustToBodyDown)
            {
                StopAllEmission(dt, false);
                return;
            }

            RaycastHit groundHit;
            if (!TryFindGroundHit(origin, exhaustDirection, vessel, ImpactPuffsRuntimeConfig.MaxRayDistance, out groundHit))
            {
                StopAllEmission(dt, false);
                return;
            }

            if (IsLaunchsiteExcludedSurface(groundHit.collider))
            {
                LogLaunchsiteSuppression("surface", vessel, groundHit.collider);
                StopAllEmission(dt, true);
                return;
            }

            Vector3 toGround = groundHit.point - origin;
            if (toGround.sqrMagnitude < 0.0001f)
            {
                StopAllEmission(dt, false);
                return;
            }

            Vector3 toGroundDirection = toGround.normalized;
            float alignment = Vector3.Dot(exhaustDirection, toGroundDirection);
            float minAlignment = Mathf.Lerp(0.42f, ImpactPuffsRuntimeConfig.MinExhaustToGroundAlignment, normalizedThrust);
            if (alignment < minAlignment)
            {
                StopAllEmission(dt, false);
                return;
            }

            if (groundHit.distance > maxEffectiveDistance)
            {
                StopAllEmission(dt, false);
                return;
            }

            float distanceFactor = Mathf.Clamp01(1f - (groundHit.distance / maxEffectiveDistance));
            distanceFactor = Mathf.Pow(distanceFactor, 1.00f);
            if (distanceFactor <= 0.001f)
            {
                StopAllEmission(dt, false);
                return;
            }

            float quality = GetModeQualityScale();
            float qualityNorm = Mathf.InverseLerp(0.25f, 2.0f, quality);
            float qualityRateScale = 1f + (Mathf.Pow(quality, 1.50f) - 1f) * 1.35f;
            cachedBodyVisibility = ImpactPuffsRuntimeConfig.GetBodyVisibilityMultiplier(vessel.mainBody != null ? vessel.mainBody.bodyName : string.Empty);

            float thrustPowerScale = ComputeThrustPowerScale(currentThrust);
            float thrustPowerNorm = Mathf.Clamp01((thrustPowerScale - ImpactPuffsRuntimeConfig.ThrustPowerMinScale) / Mathf.Max(0.01f, ImpactPuffsRuntimeConfig.ThrustPowerMaxScale - ImpactPuffsRuntimeConfig.ThrustPowerMinScale));

            float lowThrustBoost = Mathf.Lerp(1.42f, 1.00f, normalizedThrust);
            float pressure = Mathf.Clamp01(normalizedThrust * lowThrustBoost * Mathf.Lerp(0.52f, 1.95f, distanceFactor) * Mathf.Lerp(0.78f, 1.46f, thrustPowerNorm));
            float baseRate = (1320f + 14800f * pressure * pressure) * Mathf.Lerp(0.42f, 1.36f, distanceFactor);
            float engineClusterScale = ComputeEngineClusterScale(engineClusterCount);
            float modeDensityScale = ImpactPuffsConfig.UseSimplifiedEffects ? 1.00f : 1.48f;
            float targetRate = baseRate
                * qualityRateScale
                * thrustPowerScale
                * engineClusterScale
                * ImpactPuffsRuntimeConfig.EmissionMultiplier
                * RoverDustFX.KerbalFxRuntimeConfig.EmissionMultiplier
                * cachedBodyVisibility
                * modeDensityScale;

            targetRate *= 1.56f;
            targetRate = Mathf.Clamp(targetRate, 0f, 42000f);

            cachedLightFactor = ImpactPuffsConfig.UseSimplifiedEffects
                ? EvaluateEngineAwareLightFactor(vessel, groundHit.point, groundHit.normal, normalizedThrust)
                : EvaluateVolumetricLightFactor(vessel, groundHit.point, groundHit.normal, normalizedThrust);
            if (!ImpactPuffsConfig.UseSimplifiedEffects)
            {
                float volumetricLightRate = Mathf.Pow(Mathf.Clamp01(cachedLightFactor), 0.65f);
                volumetricLightRate = Mathf.Lerp(0.28f, 1f, volumetricLightRate);
                targetRate *= volumetricLightRate;
            }
            Vector3 stableNormal = groundHit.normal.sqrMagnitude > 0.0001f ? groundHit.normal.normalized : Vector3.up;

            float surfaceOffset = -0.05f;
            Vector3 lateralOffset = Vector3.zero;
            Vector3 outwardDirForCoreClamp = Vector3.zero;
            Vector3 centerPlane = Vector3.ProjectOnPlane(groundHit.point - vessel.CoM, stableNormal);
            float centerPlaneMag = centerPlane.magnitude;
            float underCenter = Mathf.Clamp01(1f - (centerPlaneMag / 0.85f));

            Vector3 outwardDir = centerPlane;
            if (outwardDir.sqrMagnitude < 0.0001f)
            {
                outwardDir = Vector3.ProjectOnPlane((Vector3)vessel.srf_velocity, stableNormal);
            }
            if (outwardDir.sqrMagnitude < 0.0001f)
            {
                outwardDir = Vector3.ProjectOnPlane(part.transform.right, stableNormal);
            }
            if (outwardDir.sqrMagnitude > 0.0001f)
            {
                outwardDir.Normalize();
                outwardDirForCoreClamp = outwardDir;
                float outwardShift = ImpactPuffsConfig.UseSimplifiedEffects
                    ? (Mathf.Lerp(0.40f, 1.35f, pressure) * (0.80f + 0.90f * underCenter))
                    : (Mathf.Lerp(0.08f, 0.42f, pressure) * Mathf.Lerp(0.30f, 0.92f, underCenter));
                lateralOffset = outwardDir * outwardShift;
            }

            Vector3 finalPosition = groundHit.point + stableNormal * surfaceOffset + lateralOffset;
            Vector3 finalPlane = Vector3.ProjectOnPlane(finalPosition - vessel.CoM, stableNormal);
            float finalRadius = finalPlane.magnitude;
            float minCoreRadius = ImpactPuffsConfig.UseSimplifiedEffects
                ? Mathf.Lerp(1.10f, 2.85f, pressure)
                : Mathf.Lerp(0.55f, 1.95f, pressure);
            if (finalRadius < minCoreRadius)
            {
                Vector3 pushDir = outwardDirForCoreClamp;
                if (pushDir.sqrMagnitude < 0.0001f && finalPlane.sqrMagnitude > 0.0001f)
                {
                    pushDir = finalPlane.normalized;
                }
                if (pushDir.sqrMagnitude > 0.0001f)
                {
                    finalPosition += pushDir * (minCoreRadius - finalRadius);
                }
            }

            root.transform.position = finalPosition;

            if (ImpactPuffsConfig.UseSimplifiedEffects)
            {
                UpdateDynamicGroundFlow(vessel, groundHit.point, stableNormal, exhaustDirection, pressure, qualityNorm, distanceFactor, thrustPowerNorm);
            }
            else
            {
                UpdateVolumetricFrame(vessel, finalPosition, stableNormal, outwardDirForCoreClamp, exhaustDirection, pressure, dt);
            }

            colorRefreshTimer -= dt;
            if (colorRefreshTimer <= 0f)
            {
                colorRefreshTimer = 0.25f;
                UpdateSurfaceColor(vessel, groundHit.collider);
                ApplyCurrentStartColor();
            }

            if (ImpactPuffsConfig.UseSimplifiedEffects)
            {
                SetTargetRate(targetRate, dt);
                if (volumetricField != null)
                {
                    volumetricField.StopSoft(dt);
                }
            }
            else
            {
                SetTargetRate(0f, dt);
                if (volumetricField != null)
                {
                    volumetricField.Update(
                        root.transform.position,
                        root.transform.rotation,
                        targetRate,
                        pressure,
                        qualityNorm,
                        currentColor,
                        cachedLightFactor,
                        dt
                    );
                }
            }

            if (ImpactPuffsConfig.DebugLogging && vessel == FlightGlobals.ActiveVessel)
            {
                debugTimer -= dt;
                if (debugTimer <= 0f)
                {
                    debugTimer = 1.2f;
                    float displayedRate = ImpactPuffsConfig.UseSimplifiedEffects ? smoothedRate : targetRate;
                    ImpactPuffsLog.DebugLog(Localizer.Format(
                        ImpactPuffsLoc.LogEngineEmitter,
                        debugId,
                        normalizedThrust.ToString("F2"),
                        groundHit.distance.ToString("F2"),
                        displayedRate.ToString("F1"),
                        cachedLightFactor.ToString("F2")
                        + " pressure=" + pressure.ToString("F2")
                        + " align=" + alignment.ToString("F2")
                        + " bodyAlign=" + exhaustToBodyDown.ToString("F2")
                        + " terrainH=" + terrainHeight.ToString("F1")
                        + " thrust=" + currentThrust.ToString("F0")
                        + " mode=" + (ImpactPuffsConfig.UseSimplifiedEffects ? "simplified" : "volumetric")
                    ));
                }
            }
        }

        public void StopEmission()
        {
            StopAllEmission(0.12f, false);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (particleSystem != null)
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (volumetricField != null)
            {
                volumetricField.Dispose();
            }

            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        private void StopAllEmission(float dt, bool immediateVolumetric)
        {
            SetTargetRate(0f, dt);
            if (volumetricField == null)
            {
                return;
            }

            if (immediateVolumetric)
            {
                volumetricField.StopImmediate();
            }
            else
            {
                volumetricField.StopSoft(dt);
            }
        }

        private void LogLaunchsiteSuppression(string reason, Vessel vessel, Collider collider)
        {
            if (!ImpactPuffsConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel)
            {
                return;
            }

            suppressionLogTimer -= Time.deltaTime;
            if (suppressionLogTimer > 0f)
            {
                return;
            }

            suppressionLogTimer = 1.5f;
            string colliderName = collider != null ? GetSurfaceNameChain(collider, 2) : "n/a";
            ImpactPuffsLog.DebugLog(
                "[ImpactPuffs] Launchsite exclusion active (" + reason + "): vessel=" + vessel.vesselName
                + " lat=" + vessel.latitude.ToString("F3", CultureInfo.InvariantCulture)
                + " lon=" + vessel.longitude.ToString("F3", CultureInfo.InvariantCulture)
                + " alt=" + vessel.altitude.ToString("F1", CultureInfo.InvariantCulture)
                + " collider=" + colliderName
            );
        }

        private void ConfigureParticleSystemBase()
        {
            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(currentColor.r, currentColor.g, currentColor.b, BaseAlpha);
            main.startRotation = new ParticleSystem.MinMaxCurve(-3.14159f, 3.14159f);

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 68f;
            shape.radius = 1.10f;

            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-2.20f, 2.40f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.10f, 0.75f);
            velocity.z = new ParticleSystem.MinMaxCurve(-2.00f, 2.00f);
            velocity.radial = new ParticleSystem.MinMaxCurve(0.45f, 1.20f);

            ParticleSystem.NoiseModule noise = particleSystem.noise;
            noise.enabled = true;
            noise.strength = 0.90f;
            noise.frequency = 0.55f;
            noise.scrollSpeed = 0.35f;
            noise.damping = true;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 2f;

            Material material = ImpactPuffsAssets.GetSharedMaterial();
            if (material != null)
            {
                renderer.material = material;
            }
        }

        private void ApplyRuntimeVisualProfile(bool force)
        {
            if (!force
                && appliedUiRevision == ImpactPuffsConfig.Revision
                && appliedRuntimeRevision == ImpactPuffsRuntimeConfig.Revision)
            {
                return;
            }

            appliedUiRevision = ImpactPuffsConfig.Revision;
            appliedRuntimeRevision = ImpactPuffsRuntimeConfig.Revision;

            float quality = GetModeQualityScale();
            float qualityNorm = Mathf.InverseLerp(0.25f, 2.0f, quality);
            float volumetricBoost = ImpactPuffsConfig.UseSimplifiedEffects ? 1.00f : 1.30f;

            ParticleSystem.MainModule main = particleSystem.main;
            float maxParticles = 2200f
                * (1f + (Mathf.Pow(quality, 1.25f) - 1f) * 1.15f)
                * volumetricBoost
                * ImpactPuffsRuntimeConfig.MaxParticlesMultiplier
                * RoverDustFX.KerbalFxRuntimeConfig.MaxParticlesMultiplier;
            main.maxParticles = Mathf.RoundToInt(Mathf.Clamp(maxParticles, 480f, 22000f));

            float minSize = 0.22f * Mathf.Lerp(0.72f, 1.70f, qualityNorm) * ImpactPuffsRuntimeConfig.RadiusScaleMultiplier * RoverDustFX.KerbalFxRuntimeConfig.RadiusScaleMultiplier;
            float maxSize = 0.72f * Mathf.Lerp(0.72f, 1.70f, qualityNorm) * ImpactPuffsRuntimeConfig.RadiusScaleMultiplier * RoverDustFX.KerbalFxRuntimeConfig.RadiusScaleMultiplier;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);

            main.startLifetime = new ParticleSystem.MinMaxCurve(
                0.78f * Mathf.Lerp(0.90f, 1.38f, qualityNorm),
                3.35f * Mathf.Lerp(0.90f, 1.45f, qualityNorm) * volumetricBoost
            );
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, Mathf.Lerp(0.45f, 1.20f, qualityNorm));
            main.gravityModifier = Mathf.Lerp(0.004f, 0.018f, qualityNorm);

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.angle = Mathf.Lerp(58f, 86f, qualityNorm);
            shape.radius = Mathf.Lerp(0.75f, 2.60f, qualityNorm) * ImpactPuffsRuntimeConfig.RadiusScaleMultiplier * RoverDustFX.KerbalFxRuntimeConfig.RadiusScaleMultiplier;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.55f);
            curve.AddKey(0.48f, 1.95f);
            curve.AddKey(1f, 0.25f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(Mathf.Lerp(0.30f, 0.56f, qualityNorm) * volumetricBoost, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.maxParticleSize = Mathf.Lerp(0.24f, 0.62f, qualityNorm);

            ApplyCurrentStartColor();

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            if (smoothedRate < 0.01f)
            {
                emission.rateOverTime = 0f;
            }
        }

        private void UpdateVolumetricFrame(Vessel vessel, Vector3 plumePosition, Vector3 surfaceNormal, Vector3 outwardHint, Vector3 exhaustDirection, float pressure, float dt)
        {
            Vector3 tangentForward = Vector3.zero;
            if (vessel != null)
            {
                tangentForward = Vector3.ProjectOnPlane(plumePosition - vessel.CoM, surfaceNormal);
            }

            if (tangentForward.sqrMagnitude < 0.0001f && outwardHint.sqrMagnitude > 0.0001f)
            {
                tangentForward = Vector3.ProjectOnPlane(outwardHint, surfaceNormal);
            }
            if (tangentForward.sqrMagnitude < 0.0001f)
            {
                tangentForward = Vector3.ProjectOnPlane(exhaustDirection, surfaceNormal);
            }
            if (tangentForward.sqrMagnitude < 0.0001f)
            {
                tangentForward = Vector3.ProjectOnPlane(part.transform.forward, surfaceNormal);
            }
            if (tangentForward.sqrMagnitude < 0.0001f)
            {
                tangentForward = Vector3.Cross(surfaceNormal, Vector3.right);
            }

            tangentForward.Normalize();
            Vector3 tangentRight = Vector3.Cross(surfaceNormal, tangentForward);
            if (tangentRight.sqrMagnitude < 0.0001f)
            {
                tangentRight = Vector3.Cross(surfaceNormal, Vector3.forward);
            }
            tangentRight.Normalize();

            Vector3 awayFromVessel = tangentForward;
            if (awayFromVessel.sqrMagnitude < 0.0001f)
            {
                awayFromVessel = tangentRight;
            }
            awayFromVessel.Normalize();

            root.transform.position = plumePosition;
            root.transform.rotation = Quaternion.LookRotation(awayFromVessel, surfaceNormal);
        }

        private void UpdateDynamicGroundFlow(Vessel vessel, Vector3 groundPoint, Vector3 surfaceNormal, Vector3 exhaustDirection, float pressure, float qualityNorm, float distanceFactor, float thrustPowerNorm)
        {
            float volumetricBoost = ImpactPuffsConfig.UseSimplifiedEffects ? 1.00f : 1.28f;
            Vector3 tangentForward = Vector3.zero;
            if (vessel != null)
            {
                tangentForward = Vector3.ProjectOnPlane(groundPoint - vessel.CoM, surfaceNormal);
            }

            if (tangentForward.sqrMagnitude < 0.0001f)
            {
                tangentForward = Vector3.ProjectOnPlane(exhaustDirection, surfaceNormal);
            }
            if (tangentForward.sqrMagnitude < 0.0001f)
            {
                tangentForward = Vector3.ProjectOnPlane(part.transform.forward, surfaceNormal);
            }
            if (tangentForward.sqrMagnitude < 0.0001f)
            {
                tangentForward = Vector3.Cross(surfaceNormal, Vector3.right);
            }

            tangentForward.Normalize();
            root.transform.rotation = Quaternion.LookRotation(tangentForward, surfaceNormal);

            dynamicSwayTime += Time.deltaTime * Mathf.Lerp(1.35f, 7.60f, pressure) * ImpactPuffsRuntimeConfig.DynamicSwayMultiplier;
            float swayA = Mathf.Sin(dynamicSwayTime * 1.75f + part.flightID * 0.013f);
            float swayB = Mathf.Cos(dynamicSwayTime * 2.30f + part.flightID * 0.017f);
            float swayC = Mathf.Sin(dynamicSwayTime * 0.78f + part.flightID * 0.051f);
            float sway = (swayA + swayB + swayC) / 3f;

            float lateral = Mathf.Lerp(2.7f, 14.2f, pressure)
                * ImpactPuffsRuntimeConfig.LateralSpreadMultiplier
                * Mathf.Lerp(0.86f, 1.24f, qualityNorm)
                * Mathf.Lerp(0.82f, 1.35f, thrustPowerNorm)
                * volumetricBoost;
            float lift = Mathf.Lerp(0.28f, 1.45f, pressure)
                * ImpactPuffsRuntimeConfig.VerticalLiftMultiplier
                * Mathf.Lerp(0.84f, 1.08f, qualityNorm)
                * volumetricBoost;
            float ring = Mathf.Lerp(0.64f, 2.52f, pressure)
                * ImpactPuffsRuntimeConfig.RingExpansionMultiplier
                * Mathf.Lerp(0.84f, 1.15f, qualityNorm)
                * Mathf.Lerp(0.72f, 1.22f, distanceFactor)
                * Mathf.Lerp(0.86f, 1.28f, thrustPowerNorm)
                * volumetricBoost;

            Vector3 centerOffsetVector = Vector3.ProjectOnPlane(groundPoint - vessel.CoM, surfaceNormal);
            float centerOffset = centerOffsetVector.magnitude;
            float directionalBias = Mathf.Clamp01(centerOffset / Mathf.Lerp(0.45f, 1.30f, pressure));
            lift *= Mathf.Lerp(0.30f, 1f, directionalBias);
            ring *= Mathf.Lerp(1.75f, 1.00f, directionalBias);
            lateral *= Mathf.Lerp(1.32f, 1.00f, directionalBias);
            sway *= Mathf.Lerp(0.32f, 1.00f, directionalBias);

            float swayScale = 1.00f;
            float swayOffset = sway * lateral * 0.95f * swayScale * directionalBias;
            float sideJitter = Mathf.Sin(dynamicSwayTime * 1.07f + part.flightID * 0.023f) * lateral * 0.76f * swayScale * Mathf.Lerp(0.45f, 1f, directionalBias);

            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve((-lateral * 0.92f) + swayOffset - sideJitter, (lateral * 0.92f) + swayOffset + sideJitter);
            float yMin = lift * 0.18f;
            float yMax = lift * 1.08f;
            velocity.y = new ParticleSystem.MinMaxCurve(yMin, yMax);
            float zBias = sideJitter * 0.28f;
            float zMin = (lateral * 0.08f) + zBias;
            float zMax = (lateral * 1.34f) + zBias;
            zMin = Mathf.Max(zMin, lateral * 0.20f);
            zMax = Mathf.Max(zMax, lateral * 1.62f);
            velocity.z = new ParticleSystem.MinMaxCurve(zMin, zMax);
            velocity.radial = new ParticleSystem.MinMaxCurve(ring * 0.72f, ring * 1.28f);

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            float radius = Mathf.Lerp(0.62f, 2.65f, pressure);
            radius *= 1.14f;
            shape.radius = radius * ImpactPuffsRuntimeConfig.RadiusScaleMultiplier * RoverDustFX.KerbalFxRuntimeConfig.RadiusScaleMultiplier;
            shape.angle = Mathf.Lerp(50f, 80f, pressure);

            ParticleSystem.NoiseModule noise = particleSystem.noise;
            noise.enabled = true;
            noise.strength = Mathf.Lerp(0.30f, 2.25f, pressure) * ImpactPuffsRuntimeConfig.TurbulenceMultiplier;
            noise.frequency = Mathf.Lerp(0.30f, 1.05f, pressure);
            noise.scrollSpeed = Mathf.Lerp(0.18f, 1.25f, pressure);
            noise.damping = true;
        }

        private void SetTargetRate(float targetRate, float dt)
        {
            if (targetRate > 0.25f)
            {
                startupRampTimer = Mathf.Min(StartupRampDuration, startupRampTimer + Mathf.Max(0f, dt));
            }
            else
            {
                startupRampTimer = Mathf.Max(0f, startupRampTimer - Mathf.Max(0f, dt) * 3.0f);
            }
            float startupFactor = Mathf.Lerp(0.12f, 1f, Mathf.Clamp01(startupRampTimer / StartupRampDuration));
            targetRate *= startupFactor;

            float smoothingSpeed = targetRate > smoothedRate ? 1.65f : 7.50f;
            float lerpSpeed = Mathf.Clamp01(dt * smoothingSpeed);
            smoothedRate = Mathf.Lerp(smoothedRate, targetRate, lerpSpeed);

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = smoothedRate;

            if (smoothedRate > 0.18f)
            {
                if (!particleSystem.isPlaying)
                {
                    particleSystem.Play(true);
                }
            }
            else if (particleSystem.isPlaying)
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private Vector3 GetPrimaryExhaustDirection(Vector3 origin)
        {
            if (thrustTransform != null)
            {
                Vector3 dirA = -thrustTransform.forward;
                Vector3 dirB = thrustTransform.forward;
                if (dirA.sqrMagnitude > 0.0001f && dirB.sqrMagnitude > 0.0001f)
                {
                    dirA.Normalize();
                    dirB.Normalize();

                    float scoreA = 0f;
                    float scoreB = 0f;

                    if (part != null && part.transform != null)
                    {
                        Vector3 fromPartCenter = origin - part.transform.position;
                        if (fromPartCenter.sqrMagnitude > 0.0001f)
                        {
                            fromPartCenter.Normalize();
                            scoreA += Vector3.Dot(dirA, fromPartCenter) * 1.45f;
                            scoreB += Vector3.Dot(dirB, fromPartCenter) * 1.45f;
                        }

                        Vector3 partDown = -part.transform.up;
                        if (partDown.sqrMagnitude > 0.0001f)
                        {
                            partDown.Normalize();
                            scoreA += Vector3.Dot(dirA, partDown) * 0.85f;
                            scoreB += Vector3.Dot(dirB, partDown) * 0.85f;
                        }
                    }

                    return scoreB > scoreA ? dirB : dirA;
                }
            }

            if (part != null && part.transform != null)
            {
                Vector3 fallback = -part.transform.up;
                if (fallback.sqrMagnitude > 0.0001f)
                {
                    return fallback.normalized;
                }
            }

            return Vector3.down;
        }

        private static bool TryFindGroundHit(Vector3 origin, Vector3 primaryDir, Vessel vessel, float maxDistance, out RaycastHit hit)
        {
            hit = new RaycastHit();
            if (primaryDir.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Vector3 primary = primaryDir.normalized;

            Vector3 bodyDown = Vector3.down;
            if (vessel != null && vessel.mainBody != null)
            {
                Vector3 toBody = vessel.mainBody.position - origin;
                if (toBody.sqrMagnitude > 0.0001f)
                {
                    bodyDown = toBody.normalized;
                }
            }

            float dotPrimary = Vector3.Dot(primary, bodyDown);
            float minDot = ImpactPuffsRuntimeConfig.MinRayDirectionToBodyDown;

            bool allowPrimary = dotPrimary >= minDot;
            if (!allowPrimary)
            {
                return false;
            }

            RaycastHit primaryHit;
            if (!TryRay(origin, primary, vessel, maxDistance, out primaryHit))
            {
                return false;
            }

            hit = primaryHit;
            return true;
        }

        private static bool TryRay(Vector3 origin, Vector3 direction, Vessel vessel, float maxDistance, out RaycastHit bestHit)
        {
            bestHit = new RaycastHit();
            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction.normalized,
                SharedHits,
                maxDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );

            if (hitCount <= 0)
            {
                return false;
            }

            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = SharedHits[i];
                if (candidate.collider == null)
                {
                    continue;
                }

                Part hitPart = candidate.collider.GetComponentInParent<Part>();
                if (hitPart != null)
                {
                    continue;
                }

                if (vessel != null && vessel.mainBody != null)
                {
                    Vector3 upFromBody = candidate.point - vessel.mainBody.position;
                    if (upFromBody.sqrMagnitude > 0.0001f)
                    {
                        upFromBody.Normalize();
                        Vector3 hitNormal = candidate.normal.sqrMagnitude > 0.0001f ? candidate.normal.normalized : upFromBody;
                        float normalToUp = Vector3.Dot(hitNormal, upFromBody);
                        if (normalToUp < 0.30f)
                        {
                            continue;
                        }
                    }
                }

                if (candidate.distance < bestDistance)
                {
                    bestDistance = candidate.distance;
                    bestHit = candidate;
                    found = true;
                }
            }

            return found;
        }

        private static float GetSafeTerrainHeightAgl(Vessel vessel)
        {
            if (vessel == null)
            {
                return -1f;
            }

            if (vessel.Landed || vessel.situation == Vessel.Situations.PRELAUNCH)
            {
                return 0f;
            }

            double agl = vessel.heightFromTerrain;
            if (!double.IsNaN(agl) && !double.IsInfinity(agl) && agl >= 0.0)
            {
                return (float)agl;
            }

            double radar = vessel.radarAltitude;
            if (!double.IsNaN(radar) && !double.IsInfinity(radar) && radar >= 0.0)
            {
                return (float)radar;
            }

            return -1f;
        }

        internal static float GetModeQualityScale()
        {
            return ImpactPuffsConfig.UseSimplifiedEffects ? 1.00f : 1.70f;
        }

        private static float GetNormalizedLiveThrust(ModuleEngines module)
        {
            if (module == null)
            {
                return 0f;
            }

            float finalThrust = Mathf.Max(0f, (float)module.finalThrust);
            if (finalThrust <= 0.06f)
            {
                return 0f;
            }

            float thrustPercent = Mathf.Clamp01(module.thrustPercentage / 100f);
            float maxPossible = Mathf.Max(0.01f, module.maxThrust * thrustPercent);
            if (maxPossible > 0.01f)
            {
                return Mathf.Clamp01(finalThrust / maxPossible);
            }

            return 0f;
        }

        private static float ComputeThrustPowerScale(float currentThrust)
        {
            float safeThrust = Mathf.Max(0f, currentThrust);
            float normalized = safeThrust / Mathf.Max(10f, ImpactPuffsRuntimeConfig.ThrustPowerReference);
            float scaled = Mathf.Pow(Mathf.Max(0.001f, normalized), ImpactPuffsRuntimeConfig.ThrustPowerExponent);
            return Mathf.Clamp(scaled, ImpactPuffsRuntimeConfig.ThrustPowerMinScale, ImpactPuffsRuntimeConfig.ThrustPowerMaxScale);
        }

        private static float ComputeEngineClusterScale(int clusterCount)
        {
            float count = Mathf.Max(1f, clusterCount);
            float scale = 1f / Mathf.Pow(count, ImpactPuffsRuntimeConfig.EngineCountExponent);
            return Mathf.Clamp(scale, ImpactPuffsRuntimeConfig.EngineCountMinScale, 1f);
        }

        internal static bool IsLaunchsiteExcludedSurface(Collider collider)
        {
            string surfaceName = GetSurfaceNameChain(collider, 6);
            if (string.IsNullOrEmpty(surfaceName))
            {
                return false;
            }

            return surfaceName.Contains("launchpad")
                || surfaceName.Contains("launch_pad")
                || surfaceName.Contains("launch pad")
                || surfaceName.Contains("launchsite")
                || surfaceName.Contains("launch_site")
                || surfaceName.Contains("ksc")
                || surfaceName.Contains("runway");
        }

        internal static bool IsInKerbinLaunchsiteZone(Vessel vessel)
        {
            if (vessel == null || vessel.mainBody == null)
            {
                return false;
            }

            if (!string.Equals(vessel.mainBody.bodyName, "Kerbin", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (vessel.altitude > 3500.0)
            {
                return false;
            }

            double lat = vessel.latitude;
            double lon = NormalizeLongitudeSigned(vessel.longitude);

            return lat >= -0.28 && lat <= 0.18 && lon >= -74.95 && lon <= -74.20;
        }

        private static double NormalizeLongitudeSigned(double lon)
        {
            while (lon > 180.0)
            {
                lon -= 360.0;
            }
            while (lon < -180.0)
            {
                lon += 360.0;
            }
            return lon;
        }

        private static string GetSurfaceNameChain(Collider collider, int depth)
        {
            if (collider == null)
            {
                return string.Empty;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder(128);
            Transform cursor = collider.transform;
            int remaining = Mathf.Max(1, depth);
            while (cursor != null && remaining > 0)
            {
                if (!string.IsNullOrEmpty(cursor.name))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append('/');
                    }
                    sb.Append(cursor.name.ToLowerInvariant());
                }

                cursor = cursor.parent;
                remaining--;
            }

            return sb.ToString();
        }

        private void UpdateSurfaceColor(Vessel vessel, Collider collider)
        {
            Color bodyColor = ImpactPuffsSurfaceColor.GetBaseDustColor(vessel);
            Color finalColor = bodyColor;

            Color colliderColor;
            if (ImpactPuffsSurfaceColor.TryGetColliderColor(collider, out colliderColor))
            {
                finalColor = ImpactPuffsSurfaceColor.BlendWithColliderColor(bodyColor, colliderColor);
            }

            Color toned = ImpactPuffsSurfaceColor.NormalizeDustTone(finalColor);
            Color softWhite = new Color(0.90f, 0.90f, 0.89f);
            currentColor = Color.Lerp(toned, softWhite, 0.18f);
        }

        private void ApplyCurrentStartColor()
        {
            if (particleSystem == null)
            {
                return;
            }

            float volumetricBoost = ImpactPuffsConfig.UseSimplifiedEffects ? 1.00f : 1.15f;
            float baseAlpha = BaseAlpha * 0.68f * volumetricBoost;
            float lightMin = ImpactPuffsConfig.UseSimplifiedEffects ? 0.18f : 0.26f;
            float lightMax = ImpactPuffsConfig.UseSimplifiedEffects ? 0.84f : 0.92f;
            float alpha = baseAlpha * Mathf.Lerp(lightMin, lightMax, Mathf.Clamp01(cachedLightFactor));
            alpha *= Mathf.Lerp(0.92f, 1.12f, Mathf.Clamp01((cachedBodyVisibility - 1f) / 0.75f));
            float alphaCap = 0.60f + (ImpactPuffsConfig.UseSimplifiedEffects ? 0.00f : 0.08f);
            alpha = Mathf.Clamp(alpha, 0f, alphaCap);

            ParticleSystem.MainModule main = particleSystem.main;
            main.startColor = new Color(currentColor.r, currentColor.g, currentColor.b, alpha);
        }

        private static float EvaluateEngineAwareLightFactor(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal, float normalizedThrust)
        {
            float sunLight = EvaluateSimplifiedSunLighting(vessel, worldPoint, surfaceNormal);
            float thrust01 = Mathf.Clamp01(normalizedThrust);
            float engineGlow = Mathf.Lerp(0.01f, 0.11f, Mathf.Pow(thrust01, 0.80f));
            return Mathf.Clamp01(Mathf.Max(sunLight, engineGlow));
        }

        private static float EvaluateVolumetricLightFactor(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal, float normalizedThrust)
        {
            float sunLight = EvaluateNonSimplifiedLighting(vessel, worldPoint, surfaceNormal, true);
            float thrust01 = Mathf.Clamp01(normalizedThrust);
            float engineGlow = Mathf.Lerp(0.0f, 0.020f, Mathf.Pow(thrust01, 2.20f));
            return Mathf.Clamp01(Mathf.Max(sunLight, engineGlow));
        }

        internal static float GetSunLightFactor(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal)
        {
            return EvaluateSimplifiedSunLighting(vessel, worldPoint, surfaceNormal);
        }

        internal static float GetTouchdownLightFactor(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal)
        {
            return EvaluateNonSimplifiedLighting(vessel, worldPoint, surfaceNormal, true);
        }

        private static float EvaluateNonSimplifiedLighting(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal, bool includeTerrainOcclusion)
        {
            if (surfaceNormal.sqrMagnitude < 0.0001f)
            {
                surfaceNormal = Vector3.up;
            }
            surfaceNormal.Normalize();

            Vector3 sunDirection;
            if (!TryGetSunDirection(worldPoint, out sunDirection))
            {
                return 0f;
            }

            float ndotl = Mathf.Clamp01(Vector3.Dot(surfaceNormal, sunDirection));
            if (ndotl <= 0f)
            {
                return 0f;
            }

            bool bodyOccluded = vessel != null
                && vessel.mainBody != null
                && IsSunOccluded(vessel.mainBody, worldPoint, sunDirection);
            bool terrainOccluded = includeTerrainOcclusion
                && !bodyOccluded
                && IsLocallySunOccluded(vessel, worldPoint, surfaceNormal, sunDirection);

            float litFactor = Mathf.Lerp(0.05f, 1f, Mathf.Pow(ndotl, 0.58f));
            if (bodyOccluded || terrainOccluded)
            {
                float shadowStrength = Mathf.Clamp01(ImpactPuffsRuntimeConfig.ShadowLightFactor);
                float shadowMul = terrainOccluded
                    ? Mathf.Lerp(0.05f, 0.12f, shadowStrength)
                    : Mathf.Lerp(0.06f, 0.14f, shadowStrength);
                if (terrainOccluded)
                {
                    shadowMul *= 0.92f;
                }
                litFactor *= shadowMul;
                float shadowCap = terrainOccluded ? 0.10f : 0.18f;
                litFactor = Mathf.Min(litFactor, shadowCap);
            }

            return Mathf.Clamp01(litFactor);
        }

        private static float EvaluateSimplifiedSunLighting(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal)
        {
            if (surfaceNormal.sqrMagnitude < 0.0001f)
            {
                surfaceNormal = Vector3.up;
            }
            surfaceNormal.Normalize();

            Vector3 sunDirection;
            if (!TryGetSunDirection(worldPoint, out sunDirection))
            {
                return 0f;
            }

            float ndotl = Mathf.Clamp01(Vector3.Dot(surfaceNormal, sunDirection));
            if (ndotl <= 0f)
            {
                return 0f;
            }

            float litFactor = Mathf.Lerp(0.05f, 1f, Mathf.Pow(ndotl, 0.58f));
            bool bodyOccluded = vessel != null
                && vessel.mainBody != null
                && IsSunOccluded(vessel.mainBody, worldPoint, sunDirection);
            if (!bodyOccluded)
            {
                return Mathf.Clamp01(litFactor);
            }

            float shadowStrength = Mathf.Clamp01(ImpactPuffsRuntimeConfig.ShadowLightFactor);
            float shadowMul = Mathf.Lerp(0.06f, 0.16f, shadowStrength);
            return Mathf.Clamp01(Mathf.Min(litFactor * shadowMul, 0.18f));
        }

        private static bool IsLocallySunOccluded(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal, Vector3 sunDirection)
        {
            if (vessel == null || vessel.packed || !vessel.loaded || sunDirection.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Guid vesselId = vessel.id;
            float now = Time.time;
            SunOcclusionCacheEntry cacheEntry;
            if (SunOcclusionCache.TryGetValue(vesselId, out cacheEntry))
            {
                float pointDeltaSq = (cacheEntry.SamplePoint - worldPoint).sqrMagnitude;
                float directionDot = Vector3.Dot(cacheEntry.SunDirection, sunDirection);
                if (now <= cacheEntry.ValidUntil && pointDeltaSq <= 9f && directionDot >= 0.995f)
                {
                    return cacheEntry.Occluded;
                }
            }

            bool occluded = ComputeLocalSunOcclusion(vessel, worldPoint, surfaceNormal, sunDirection);
            SunOcclusionCacheEntry updatedEntry = new SunOcclusionCacheEntry
            {
                Occluded = occluded,
                ValidUntil = now + (ImpactPuffsConfig.UseSimplifiedEffects ? 0.30f : 0.14f),
                SamplePoint = worldPoint,
                SunDirection = sunDirection
            };
            SunOcclusionCache[vesselId] = updatedEntry;
            return occluded;
        }

        private static bool ComputeLocalSunOcclusion(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal, Vector3 sunDirection)
        {
            Vector3 normal = surfaceNormal;
            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = Vector3.up;
            }
            normal.Normalize();

            Vector3 direction = sunDirection.normalized;
            Vector3 origin = worldPoint + normal * 0.20f + direction * 0.05f;
            float maxDistance = 1200f;

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                SunOcclusionHits,
                maxDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );
            if (hitCount <= 0)
            {
                return false;
            }

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = SunOcclusionHits[i];
                if (hit.collider == null || hit.distance <= 0.03f)
                {
                    continue;
                }

                Part hitPart = hit.collider.GetComponentInParent<Part>();
                if (hitPart != null && hitPart.vessel == vessel)
                {
                    continue;
                }

                if (vessel.rootPart != null && hit.collider.transform != null && hit.collider.transform.IsChildOf(vessel.rootPart.transform))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool TryGetSunDirection(Vector3 worldPoint, out Vector3 sunDirection)
        {
            sunDirection = Vector3.up;

            CelestialBody sunBody = Planetarium.fetch != null ? Planetarium.fetch.Sun : null;
            if (sunBody == null)
            {
                return false;
            }

            Vector3 toSun = sunBody.position - worldPoint;
            if (toSun.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            sunDirection = toSun.normalized;
            return true;
        }

        private static bool IsSunOccluded(CelestialBody body, Vector3 worldPoint, Vector3 sunDirection)
        {
            if (body == null || sunDirection.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            Vector3 toCenter = body.position - worldPoint;
            float projection = Vector3.Dot(toCenter, sunDirection.normalized);
            if (projection <= 0f)
            {
                return false;
            }

            double radius = body.Radius;
            double perpendicularSq = toCenter.sqrMagnitude - projection * projection;
            return perpendicularSq <= radius * radius;
        }
    }

    internal sealed class VolumetricPlumeField
    {
        private readonly GameObject root;
        private readonly ParticleSystem core;
        private readonly ParticleSystem shell;
        private readonly ParticleSystem loft;

        private float coreRate;
        private float shellRate;
        private float loftRate;
        private float phaseA;
        private float phaseB;
        private float phaseC;
        private float phaseD;

        public VolumetricPlumeField(Transform parent, int layer)
        {
            root = new GameObject("KerbalFX_VolumetricPlume");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.layer = layer;

            core = CreateLayer("Core", root.transform, layer, 0.58f, 2.20f, 0.88f);
            shell = CreateLayer("Shell", root.transform, layer, 0.90f, 3.80f, 1.10f);
            loft = CreateLayer("Loft", root.transform, layer, 1.50f, 6.20f, 1.35f);

            float seed = parent != null ? Mathf.Abs(parent.GetInstanceID()) * 0.017f : 0f;
            phaseA = 0.6f + seed;
            phaseB = 1.2f + seed * 1.17f;
            phaseC = 1.8f + seed * 1.31f;
            phaseD = 2.4f + seed * 0.93f;

            StopImmediate();
        }

        public void Update(
            Vector3 worldPosition,
            Quaternion worldRotation,
            float targetRate,
            float pressure,
            float qualityNorm,
            Color dustColor,
            float lightFactor,
            float dt
        )
        {
            if (root == null)
            {
                return;
            }

            phaseA += dt * Mathf.Lerp(2.2f, 5.4f, pressure);
            phaseB += dt * Mathf.Lerp(1.7f, 4.1f, pressure);
            phaseC += dt * Mathf.Lerp(1.1f, 2.9f, pressure);
            phaseD += dt * Mathf.Lerp(2.8f, 6.8f, pressure);

            float pulsePrimary = 0.5f + 0.5f * Mathf.Sin(phaseA);
            float pulseSecondary = 0.5f + 0.5f * Mathf.Sin(phaseB);
            float pulse = Mathf.Lerp(0.50f, 1.30f, pulsePrimary * 0.58f + pulseSecondary * 0.42f);
            float burstGate = Mathf.Lerp(0.72f, 1.24f, 0.5f + 0.5f * Mathf.Sin(phaseC));

            float driftX = Mathf.Sin(phaseA * 0.96f + phaseD * 0.24f);
            float driftZ = Mathf.Cos(phaseB * 1.08f - phaseA * 0.19f);

            root.transform.rotation = worldRotation;
            float density = Mathf.Clamp(targetRate / 16000f, 0f, 3.2f);
            root.transform.position = worldPosition;

            float qualityBoost = Mathf.Lerp(1.10f, 1.85f, qualityNorm);
            float pressureBoost = Mathf.Lerp(0.55f, 1.60f, pressure);

            float coreTarget = Mathf.Clamp(targetRate * 0.66f * qualityBoost * pulse, 0f, 56000f);
            float shellTarget = Mathf.Clamp(targetRate * 0.52f * qualityBoost * burstGate, 0f, 48000f);
            float loftTarget = Mathf.Clamp(targetRate * 0.29f * qualityBoost * Mathf.Lerp(pulse, burstGate, 0.5f), 0f, 34000f);

            float smoothIn = Mathf.Clamp01(dt * 4.8f);
            float smoothOut = Mathf.Clamp01(dt * 7.2f);

            coreRate = Mathf.Lerp(coreRate, coreTarget, coreTarget > coreRate ? smoothIn : smoothOut);
            shellRate = Mathf.Lerp(shellRate, shellTarget, shellTarget > shellRate ? smoothIn : smoothOut);
            loftRate = Mathf.Lerp(loftRate, loftTarget, loftTarget > loftRate ? smoothIn : smoothOut);

            UpdateLayer(core, coreRate, pressureBoost, density, qualityNorm, lightFactor, Color.Lerp(dustColor, Color.white, 0.30f), 0.86f, 0.18f, 0.36f, driftX, driftZ, pulse);
            UpdateLayer(shell, shellRate, pressureBoost, density, qualityNorm, lightFactor, Color.Lerp(dustColor, Color.white, 0.22f), 1.20f, 0.24f, 0.30f, -driftX * 0.62f, driftZ * 0.70f, burstGate);
            UpdateLayer(loft, loftRate, pressureBoost, density, qualityNorm, lightFactor, Color.Lerp(dustColor, Color.white, 0.38f), 1.58f, 0.36f, 0.20f, driftX * 0.40f, -driftZ * 0.30f, pulse * 0.90f);
        }

        public void StopSoft(float dt)
        {
            float fade = Mathf.Clamp01(dt * 6.5f);
            coreRate = Mathf.Lerp(coreRate, 0f, fade);
            shellRate = Mathf.Lerp(shellRate, 0f, fade);
            loftRate = Mathf.Lerp(loftRate, 0f, fade);

            SetRate(core, coreRate);
            SetRate(shell, shellRate);
            SetRate(loft, loftRate);

            if (coreRate <= 0.25f) core.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (shellRate <= 0.25f) shell.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (loftRate <= 0.25f) loft.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public void StopImmediate()
        {
            coreRate = 0f;
            shellRate = 0f;
            loftRate = 0f;
            SetRate(core, 0f);
            SetRate(shell, 0f);
            SetRate(loft, 0f);
            core.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            shell.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            loft.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void Dispose()
        {
            StopImmediate();
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        private static void UpdateLayer(
            ParticleSystem ps,
            float rate,
            float pressureBoost,
            float density,
            float qualityNorm,
            float lightFactor,
            Color color,
            float sizeScale,
            float liftScale,
            float alphaScale,
            float driftX,
            float driftZ,
            float pulse
        )
        {
            if (ps == null)
            {
                return;
            }

            SetRate(ps, rate);
            if (rate > 0.25f)
            {
                if (!ps.isPlaying)
                {
                    ps.Play(true);
                }
            }

            float light01 = Mathf.Clamp01(lightFactor);
            float lightCurve = Mathf.Pow(light01, 1.08f);
            float lightFloor = Mathf.Lerp(0.045f, 0.085f, Mathf.Clamp01(pressureBoost / 1.60f));
            float visibility = Mathf.Lerp(lightFloor, 1f, lightCurve);
            float alpha = Mathf.Clamp(visibility * Mathf.Lerp(0.26f, 0.50f, density / 3.2f) * alphaScale * Mathf.Lerp(0.74f, 1.06f, pulse), 0f, 0.27f);

            ParticleSystem.MainModule main = ps.main;
            main.startColor = new Color(color.r, color.g, color.b, alpha);
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.34f * sizeScale * Mathf.Lerp(0.90f, 1.55f, qualityNorm),
                1.12f * sizeScale * Mathf.Lerp(0.90f, 1.55f, qualityNorm)
            );
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                0.34f * Mathf.Lerp(0.86f, 1.26f, qualityNorm),
                1.56f * sizeScale * Mathf.Lerp(0.86f, 1.26f, qualityNorm)
            );

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.angle = Mathf.Lerp(44f, 82f, pressureBoost / 1.60f);
            shape.radius = Mathf.Lerp(0.58f, 2.85f, density / 3.2f) * sizeScale * Mathf.Lerp(0.88f, 1.14f, pulse);

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            float lateral = Mathf.Lerp(2.2f, 14.8f, density / 3.2f) * sizeScale;
            float lift = Mathf.Lerp(0.18f, 2.15f, pressureBoost / 1.60f) * liftScale;
            float driftLateral = driftX * lateral * 0.62f;
            velocity.x = new ParticleSystem.MinMaxCurve((-lateral * 0.42f) + driftLateral, (lateral * 0.98f) + driftLateral);
            velocity.y = new ParticleSystem.MinMaxCurve(lift * 0.22f, lift * 1.42f);
            float driftForward = driftZ * lateral * 0.58f;
            float zMin = Mathf.Max(-lateral * 0.08f, driftForward - lateral * 0.12f);
            float zMax = (lateral * 1.40f) + driftForward;
            if (zMax <= zMin + 0.05f)
            {
                zMax = zMin + 0.05f;
            }
            velocity.z = new ParticleSystem.MinMaxCurve(zMin, zMax);
            velocity.radial = new ParticleSystem.MinMaxCurve(lateral * 0.05f, lateral * 0.36f);

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = Mathf.Lerp(0.78f, 3.70f, density / 3.2f) * Mathf.Lerp(0.90f, 1.34f, pulse);
            noise.frequency = Mathf.Lerp(0.52f, 1.62f, pressureBoost / 1.60f);
            noise.scrollSpeed = Mathf.Lerp(0.40f, 1.92f, pressureBoost / 1.60f);
            noise.damping = true;
        }

        private static ParticleSystem CreateLayer(string name, Transform parent, int layer, float minSize, float maxSize, float maxLifetime)
        {
            GameObject go = new GameObject("KerbalFX_Volumetric_" + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.layer = layer;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(-3.14159f, 3.14159f);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, maxLifetime);
            main.maxParticles = 28000;
            main.gravityModifier = 0.006f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 70f;
            shape.radius = 1.2f;

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.42f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            color.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.50f);
            curve.AddKey(0.52f, 1.90f);
            curve.AddKey(1f, 0.18f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 1.6f;
            renderer.maxParticleSize = 0.92f;

            Material material = ImpactPuffsAssets.GetSharedMaterial();
            if (material != null)
            {
                renderer.material = material;
            }

            return ps;
        }

        private static void SetRate(ParticleSystem ps, float value)
        {
            if (ps == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = Mathf.Max(0f, value);
        }
    }

    internal sealed class TouchdownBurstEmitter
    {
        private readonly Vessel vessel;
        private bool wasGrounded;
        private float cooldown;
        private static readonly RaycastHit[] BurstHits = new RaycastHit[32];
        private static readonly Gradient BurstCoreGradient = CreateBurstCoreGradient();
        private static readonly Gradient BurstHazeGradient = CreateBurstHazeGradient();
        private static readonly Gradient RingShockGradient = CreateRingShockGradient();
        private static readonly AnimationCurve BurstCoreSizeCurve = CreateBurstCoreSizeCurve();
        private static readonly AnimationCurve BurstHazeSizeCurve = CreateBurstHazeSizeCurve();
        private static readonly AnimationCurve RingShockSizeCurve = CreateRingShockSizeCurve();

        public TouchdownBurstEmitter(Vessel vessel)
        {
            this.vessel = vessel;
            wasGrounded = vessel != null && (vessel.Landed || vessel.situation == Vessel.Situations.PRELAUNCH);
        }

        public void Tick(float dt)
        {
            if (vessel == null || !vessel.loaded || vessel.packed)
            {
                return;
            }

            cooldown = Mathf.Max(0f, cooldown - dt);

            bool grounded = vessel.Landed
                || vessel.Splashed
                || vessel.situation == Vessel.Situations.PRELAUNCH
                || vessel.situation == Vessel.Situations.SPLASHED;
            float descentSpeed = Mathf.Max(0f, (float)(-vessel.verticalSpeed));
            bool useSimplified = ImpactPuffsConfig.UseSimplifiedEffects;
            float touchdownSpeedThreshold = useSimplified
                ? Mathf.Max(ImpactPuffsRuntimeConfig.TouchdownMinSpeed * 1.45f, 3.2f)
                : Mathf.Max(ImpactPuffsRuntimeConfig.TouchdownMinSpeed * 1.20f, 2.8f);
            if (!wasGrounded && grounded && cooldown <= 0f && descentSpeed >= touchdownSpeedThreshold)
            {
                if (useSimplified)
                {
                    SpawnBurst(descentSpeed);
                }
                else
                {
                    SpawnRingShock(descentSpeed);
                }
                cooldown = useSimplified ? 0.45f : 0.38f;
            }

            wasGrounded = grounded;
        }

        public void Dispose()
        {
        }

        private static float GetTouchdownEnergy01(float impactSpeed)
        {
            float cappedSpeed = Mathf.Clamp(impactSpeed, 0f, 30f);
            float speed01 = cappedSpeed / 30f;
            return speed01 * speed01;
        }

        private void SpawnRingShock(float impactSpeed)
        {
            if (vessel == null || vessel.mainBody == null)
            {
                return;
            }

            Vector3 point;
            Vector3 normal;
            Collider collider;
            if (!TryGetGroundPoint(out point, out normal, out collider))
            {
                return;
            }

            if (EngineGroundPuffEmitter.IsInKerbinLaunchsiteZone(vessel))
            {
                return;
            }

            if (EngineGroundPuffEmitter.IsLaunchsiteExcludedSurface(collider))
            {
                return;
            }

            bool splash = vessel.Splashed || vessel.situation == Vessel.Situations.SPLASHED;
            float quality = EngineGroundPuffEmitter.GetModeQualityScale();
            float qualityNorm = Mathf.InverseLerp(0.25f, 2.0f, quality);
            float bodyVisibility = ImpactPuffsRuntimeConfig.GetBodyVisibilityMultiplier(vessel.mainBody.bodyName);
            float burstStrength = Mathf.Clamp01(impactSpeed / 10.5f);
            float touchdownEnergy01 = GetTouchdownEnergy01(impactSpeed);
            float burstLight = EngineGroundPuffEmitter.GetTouchdownLightFactor(vessel, point, normal);
            float burstLightVisibility = Mathf.Pow(Mathf.Clamp01(burstLight), 0.90f);
            float touchdownLightFloor = Mathf.Lerp(0.03f, 0.12f, touchdownEnergy01);
            float burstLightScale = Mathf.Lerp(touchdownLightFloor, 1f, burstLightVisibility);

            Color color = ImpactPuffsSurfaceColor.GetBaseDustColor(vessel);
            Color colliderColor;
            if (ImpactPuffsSurfaceColor.TryGetColliderColor(collider, out colliderColor))
            {
                color = ImpactPuffsSurfaceColor.BlendWithColliderColor(color, colliderColor);
            }
            color = ImpactPuffsSurfaceColor.NormalizeDustTone(color);
            color = Color.Lerp(color, new Color(0.90f, 0.90f, 0.89f), 0.08f);
            if (splash)
            {
                color = Color.Lerp(color, new Color(0.93f, 0.94f, 0.95f), 0.14f);
            }

            float energyRadiusScale = Mathf.Lerp(1.00f, 1.12f, touchdownEnergy01);
            float energySpreadScale = Mathf.Lerp(1.00f, 1.16f, touchdownEnergy01);
            float energyLiftScale = Mathf.Lerp(1.00f, 1.10f, touchdownEnergy01);
            float energyCountScale = Mathf.Lerp(1.00f, 1.32f, touchdownEnergy01);
            float radiusBase = Mathf.Lerp(0.86f, 2.22f, qualityNorm)
                * (splash ? 1.24f : 1.00f)
                * ImpactPuffsRuntimeConfig.RadiusScaleMultiplier
                * RoverDustFX.KerbalFxRuntimeConfig.RadiusScaleMultiplier
                * 0.48f
                * energyRadiusScale;
            float lateralScale = Mathf.Lerp(
                0.72f,
                1.24f,
                Mathf.InverseLerp(1.0f, 3.0f, ImpactPuffsRuntimeConfig.LateralSpreadMultiplier)
            );
            float lateralBase = Mathf.Lerp(0.90f, 2.80f, burstStrength)
                * Mathf.Lerp(0.92f, 1.16f, qualityNorm)
                * lateralScale
                * (splash ? 1.12f : 1.00f)
                * energySpreadScale;
            float liftBase = Mathf.Lerp(0.03f, 0.12f, burstStrength)
                * ImpactPuffsRuntimeConfig.VerticalLiftMultiplier
                * (splash ? 0.74f : 1.00f)
                * energyLiftScale;
            float densityScale = Mathf.Lerp(0.96f, 1.14f, qualityNorm);
            Vector3 bodyUp = (vessel.CoM - vessel.mainBody.position).normalized;
            float slopeFactor = 1f - Mathf.Clamp01(Vector3.Dot(normal, bodyUp));
            float surfaceOffset = (splash ? 0.018f : 0.026f) + slopeFactor * 0.055f + Mathf.Clamp(radiusBase * 0.004f, 0f, 0.018f);

            GameObject root = new GameObject("KerbalFX_ImpactRingShock");
            root.transform.position = point + normal * surfaceOffset;
            root.transform.rotation = Quaternion.FromToRotation(Vector3.forward, normal);
            int fxLayer = vessel.rootPart != null ? vessel.rootPart.gameObject.layer : 0;
            root.layer = fxLayer;

            float ringAlpha = Mathf.Lerp(splash ? 0.26f : 0.30f, splash ? 0.36f : 0.43f, qualityNorm) * burstLightScale * densityScale * 0.90f;
            float ringBase =
                (140f + 460f * burstStrength)
                * ImpactPuffsRuntimeConfig.TouchdownBurstMultiplier
                * (splash ? 1.08f : 1.00f)
                * bodyVisibility
                * 1.15f
                * energyCountScale;
            int ringDustCount = Mathf.RoundToInt(Mathf.Clamp(ringBase, 360f, 4600f));
            int ringEdgeCount = Mathf.Clamp(Mathf.RoundToInt(ringDustCount * 0.62f), 180, 2800);
            int ringMistCount = Mathf.Clamp(ringDustCount - ringEdgeCount, 120, 1800);

            Material dustMaterial = ImpactPuffsAssets.GetSharedMaterial();
            if (dustMaterial == null)
            {
                dustMaterial = ImpactPuffsAssets.GetBurstMaterial();
            }

            ParticleSystem ringEdge = CreateTouchdownLayer(
                "RingEdge",
                root.transform,
                fxLayer,
                color,
                ringAlpha,
                0.16f * Mathf.Lerp(0.94f, 1.18f, qualityNorm) * bodyVisibility,
                0.64f * Mathf.Lerp(0.94f, 1.18f, qualityNorm) * bodyVisibility,
                0.54f,
                (splash ? 1.48f : 1.34f) * Mathf.Lerp(0.96f, 1.14f, qualityNorm),
                0.00f,
                0.10f * Mathf.Lerp(0.96f, 1.12f, qualityNorm),
                0.020f,
                ringEdgeCount,
                Mathf.Lerp(splash ? 76f : 74f, splash ? 84f : 82f, qualityNorm),
                radiusBase * 0.98f,
                lateralBase * 0.06f,
                splash ? liftBase * 0.03f : liftBase * 0.06f,
                splash ? liftBase * 0.10f : liftBase * 0.18f,
                lateralBase * 1.46f,
                lateralBase * 2.70f,
                Mathf.Lerp(0.06f, 0.18f, burstStrength) * ImpactPuffsRuntimeConfig.TurbulenceMultiplier,
                Mathf.Lerp(0.05f, 0.14f, burstStrength),
                Mathf.Lerp(0.01f, 0.05f, burstStrength),
                RingShockGradient,
                RingShockSizeCurve,
                Mathf.Lerp(0.78f, 0.98f, qualityNorm),
                2.2f,
                0.00f,
                ParticleSystemRenderMode.Billboard,
                dustMaterial,
                true
            );

            ParticleSystem ringMist = CreateTouchdownLayer(
                "RingMist",
                root.transform,
                fxLayer,
                Color.Lerp(color, Color.white, 0.06f),
                ringAlpha * 0.58f,
                0.28f * Mathf.Lerp(0.96f, 1.22f, qualityNorm) * bodyVisibility,
                1.04f * Mathf.Lerp(0.96f, 1.22f, qualityNorm) * bodyVisibility,
                0.80f,
                (splash ? 2.15f : 1.96f) * Mathf.Lerp(0.98f, 1.18f, qualityNorm),
                0.01f,
                0.08f * Mathf.Lerp(0.96f, 1.12f, qualityNorm),
                0.022f,
                ringMistCount,
                Mathf.Lerp(splash ? 72f : 70f, splash ? 80f : 78f, qualityNorm),
                radiusBase * 0.88f,
                lateralBase * 0.05f,
                splash ? liftBase * 0.04f : liftBase * 0.08f,
                splash ? liftBase * 0.12f : liftBase * 0.22f,
                lateralBase * 1.04f,
                lateralBase * 2.00f,
                Mathf.Lerp(0.04f, 0.14f, burstStrength) * ImpactPuffsRuntimeConfig.TurbulenceMultiplier,
                Mathf.Lerp(0.04f, 0.12f, burstStrength),
                Mathf.Lerp(0.00f, 0.04f, burstStrength),
                RingShockGradient,
                RingShockSizeCurve,
                Mathf.Lerp(0.92f, 1.18f, qualityNorm),
                2.0f,
                0.08f,
                ParticleSystemRenderMode.Billboard,
                dustMaterial,
                true
            );

            if (ringEdge != null) ringEdge.Play(true);
            if (ringMist != null) ringMist.Play(true);

            float ringLifeMax = splash ? 2.55f : 2.30f;
            UnityEngine.Object.Destroy(root, ringLifeMax + 0.90f);

            if (ImpactPuffsConfig.DebugLogging && vessel == FlightGlobals.ActiveVessel)
            {
                ImpactPuffsLog.DebugLog(Localizer.Format(
                    ImpactPuffsLoc.LogBurst,
                    impactSpeed.ToString("F2"),
                    ringDustCount,
                    bodyVisibility.ToString("F2")
                ));
                ImpactPuffsLog.DebugLog(
                    "[ImpactPuffs] RingShockDbg speed=" + impactSpeed.ToString("F2", CultureInfo.InvariantCulture)
                    + " count=" + ringDustCount
                    + " edge=" + ringEdgeCount
                    + " mist=" + ringMistCount
                    + " core=0"
                    + " radius=" + radiusBase.ToString("F2", CultureInfo.InvariantCulture)
                    + " lateral=" + lateralBase.ToString("F2", CultureInfo.InvariantCulture)
                    + " lift=" + liftBase.ToString("F3", CultureInfo.InvariantCulture)
                    + " offset=" + surfaceOffset.ToString("F3", CultureInfo.InvariantCulture)
                    + " slope=" + slopeFactor.ToString("F2", CultureInfo.InvariantCulture)
                    + " alpha=" + ringAlpha.ToString("F2", CultureInfo.InvariantCulture)
                    + " light=" + burstLightScale.ToString("F2", CultureInfo.InvariantCulture)
                    + " energy01=" + touchdownEnergy01.ToString("F2", CultureInfo.InvariantCulture)
                );
            }
        }

        private void SpawnBurst(float impactSpeed)
        {
            if (vessel == null || vessel.mainBody == null)
            {
                return;
            }

            Vector3 point;
            Vector3 normal;
            Collider collider;
            if (!TryGetGroundPoint(out point, out normal, out collider))
            {
                return;
            }

            if (EngineGroundPuffEmitter.IsInKerbinLaunchsiteZone(vessel))
            {
                return;
            }

            if (EngineGroundPuffEmitter.IsLaunchsiteExcludedSurface(collider))
            {
                return;
            }
            bool splash = vessel.Splashed || vessel.situation == Vessel.Situations.SPLASHED;

            float quality = EngineGroundPuffEmitter.GetModeQualityScale();
            float qualityNorm = Mathf.InverseLerp(0.25f, 2.0f, quality);
            float bodyVisibility = ImpactPuffsRuntimeConfig.GetBodyVisibilityMultiplier(vessel.mainBody.bodyName);
            float burstStrength = Mathf.Clamp01(impactSpeed / 11.5f);
            float touchdownEnergy01 = GetTouchdownEnergy01(impactSpeed);
            float burstLight = EngineGroundPuffEmitter.GetSunLightFactor(vessel, point, normal);
            float burstLightVisibility = Mathf.Pow(Mathf.Clamp01(burstLight), 0.88f);
            float touchdownLightFloor = Mathf.Lerp(0.02f, 0.09f, touchdownEnergy01);
            float burstLightScale = Mathf.Lerp(touchdownLightFloor, 1f, burstLightVisibility);

            Color color = ImpactPuffsSurfaceColor.GetBaseDustColor(vessel);
            Color colliderColor;
            if (ImpactPuffsSurfaceColor.TryGetColliderColor(collider, out colliderColor))
            {
                color = ImpactPuffsSurfaceColor.BlendWithColliderColor(color, colliderColor);
            }
            color = ImpactPuffsSurfaceColor.NormalizeDustTone(color);
            color = Color.Lerp(color, new Color(0.90f, 0.90f, 0.89f), 0.06f);
            if (splash)
            {
                color = Color.Lerp(color, new Color(0.93f, 0.94f, 0.95f), 0.12f);
            }

            GameObject burstRoot = new GameObject("KerbalFX_ImpactBurst");
            burstRoot.transform.position = splash ? (point - normal * 0.14f) : (point - normal * 0.08f);
            burstRoot.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            float energyRadiusScale = Mathf.Lerp(1.00f, 1.10f, touchdownEnergy01);
            float energySpreadScale = Mathf.Lerp(1.00f, 1.14f, touchdownEnergy01);
            float energyLiftScale = Mathf.Lerp(1.00f, 1.10f, touchdownEnergy01);
            float energyCountScale = Mathf.Lerp(1.00f, 1.28f, touchdownEnergy01);
            float burstBase =
                (90f + 420f * burstStrength)
                * ImpactPuffsRuntimeConfig.TouchdownBurstMultiplier
                * (splash ? 1.08f : 1.00f)
                * bodyVisibility
                * energyCountScale;
            float burstParticleMultiplier = Mathf.Lerp(1.12f, 1.52f, burstStrength);
            int burstCount = Mathf.RoundToInt(
                Mathf.Clamp(
                    burstBase * burstParticleMultiplier,
                    140f,
                    5200f
                )
            );
            float splashRadiusScale = splash ? 1.34f : 1.00f;
            Material material = ImpactPuffsAssets.GetBurstMaterial();
            int fxLayer = vessel.rootPart != null ? vessel.rootPart.gameObject.layer : 0;
            burstRoot.layer = fxLayer;

            int coreCount = Mathf.Clamp(Mathf.RoundToInt(burstCount * 0.58f), 80, 3000);
            int hazeCount = Mathf.Clamp(burstCount - coreCount, 64, 2300);

            float radiusBase = Mathf.Lerp(0.58f, 1.58f, qualityNorm) * splashRadiusScale
                * ImpactPuffsRuntimeConfig.RadiusScaleMultiplier
                * RoverDustFX.KerbalFxRuntimeConfig.RadiusScaleMultiplier
                * energyRadiusScale;
            float lateralBase = Mathf.Lerp(0.78f, 2.85f, burstStrength) * ImpactPuffsRuntimeConfig.LateralSpreadMultiplier * (splash ? 1.08f : 1.00f) * energySpreadScale;
            float liftBase = Mathf.Lerp(0.015f, 0.14f, burstStrength) * ImpactPuffsRuntimeConfig.VerticalLiftMultiplier * (splash ? 0.05f : 0.16f) * energyLiftScale;
            float alphaDensityScale = Mathf.Lerp(1.04f, 1.36f, qualityNorm);
            float coreAlpha = Mathf.Lerp(splash ? 0.62f : 0.58f, splash ? 0.82f : 0.78f, qualityNorm) * burstLightScale * alphaDensityScale;
            float hazeAlpha = coreAlpha * 0.76f * alphaDensityScale;
            float coreMaxParticleLife = (splash ? 2.30f : 2.20f) * Mathf.Lerp(0.98f, 1.20f, qualityNorm);
            float hazeMaxParticleLife = (splash ? 3.00f : 2.70f) * Mathf.Lerp(0.98f, 1.20f, qualityNorm);

            ParticleSystem burstCore = CreateTouchdownLayer(
                "Core",
                burstRoot.transform,
                fxLayer,
                color,
                coreAlpha,
                0.058f * Mathf.Lerp(0.92f, 1.18f, qualityNorm) * bodyVisibility * 1.45f,
                0.19f * Mathf.Lerp(0.92f, 1.18f, qualityNorm) * bodyVisibility * 1.45f,
                0.46f,
                coreMaxParticleLife,
                0.06f,
                0.76f * Mathf.Lerp(0.98f, 1.20f, qualityNorm),
                0.030f,
                coreCount,
                Mathf.Lerp(splash ? 86f : 82f, splash ? 94f : 92f, qualityNorm),
                radiusBase * 0.92f,
                lateralBase * 1.06f,
                splash ? 0f : liftBase * 0.022f,
                splash ? liftBase * 0.015f : liftBase * 0.065f,
                lateralBase * 0.62f,
                lateralBase * 1.40f,
                Mathf.Lerp(0.60f, 1.40f, burstStrength) * ImpactPuffsRuntimeConfig.TurbulenceMultiplier,
                Mathf.Lerp(0.26f, 0.72f, burstStrength),
                Mathf.Lerp(0.10f, 0.48f, burstStrength),
                BurstCoreGradient,
                BurstCoreSizeCurve,
                Mathf.Lerp(0.72f, 1.04f, qualityNorm),
                2.6f,
                0.82f,
                ParticleSystemRenderMode.Billboard,
                material
            );

            ParticleSystem burstHaze = CreateTouchdownLayer(
                "Haze",
                burstRoot.transform,
                fxLayer,
                Color.Lerp(color, Color.white, 0.02f),
                hazeAlpha,
                0.090f * Mathf.Lerp(0.94f, 1.20f, qualityNorm) * bodyVisibility * 1.42f,
                0.30f * Mathf.Lerp(0.94f, 1.20f, qualityNorm) * bodyVisibility * 1.42f,
                0.72f,
                hazeMaxParticleLife,
                0.01f,
                0.42f * Mathf.Lerp(0.96f, 1.14f, qualityNorm),
                0.022f,
                hazeCount,
                Mathf.Lerp(splash ? 90f : 88f, splash ? 98f : 96f, qualityNorm),
                radiusBase * 1.86f,
                lateralBase * 1.18f,
                splash ? 0f : liftBase * 0.015f,
                splash ? liftBase * 0.022f : liftBase * 0.040f,
                lateralBase * 1.02f,
                lateralBase * 2.05f,
                Mathf.Lerp(0.46f, 1.04f, burstStrength) * ImpactPuffsRuntimeConfig.TurbulenceMultiplier,
                Mathf.Lerp(0.20f, 0.60f, burstStrength),
                Mathf.Lerp(0.08f, 0.38f, burstStrength),
                BurstHazeGradient,
                BurstHazeSizeCurve,
                Mathf.Lerp(0.80f, 1.08f, qualityNorm),
                2.3f,
                0.10f,
                ParticleSystemRenderMode.HorizontalBillboard,
                material
            );

            if (burstCore != null) burstCore.Play(true);
            if (burstHaze != null) burstHaze.Play(true);

            float burstTail = 0.30f;
            float maxLife = Mathf.Max(coreMaxParticleLife + burstTail, hazeMaxParticleLife + burstTail) + 0.80f;
            UnityEngine.Object.Destroy(burstRoot, maxLife);

            if (ImpactPuffsConfig.DebugLogging && vessel == FlightGlobals.ActiveVessel)
            {
                ImpactPuffsLog.DebugLog(Localizer.Format(
                    ImpactPuffsLoc.LogBurst,
                    impactSpeed.ToString("F2"),
                    burstCount,
                    bodyVisibility.ToString("F2")
                ));
                ImpactPuffsLog.DebugLog(
                    "[ImpactPuffs] BurstDbg mode=simplified speed=" + impactSpeed.ToString("F2", CultureInfo.InvariantCulture)
                    + " count=" + burstCount
                    + " core=" + coreCount
                    + " haze=" + hazeCount
                    + " radius=" + radiusBase.ToString("F2", CultureInfo.InvariantCulture)
                    + " lateral=" + lateralBase.ToString("F2", CultureInfo.InvariantCulture)
                    + " lift=" + liftBase.ToString("F3", CultureInfo.InvariantCulture)
                    + " alphaCore=" + coreAlpha.ToString("F2", CultureInfo.InvariantCulture)
                    + " alphaHaze=" + hazeAlpha.ToString("F2", CultureInfo.InvariantCulture)
                    + " light=" + burstLightScale.ToString("F2", CultureInfo.InvariantCulture)
                    + " energy01=" + touchdownEnergy01.ToString("F2", CultureInfo.InvariantCulture)
                );
            }
        }

        private static ParticleSystem CreateTouchdownLayer(
            string name,
            Transform parent,
            int layer,
            Color color,
            float alpha,
            float minSize,
            float maxSize,
            float minLife,
            float maxLife,
            float minSpeed,
            float maxSpeed,
            float gravity,
            int burstCount,
            float shapeAngle,
            float shapeRadius,
            float lateral,
            float liftMin,
            float liftMax,
            float radialMin,
            float radialMax,
            float noiseStrength,
            float noiseFrequency,
            float noiseScroll,
            Gradient gradient,
            AnimationCurve sizeCurve,
            float maxParticleSize,
            float sortingFudge,
            float shapeRadiusThickness,
            ParticleSystemRenderMode renderMode,
            Material material,
            bool useCircleShape = false
        )
        {
            GameObject go = new GameObject("KerbalFX_ImpactBurst_" + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.layer = layer;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(-3.14159f, 3.14159f);
            main.startColor = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.gravityModifier = gravity;
            main.maxParticles = Mathf.RoundToInt(Mathf.Clamp(36000f * ImpactPuffsRuntimeConfig.MaxParticlesMultiplier * RoverDustFX.KerbalFxRuntimeConfig.MaxParticlesMultiplier, 6000f, 360000f));

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            short burstPrimary = (short)Mathf.Clamp(Mathf.RoundToInt(burstCount * 0.40f), 1, short.MaxValue);
            short burstSecondary = (short)Mathf.Clamp(Mathf.RoundToInt(burstCount * 0.28f), 1, short.MaxValue);
            short burstTertiary = (short)Mathf.Clamp(Mathf.RoundToInt(burstCount * 0.20f), 1, short.MaxValue);
            short burstQuaternary = (short)Mathf.Clamp(Mathf.RoundToInt(burstCount * 0.12f), 1, short.MaxValue);
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, burstPrimary),
                new ParticleSystem.Burst(0.04f, burstSecondary),
                new ParticleSystem.Burst(0.11f, burstTertiary),
                new ParticleSystem.Burst(0.21f, burstQuaternary)
            });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = useCircleShape ? ParticleSystemShapeType.Circle : ParticleSystemShapeType.Cone;
            shape.angle = useCircleShape ? 0f : shapeAngle;
            shape.radius = shapeRadius;
            shape.radiusThickness = Mathf.Clamp01(shapeRadiusThickness);

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-lateral, lateral);
            velocity.y = new ParticleSystem.MinMaxCurve(liftMin, liftMax);
            velocity.z = new ParticleSystem.MinMaxCurve(-lateral, lateral);
            velocity.radial = new ParticleSystem.MinMaxCurve(radialMin, radialMax);

            ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = ps.limitVelocityOverLifetime;
            limitVelocity.enabled = true;
            limitVelocity.space = ParticleSystemSimulationSpace.Local;
            limitVelocity.separateAxes = false;
            float maxRadial = Mathf.Max(Mathf.Abs(radialMin), Mathf.Abs(radialMax));
            float speedCap = Mathf.Max(0.20f, Mathf.Max(maxSpeed, maxRadial) * 1.16f);
            limitVelocity.limit = new ParticleSystem.MinMaxCurve(speedCap);
            limitVelocity.dampen = 0.46f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = noiseStrength;
            noise.frequency = noiseFrequency;
            noise.scrollSpeed = noiseScroll;
            noise.damping = true;

            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = renderMode;
            renderer.sortingFudge = sortingFudge;
            renderer.maxParticleSize = maxParticleSize;
            if (material != null)
            {
                renderer.material = material;
            }

            return ps;
        }

        private static Gradient CreateBurstCoreGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.98f, 0f),
                    new GradientAlphaKey(0.72f, 0.20f),
                    new GradientAlphaKey(0.42f, 0.52f),
                    new GradientAlphaKey(0.18f, 0.82f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            return gradient;
        }

        private static Gradient CreateBurstHazeGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.76f, 0f),
                    new GradientAlphaKey(0.52f, 0.26f),
                    new GradientAlphaKey(0.30f, 0.58f),
                    new GradientAlphaKey(0.10f, 0.86f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            return gradient;
        }

        private static AnimationCurve CreateBurstCoreSizeCurve()
        {
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.26f);
            curve.AddKey(0.28f, 0.86f);
            curve.AddKey(0.64f, 1.06f);
            curve.AddKey(1f, 0.24f);
            return curve;
        }

        private static AnimationCurve CreateBurstHazeSizeCurve()
        {
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.32f);
            curve.AddKey(0.44f, 0.94f);
            curve.AddKey(0.78f, 1.14f);
            curve.AddKey(1f, 0.30f);
            return curve;
        }

        private static Gradient CreateRingShockGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.68f, 0f),
                    new GradientAlphaKey(0.46f, 0.16f),
                    new GradientAlphaKey(0.22f, 0.52f),
                    new GradientAlphaKey(0.06f, 0.82f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            return gradient;
        }

        private static AnimationCurve CreateRingShockSizeCurve()
        {
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.44f);
            curve.AddKey(0.20f, 0.94f);
            curve.AddKey(0.62f, 1.34f);
            curve.AddKey(1f, 0.60f);
            return curve;
        }

        private bool TryGetGroundPoint(out Vector3 point, out Vector3 normal, out Collider collider)
        {
            point = Vector3.zero;
            normal = Vector3.up;
            collider = null;

            if (vessel == null || vessel.mainBody == null)
            {
                return false;
            }

            Vector3 up = vessel.CoM - vessel.mainBody.position;
            if (up.sqrMagnitude < 0.0001f)
            {
                up = Vector3.up;
            }
            up.Normalize();

            Vector3 origin = vessel.CoM + up * 3.0f;
            Vector3 direction = -up;

            RaycastHit hit;
            if (!TryRayDown(origin, direction, 14f, out hit))
            {
                return false;
            }

            point = hit.point;
            normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : up;
            collider = hit.collider;
            return true;
        }

        private bool TryRayDown(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit bestHit)
        {
            bestHit = new RaycastHit();
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction.normalized,
                BurstHits,
                maxDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );

            if (hitCount <= 0)
            {
                return false;
            }

            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = BurstHits[i];
                if (candidate.collider == null)
                {
                    continue;
                }

                Part hitPart = candidate.collider.GetComponentInParent<Part>();
                if (hitPart != null && hitPart.vessel == vessel)
                {
                    continue;
                }

                if (vessel != null && vessel.rootPart != null)
                {
                    Transform rootTransform = vessel.rootPart.transform;
                    if (rootTransform != null && candidate.collider.transform != null && candidate.collider.transform.IsChildOf(rootTransform))
                    {
                        continue;
                    }
                }

                Rigidbody hitBody = candidate.rigidbody;
                if (hitBody != null)
                {
                    Part rbPart = hitBody.GetComponentInParent<Part>();
                    if (rbPart != null && rbPart.vessel == vessel)
                    {
                        continue;
                    }
                }

                if (candidate.distance < bestDistance)
                {
                    bestDistance = candidate.distance;
                    bestHit = candidate;
                    found = true;
                }
            }

            return found;
        }
    }

    internal static class ImpactPuffsSurfaceColor
    {
        public static Color GetBaseDustColor(Vessel vessel)
        {
            if (vessel == null || vessel.mainBody == null || string.IsNullOrEmpty(vessel.mainBody.bodyName))
            {
                return new Color(0.70f, 0.66f, 0.58f);
            }

            string key = vessel.mainBody.bodyName.ToLowerInvariant();

            if (key.Contains("minmus")) return new Color(0.73f, 0.80f, 0.74f);
            if (key.Contains("mun")) return new Color(0.76f, 0.74f, 0.70f);
            if (key.Contains("duna")) return new Color(0.72f, 0.48f, 0.33f);
            if (key.Contains("eve")) return new Color(0.77f, 0.71f, 0.60f);
            if (key.Contains("moho")) return new Color(0.63f, 0.56f, 0.50f);
            if (key.Contains("gilly")) return new Color(0.62f, 0.58f, 0.52f);
            if (key.Contains("bop")) return new Color(0.60f, 0.52f, 0.45f);
            if (key.Contains("pol")) return new Color(0.66f, 0.64f, 0.62f);
            if (key.Contains("tylo")) return new Color(0.67f, 0.67f, 0.66f);
            if (key.Contains("vall")) return new Color(0.70f, 0.72f, 0.74f);
            if (key.Contains("eeloo")) return new Color(0.74f, 0.75f, 0.77f);
            if (key.Contains("kerbin")) return new Color(0.67f, 0.61f, 0.53f);

            return new Color(0.70f, 0.66f, 0.58f);
        }

        public static bool TryGetColliderColor(Collider collider, out Color color)
        {
            color = Color.white;
            if (collider == null)
            {
                return false;
            }

            Renderer renderer = collider.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null)
            {
                return false;
            }

            if (!renderer.sharedMaterial.HasProperty("_Color"))
            {
                return false;
            }

            color = renderer.sharedMaterial.color;
            return true;
        }

        public static Color BlendWithColliderColor(Color baseColor, Color colliderColor)
        {
            float h;
            float s;
            float v;
            Color.RGBToHSV(colliderColor, out h, out s, out v);

            s = Mathf.Clamp(s, 0.05f, 0.34f);
            v = Mathf.Clamp(v, 0.20f, 0.90f);

            if (h > 0.20f && h < 0.45f)
            {
                h = Mathf.Lerp(h, 0.11f, 0.50f);
                s *= 0.44f;
                v *= 0.92f;
            }

            Color tunedCollider = Color.HSVToRGB(h, s, v);
            return Color.Lerp(baseColor, tunedCollider, 0.14f);
        }

        public static Color NormalizeDustTone(Color input)
        {
            float h;
            float s;
            float v;
            Color.RGBToHSV(input, out h, out s, out v);

            s = Mathf.Clamp(s, 0.10f, 0.34f);
            v = Mathf.Clamp(v, 0.22f, 0.88f);

            if (h > 0.20f && h < 0.45f)
            {
                h = Mathf.Lerp(h, 0.12f, 0.33f);
                s *= 0.70f;
            }

            return Color.HSVToRGB(h, s, v);
        }
    }

    internal static class ImpactPuffsAssets
    {
        private static Material sharedMaterial;
        private static Material sharedBurstMaterial;
        private static Texture2D sharedTexture;
        private static Texture2D sharedBurstTexture;

        public static Material GetSharedMaterial()
        {
            if (sharedMaterial != null)
            {
                return sharedMaterial;
            }

            Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            if (shader == null)
            {
                return null;
            }

            sharedMaterial = new Material(shader);
            sharedMaterial.name = "KerbalFX_ImpactPuffsMaterial";
            sharedMaterial.color = Color.white;
            sharedMaterial.mainTexture = GetSharedTexture();
            return sharedMaterial;
        }

        public static Material GetBurstMaterial()
        {
            if (sharedBurstMaterial != null)
            {
                return sharedBurstMaterial;
            }

            Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            if (shader == null)
            {
                return GetSharedMaterial();
            }

            sharedBurstMaterial = new Material(shader);
            sharedBurstMaterial.name = "KerbalFX_ImpactPuffsBurstMaterial";
            sharedBurstMaterial.color = Color.white;
            sharedBurstMaterial.mainTexture = GetBurstTexture();
            return sharedBurstMaterial;
        }

        private static Texture2D GetSharedTexture()
        {
            if (sharedTexture != null)
            {
                return sharedTexture;
            }

            const int size = 96;
            sharedTexture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            sharedTexture.wrapMode = TextureWrapMode.Clamp;
            sharedTexture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size) * 2f - 1f;
                    float ny = ((y + 0.5f) / size) * 2f - 1f;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);

                    float radial = Mathf.Clamp01(1f - radius);
                    float soft = Mathf.Pow(radial, 1.45f);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.34f) * 2.8f);
                    float noiseA = Mathf.PerlinNoise(x * 0.095f, y * 0.095f);
                    float noiseB = Mathf.PerlinNoise(x * 0.185f + 12.3f, y * 0.185f + 3.7f);
                    float noise = Mathf.Lerp(noiseA, noiseB, 0.45f);

                    float alpha = Mathf.Clamp01((soft * 0.80f + ring * 0.20f) * (0.82f + 0.18f * noise));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            sharedTexture.SetPixels(pixels);
            sharedTexture.Apply(false, true);
            return sharedTexture;
        }

        private static Texture2D GetBurstTexture()
        {
            if (sharedBurstTexture != null)
            {
                return sharedBurstTexture;
            }

            const int size = 128;
            sharedBurstTexture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            sharedBurstTexture.wrapMode = TextureWrapMode.Clamp;
            sharedBurstTexture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size) * 2f - 1f;
                    float ny = ((y + 0.5f) / size) * 2f - 1f;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);

                    float radial = Mathf.Clamp01(1f - radius);
                    float softBody = Mathf.Pow(radial, 1.95f);
                    float centerCut = Mathf.Clamp01((radius - 0.06f) / 0.22f);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.42f) * 3.6f);
                    float feather = Mathf.Pow(Mathf.Clamp01(1f - radius * 0.84f), 1.35f);
                    float noiseA = Mathf.PerlinNoise(x * 0.060f + 5.1f, y * 0.060f + 2.7f);
                    float noiseB = Mathf.PerlinNoise(x * 0.125f + 17.2f, y * 0.125f + 9.4f);
                    float breakup = Mathf.Lerp(noiseA, noiseB, 0.42f);
                    float alphaBody = softBody * centerCut;
                    float alpha = Mathf.Clamp01((alphaBody * 0.38f + ring * 0.44f + feather * 0.18f) * (0.72f + 0.28f * breakup));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            sharedBurstTexture.SetPixels(pixels);
            sharedBurstTexture.Apply(false, true);
            return sharedBurstTexture;
        }

    }
}

