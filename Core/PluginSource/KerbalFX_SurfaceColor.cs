using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace KerbalFX
{
    internal enum KerbalFxSurfaceColorSource
    {
        None,
        BiomeMap,
        TintProfile,
        Renderer,
        Neutral
    }

    internal struct KerbalFxBodyTintEntry
    {
        public bool HasColor;
        public Color Color;
        public float StrengthMultiplier;
        public bool OverrideBiome;

        public static KerbalFxBodyTintEntry Default { get { return new KerbalFxBodyTintEntry { StrengthMultiplier = 1f }; } }

        public static KerbalFxBodyTintEntry FromColor(Color color)
        {
            return new KerbalFxBodyTintEntry { HasColor = true, Color = color, StrengthMultiplier = 1f };
        }

        public static KerbalFxBodyTintEntry FromColor(Color color, float strength)
        {
            return new KerbalFxBodyTintEntry { HasColor = true, Color = color, StrengthMultiplier = strength };
        }
    }

    internal static class KerbalFxSurfaceColorCore
    {
        public const float MinAcceptableAlpha = 0.05f;
        public const float MinAcceptableLuminance = 0.04f;
        public const float LumaR = 0.2126f;
        public const float LumaG = 0.7152f;
        public const float LumaB = 0.0722f;

        public static bool TryReadBiomeColor(
            CelestialBody body,
            double latitudeDeg,
            double longitudeDeg,
            out Color color,
            out string biomeName)
        {
            color = Color.white;
            biomeName = string.Empty;
            if (body == null || body.BiomeMap == null)
                return false;

            try
            {
                CBAttributeMapSO.MapAttribute attribute = body.BiomeMap.GetAtt(
                    latitudeDeg * Mathf.Deg2Rad,
                    longitudeDeg * Mathf.Deg2Rad);
                if (attribute == null)
                    return false;

                Color sampled = attribute.mapColor;
                if (sampled.a < MinAcceptableAlpha)
                    return false;

                float luminance = LumaR * sampled.r + LumaG * sampled.g + LumaB * sampled.b;
                if (luminance < MinAcceptableLuminance)
                    return false;

                color = new Color(
                    Mathf.Clamp01(sampled.r),
                    Mathf.Clamp01(sampled.g),
                    Mathf.Clamp01(sampled.b),
                    1f);
                biomeName = attribute.name ?? string.Empty;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryReadRendererColor(Collider collider, out Color color)
        {
            color = Color.white;
            if (collider == null)
                return false;

            Renderer renderer = collider.GetComponent<Renderer>();
            if (renderer == null)
                renderer = collider.GetComponentInParent<Renderer>();
            if (renderer == null)
                return false;

            Material material = renderer.sharedMaterial;
            if (material == null)
                return false;

            try
            {
                if (material.HasProperty("_Color"))
                {
                    Color sampled = material.GetColor("_Color");
                    if (sampled.a < MinAcceptableAlpha)
                        return false;
                    color = new Color(Mathf.Clamp01(sampled.r), Mathf.Clamp01(sampled.g), Mathf.Clamp01(sampled.b), 1f);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        public static string FormatColor(Color color)
        {
            return color.r.ToString("F2", CultureInfo.InvariantCulture)
                + "," + color.g.ToString("F2", CultureInfo.InvariantCulture)
                + "," + color.b.ToString("F2", CultureInfo.InvariantCulture);
        }
    }

    internal sealed class KerbalFxSurfaceTintProfile
    {
        private readonly string configNodeName;
        private readonly Func<string> pathProvider;
        private readonly Action<Dictionary<string, KerbalFxBodyTintEntry>> seedDefaults;
        private readonly Dictionary<string, KerbalFxBodyTintEntry> entries;
        private DateTime lastConfigWriteUtc = DateTime.MinValue;

        public KerbalFxSurfaceTintProfile(
            string configNodeName,
            Func<string> pathProvider,
            Action<Dictionary<string, KerbalFxBodyTintEntry>> seedDefaults)
        {
            this.configNodeName = configNodeName;
            this.pathProvider = pathProvider;
            this.seedDefaults = seedDefaults;
            this.entries = new Dictionary<string, KerbalFxBodyTintEntry>(16, StringComparer.OrdinalIgnoreCase);
        }

        public int Count { get { return entries.Count; } }

        public void Refresh()
        {
            SeedDefaults();
            if (GameDatabase.Instance != null)
            {
                ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes(configNodeName);
                if (nodes != null)
                    for (int i = 0; i < nodes.Length; i++)
                        if (nodes[i] != null)
                            KerbalFxUtil.LoadBodyTints(nodes[i], entries);
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
                                    KerbalFxUtil.LoadBodyTints(nodes[i], entries);
                    }
                }
            }
            catch (Exception ex)
            {
                failure = ex.Message;
            }
            return true;
        }

        public bool TryGet(string bodyName, out KerbalFxBodyTintEntry entry)
        {
            entry = KerbalFxBodyTintEntry.Default;
            if (string.IsNullOrEmpty(bodyName))
                return false;
            return entries.TryGetValue(bodyName.Trim(), out entry);
        }

        private string GetPath()
        {
            return pathProvider != null ? pathProvider() : null;
        }

        private void SeedDefaults()
        {
            entries.Clear();
            if (seedDefaults != null)
                seedDefaults(entries);
        }
    }

    internal sealed class KerbalFxSurfaceColorSampler
    {
        private readonly Color neutralColor;
        private readonly float blendStrength;
        private readonly float refreshInterval;
        private readonly float smoothingSpeed;
        private readonly bool allowRendererFallback;
        private readonly KerbalFxSurfaceTintProfile tintProfile;

        private Color targetColor;
        private Color smoothedColor;
        private Color lastRawSample;
        private string lastBiomeName;
        private string lastBodyName;
        private KerbalFxSurfaceColorSource lastSource;
        private float refreshTimer;
        private int lastBodyIndex = int.MinValue;
        private int lastColliderId = int.MinValue;
        private Vector3 lastSamplePoint;
        private bool hasTarget;

        private const float PositionReuseDistanceSq = 9f;

        public KerbalFxSurfaceColorSampler(
            Color neutralColor,
            float blendStrength,
            float refreshInterval,
            float smoothingSpeed,
            bool allowRendererFallback,
            KerbalFxSurfaceTintProfile tintProfile)
        {
            this.neutralColor = neutralColor;
            this.blendStrength = Mathf.Clamp01(blendStrength);
            this.refreshInterval = Mathf.Max(0.05f, refreshInterval);
            this.smoothingSpeed = Mathf.Max(0.5f, smoothingSpeed);
            this.allowRendererFallback = allowRendererFallback;
            this.tintProfile = tintProfile;
            this.targetColor = neutralColor;
            this.smoothedColor = neutralColor;
            this.lastRawSample = neutralColor;
            this.lastBiomeName = string.Empty;
            this.lastBodyName = string.Empty;
            this.lastSource = KerbalFxSurfaceColorSource.None;
        }

        public Color CurrentColor { get { return smoothedColor; } }

        public Color TargetColor { get { return targetColor; } }

        public Color LastRawSample { get { return lastRawSample; } }

        public string LastBiomeName { get { return lastBiomeName; } }

        public string LastBodyName { get { return lastBodyName; } }

        public KerbalFxSurfaceColorSource LastSource { get { return lastSource; } }

        public bool HasSample { get { return hasTarget; } }

        public void Reset()
        {
            targetColor = neutralColor;
            smoothedColor = neutralColor;
            lastRawSample = neutralColor;
            lastBiomeName = string.Empty;
            lastBodyName = string.Empty;
            lastSource = KerbalFxSurfaceColorSource.None;
            refreshTimer = 0f;
            lastBodyIndex = int.MinValue;
            lastColliderId = int.MinValue;
            lastSamplePoint = Vector3.zero;
            hasTarget = false;
        }

        public void Tick(Vessel vessel, Vector3 worldPoint, Collider collider, float dt)
        {
            CelestialBody body = vessel != null ? vessel.mainBody : null;
            int bodyIndex = body != null ? body.flightGlobalsIndex : int.MinValue;
            int colliderId = GetColliderId(collider);

            refreshTimer -= dt;
            if (refreshTimer <= 0f || bodyIndex != lastBodyIndex)
            {
                refreshTimer = refreshInterval;
                bool bodyChanged = bodyIndex != lastBodyIndex;
                if (bodyChanged || !CanReuseTarget(worldPoint, colliderId))
                {
                    targetColor = ResolveTargetColor(body, worldPoint, collider);
                    hasTarget = true;
                }

                StoreSampleSignature(bodyIndex, colliderId, worldPoint);
            }

            float lerpSpeed = Mathf.Clamp01(dt * smoothingSpeed);
            smoothedColor = Color.Lerp(smoothedColor, targetColor, lerpSpeed);
        }

        public void ApplySnapshot(Vessel vessel, Vector3 worldPoint, Collider collider)
        {
            CelestialBody body = vessel != null ? vessel.mainBody : null;
            int bodyIndex = body != null ? body.flightGlobalsIndex : int.MinValue;
            int colliderId = GetColliderId(collider);
            refreshTimer = refreshInterval;
            targetColor = ResolveTargetColor(body, worldPoint, collider);
            smoothedColor = targetColor;
            hasTarget = true;
            StoreSampleSignature(bodyIndex, colliderId, worldPoint);
        }

        private static int GetColliderId(Collider collider)
        {
            return collider != null ? collider.GetInstanceID() : 0;
        }

        private bool CanReuseTarget(Vector3 worldPoint, int colliderId)
        {
            return hasTarget
                && colliderId == lastColliderId
                && (worldPoint - lastSamplePoint).sqrMagnitude <= PositionReuseDistanceSq;
        }

        private void StoreSampleSignature(int bodyIndex, int colliderId, Vector3 worldPoint)
        {
            lastBodyIndex = bodyIndex;
            lastColliderId = colliderId;
            lastSamplePoint = worldPoint;
        }

        private Color ResolveTargetColor(CelestialBody body, Vector3 worldPoint, Collider collider)
        {
            lastBodyName = body != null ? body.bodyName : string.Empty;

            KerbalFxBodyTintEntry entry = KerbalFxBodyTintEntry.Default;
            bool hasEntry = body != null && tintProfile != null && tintProfile.TryGet(body.bodyName, out entry);
            float effectiveBlend = Mathf.Clamp01(blendStrength * entry.StrengthMultiplier);

            if (hasEntry && entry.HasColor && entry.OverrideBiome)
            {
                lastRawSample = entry.Color;
                lastBiomeName = string.Empty;
                lastSource = KerbalFxSurfaceColorSource.TintProfile;
                return Color.Lerp(neutralColor, entry.Color, effectiveBlend);
            }

            Color sampled;
            string biomeName;
            if (body != null
                && KerbalFxSurfaceColorCore.TryReadBiomeColor(
                    body,
                    body.GetLatitude(worldPoint),
                    body.GetLongitude(worldPoint),
                    out sampled,
                    out biomeName))
            {
                lastRawSample = sampled;
                lastBiomeName = biomeName;
                lastSource = KerbalFxSurfaceColorSource.BiomeMap;
                return Color.Lerp(neutralColor, sampled, effectiveBlend);
            }

            if (hasEntry && entry.HasColor)
            {
                lastRawSample = entry.Color;
                lastBiomeName = string.Empty;
                lastSource = KerbalFxSurfaceColorSource.TintProfile;
                return Color.Lerp(neutralColor, entry.Color, effectiveBlend);
            }

            if (allowRendererFallback
                && KerbalFxSurfaceColorCore.TryReadRendererColor(collider, out sampled))
            {
                lastRawSample = sampled;
                lastBiomeName = string.Empty;
                lastSource = KerbalFxSurfaceColorSource.Renderer;
                return Color.Lerp(neutralColor, sampled, effectiveBlend);
            }

            lastRawSample = neutralColor;
            lastBiomeName = string.Empty;
            lastSource = KerbalFxSurfaceColorSource.Neutral;
            return neutralColor;
        }
    }
}
