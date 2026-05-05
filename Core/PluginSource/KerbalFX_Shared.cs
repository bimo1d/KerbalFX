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
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return Mathf.Clamp(value, min, max);
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                return Mathf.Clamp(value, min, max);
            return fallback;
        }

        public static bool ReadBool(ConfigNode node, string key, bool fallback)
        {
            if (node == null || string.IsNullOrEmpty(key) || !node.HasValue(key))
                return fallback;
            return bool.TryParse(node.GetValue(key), out var value) ? value : fallback;
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

        public static bool ContainsAnyTokenInObjectHierarchy(Transform t, string[] tokens, int maxDepth)
        {
            if (t == null || tokens == null || maxDepth <= 0)
                return false;
            Transform current = t;
            int depth = 0;
            while (current != null && depth < maxDepth)
            {
                if (ContainsAnyToken(current.name, tokens))
                    return true;

                GameObject go = current.gameObject;
                if (go != null && ContainsAnyToken(go.name, tokens))
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

        public static void LoadBodyTints(ConfigNode node, Dictionary<string, KerbalFxBodyTintEntry> target)
        {
            if (node == null || target == null)
                return;
            ConfigNode[] bodyNodes = node.GetNodes("BODY_TINT");
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

                bool hasColor = TryParseColor(bodyNode.GetValue("color"), out var color);
                float strength = ReadFloat(bodyNode, "strength", 1f, 0f, 4f);
                bool overrideBiome = ReadBool(bodyNode, "override_biome", ReadBool(bodyNode, "overrideBiome", false));
                if (!hasColor && !bodyNode.HasValue("strength"))
                    continue;
                target[name.Trim()] = new KerbalFxBodyTintEntry
                {
                    HasColor = hasColor,
                    Color = color,
                    StrengthMultiplier = strength,
                    OverrideBiome = overrideBiome
                };
            }
        }

        public static void LoadLightAware(
            ConfigNode node,
            Dictionary<string, KerbalFxLightAwareEntry> target,
            KerbalFxLightAwareEntry defaults)
        {
            if (node == null || target == null)
                return;
            ConfigNode[] bodyNodes = node.GetNodes("LIGHT_AWARE");
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

                target[name.Trim()] = new KerbalFxLightAwareEntry
                {
                    DarkScale = ReadFloat(bodyNode, "dark_scale", defaults.DarkScale, 0f, 1f),
                    BrightScale = ReadFloat(bodyNode, "bright_scale", defaults.BrightScale, 0f, 2f),
                    TwilightFloor = ReadFloat(bodyNode, "twilight_floor", defaults.TwilightFloor, 0f, 1f),
                    MinPerceived = ReadFloat(bodyNode, "min_perceived", defaults.MinPerceived, 0f, 1f),
                    ColorTintStrength = ReadFloat(bodyNode, "color_tint", defaults.ColorTintStrength, 0f, 1f)
                };
            }
        }

        public static bool TryParseColor(string raw, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrEmpty(raw))
                return false;
            string[] parts = raw.Split(',');
            if (parts.Length < 3)
                return false;
            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var r))
                return false;
            if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var g))
                return false;
            if (!float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
                return false;
            float a = 1f;
            if (parts.Length >= 4)
                float.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out a);
            color = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), Mathf.Clamp01(a));
            return true;
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

        public static float ProjectBoundsExtent(Bounds bounds, Vector3 axis)
        {
            Vector3 normalized = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up;
            Vector3 extents = bounds.extents;
            return Mathf.Abs(normalized.x) * extents.x
                + Mathf.Abs(normalized.y) * extents.y
                + Mathf.Abs(normalized.z) * extents.z;
        }

        public static bool ExpandedBoundsContains(Bounds bounds, Vector3 point, float radius)
        {
            float safeRadius = Mathf.Max(0f, radius);
            if (safeRadius > 0f)
                bounds.Expand(safeRadius);
            return bounds.Contains(point) || bounds.SqrDistance(point) <= safeRadius * safeRadius;
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

        public static object ReadMemberValue(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
                return null;

            Type type = target.GetType();
            if (type == null)
                return null;

            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                FieldInfo field = type.GetField(memberName, Flags);
                if (field != null)
                    return field.GetValue(target);

                PropertyInfo property = type.GetProperty(memberName, Flags);
                if (property != null)
                    return property.GetValue(target, null);
            }
            catch
            {
            }

            return null;
        }

        public static string ReadMemberString(object target, string memberName)
        {
            object value = ReadMemberValue(target, memberName);
            return value != null ? value.ToString() : string.Empty;
        }

        public static string ReadMemberStringLowerInvariant(object target, string memberName)
        {
            string value = ReadMemberString(target, memberName);
            if (!string.IsNullOrEmpty(value))
                return value.ToLowerInvariant();
            return string.Empty;
        }

        public static Transform ReadMemberTransform(object target, string memberName)
        {
            return ReadMemberValue(target, memberName) is Transform transform ? transform : null;
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

        public static Material CreateParticleMaterial(
            string name,
            Texture texture,
            bool additive,
            bool useTransparentShader,
            bool allowTransparentFallback)
        {
            Shader shader = useTransparentShader ? FindTransparentShader() : FindParticleShader(additive);
            if (shader == null && allowTransparentFallback)
                shader = FindTransparentShader();
            if (shader == null)
                return null;

            Material material = new Material(shader);
            material.name = name;
            material.color = Color.white;
            material.mainTexture = texture;
            return material;
        }

        public static Texture2D CreateProceduralTexture(int width, int height, Color[] pixels)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
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
            if (multipliers.TryGetValue(bodyName.Trim(), out var m))
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

    internal static class KerbalFxDustVisualDefaults
    {
        private const float NeutralDustChannel = 0.7f;

        public static readonly Color Color = new Color(NeutralDustChannel, NeutralDustChannel, NeutralDustChannel, 1f);
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
