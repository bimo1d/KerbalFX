using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace KerbalFX
{
    internal static class KerbalFxUtil
    {
        public static float ReadFloat(ConfigNode node, string key, float fallback, float min, float max)
        {
            if (node == null || string.IsNullOrEmpty(key) || !node.HasValue(key))
                return fallback;
            string raw = node.GetValue(key);
            if (string.IsNullOrEmpty(raw))
                return fallback;
            float value;
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return Mathf.Clamp(value, min, max);
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                return Mathf.Clamp(value, min, max);
            return fallback;
        }

        public static int ReadInt(ConfigNode node, string key, int fallback, int min, int max)
        {
            if (node == null || string.IsNullOrEmpty(key) || !node.HasValue(key))
                return fallback;
            string raw = node.GetValue(key);
            if (string.IsNullOrEmpty(raw))
                return fallback;
            int value;
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return Mathf.Clamp(value, min, max);
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out value))
                return Mathf.Clamp(value, min, max);
            return fallback;
        }

        public static bool ReadBool(ConfigNode node, string key, bool fallback)
        {
            if (node == null || string.IsNullOrEmpty(key) || !node.HasValue(key))
                return fallback;
            bool value;
            return bool.TryParse(node.GetValue(key), out value) ? value : fallback;
        }

        public static string ReadString(ConfigNode node, string key, string fallback)
        {
            if (node == null || string.IsNullOrEmpty(key) || !node.HasValue(key))
                return fallback;
            string value = node.GetValue(key);
            return string.IsNullOrEmpty(value) ? fallback : value.Trim();
        }

        public static bool ContainsAnyToken(string text, string[] tokens)
        {
            if (string.IsNullOrEmpty(text) || tokens == null)
                return false;
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token) && text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        public static bool ContainsIgnoreCase(string source, string value)
        {
            return !string.IsNullOrEmpty(source)
                && !string.IsNullOrEmpty(value)
                && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool ContainsAnyTokenInHierarchy(Transform t, string[] tokens, int maxDepth)
        {
            if (t == null || tokens == null || maxDepth <= 0)
                return false;
            Transform current = t;
            int depth = 0;
            while (current != null && depth < maxDepth)
            {
                if (ContainsAnyToken(current.name, tokens))
                    return true;
                current = current.parent;
                depth++;
            }
            return false;
        }

        public static void LoadBodyVisibility(ConfigNode node, Dictionary<string, float> target, float min, float max)
        {
            if (node == null || target == null)
                return;
            ConfigNode[] bodyNodes = node.GetNodes("BODY_VISIBILITY");
            if (bodyNodes == null || bodyNodes.Length == 0)
                return;
            for (int i = 0; i < bodyNodes.Length; i++)
            {
                ConfigNode bodyNode = bodyNodes[i];
                if (bodyNode == null)
                    continue;
                string name = bodyNode.GetValue("name");
                if (string.IsNullOrEmpty(name))
                    continue;
                float multiplier = ReadFloat(bodyNode, "multiplier", 1f, min, max);
                target[name.Trim()] = multiplier;
            }
        }

        public static bool HasConfigFileChanged(string path, ref DateTime stamp)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;
            DateTime writeUtc = File.GetLastWriteTimeUtc(path);
            if (writeUtc <= DateTime.MinValue || writeUtc <= stamp)
                return false;
            stamp = writeUtc;
            return true;
        }

        public static void PrimeConfigFileStamp(string path, ref DateTime stamp)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                stamp = File.GetLastWriteTimeUtc(path);
        }

        public static bool ModuleNameMatches(PartModule module, string moduleName)
        {
            if (module == null || string.IsNullOrEmpty(moduleName))
                return false;

            string runtimeName = module.moduleName;
            if (!string.IsNullOrEmpty(runtimeName)
                && string.Equals(runtimeName, moduleName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string typeName = module.GetType().Name;
            return !string.IsNullOrEmpty(typeName)
                && string.Equals(typeName, moduleName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool PartHasModule(Part part, string moduleName)
        {
            if (part == null || part.Modules == null || string.IsNullOrEmpty(moduleName))
                return false;

            for (int i = 0; i < part.Modules.Count; i++)
            {
                if (ModuleNameMatches(part.Modules[i], moduleName))
                    return true;
            }

            return false;
        }

        public static bool TryGetPartColliderBounds(Part part, out Bounds bounds, bool requireEnabled)
        {
            bounds = new Bounds();
            if (part == null)
                return false;

            List<Collider> colliders = part.FindModelComponents<Collider>();
            if (colliders == null || colliders.Count == 0)
                return false;

            bool initialized = false;
            for (int i = 0; i < colliders.Count; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || (requireEnabled && !collider.enabled))
                    continue;

                if (!initialized)
                {
                    bounds = collider.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return initialized;
        }

        public static uint ComputeVesselPartSignature(Vessel vessel)
        {
            if (vessel == null || vessel.parts == null)
                return 0u;

            uint hash = 17u;
            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part part = vessel.parts[i];
                if (part != null)
                    hash = hash * 31u + part.flightID;
            }

            return hash;
        }

        public static string ReadMemberStringLowerInvariant(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
                return string.Empty;

            Type type = target.GetType();
            if (type == null)
                return string.Empty;

            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                FieldInfo field = type.GetField(memberName, Flags);
                if (field != null)
                {
                    object fieldValue = field.GetValue(target);
                    if (fieldValue != null)
                        return fieldValue.ToString().ToLowerInvariant();
                }

                PropertyInfo property = type.GetProperty(memberName, Flags);
                if (property != null)
                {
                    object propertyValue = property.GetValue(target, null);
                    if (propertyValue != null)
                        return propertyValue.ToString().ToLowerInvariant();
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static Shader cachedAlphaBlended;
        private static Shader cachedAdditive;
        private static Shader cachedTransparent;
        private static bool shadersCached;

        public static Shader FindParticleShader(bool additive = false)
        {
            if (!shadersCached)
            {
                cachedAdditive = Shader.Find("Legacy Shaders/Particles/Additive");
                cachedAlphaBlended = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
                if (cachedAlphaBlended == null)
                    cachedAlphaBlended = Shader.Find("Particles/Standard Unlit");
                if (cachedAlphaBlended == null)
                    cachedAlphaBlended = Shader.Find("Sprites/Default");
                cachedTransparent = Shader.Find("Legacy Shaders/Transparent/Diffuse");
                shadersCached = true;
            }

            if (additive && cachedAdditive != null)
                return cachedAdditive;
            return cachedAlphaBlended;
        }

        public static Shader FindTransparentShader()
        {
            if (!shadersCached)
                FindParticleShader();
            return cachedTransparent != null ? cachedTransparent : cachedAlphaBlended;
        }
    }

    internal static class KerbalFxVesselUtil
    {
        public static bool IsSupportedFlightVessel(Vessel vessel)
        {
            if (vessel == null || !vessel.loaded || vessel.packed || vessel.isEVA)
                return false;
            return vessel.vesselType != VesselType.Flag && vessel.vesselType != VesselType.Debris;
        }

        public static float GetBodyVisibilityMultiplier(
            string bodyName,
            Dictionary<string, float> multipliers,
            float min,
            float max)
        {
            if (string.IsNullOrEmpty(bodyName) || multipliers == null)
                return 1f;
            float m;
            if (multipliers.TryGetValue(bodyName.Trim(), out m))
                return Mathf.Clamp(m, min, max);
            return 1f;
        }
    }

    internal struct KerbalFxRevisionStamp
    {
        private int uiRevision;
        private int runtimeRevision;

        public bool NeedsApply(int currentUiRevision, int currentRuntimeRevision)
        {
            return uiRevision != currentUiRevision || runtimeRevision != currentRuntimeRevision;
        }

        public bool ShouldSkipApply(bool force, int currentUiRevision, int currentRuntimeRevision)
        {
            return !force && !NeedsApply(currentUiRevision, currentRuntimeRevision);
        }

        public void MarkApplied(int currentUiRevision, int currentRuntimeRevision)
        {
            uiRevision = currentUiRevision;
            runtimeRevision = currentRuntimeRevision;
        }
    }

    internal struct KerbalFxVesselPartSnapshot
    {
        private int partCount;
        private uint partSignature;

        public bool HasChanged(Vessel vessel)
        {
            if (vessel == null || vessel.parts == null)
                return true;

            return vessel.parts.Count != partCount
                || KerbalFxUtil.ComputeVesselPartSignature(vessel) != partSignature;
        }

        public void Capture(Vessel vessel)
        {
            partCount = vessel != null && vessel.parts != null ? vessel.parts.Count : -1;
            partSignature = vessel != null ? KerbalFxUtil.ComputeVesselPartSignature(vessel) : 0u;
        }
    }

    internal static class KerbalFxSurfaceColor
    {
        private static readonly Dictionary<string, Color> BodyColors = InitBodyColors();
        public static readonly Color DefaultDustColor = new Color(0.70f, 0.66f, 0.58f);

        private static Dictionary<string, Color> InitBodyColors()
        {
            Dictionary<string, Color> d = new Dictionary<string, Color>(16, StringComparer.OrdinalIgnoreCase);
            d["Minmus"] = new Color(0.73f, 0.80f, 0.74f);
            d["Mun"] = new Color(0.75f, 0.73f, 0.69f);
            d["Duna"] = new Color(0.70f, 0.46f, 0.31f);
            d["Eve"] = new Color(0.77f, 0.71f, 0.60f);
            d["Moho"] = new Color(0.63f, 0.56f, 0.50f);
            d["Gilly"] = new Color(0.62f, 0.58f, 0.52f);
            d["Bop"] = new Color(0.60f, 0.52f, 0.45f);
            d["Pol"] = new Color(0.66f, 0.64f, 0.62f);
            d["Tylo"] = new Color(0.67f, 0.67f, 0.66f);
            d["Vall"] = new Color(0.70f, 0.72f, 0.74f);
            d["Eeloo"] = new Color(0.74f, 0.75f, 0.77f);
            d["Kerbin"] = new Color(0.67f, 0.61f, 0.53f);
            return d;
        }

        public static Color GetBaseDustColor(Vessel vessel)
        {
            if (vessel == null || vessel.mainBody == null || string.IsNullOrEmpty(vessel.mainBody.bodyName))
                return DefaultDustColor;
            Color color;
            if (BodyColors.TryGetValue(vessel.mainBody.bodyName, out color))
                return color;
            return DefaultDustColor;
        }

        public static bool TryGetColliderColor(Collider collider, out Color color)
        {
            color = Color.white;
            if (collider == null)
                return false;
            Renderer renderer = collider.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null)
                return false;
            if (!renderer.sharedMaterial.HasProperty("_Color"))
                return false;
            color = renderer.sharedMaterial.color;
            return true;
        }

        public static Color BlendWithColliderColor(Color baseColor, Color colliderColor)
        {
            float h, s, v;
            Color.RGBToHSV(colliderColor, out h, out s, out v);
            s = Mathf.Clamp(s, 0.05f, 0.35f);
            v = Mathf.Clamp(v, 0.20f, 0.88f);
            if (h > 0.20f && h < 0.45f)
            {
                h = Mathf.Lerp(h, 0.11f, 0.48f);
                s *= 0.45f;
                v *= 0.92f;
            }
            Color tuned = Color.HSVToRGB(h, s, v);
            return Color.Lerp(baseColor, tuned, 0.15f);
        }

        public static Color NormalizeDustTone(Color input)
        {
            float h, s, v;
            Color.RGBToHSV(input, out h, out s, out v);
            s = Mathf.Clamp(s, 0.11f, 0.37f);
            v = Mathf.Clamp(v, 0.23f, 0.88f);
            if (h > 0.20f && h < 0.45f)
            {
                h = Mathf.Lerp(h, 0.12f, 0.31f);
                s *= 0.71f;
            }
            return Color.HSVToRGB(h, s, v);
        }
    }

    internal static class KerbalFxSunLight
    {
        public static bool TryGetSunDirection(Vector3 worldPoint, out Vector3 sunDirection)
        {
            sunDirection = Vector3.zero;
            CelestialBody sun = null;
            if (Planetarium.fetch != null)
                sun = Planetarium.fetch.Sun;
            if (sun == null && FlightGlobals.Bodies != null && FlightGlobals.Bodies.Count > 0)
            {
                for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
                {
                    CelestialBody body = FlightGlobals.Bodies[i];
                    if (body != null && !string.IsNullOrEmpty(body.bodyName)
                        && body.bodyName.IndexOf("sun", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        sun = body;
                        break;
                    }
                }
                if (sun == null)
                    sun = FlightGlobals.Bodies[0];
            }
            if (sun == null)
                return false;
            Vector3d toSun = sun.position - (Vector3d)worldPoint;
            if (toSun.sqrMagnitude < 1e-8)
                return false;
            sunDirection = ((Vector3)toSun).normalized;
            return sunDirection.sqrMagnitude > 0.0001f;
        }

        public static bool IsSunOccludedByBody(CelestialBody body, Vector3 worldPoint, Vector3 sunDirection)
        {
            if (body == null || sunDirection.sqrMagnitude < 0.0001f)
                return false;
            Vector3 toCenter = body.position - worldPoint;
            float projection = Vector3.Dot(toCenter, sunDirection.normalized);
            if (projection <= 0f)
                return false;
            double radius = body.Radius;
            double perpSq = toCenter.sqrMagnitude - (double)projection * projection;
            return perpSq <= radius * radius;
        }

        public static float GetVesselSunFactor(Vessel vessel, float shadowFloor)
        {
            if (vessel == null)
                return 1f;
            return vessel.directSunlight ? 1f : Mathf.Clamp01(shadowFloor);
        }

        public static float GetBodyDaylightFactor(
            Vessel vessel,
            Vector3 worldPoint,
            Vector3 bodyUp,
            float daylightFloor,
            float shadowFloor)
        {
            if (vessel != null && vessel.directSunlight)
                return 1f;

            Vector3 sunDirection;
            if (!TryGetSunDirection(worldPoint, out sunDirection))
                return 1f;

            if (vessel != null
                && vessel.mainBody != null
                && IsSunOccludedByBody(vessel.mainBody, worldPoint, sunDirection))
            {
                return Mathf.Clamp01(shadowFloor);
            }

            Vector3 up = bodyUp.sqrMagnitude > 0.0001f
                ? bodyUp.normalized
                : Vector3.up;

            float sunHeight = Mathf.Clamp01(Vector3.Dot(up, sunDirection));
            float daylight = Mathf.Pow(sunHeight, 0.42f);
            return Mathf.Lerp(Mathf.Clamp01(daylightFloor), 1f, daylight);
        }
    }

    internal sealed class KerbalFxLog
    {
        private const string Prefix = "[KerbalFX] ";
        private readonly System.Func<bool> debugEnabled;

        public KerbalFxLog(System.Func<bool> debugEnabled)
        {
            this.debugEnabled = debugEnabled;
        }

        public void Info(string message)
        {
            Debug.Log(Prefix + message);
        }

        public void DebugLog(string message)
        {
            if (debugEnabled != null && debugEnabled())
                Debug.Log(Prefix + message);
        }

        public void DebugException(string scope, System.Exception ex)
        {
            if (ex == null || debugEnabled == null || !debugEnabled())
                return;
            Debug.Log(Prefix + scope + " failed: " + ex.Message);
        }
    }

    internal sealed class KerbalFxBodyVisibilityProfile
    {
        private readonly string configNodeName;
        private readonly System.Func<string> pathProvider;
        private readonly System.Action<Dictionary<string, float>> seedDefaults;
        private readonly float minMult;
        private readonly float maxMult;
        private readonly Dictionary<string, float> multipliers;
        private System.DateTime lastConfigWriteUtc = System.DateTime.MinValue;

        public KerbalFxBodyVisibilityProfile(
            string configNodeName,
            System.Func<string> pathProvider,
            System.Action<Dictionary<string, float>> seedDefaults,
            float minMult,
            float maxMult)
        {
            this.configNodeName = configNodeName;
            this.pathProvider = pathProvider;
            this.seedDefaults = seedDefaults;
            this.minMult = minMult;
            this.maxMult = maxMult;
            this.multipliers = new Dictionary<string, float>(16, System.StringComparer.OrdinalIgnoreCase);
        }

        public int Count { get { return multipliers.Count; } }

        public void Refresh()
        {
            SeedDefaults();
            if (GameDatabase.Instance != null)
            {
                ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes(configNodeName);
                if (nodes != null)
                    for (int i = 0; i < nodes.Length; i++)
                        if (nodes[i] != null)
                            KerbalFxUtil.LoadBodyVisibility(nodes[i], multipliers, minMult, maxMult);
            }
            KerbalFxUtil.PrimeConfigFileStamp(GetPath(), ref lastConfigWriteUtc);
        }

        public bool TryHotReloadFromDisk(out string failure)
        {
            failure = null;
            string path = GetPath();
            if (!KerbalFxUtil.HasConfigFileChanged(path, ref lastConfigWriteUtc))
                return false;

            SeedDefaults();
            try
            {
                if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                {
                    ConfigNode root = ConfigNode.Load(path);
                    if (root != null)
                    {
                        ConfigNode[] nodes = root.GetNodes(configNodeName);
                        if (nodes != null)
                            for (int i = 0; i < nodes.Length; i++)
                                if (nodes[i] != null)
                                    KerbalFxUtil.LoadBodyVisibility(nodes[i], multipliers, minMult, maxMult);
                    }
                }
            }
            catch (System.Exception ex)
            {
                failure = ex.Message;
            }
            return true;
        }

        public float Get(string bodyName)
        {
            return KerbalFxVesselUtil.GetBodyVisibilityMultiplier(bodyName, multipliers, minMult, maxMult);
        }

        private string GetPath()
        {
            return pathProvider != null ? pathProvider() : null;
        }

        private void SeedDefaults()
        {
            multipliers.Clear();
            if (seedDefaults != null)
                seedDefaults(multipliers);
        }
    }
}
