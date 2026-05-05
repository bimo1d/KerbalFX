using System;
using System.Text;
using UnityEngine;

namespace KerbalFX.ImpactPuffs
{
    internal sealed partial class EngineGroundPuffEmitter
    {
        private static bool TryFindGroundHit(
            Vector3 origin,
            Vector3 primaryDir,
            Vessel vessel,
            float maxDistance,
            out RaycastHit hit,
            out int rawHitCount,
            out int rigidbodySkipped,
            out int partSkipped,
            out int normalSkipped)
        {
            hit = new RaycastHit();
            rawHitCount = 0;
            rigidbodySkipped = 0;
            partSkipped = 0;
            normalSkipped = 0;
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
            if (!TryRay(origin, primary, vessel, maxDistance, out primaryHit, out rawHitCount, out rigidbodySkipped, out partSkipped, out normalSkipped))
            {
                return false;
            }

            hit = primaryHit;
            return true;
        }

        private static bool TryRay(
            Vector3 origin,
            Vector3 direction,
            Vessel vessel,
            float maxDistance,
            out RaycastHit bestHit,
            out int rawHitCount,
            out int rigidbodySkipped,
            out int partSkipped,
            out int normalSkipped)
        {
            bestHit = new RaycastHit();
            rawHitCount = 0;
            rigidbodySkipped = 0;
            partSkipped = 0;
            normalSkipped = 0;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction.normalized,
                SharedHits,
                maxDistance,
                TerrainRaycastMask,
                QueryTriggerInteraction.Ignore
            );
            rawHitCount = hitCount;

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

                Rigidbody hitBody = candidate.rigidbody != null ? candidate.rigidbody : candidate.collider.attachedRigidbody;
                if (hitBody != null)
                {
                    rigidbodySkipped++;
                    continue;
                }

                Part hitPart = candidate.collider.GetComponentInParent<Part>();
                if (hitPart != null)
                {
                    partSkipped++;
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
                            normalSkipped++;
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

        internal static float GetSafeTerrainHeightAgl(Vessel vessel)
        {
            if (vessel == null)
            {
                return -1f;
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

            if (vessel.Landed || vessel.situation == Vessel.Situations.PRELAUNCH)
            {
                return 0f;
            }

            return -1f;
        }

        internal static bool IsLaunchsiteExcludedSurface(Collider collider)
        {
            if (collider == null || collider.transform == null)
            {
                return false;
            }

            return KerbalFxUtil.ContainsAnyTokenInObjectHierarchy(collider.transform, LaunchsiteSurfaceTokens, 6);
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

            StringBuilder sb = new StringBuilder(128);
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
    }
}
