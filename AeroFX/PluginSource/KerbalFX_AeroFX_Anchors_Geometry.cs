using System.Collections.Generic;
using UnityEngine;

namespace KerbalFX.AeroFX
{
    internal static partial class AeroTrailAnchors
    {
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
                if (renderer == null || !renderer.enabled || IsIgnoredFxRenderer(renderer))
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
            Vector3 rightAxis,
            WingtipAnchorRole role,
            out Vector3 point,
            out Vector3 outward,
            out float score)
        {
            point = bounds.center;
            outward = Vector3.right;
            score = float.MinValue;

            if (useFastAnchorScan)
                return TryGetBoundsTipPoint(
                    bounds,
                    centerOfMass,
                    forwardAxis,
                    rightAxis,
                    role,
                    out point,
                    out outward,
                    out score);

            Vector3 partCenter = bounds.center;
            bool foundMeshPoint = false;
            float bestScore = float.MinValue;
            Vector3 bestPoint = partCenter;
            float bestLateralDist = float.MinValue;
            float bestHeightAboveCom = float.MinValue;
            float bestRadialDist = float.MinValue;
            float bestForwardOffset = float.MaxValue;

            var meshFilters = part.FindModelComponents<MeshFilter>();
            if (meshFilters != null)
            {
                for (int i = 0; i < meshFilters.Count; i++)
                {
                    MeshFilter meshFilter = meshFilters[i];
                    if (meshFilter == null || meshFilter.sharedMesh == null || meshFilter.transform == null || IsIgnoredFxMeshFilter(meshFilter))
                        continue;

                    Vector3[] vertices = meshFilter.sharedMesh.vertices;
                    if (vertices == null || vertices.Length == 0)
                        continue;

                    Transform meshTransform = meshFilter.transform;
                    for (int j = 0; j < vertices.Length; j++)
                    {
                        Vector3 worldPoint = meshTransform.TransformPoint(vertices[j]);
                        Vector3 offsetFromCom = worldPoint - centerOfMass;
                        Vector3 radialOffset = Vector3.ProjectOnPlane(offsetFromCom, forwardAxis);
                        float radialDist = radialOffset.magnitude;
                        if (radialDist < 0.20f)
                            continue;

                        float lateralDist = Mathf.Abs(Vector3.Dot(offsetFromCom, rightAxis));
                        float heightAboveCom = Vector3.Dot(offsetFromCom, cachedUpAxis);
                        Vector3 offsetFromPart = worldPoint - partCenter;
                        float forwardOffset = Mathf.Abs(Vector3.Dot(offsetFromPart, forwardAxis));
                        float candidateScore = EvaluateSupportPointScore(role, lateralDist, radialDist, forwardOffset, heightAboveCom);
                        if (!IsBetterSupportPoint(
                            role,
                            lateralDist,
                            heightAboveCom,
                            radialDist,
                            forwardOffset,
                            candidateScore,
                            bestLateralDist,
                            bestHeightAboveCom,
                            bestRadialDist,
                            bestForwardOffset,
                            bestScore))
                            continue;

                        bestScore = candidateScore;
                        bestLateralDist = lateralDist;
                        bestHeightAboveCom = heightAboveCom;
                        bestRadialDist = radialDist;
                        bestForwardOffset = forwardOffset;
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
                float lateralDist = Mathf.Abs(Vector3.Dot(offset, rightAxis));
                bestScore = EvaluateSupportPointFallbackScore(role, lateralDist, radialOffset.magnitude);
            }
            else if (role == WingtipAnchorRole.MainWing
                || role == WingtipAnchorRole.Control
                || role == WingtipAnchorRole.Canard)
            {
                float sideSign = Vector3.Dot(bestPoint - centerOfMass, rightAxis) < 0f ? -1f : 1f;
                Vector3 centeredPoint;
                float centeredLateral;
                float centeredHeight;
                float centeredRadial;
                if (TryGetCenteredFrontierPoint(
                    part,
                    centerOfMass,
                    forwardAxis,
                    rightAxis,
                    sideSign,
                    out centeredPoint,
                    out centeredLateral,
                    out centeredHeight,
                    out centeredRadial)
                    && centeredLateral >= bestLateralDist - MainSurfaceLateralTieEpsilon)
                {
                    bestPoint = centeredPoint;
                    bestLateralDist = centeredLateral;
                    bestHeightAboveCom = centeredHeight;
                    bestRadialDist = centeredRadial;
                    bestScore = EvaluateSupportPointScore(
                        role,
                        centeredLateral,
                        centeredRadial,
                        Mathf.Abs(Vector3.Dot(centeredPoint - partCenter, forwardAxis)),
                        centeredHeight);
                }
            }

            Vector3 finalRadialOffset = Vector3.ProjectOnPlane(bestPoint - centerOfMass, forwardAxis);
            if (finalRadialOffset.sqrMagnitude < 0.01f)
                return false;

            point = bestPoint;
            outward = finalRadialOffset.normalized;
            score = bestScore;
            return true;
        }

        private static bool TryGetBoundsTipPoint(
            Bounds bounds,
            Vector3 centerOfMass,
            Vector3 forwardAxis,
            Vector3 rightAxis,
            WingtipAnchorRole role,
            out Vector3 point,
            out Vector3 outward,
            out float score)
        {
            Vector3 offset = bounds.center - centerOfMass;
            Vector3 radialOffset = Vector3.ProjectOnPlane(offset, forwardAxis);
            float sideSign = Vector3.Dot(offset, rightAxis) < 0f ? -1f : 1f;
            Vector3 radialDirection = radialOffset.sqrMagnitude > 0.01f
                ? radialOffset.normalized
                : ((role == WingtipAnchorRole.MainWing
                    || role == WingtipAnchorRole.Control
                    || role == WingtipAnchorRole.Canard)
                        ? rightAxis * sideSign
                        : cachedUpAxis);
            Vector3 supportDirection = role == WingtipAnchorRole.MainWing
                || role == WingtipAnchorRole.Control
                || role == WingtipAnchorRole.Canard
                    ? rightAxis * sideSign
                    : radialDirection;

            point = bounds.center + supportDirection * GetBoundsReach(bounds, supportDirection);
            Vector3 supportOffset = point - centerOfMass;
            Vector3 supportRadialOffset = Vector3.ProjectOnPlane(supportOffset, forwardAxis);
            outward = supportRadialOffset.sqrMagnitude > 0.01f ? supportRadialOffset.normalized : radialDirection;
            score = float.MinValue;
            if (supportRadialOffset.sqrMagnitude < 0.01f)
                return false;

            float lateralDist = Mathf.Abs(Vector3.Dot(supportOffset, rightAxis));
            float heightAboveCom = Vector3.Dot(supportOffset, cachedUpAxis);
            float radialDist = supportRadialOffset.magnitude;
            float forwardOffset = Mathf.Abs(Vector3.Dot(point - bounds.center, forwardAxis));
            score = EvaluateSupportPointScore(role, lateralDist, radialDist, forwardOffset, heightAboveCom);
            return radialDist >= 0.20f;
        }

        private static bool TryGetBoundsFrontierPoint(
            Part part,
            Vector3 centerOfMass,
            Vector3 forwardAxis,
            Vector3 rightAxis,
            float sideSign,
            out Vector3 point,
            out float lateralDist,
            out float heightAboveCom,
            out float radialDist)
        {
            point = Vector3.zero;
            lateralDist = 0f;
            heightAboveCom = 0f;
            radialDist = 0f;

            Bounds bounds;
            if (!TryGetPartBounds(part, out bounds))
                return false;

            Vector3 direction = rightAxis * (sideSign < 0f ? -1f : 1f);
            point = bounds.center + direction * GetBoundsReach(bounds, direction);
            Vector3 offset = point - centerOfMass;
            lateralDist = Mathf.Abs(Vector3.Dot(offset, rightAxis));
            heightAboveCom = Vector3.Dot(offset, cachedUpAxis);
            radialDist = Vector3.ProjectOnPlane(offset, forwardAxis).magnitude;
            return radialDist >= 0.20f;
        }

        private static float GetBoundsReach(Bounds bounds, Vector3 axis)
        {
            return KerbalFxUtil.ProjectBoundsExtent(bounds, axis);
        }

        private static bool IsIgnoredFxRenderer(Renderer renderer)
        {
            if (renderer == null)
                return false;

            if (IsWaterfallTransform(renderer.transform))
                return true;

            Material material = renderer.sharedMaterial;
            if (material == null)
                return false;

            Shader shader = material.shader;
            return ContainsWaterfallToken(material.name)
                || (shader != null && ContainsWaterfallToken(shader.name));
        }

        private static bool IsIgnoredFxMeshFilter(MeshFilter meshFilter)
        {
            if (meshFilter == null)
                return false;

            if (IsWaterfallTransform(meshFilter.transform))
                return true;

            Renderer renderer = meshFilter.GetComponent<Renderer>();
            if (renderer != null && IsIgnoredFxRenderer(renderer))
                return true;

            Mesh mesh = meshFilter.sharedMesh;
            return mesh != null && ContainsWaterfallToken(mesh.name);
        }

        private static bool IsWaterfallTransform(Transform transform)
        {
            int depth = 0;
            while (transform != null && depth < 12)
            {
                if (ContainsWaterfallToken(transform.name))
                    return true;
                transform = transform.parent;
                depth++;
            }

            return false;
        }

        private static bool ContainsWaterfallToken(string value)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf("waterfall", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsBetterSupportPoint(
            WingtipAnchorRole role,
            float lateralDist,
            float heightAboveCom,
            float radialDist,
            float forwardOffset,
            float candidateScore,
            float bestLateralDist,
            float bestHeightAboveCom,
            float bestRadialDist,
            float bestForwardOffset,
            float bestScore)
        {
            if (bestScore == float.MinValue)
                return true;

            if (role == WingtipAnchorRole.MainWing
                || role == WingtipAnchorRole.Control
                || role == WingtipAnchorRole.Canard)
            {
                float heightLoss = bestHeightAboveCom - heightAboveCom;
                float maxHeightLoss = role == WingtipAnchorRole.Control
                    ? ControlSurfaceSupportMaxHeightLoss
                    : MainSurfaceSupportMaxHeightLoss;

                if (lateralDist > bestLateralDist + MainSurfaceLateralTieEpsilon)
                {
                    if (heightLoss > maxHeightLoss)
                        return false;
                    return true;
                }
                if (lateralDist < bestLateralDist - MainSurfaceLateralTieEpsilon)
                    return false;

                if (heightAboveCom > bestHeightAboveCom + SupportPointHeightTieEpsilon)
                    return true;
                if (heightAboveCom < bestHeightAboveCom - SupportPointHeightTieEpsilon)
                    return false;

                if (radialDist > bestRadialDist + SupportPointRadialTieEpsilon)
                    return true;
                if (radialDist < bestRadialDist - SupportPointRadialTieEpsilon)
                    return false;

                if (forwardOffset < bestForwardOffset - SupportPointForwardTieEpsilon)
                    return true;
                if (forwardOffset > bestForwardOffset + SupportPointForwardTieEpsilon)
                    return false;
            }

            return candidateScore > bestScore;
        }

        private static bool TryGetCenteredFrontierPoint(
            Part part,
            Vector3 centerOfMass,
            Vector3 forwardAxis,
            Vector3 rightAxis,
            float sideSign,
            out Vector3 point,
            out float lateralDist,
            out float heightAboveCom,
            out float radialDist)
        {
            point = Vector3.zero;
            lateralDist = 0f;
            heightAboveCom = 0f;
            radialDist = 0f;

            if (useFastAnchorScan)
                return TryGetBoundsFrontierPoint(
                    part,
                    centerOfMass,
                    forwardAxis,
                    rightAxis,
                    sideSign,
                    out point,
                    out lateralDist,
                    out heightAboveCom,
                    out radialDist);

            var meshFilters = part.FindModelComponents<MeshFilter>();
            if (meshFilters == null)
                return false;

            float bestLateral = float.MinValue;
            bool found = false;
            Vector3 highPoint = Vector3.zero;
            Vector3 lowPoint = Vector3.zero;
            float highest = float.MinValue;
            float lowest = float.MaxValue;

            for (int i = 0; i < meshFilters.Count; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null || meshFilter.transform == null || IsIgnoredFxMeshFilter(meshFilter))
                    continue;

                Vector3[] vertices = meshFilter.sharedMesh.vertices;
                if (vertices == null || vertices.Length == 0)
                    continue;

                Transform meshTransform = meshFilter.transform;
                for (int j = 0; j < vertices.Length; j++)
                {
                    Vector3 worldPoint = meshTransform.TransformPoint(vertices[j]);
                    float signedLateral = Vector3.Dot(worldPoint - centerOfMass, rightAxis);
                    if (sideSign < 0f && signedLateral >= 0f)
                        continue;
                    if (sideSign > 0f && signedLateral <= 0f)
                        continue;

                    float absLateral = Mathf.Abs(signedLateral);
                    if (absLateral > bestLateral + MainSurfaceLateralTieEpsilon)
                    {
                        bestLateral = absLateral;
                        float height = Vector3.Dot(worldPoint - centerOfMass, cachedUpAxis);
                        highest = height;
                        lowest = height;
                        highPoint = worldPoint;
                        lowPoint = worldPoint;
                        found = true;
                        continue;
                    }

                    if (absLateral < bestLateral - MainSurfaceLateralTieEpsilon)
                        continue;

                    float pointHeight = Vector3.Dot(worldPoint - centerOfMass, cachedUpAxis);
                    if (!found)
                    {
                        bestLateral = absLateral;
                        highest = pointHeight;
                        lowest = pointHeight;
                        highPoint = worldPoint;
                        lowPoint = worldPoint;
                        found = true;
                        continue;
                    }

                    if (pointHeight > highest)
                    {
                        highest = pointHeight;
                        highPoint = worldPoint;
                    }

                    if (pointHeight < lowest)
                    {
                        lowest = pointHeight;
                        lowPoint = worldPoint;
                    }
                }
            }

            if (!found)
                return false;

            point = (highPoint + lowPoint) * 0.5f;
            lateralDist = Mathf.Abs(Vector3.Dot(point - centerOfMass, rightAxis));
            heightAboveCom = Vector3.Dot(point - centerOfMass, cachedUpAxis);
            radialDist = Vector3.ProjectOnPlane(point - centerOfMass, forwardAxis).magnitude;
            return radialDist >= 0.20f;
        }

        private static void RecomputeAsTopPoint(ref Candidate candidate)
        {
            if (candidate.Part == null)
                return;

            Bounds bounds;
            if (!TryGetPartBounds(candidate.Part, out bounds))
                return;

            Vector3 bestPoint = bounds.center + cachedUpAxis * GetBoundsReach(bounds, cachedUpAxis);
            if (useFastAnchorScan)
            {
                candidate.Point = bestPoint;
                Vector3 fastRadialOffset = Vector3.ProjectOnPlane(bestPoint - cachedCenterOfMass, cachedForwardAxis);
                candidate.Outward = fastRadialOffset.sqrMagnitude > 0.01f ? fastRadialOffset.normalized : cachedUpAxis;
                return;
            }

            float bestDot = float.MinValue;

            var meshFilters = candidate.Part.FindModelComponents<MeshFilter>();
            if (meshFilters != null)
            {
                for (int i = 0; i < meshFilters.Count; i++)
                {
                    MeshFilter mf = meshFilters[i];
                    if (mf == null || mf.sharedMesh == null || mf.transform == null || IsIgnoredFxMeshFilter(mf))
                        continue;

                    Vector3[] vertices = mf.sharedMesh.vertices;
                    if (vertices == null || vertices.Length == 0)
                        continue;

                    Transform t = mf.transform;
                    for (int j = 0; j < vertices.Length; j++)
                    {
                        Vector3 wp = t.TransformPoint(vertices[j]);
                        float dot = Vector3.Dot(wp - cachedCenterOfMass, cachedUpAxis);
                        if (dot > bestDot)
                        {
                            bestDot = dot;
                            bestPoint = wp;
                        }
                    }
                }
            }

            candidate.Point = bestPoint;
            Vector3 radialOffset = Vector3.ProjectOnPlane(bestPoint - cachedCenterOfMass, cachedForwardAxis);
            if (radialOffset.sqrMagnitude > 0.01f)
                candidate.Outward = radialOffset.normalized;
            else
                candidate.Outward = cachedUpAxis;
        }

        private static void RecomputeAsRadialOuterPoint(ref Candidate candidate)
        {
            if (candidate.Part == null)
                return;

            if (useFastAnchorScan)
            {
                Bounds bounds;
                if (!TryGetPartBounds(candidate.Part, out bounds))
                    return;

                Vector3 direction;
                if (!TryGetRadialDirection(bounds.center, out direction))
                    return;

                Vector3 fastPoint = bounds.center + direction * GetBoundsReach(bounds, direction);
                Vector3 fastOffset = fastPoint - cachedCenterOfMass;
                Vector3 fastRadialOffset = Vector3.ProjectOnPlane(fastOffset, cachedForwardAxis);
                if (fastRadialOffset.sqrMagnitude < 0.01f)
                    return;

                candidate.Point = fastPoint;
                candidate.Outward = fastRadialOffset.normalized;
                candidate.RadialDistance = fastRadialOffset.magnitude;
                candidate.LateralDistance = Mathf.Abs(Vector3.Dot(fastOffset, cachedRightAxis));
                candidate.ForwardOffset = Vector3.Dot(fastOffset, cachedForwardAxis);
                return;
            }

            Vector3 bestPoint = candidate.Point;
            float bestRadial = candidate.RadialDistance;
            float bestLateral = candidate.LateralDistance;
            bool found = false;

            var meshFilters = candidate.Part.FindModelComponents<MeshFilter>();
            if (meshFilters != null)
            {
                for (int i = 0; i < meshFilters.Count; i++)
                {
                    MeshFilter meshFilter = meshFilters[i];
                    if (meshFilter == null || meshFilter.sharedMesh == null || meshFilter.transform == null || IsIgnoredFxMeshFilter(meshFilter))
                        continue;

                    Vector3[] vertices = meshFilter.sharedMesh.vertices;
                    if (vertices == null || vertices.Length == 0)
                        continue;

                    Transform meshTransform = meshFilter.transform;
                    for (int j = 0; j < vertices.Length; j++)
                    {
                        Vector3 worldPoint = meshTransform.TransformPoint(vertices[j]);
                        Vector3 offset = worldPoint - cachedCenterOfMass;
                        Vector3 radialOffset = Vector3.ProjectOnPlane(offset, cachedForwardAxis);
                        float radialDist = radialOffset.magnitude;
                        float lateralDist = Mathf.Abs(Vector3.Dot(offset, cachedRightAxis));
                        if (radialDist < bestRadial - SupportPointRadialTieEpsilon)
                            continue;
                        if (radialDist < bestRadial + SupportPointRadialTieEpsilon
                            && lateralDist <= bestLateral + PrimaryOuterLateralEpsilon)
                            continue;

                        bestRadial = radialDist;
                        bestLateral = lateralDist;
                        bestPoint = worldPoint;
                        found = true;
                    }
                }
            }

            if (!found)
                return;

            Vector3 finalOffset = bestPoint - cachedCenterOfMass;
            Vector3 finalRadialOffset = Vector3.ProjectOnPlane(finalOffset, cachedForwardAxis);
            if (finalRadialOffset.sqrMagnitude < 0.01f)
                return;

            candidate.Point = bestPoint;
            candidate.Outward = finalRadialOffset.normalized;
            candidate.RadialDistance = finalRadialOffset.magnitude;
            candidate.LateralDistance = Mathf.Abs(Vector3.Dot(finalOffset, cachedRightAxis));
            candidate.ForwardOffset = Vector3.Dot(finalOffset, cachedForwardAxis);
        }

        private static void RecomputeAftMainWingAsTopPoint(ref Candidate candidate)
        {
            if (candidate.Role == WingtipAnchorRole.MainWing
                && candidate.ForwardOffset < AircraftMainWingForwardLimit)
            {
                RecomputeAsTopPoint(ref candidate);
            }
        }

        private static void ExtendPrimaryTipsToVisualExtremity(
            List<Part> parts,
            Vector3 centerOfMass,
            Vector3 forwardAxis,
            Vector3 rightAxis)
        {
            int leftIdx = -1, rightIdx = -1;
            for (int i = 0; i < selectedCandidates.Count; i++)
            {
                if (selectedCandidates[i].Role != WingtipAnchorRole.MainWing)
                    continue;
                if (selectedCandidates[i].SideSign < 0f && leftIdx < 0)
                    leftIdx = i;
                else if (selectedCandidates[i].SideSign > 0f && rightIdx < 0)
                    rightIdx = i;
            }
            if (leftIdx < 0 && rightIdx < 0)
                return;

            float bestLeftLat = leftIdx >= 0 ? selectedCandidates[leftIdx].LateralDistance : 0f;
            float bestRightLat = rightIdx >= 0 ? selectedCandidates[rightIdx].LateralDistance : 0f;
            Vector3 bestLeftPt = leftIdx >= 0 ? selectedCandidates[leftIdx].Point : Vector3.zero;
            Vector3 bestRightPt = rightIdx >= 0 ? selectedCandidates[rightIdx].Point : Vector3.zero;
            Part bestLeftTrackingPart = leftIdx >= 0 ? selectedCandidates[leftIdx].TrackingPart : null;
            Part bestRightTrackingPart = rightIdx >= 0 ? selectedCandidates[rightIdx].TrackingPart : null;
            float bestLeftHeight = leftIdx >= 0 ? Vector3.Dot(bestLeftPt - centerOfMass, cachedUpAxis) : float.MinValue;
            float bestRightHeight = rightIdx >= 0 ? Vector3.Dot(bestRightPt - centerOfMass, cachedUpAxis) : float.MinValue;
            float bestLeftRadial = leftIdx >= 0
                ? Vector3.ProjectOnPlane(bestLeftPt - centerOfMass, forwardAxis).magnitude
                : float.MinValue;
            float bestRightRadial = rightIdx >= 0
                ? Vector3.ProjectOnPlane(bestRightPt - centerOfMass, forwardAxis).magnitude
                : float.MinValue;
            bool leftExt = false, rightExt = false;

            for (int p = 0; p < parts.Count; p++)
            {
                Part part = parts[p];
                if (part == null)
                    continue;

                WingtipAnchorRole sourceRole;
                if (!TryGetAnchorRole(part, out sourceRole))
                    sourceRole = WingtipAnchorRole.None;

                Bounds bounds;
                if (!TryGetPartBounds(part, out bounds))
                    continue;

                float bCenterLat = Vector3.Dot(bounds.center - centerOfMass, rightAxis);
                float bReach = bounds.extents.x * Mathf.Abs(rightAxis.x)
                    + bounds.extents.y * Mathf.Abs(rightAxis.y)
                    + bounds.extents.z * Mathf.Abs(rightAxis.z);
                bool canLeft = leftIdx >= 0
                    && (-bCenterLat + bReach > bestLeftLat - MainSurfaceLateralTieEpsilon);
                bool canRight = rightIdx >= 0
                    && (bCenterLat + bReach > bestRightLat - MainSurfaceLateralTieEpsilon);
                if (!canLeft && !canRight)
                    continue;

                if (canLeft)
                {
                    Vector3 leftPoint;
                    float leftLateral;
                    float leftHeight;
                    float leftRadial;
                    if (TryGetCenteredFrontierPoint(
                        part,
                        centerOfMass,
                        forwardAxis,
                        rightAxis,
                        -1f,
                        out leftPoint,
                        out leftLateral,
                        out leftHeight,
                        out leftRadial)
                        && IsBetterExtendedPrimaryPoint(
                            sourceRole,
                            leftLateral,
                            leftHeight,
                            leftRadial,
                            bestLeftLat,
                            bestLeftHeight,
                            bestLeftRadial))
                    {
                        bestLeftLat = leftLateral;
                        bestLeftHeight = leftHeight;
                        bestLeftRadial = leftRadial;
                        bestLeftPt = leftPoint;
                        bestLeftTrackingPart = part;
                        leftExt = true;
                    }
                }

                if (canRight)
                {
                    Vector3 rightPoint;
                    float rightLateral;
                    float rightHeight;
                    float rightRadial;
                    if (TryGetCenteredFrontierPoint(
                        part,
                        centerOfMass,
                        forwardAxis,
                        rightAxis,
                        1f,
                        out rightPoint,
                        out rightLateral,
                        out rightHeight,
                        out rightRadial)
                        && IsBetterExtendedPrimaryPoint(
                            sourceRole,
                            rightLateral,
                            rightHeight,
                            rightRadial,
                            bestRightLat,
                            bestRightHeight,
                            bestRightRadial))
                    {
                        bestRightLat = rightLateral;
                        bestRightHeight = rightHeight;
                        bestRightRadial = rightRadial;
                        bestRightPt = rightPoint;
                        bestRightTrackingPart = part;
                        rightExt = true;
                    }
                }
            }

            if (leftExt)
            {
                Candidate c = selectedCandidates[leftIdx];
                c.Point = bestLeftPt;
                c.TrackingPart = bestLeftTrackingPart != null ? bestLeftTrackingPart : c.Part;
                Vector3 radial = Vector3.ProjectOnPlane(bestLeftPt - centerOfMass, forwardAxis);
                if (radial.sqrMagnitude > 0.01f)
                    c.Outward = radial.normalized;
                c.LateralDistance = bestLeftLat;
                selectedCandidates[leftIdx] = c;
            }
            if (rightExt)
            {
                Candidate c = selectedCandidates[rightIdx];
                c.Point = bestRightPt;
                c.TrackingPart = bestRightTrackingPart != null ? bestRightTrackingPart : c.Part;
                Vector3 radial = Vector3.ProjectOnPlane(bestRightPt - centerOfMass, forwardAxis);
                if (radial.sqrMagnitude > 0.01f)
                    c.Outward = radial.normalized;
                c.LateralDistance = bestRightLat;
                selectedCandidates[rightIdx] = c;
            }
        }

        private static bool IsBetterExtendedPrimaryPoint(
            WingtipAnchorRole sourceRole,
            float lateralDist,
            float heightAboveCom,
            float radialDist,
            float bestLateralDist,
            float bestHeightAboveCom,
            float bestRadialDist)
        {
            float lateralGain = lateralDist - bestLateralDist;
            if (lateralGain < -PrimaryExtensionLateralGainEpsilon)
                return false;

            float heightLoss = bestHeightAboveCom - heightAboveCom;
            float maxHeightLoss = sourceRole == WingtipAnchorRole.Control
                ? PrimaryExtensionControlMaxHeightLoss
                : PrimaryExtensionMaxHeightLoss;
            if (heightLoss > maxHeightLoss)
                return false;

            if (lateralGain > PrimaryExtensionForcedLateralGain)
                return true;

            if (lateralGain > PrimaryExtensionLateralGainEpsilon)
            {
                return true;
            }

            if (heightAboveCom > bestHeightAboveCom + SupportPointHeightTieEpsilon)
                return true;
            if (heightAboveCom < bestHeightAboveCom - SupportPointHeightTieEpsilon)
                return false;

            return radialDist > bestRadialDist + SupportPointRadialTieEpsilon;
        }
    }
}
