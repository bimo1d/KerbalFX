using System;
using System.Collections.Generic;
using UnityEngine;

namespace KerbalFX.RoverDust
{
    internal static class RoverDustSurfaceRules
    {
        private static readonly string[] KscSurfaceTokens = { "runway", "launchpad", "launch_pad", "launch pad", "crawlerway", "launchsite", "launch_site" };
        private static readonly string[] KscMaterialTokens = { "runway", "launchpad", "crawlerway" };
        private static readonly string[] WaterSurfaceTokens = { "water", "ocean", "sea" };
        private static readonly string[] KerbalKonstructsTokens = { "kerbalkonstructs", "staticobject" };
        private static readonly List<Component> sharedComponentBuffer = new List<Component>(24);
        private static readonly Dictionary<int, SurfaceSuppressionCacheEntry> suppressionCache = new Dictionary<int, SurfaceSuppressionCacheEntry>(256);

        private const float SuppressionCacheTtl = 8.0f;
        private const int SuppressionCacheMaxEntries = 512;

        public static bool ShouldSuppressSurface(Collider collider, out string reason)
        {
            reason = string.Empty;
            if (collider == null)
                return false;

            int colliderId = collider.GetInstanceID();
            float now = Time.time;
            if (suppressionCache.TryGetValue(colliderId, out var cached) && now <= cached.ExpiresAt)
            {
                reason = cached.Reason;
                return cached.Suppress;
            }

            bool suppress = EvaluateSurface(collider, out reason);
            StoreSuppressionCache(colliderId, suppress, reason, now);
            return suppress;
        }

        private static bool EvaluateSurface(Collider collider, out string reason)
        {
            reason = string.Empty;

            if (IsPartSurface(collider))
            {
                reason = "Part_Surface";
                return true;
            }

            if (KerbalFxUtil.ContainsAnyToken(collider.name, KscSurfaceTokens)
                || KerbalFxUtil.ContainsAnyTokenInObjectHierarchy(collider.transform, KscSurfaceTokens, 14))
            {
                reason = "KSC_Surface";
                return true;
            }

            Renderer renderer = collider.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null
                && KerbalFxUtil.ContainsAnyToken(renderer.sharedMaterial.name, KscMaterialTokens))
            {
                reason = "KSC_Material";
                return true;
            }

            if (KerbalFxUtil.ContainsAnyToken(collider.name, WaterSurfaceTokens)
                || KerbalFxUtil.ContainsAnyTokenInObjectHierarchy(collider.transform, WaterSurfaceTokens, 8))
            {
                reason = "Water_Surface";
                return true;
            }

            if (IsKerbalKonstructsStatic(collider))
            {
                reason = "KerbalKonstructs_Static";
                return true;
            }

            return false;
        }

        private static void StoreSuppressionCache(int colliderId, bool suppress, string reason, float now)
        {
            if (suppressionCache.Count > SuppressionCacheMaxEntries)
                suppressionCache.Clear();

            suppressionCache[colliderId] = new SurfaceSuppressionCacheEntry(
                suppress,
                reason ?? string.Empty,
                now + SuppressionCacheTtl);
        }

        private static bool IsPartSurface(Collider collider)
        {
            return collider != null && collider.GetComponentInParent<Part>() != null;
        }

        private static bool IsKerbalKonstructsStatic(Collider collider)
        {
            if (collider == null)
                return false;

            Transform t = collider.transform;
            int depth = 0;
            while (t != null && depth < 10)
            {
                sharedComponentBuffer.Clear();
                t.GetComponents<Component>(sharedComponentBuffer);
                if (ContainsAnyTokenInTypes(sharedComponentBuffer, KerbalKonstructsTokens))
                    return true;
                t = t.parent;
                depth++;
            }
            return false;
        }

        private static bool ContainsAnyTokenInTypes(List<Component> components, string[] tokens)
        {
            if (components == null || tokens == null)
                return false;

            for (int i = 0; i < components.Count; i++)
            {
                Component c = components[i];
                if (c == null)
                    continue;
                Type type = c.GetType();
                if (type == null)
                    continue;

                string fullName = type.FullName;
                if (!string.IsNullOrEmpty(fullName) && KerbalFxUtil.ContainsAnyToken(fullName, tokens))
                    return true;
                if (KerbalFxUtil.ContainsAnyToken(type.Name, tokens))
                    return true;
            }
            return false;
        }

        private readonly struct SurfaceSuppressionCacheEntry
        {
            public readonly bool Suppress;
            public readonly string Reason;
            public readonly float ExpiresAt;

            public SurfaceSuppressionCacheEntry(bool suppress, string reason, float expiresAt)
            {
                Suppress = suppress;
                Reason = reason;
                ExpiresAt = expiresAt;
            }
        }
    }
}
