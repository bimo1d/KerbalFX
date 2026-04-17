using UnityEngine;

namespace KerbalFX.ImpactPuffs
{
    internal sealed partial class EngineGroundPuffEmitter
    {
        private static float EvaluateVolumetricLightFactor(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal, float normalizedThrust)
        {
            if (!ImpactPuffsConfig.UseLightAware)
            {
                return 1f;
            }

            float sunLight = EvaluateSunLighting(vessel, worldPoint, surfaceNormal, true);
            float thrust01 = Mathf.Clamp01(normalizedThrust);
            float engineGlow = Mathf.Lerp(0.0f, 0.020f, Mathf.Pow(thrust01, 2.20f));
            return Mathf.Clamp01(Mathf.Max(sunLight, engineGlow));
        }

        internal static float GetSunLightFactor(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal)
        {
            if (!ImpactPuffsConfig.UseLightAware)
                return 1f;
            return EvaluateSunLighting(vessel, worldPoint, surfaceNormal, false);
        }

        internal static float GetTouchdownLightFactor(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal)
        {
            if (!ImpactPuffsConfig.UseLightAware)
                return 1f;
            return EvaluateSunLighting(vessel, worldPoint, surfaceNormal, true);
        }

        private static float EvaluateSunLighting(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal, bool includeTerrainOcclusion)
        {
            if (surfaceNormal.sqrMagnitude < 0.0001f)
                surfaceNormal = Vector3.up;
            surfaceNormal.Normalize();

            Vector3 sunDirection;
            if (!KerbalFxSunLight.TryGetSunDirection(worldPoint, out sunDirection))
                return 0f;

            float ndotl = Mathf.Clamp01(Vector3.Dot(surfaceNormal, sunDirection));
            if (ndotl <= 0f)
                return 0f;

            float litFactor = Mathf.Lerp(0.05f, 1f, Mathf.Pow(ndotl, 0.58f));

            bool bodyOccluded = vessel != null
                && vessel.mainBody != null
                && KerbalFxSunLight.IsSunOccludedByBody(vessel.mainBody, worldPoint, sunDirection);
            bool terrainOccluded = includeTerrainOcclusion
                && !bodyOccluded
                && IsLocallySunOccluded(vessel, worldPoint, surfaceNormal, sunDirection);

            if (!bodyOccluded && !terrainOccluded)
                return Mathf.Clamp01(litFactor);

            float shadowStrength = Mathf.Clamp01(ImpactPuffsRuntimeConfig.ShadowLightFactor);
            float shadowMul = terrainOccluded
                ? Mathf.Lerp(0.05f, 0.12f, shadowStrength) * 0.92f
                : Mathf.Lerp(0.06f, includeTerrainOcclusion ? 0.14f : 0.16f, shadowStrength);
            litFactor *= shadowMul;
            float shadowCap = terrainOccluded ? 0.10f : 0.18f;
            return Mathf.Clamp01(Mathf.Min(litFactor, shadowCap));
        }

        private static bool IsLocallySunOccluded(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal, Vector3 sunDirection)
        {
            if (vessel == null || vessel.packed || !vessel.loaded || sunDirection.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            System.Guid vesselId = vessel.id;
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
                ValidUntil = now + 0.14f,
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
            float maxDistance = SunOcclusionRayDistance;

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
    }
}
