using System.Collections.Generic;
using UnityEngine;

namespace KerbalFX.AeroFX
{
    internal struct WingtipRibbonAnchor
    {
        public Part Part;
        public Vector3 WorldPoint;
        public Vector3 Outward;
        public Vector3 LocalPoint;
        public Vector3 LocalOutward;
        public float Clearance;
        public float Score;
        public float SideSign;

        public bool IsValid
        {
            get { return Part != null; }
        }

        public string PartName
        {
            get
            {
                if (Part == null)
                    return "none";
                return Part.partInfo != null ? Part.partInfo.name : Part.name;
            }
        }
    }

    internal static class AeroFlightUtil
    {
        public static bool TryGetFlightBasis(
            Vessel vessel,
            out Vector3 forward,
            out Vector3 right,
            out Vector3 up,
            out Vector3 airflowBack)
        {
            forward = Vector3.forward;
            right = Vector3.right;
            up = Vector3.up;
            airflowBack = Vector3.back;

            if (vessel == null)
                return false;

            Transform reference = vessel.ReferenceTransform != null
                ? vessel.ReferenceTransform
                : (vessel.rootPart != null ? vessel.rootPart.transform : null);
            if (reference == null)
                return false;

            Vector3 velocity = ToVector3(vessel.srf_velocity);
            if (velocity.sqrMagnitude > 1f)
                forward = velocity.normalized;
            else
                forward = reference.forward.sqrMagnitude > 0.0001f ? reference.forward.normalized : Vector3.forward;

            Vector3 craftUp = reference.up.sqrMagnitude > 0.0001f ? reference.up.normalized : Vector3.up;
            right = Vector3.Cross(craftUp, forward);
            if (right.sqrMagnitude < 0.0001f)
                right = reference.right.sqrMagnitude > 0.0001f ? reference.right.normalized : Vector3.right;
            else
                right.Normalize();

            up = Vector3.Cross(forward, right);
            if (up.sqrMagnitude < 0.0001f)
                up = craftUp;
            else
                up.Normalize();

            airflowBack = -forward;
            return true;
        }

        public static Vector3 ToVector3(Vector3d input)
        {
            return new Vector3((float)input.x, (float)input.y, (float)input.z);
        }
    }

    internal static class AeroTrailAnchors
    {
        private struct Candidate
        {
            public Part Part;
            public Vector3 Point;
            public Vector3 Outward;
            public float Clearance;
            public float Score;
            public float SideSign;
        }

        private static readonly string[] WingLikeTokens =
        {
            "wing",
            "winglet",
            "elevon",
            "aileron",
            "canard",
            "stabilizer",
            "stabilator",
            "fin",
            "tail",
            "flap"
        };

        private static readonly List<Candidate> allCandidates = new List<Candidate>(32);

        private class ScoreDescending : IComparer<Candidate>
        {
            public int Compare(Candidate a, Candidate b)
            {
                return b.Score.CompareTo(a.Score);
            }
        }

        private static readonly ScoreDescending scoreComparer = new ScoreDescending();

        public static int TryResolveAll(
            Vessel vessel,
            WingtipRibbonAnchor[] results,
            int maxResults,
            out int liftPartCount,
            out int candidateCount)
        {
            liftPartCount = 0;
            candidateCount = 0;

            if (vessel == null || vessel.parts == null || vessel.rootPart == null)
                return 0;

            Vector3 forward;
            Vector3 rightAxis;
            Vector3 upAxis;
            Vector3 airflowBack;
            if (!AeroFlightUtil.TryGetFlightBasis(vessel, out forward, out rightAxis, out upAxis, out airflowBack))
                return 0;

            Vector3 centerOfMass = vessel.CoM;
            allCandidates.Clear();

            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part part = vessel.parts[i];
                if (part == null || !IsLiftCapablePart(part))
                    continue;

                liftPartCount++;
                if (part.ShieldedFromAirstream)
                    continue;

                Bounds bounds;
                if (!TryGetPartBounds(part, out bounds))
                    continue;

                Vector3 boundsCenter = bounds.center;
                Vector3 offset = boundsCenter - centerOfMass;
                Vector3 radialOffset = Vector3.ProjectOnPlane(offset, forward);
                if (radialOffset.magnitude < 0.45f)
                    continue;

                Vector3 supportPoint;
                Vector3 outward;
                float score;
                if (!TryGetPartTipPoint(
                    part,
                    bounds,
                    centerOfMass,
                    forward,
                    out supportPoint,
                    out outward,
                    out score))
                {
                    continue;
                }

                float lateral = Vector3.Dot(outward, rightAxis);
                Candidate candidate = new Candidate
                {
                    Part = part,
                    Point = supportPoint,
                    Outward = outward,
                    Clearance = Mathf.Clamp(bounds.extents.magnitude * 0.18f, 0.05f, 0.18f),
                    Score = score,
                    SideSign = lateral >= 0f ? 1f : -1f
                };

                allCandidates.Add(candidate);
                candidateCount++;
            }

            if (allCandidates.Count == 0)
                return 0;

            allCandidates.Sort(scoreComparer);
            int count = Mathf.Min(allCandidates.Count, maxResults);
            for (int i = 0; i < count; i++)
            {
                Candidate c = allCandidates[i];
                WingtipRibbonAnchor anchor;
                anchor.Part = c.Part;
                anchor.WorldPoint = c.Point;
                anchor.Outward = c.Outward;
                anchor.LocalPoint = c.Part.transform.InverseTransformPoint(c.Point);
                anchor.LocalOutward = c.Part.transform.InverseTransformDirection(c.Outward);
                anchor.Clearance = c.Clearance;
                anchor.Score = c.Score;
                anchor.SideSign = c.SideSign;
                results[i] = anchor;
            }

            return count;
        }

        private static bool IsLiftCapablePart(Part part)
        {
            if (part == null)
                return false;

            string name = part.partInfo != null ? part.partInfo.name : part.name;
            bool looksWingLike = KerbalFxUtil.ContainsAnyToken(name, WingLikeTokens);
            if (!looksWingLike)
                return false;

            if (part.FindModuleImplementing<ModuleControlSurface>() != null)
                return true;
            if (part.FindModuleImplementing<ModuleLiftingSurface>() != null)
                return true;
            if (part.FindModuleImplementing<ModuleAeroSurface>() != null)
                return true;

            return true;
        }

        private static bool TryGetPartBounds(Part part, out Bounds bounds)
        {
            bounds = new Bounds();
            if (part == null)
                return false;

            var renderers = part.FindModelComponents<Renderer>();
            if (renderers == null || renderers.Count == 0)
                return false;

            bool initialized = false;
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return initialized;
        }

        private static bool TryGetPartTipPoint(
            Part part,
            Bounds bounds,
            Vector3 centerOfMass,
            Vector3 forwardAxis,
            out Vector3 point,
            out Vector3 outward,
            out float score)
        {
            point = bounds.center;
            outward = Vector3.right;
            score = float.MinValue;

            Vector3 partCenter = bounds.center;
            bool foundMeshPoint = false;
            float bestScore = float.MinValue;
            Vector3 bestPoint = partCenter;

            var meshFilters = part.FindModelComponents<MeshFilter>();
            if (meshFilters != null)
            {
                for (int i = 0; i < meshFilters.Count; i++)
                {
                    MeshFilter meshFilter = meshFilters[i];
                    if (meshFilter == null || meshFilter.sharedMesh == null || meshFilter.transform == null)
                        continue;

                    Vector3[] vertices = meshFilter.sharedMesh.vertices;
                    if (vertices == null || vertices.Length == 0)
                        continue;

                    Transform meshTransform = meshFilter.transform;
                    for (int j = 0; j < vertices.Length; j += 2)
                    {
                        Vector3 worldPoint = meshTransform.TransformPoint(vertices[j]);
                        Vector3 offsetFromCom = worldPoint - centerOfMass;
                        Vector3 radialOffset = Vector3.ProjectOnPlane(offsetFromCom, forwardAxis);
                        float radialDist = radialOffset.magnitude;
                        if (radialDist < 0.20f)
                            continue;

                        Vector3 offsetFromPart = worldPoint - partCenter;
                        float forwardOffset = Mathf.Abs(Vector3.Dot(offsetFromPart, forwardAxis));
                        float candidateScore = radialDist * 1.95f - forwardOffset * 0.32f;
                        if (candidateScore <= bestScore)
                            continue;

                        bestScore = candidateScore;
                        bestPoint = worldPoint;
                        foundMeshPoint = true;
                    }
                }
            }

            if (!foundMeshPoint)
            {
                Vector3 offset = bounds.center - centerOfMass;
                Vector3 radialOffset = Vector3.ProjectOnPlane(offset, forwardAxis);
                bestPoint = bounds.center + radialOffset.normalized * bounds.extents.magnitude * 0.5f;
                bestScore = radialOffset.magnitude * 1.60f;
            }

            Vector3 finalRadialOffset = Vector3.ProjectOnPlane(bestPoint - centerOfMass, forwardAxis);
            if (finalRadialOffset.sqrMagnitude < 0.01f)
                return false;

            point = bestPoint;
            outward = finalRadialOffset.normalized;
            score = bestScore;
            return true;
        }
    }
}
