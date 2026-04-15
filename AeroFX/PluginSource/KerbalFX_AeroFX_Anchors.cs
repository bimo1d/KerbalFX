using System.Collections.Generic;
using System.Globalization;
using System.Text;
using KSP.Localization;
using UnityEngine;

namespace KerbalFX.AeroFX
{
    internal enum WingtipAnchorRole
    {
        None = 0,
        MainWing,
        Tail,
        Control,
        Canard
    }

    internal struct WingtipRibbonAnchor
    {
        public Part Part;
        public Vector3 WorldPoint;
        public Vector3 Outward;
        public Vector3 LocalPoint;
        public Vector3 LocalOutward;
        public float Score;
        public float SideSign;
        public WingtipAnchorRole Role;

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

        public string RoleName
        {
            get
            {
                return WingtipAnchorRoleUtil.GetShortName(Role);
            }
        }
    }

    internal static class WingtipAnchorRoleUtil
    {
        public static string GetShortName(WingtipAnchorRole role)
        {
            switch (role)
            {
                case WingtipAnchorRole.MainWing:
                    return "main";
                case WingtipAnchorRole.Tail:
                    return "tail";
                case WingtipAnchorRole.Control:
                    return "control";
                case WingtipAnchorRole.Canard:
                    return "canard";
                default:
                    return "none";
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

            right = reference.right.sqrMagnitude > 0.0001f ? reference.right.normalized : Vector3.right;
            up = reference.up.sqrMagnitude > 0.0001f ? reference.up.normalized : Vector3.up;

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
            public float Score;
            public float WeightedScore;
            public float SideSign;
            public float LateralDistance;
            public float RadialDistance;
            public float ForwardOffset;
            public WingtipAnchorRole Role;
        }

        private static readonly string[] MainWingTokens =
        {
            "wing",
            "winglet"
        };

        private static readonly string[] ControlTokens =
        {
            "elevon",
            "aileron",
            "flap"
        };

        private static readonly string[] TailTokens =
        {
            "tail",
            "stabilizer",
            "stabilator",
            "fin",
            "rudder"
        };

        private static readonly string[] TailplaneTokens =
        {
            "tail",
            "stabilizer",
            "stabilator"
        };

        private static readonly string[] FinTokens =
        {
            "fin",
            "rudder"
        };

        private static readonly string[] CanardTokens =
        {
            "canard"
        };

        private static readonly string[] DenyTokens =
        {
            "fuselage",
            "cockpit",
            "cabin",
            "cargo",
            "bay",
            "adapter",
            "bicoupler",
            "dock",
            "lander",
            "dronecore"
        };

        private static readonly WingtipAnchorRole[] SecondaryRoleOrder =
        {
            WingtipAnchorRole.Canard,
            WingtipAnchorRole.Tail,
            WingtipAnchorRole.MainWing,
            WingtipAnchorRole.Control
        };
        private const float PrimaryOuterLateralEpsilon = 0.01f;
        private const float CenterLateralThreshold = 0.45f;
        private const float MainSurfaceLateralTieEpsilon = 0.05f;
        private const float PrimaryExtensionLateralGainEpsilon = 0.01f;
        private const float PrimaryExtensionForcedLateralGain = 0.08f;
        private const float PrimaryExtensionMaxHeightLoss = 0.22f;
        private const float PrimaryExtensionControlMaxHeightLoss = 0.70f;
        private const float MainSurfaceSupportMaxHeightLoss = 0.35f;
        private const float ControlSurfaceSupportMaxHeightLoss = 0.70f;
        private const float SupportPointHeightTieEpsilon = 0.01f;
        private const float SupportPointRadialTieEpsilon = 0.01f;
        private const float SupportPointForwardTieEpsilon = 0.01f;
        private const float CenterMinTopHeight = 0.30f;

        private static Vector3 cachedUpAxis;
        private static Vector3 cachedForwardAxis;
        private static Vector3 cachedCenterOfMass;

        private static readonly List<Candidate> allCandidates = new List<Candidate>(32);
        private static readonly List<Candidate> selectedCandidates = new List<Candidate>(8);
        private static readonly Dictionary<string, WingtipAnchorRole> roleByPartName =
            new Dictionary<string, WingtipAnchorRole>(64, System.StringComparer.OrdinalIgnoreCase);
        private static readonly StringBuilder candidateSummaryBuilder = new StringBuilder(256);

        private sealed class WeightedScoreDescending : IComparer<Candidate>
        {
            public int Compare(Candidate a, Candidate b)
            {
                int weightedCompare = b.WeightedScore.CompareTo(a.WeightedScore);
                if (weightedCompare != 0)
                    return weightedCompare;
                return b.Score.CompareTo(a.Score);
            }
        }

        private static readonly WeightedScoreDescending weightedScoreComparer = new WeightedScoreDescending();

        public static int TryResolveAll(
            Vessel vessel,
            WingtipRibbonAnchor[] results,
            int maxResults,
            out int liftPartCount,
            out int candidateCount,
            out string candidateSummary)
        {
            liftPartCount = 0;
            candidateCount = 0;
            candidateSummary = "none";

            if (vessel == null || vessel.parts == null || vessel.rootPart == null || results == null || results.Length == 0)
                return 0;

            Transform reference = vessel.ReferenceTransform;
            if (reference == null && vessel.rootPart != null)
                reference = vessel.rootPart.transform;
            if (reference == null)
                return 0;

            Vector3 forward = reference.up.sqrMagnitude > 0.0001f
                ? reference.up.normalized
                : Vector3.forward;
            Vector3 rightAxis = reference.right.sqrMagnitude > 0.0001f
                ? reference.right.normalized
                : Vector3.right;
            Vector3 upAxis = Vector3.Cross(forward, rightAxis).normalized;
            cachedUpAxis = upAxis;
            cachedForwardAxis = forward;
            cachedCenterOfMass = vessel.CoM;

            int resultLimit = Mathf.Clamp(maxResults, 1, results.Length);
            Vector3 centerOfMass = vessel.CoM;
            allCandidates.Clear();
            selectedCandidates.Clear();

            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part part = vessel.parts[i];
                WingtipAnchorRole role;
                if (part == null || !TryGetAnchorRole(part, out role))
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
                if (radialOffset.magnitude < 0.20f)
                    continue;

                Vector3 supportPoint;
                Vector3 outward;
                float score;
                if (!TryGetPartTipPoint(
                    part,
                    bounds,
                    centerOfMass,
                    forward,
                    rightAxis,
                    role,
                    out supportPoint,
                    out outward,
                    out score))
                {
                    continue;
                }

                Vector3 supportOffset = supportPoint - centerOfMass;
                Vector3 supportRadialOffset = Vector3.ProjectOnPlane(supportOffset, forward);
                float lateral = Vector3.Dot(outward, rightAxis);
                Candidate candidate = new Candidate
                {
                    Part = part,
                    Point = supportPoint,
                    Outward = outward,
                    Score = score,
                    WeightedScore = score + GetRoleScoreBias(role),
                    SideSign = lateral >= 0f ? 1f : -1f,
                    LateralDistance = Mathf.Abs(Vector3.Dot(supportOffset, rightAxis)),
                    RadialDistance = supportRadialOffset.magnitude,
                    ForwardOffset = Vector3.Dot(offset, forward),
                    Role = role
                };

                allCandidates.Add(candidate);
                candidateCount++;
            }

            if (allCandidates.Count == 0)
                return 0;

            allCandidates.Sort(weightedScoreComparer);
            candidateSummary = BuildCandidateSummary();

            SelectPrimaryOuterPair(resultLimit);
            SelectCenterCandidate(resultLimit);
            SelectSecondaryPriorityPairs(resultLimit);
            FillRemainingCandidates(resultLimit);
            EnforcePairSymmetry();
            ExtendPrimaryTipsToVisualExtremity(vessel.parts, centerOfMass, forward, rightAxis);

            int count = Mathf.Min(selectedCandidates.Count, resultLimit);
            for (int i = 0; i < count; i++)
            {
                Candidate c = selectedCandidates[i];
                WingtipRibbonAnchor anchor;
                anchor.Part = c.Part;
                anchor.WorldPoint = c.Point;
                anchor.Outward = c.Outward;
                anchor.LocalPoint = c.Part.transform.InverseTransformPoint(c.Point);
                anchor.LocalOutward = c.Part.transform.InverseTransformDirection(c.Outward);
                anchor.Score = c.WeightedScore;
                anchor.SideSign = c.SideSign;
                anchor.Role = c.Role;
                results[i] = anchor;
            }

            for (int i = count; i < results.Length; i++)
            {
                results[i] = default(WingtipRibbonAnchor);
            }

            return count;
        }

        private static void SelectPrimaryOuterPair(int resultLimit)
        {
            if (resultLimit <= 0)
            {
                return;
            }

            SelectPrimarySideCandidate(-1f, resultLimit);

            if (selectedCandidates.Count >= resultLimit)
            {
                return;
            }

            SelectPrimarySideCandidate(1f, resultLimit);
        }

        private static void SelectPrimarySideCandidate(float sideSign, int resultLimit)
        {
            if (selectedCandidates.Count >= resultLimit)
            {
                return;
            }

            Candidate candidate;
            if (TryGetBestPrimaryCandidate(sideSign, out candidate))
            {
                AddSelectedCandidate(candidate);
            }
        }

        private static bool TryGetBestPrimaryCandidate(float sideSign, out Candidate candidate)
        {
            candidate = default(Candidate);
            float bestLateral = float.MinValue;

            for (int i = 0; i < allCandidates.Count; i++)
            {
                Candidate current = allCandidates[i];
                if (current.SideSign != sideSign || IsSelected(current.Part))
                    continue;
                if (current.LateralDistance < CenterLateralThreshold)
                    continue;
                if (current.Role == WingtipAnchorRole.Control)
                    continue;

                if (current.LateralDistance > bestLateral)
                {
                    bestLateral = current.LateralDistance;
                    candidate = current;
                }
            }

            if (candidate.Part != null)
                return true;

            bestLateral = float.MinValue;
            for (int i = 0; i < allCandidates.Count; i++)
            {
                Candidate current = allCandidates[i];
                if (current.SideSign != sideSign || IsSelected(current.Part))
                    continue;
                if (current.LateralDistance < CenterLateralThreshold)
                    continue;

                if (current.LateralDistance > bestLateral)
                {
                    bestLateral = current.LateralDistance;
                    candidate = current;
                }
            }

            return candidate.Part != null;
        }

        private static void SelectCenterCandidate(int resultLimit)
        {
            if (selectedCandidates.Count >= resultLimit)
                return;

            Candidate center;
            if (TryGetValidCenterCandidate(WingtipAnchorRole.Tail, out center)
                || TryGetValidCenterCandidate(WingtipAnchorRole.MainWing, out center)
                || TryGetValidCenterCandidate(WingtipAnchorRole.Control, out center)
                || TryGetValidCenterCandidate(WingtipAnchorRole.Canard, out center))
            {
                AddSelectedCandidate(center);
                if (AeroFxConfig.DebugLogging)
                    AeroFxLog.DebugLog(Localizer.Format(AeroFxLoc.LogCenterResult,
                        WingtipAnchorRoleUtil.GetShortName(center.Role),
                        center.Part != null && center.Part.partInfo != null ? center.Part.partInfo.name : "?",
                        center.LateralDistance.ToString("F2", CultureInfo.InvariantCulture),
                        center.RadialDistance.ToString("F2", CultureInfo.InvariantCulture),
                        center.ForwardOffset.ToString("F2", CultureInfo.InvariantCulture)));
            }
            else if (AeroFxConfig.DebugLogging)
            {
                AeroFxLog.DebugLog(Localizer.Format(AeroFxLoc.LogCenterNone));
            }
        }

        private static bool TryGetValidCenterCandidate(WingtipAnchorRole role, out Candidate result)
        {
            result = default(Candidate);
            Candidate center;
            if (!TryGetBestCenterCandidate(role, out center))
                return false;

            RecomputeAsTopPoint(ref center);
            float topHeight = Vector3.Dot(center.Point - cachedCenterOfMass, cachedUpAxis);
            if (topHeight < CenterMinTopHeight)
                return false;

            result = center;
            return true;
        }

        private static bool TryGetBestCenterCandidate(WingtipAnchorRole role, out Candidate candidate)
        {
            candidate = default(Candidate);
            float bestScore = float.MinValue;

            for (int i = 0; i < allCandidates.Count; i++)
            {
                Candidate c = allCandidates[i];
                if (c.Role != role || IsSelected(c.Part))
                    continue;
                if (c.LateralDistance > CenterLateralThreshold)
                    continue;

                float centeredness = 1f - c.LateralDistance / CenterLateralThreshold;
                float aft01 = Mathf.InverseLerp(0f, 3f, -c.ForwardOffset);
                float score = centeredness * 4.0f + aft01 * 2.5f;
                if (score > bestScore)
                {
                    bestScore = score;
                    candidate = c;
                }
            }

            return candidate.Part != null;
        }

        private static void RecomputeAsTopPoint(ref Candidate candidate)
        {
            if (candidate.Part == null)
                return;

            Bounds bounds;
            if (!TryGetPartBounds(candidate.Part, out bounds))
                return;

            Vector3 bestPoint = bounds.center + cachedUpAxis * bounds.extents.magnitude * 0.5f;
            float bestDot = float.MinValue;

            var meshFilters = candidate.Part.FindModelComponents<MeshFilter>();
            if (meshFilters != null)
            {
                for (int i = 0; i < meshFilters.Count; i++)
                {
                    MeshFilter mf = meshFilters[i];
                    if (mf == null || mf.sharedMesh == null || mf.transform == null)
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

        private static void SelectSecondaryPriorityPairs(int resultLimit)
        {
            for (int i = 0; i < SecondaryRoleOrder.Length && selectedCandidates.Count < resultLimit; i++)
            {
                WingtipAnchorRole role = SecondaryRoleOrder[i];
                SelectSecondaryRoleCandidate(-1f, role, resultLimit);
                SelectSecondaryRoleCandidate(1f, role, resultLimit);
            }
        }

        private static void SelectSecondaryRoleCandidate(float sideSign, WingtipAnchorRole role, int resultLimit)
        {
            if (selectedCandidates.Count >= resultLimit)
            {
                return;
            }

            if (GetSelectedCountForSide(sideSign) >= 2)
            {
                return;
            }

            Candidate candidate;
            if (TryGetBestSecondaryRoleCandidate(sideSign, role, out candidate))
            {
                AddSelectedCandidate(candidate);
                if (AeroFxConfig.DebugLogging)
                    AeroFxLog.DebugLog(Localizer.Format(AeroFxLoc.LogSecondaryResult,
                        sideSign < 0 ? "L" : "R",
                        WingtipAnchorRoleUtil.GetShortName(role),
                        WingtipAnchorRoleUtil.GetShortName(candidate.Role),
                        candidate.Part != null && candidate.Part.partInfo != null ? candidate.Part.partInfo.name : "?",
                        candidate.LateralDistance.ToString("F2", CultureInfo.InvariantCulture),
                        candidate.RadialDistance.ToString("F2", CultureInfo.InvariantCulture),
                        candidate.ForwardOffset.ToString("F2", CultureInfo.InvariantCulture)));
            }
            else if (AeroFxConfig.DebugLogging)
            {
                AeroFxLog.DebugLog(Localizer.Format(AeroFxLoc.LogSecondaryNone,
                    sideSign < 0 ? "L" : "R",
                    WingtipAnchorRoleUtil.GetShortName(role)));
            }
        }

        private static void FillRemainingCandidates(int resultLimit)
        {
            while (selectedCandidates.Count < resultLimit)
            {
                float preferredSide = GetPreferredSide();
                Candidate candidate;
                if (!TryGetBestOrderedFallbackCandidate(preferredSide, out candidate)
                    && !TryGetBestOrderedFallbackCandidate(-preferredSide, out candidate)
                    && !TryGetBestAnyUnusedCandidate(out candidate))
                {
                    break;
                }

                AddSelectedCandidate(candidate);
            }
        }

        private static void EnforcePairSymmetry()
        {
            int leftCount = 0;
            int rightCount = 0;
            for (int i = 0; i < selectedCandidates.Count; i++)
            {
                if (selectedCandidates[i].LateralDistance < CenterLateralThreshold)
                    continue;
                if (selectedCandidates[i].SideSign < 0f)
                    leftCount++;
                else
                    rightCount++;
            }

            while (leftCount != rightCount && selectedCandidates.Count > 0)
            {
                float removeSide = leftCount > rightCount ? -1f : 1f;
                for (int i = selectedCandidates.Count - 1; i >= 0; i--)
                {
                    if (selectedCandidates[i].LateralDistance < CenterLateralThreshold)
                        continue;
                    if ((removeSide < 0f && selectedCandidates[i].SideSign < 0f)
                        || (removeSide >= 0f && selectedCandidates[i].SideSign >= 0f))
                    {
                        selectedCandidates.RemoveAt(i);
                        if (removeSide < 0f) leftCount--;
                        else rightCount--;
                        break;
                    }
                }
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
                        rightExt = true;
                    }
                }
            }

            if (leftExt)
            {
                Candidate c = selectedCandidates[leftIdx];
                c.Point = bestLeftPt;
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

        private static float GetPreferredSide()
        {
            int leftCount = 0;
            int rightCount = 0;
            for (int i = 0; i < selectedCandidates.Count; i++)
            {
                if (selectedCandidates[i].SideSign < 0f)
                    leftCount++;
                else
                    rightCount++;
            }

            return leftCount <= rightCount ? -1f : 1f;
        }

        private static int GetSelectedCountForSide(float sideSign)
        {
            int count = 0;
            for (int i = 0; i < selectedCandidates.Count; i++)
            {
                if (selectedCandidates[i].SideSign == sideSign)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool TryGetBestOuterCandidateForRole(float sideSign, WingtipAnchorRole role, out Candidate candidate)
        {
            float bestScore = float.MinValue;
            candidate = default(Candidate);

            for (int i = 0; i < allCandidates.Count; i++)
            {
                Candidate current = allCandidates[i];
                if (current.SideSign != sideSign || current.Role != role || IsSelected(current.Part))
                    continue;

                float outerScore = current.WeightedScore
                    + current.RadialDistance * 0.55f
                    + GetOuterRoleBias(current.Role);
                if (outerScore <= bestScore)
                    continue;

                bestScore = outerScore;
                candidate = current;
            }

            return candidate.Part != null;
        }

        private static bool TryGetSelectedOutermostCandidate(float sideSign, out Candidate selected)
        {
            selected = default(Candidate);
            bool found = false;
            for (int i = 0; i < selectedCandidates.Count; i++)
            {
                Candidate candidate = selectedCandidates[i];
                if (candidate.SideSign != sideSign)
                    continue;

                if (!found || candidate.RadialDistance > selected.RadialDistance)
                {
                    selected = candidate;
                    found = true;
                }
            }

            return found;
        }

        private static bool TryGetBestSecondaryRoleCandidate(float sideSign, WingtipAnchorRole role, out Candidate candidate)
        {
            candidate = default(Candidate);
            Candidate outerCandidate;
            if (!TryGetSelectedOutermostCandidate(sideSign, out outerCandidate))
            {
                return false;
            }

            switch (role)
            {
                case WingtipAnchorRole.Tail:
                    return TryGetBestPreferredTailCandidate(sideSign, outerCandidate, out candidate)
                        || TryGetBestTailCandidate(sideSign, outerCandidate, out candidate);
                case WingtipAnchorRole.Canard:
                    return TryGetBestCanardCandidate(sideSign, outerCandidate.RadialDistance, out candidate);
                case WingtipAnchorRole.MainWing:
                case WingtipAnchorRole.Control:
                    return TryGetBestAftFallbackCandidate(sideSign, role, outerCandidate.RadialDistance, out candidate);
                default:
                    return false;
            }
        }

        private static bool TryGetBestPreferredTailCandidate(float sideSign, Candidate outerCandidate, out Candidate candidate)
        {
            float bestScore = float.MinValue;
            candidate = default(Candidate);

            for (int i = 0; i < allCandidates.Count; i++)
            {
                Candidate current = allCandidates[i];
                if (current.SideSign != sideSign || IsSelected(current.Part))
                    continue;
                if (current.LateralDistance < CenterLateralThreshold)
                    continue;
                if (!IsPreferredTailCandidate(current, outerCandidate))
                    continue;

                float preferredScore = EvaluatePreferredTailScore(current, outerCandidate);
                if (preferredScore <= bestScore)
                    continue;

                bestScore = preferredScore;
                candidate = current;
            }

            return candidate.Part != null;
        }

        private static bool TryGetBestTailCandidate(float sideSign, Candidate outerCandidate, out Candidate candidate)
        {
            float bestScore = float.MinValue;
            candidate = default(Candidate);

            for (int i = 0; i < allCandidates.Count; i++)
            {
                Candidate current = allCandidates[i];
                if (current.Role == WingtipAnchorRole.Tail && AeroFxConfig.DebugLogging)
                {
                    AeroFxLog.DebugLog(Localizer.Format(AeroFxLoc.LogTailScan,
                        current.Part != null && current.Part.partInfo != null ? current.Part.partInfo.name : "?",
                        current.SideSign < 0 ? "L" : "R",
                        sideSign < 0 ? "L" : "R",
                        current.LateralDistance.ToString("F2", CultureInfo.InvariantCulture),
                        IsSelected(current.Part)));
                }
                if (current.SideSign != sideSign || current.Role != WingtipAnchorRole.Tail || IsSelected(current.Part))
                    continue;
                if (current.LateralDistance < CenterLateralThreshold)
                    continue;

                float tailScore = EvaluateTailRoleScore(current, outerCandidate);
                if (tailScore <= bestScore)
                    continue;

                bestScore = tailScore;
                candidate = current;
            }

            return candidate.Part != null;
        }

        private static bool TryGetBestCanardCandidate(
            float sideSign,
            float outerRadial,
            out Candidate candidate)
        {
            float bestScore = float.MinValue;
            candidate = default(Candidate);

            for (int i = 0; i < allCandidates.Count; i++)
            {
                Candidate current = allCandidates[i];
                if (current.SideSign != sideSign || current.Role != WingtipAnchorRole.Canard || IsSelected(current.Part))
                    continue;
                if (current.LateralDistance < CenterLateralThreshold)
                    continue;

                float canardScore = EvaluateCanardRoleScore(current, outerRadial);
                if (canardScore <= bestScore)
                    continue;

                bestScore = canardScore;
                candidate = current;
            }

            return candidate.Part != null;
        }

        private static bool TryGetBestAftFallbackCandidate(
            float sideSign,
            WingtipAnchorRole role,
            float outerRadial,
            out Candidate candidate)
        {
            float bestScore = float.MinValue;
            candidate = default(Candidate);

            for (int i = 0; i < allCandidates.Count; i++)
            {
                Candidate current = allCandidates[i];
                if (current.SideSign != sideSign || current.Role != role || IsSelected(current.Part))
                    continue;
                if (current.LateralDistance < CenterLateralThreshold)
                    continue;

                float fallbackScore = EvaluateAftFallbackScore(current, outerRadial);
                if (fallbackScore <= bestScore)
                    continue;

                bestScore = fallbackScore;
                candidate = current;
            }

            return candidate.Part != null;
        }

        private static bool TryGetBestOrderedFallbackCandidate(float sideSign, out Candidate candidate)
        {
            for (int i = 0; i < SecondaryRoleOrder.Length; i++)
            {
                if (TryGetBestSecondaryRoleCandidate(sideSign, SecondaryRoleOrder[i], out candidate))
                {
                    return true;
                }
            }

            for (int i = 0; i < SecondaryRoleOrder.Length; i++)
            {
                if (TryGetBestOuterCandidateForRole(sideSign, SecondaryRoleOrder[i], out candidate))
                {
                    return true;
                }
            }

            candidate = default(Candidate);
            return false;
        }

        private static bool TryGetBestAnyUnusedCandidate(out Candidate candidate)
        {
            for (int i = 0; i < SecondaryRoleOrder.Length; i++)
            {
                if (TryGetBestAnyUnusedCandidateForRole(SecondaryRoleOrder[i], out candidate))
                {
                    return true;
                }
            }

            candidate = default(Candidate);
            return false;
        }

        private static bool TryGetBestAnyUnusedCandidateForRole(WingtipAnchorRole role, out Candidate candidate)
        {
            float bestScore = float.MinValue;
            candidate = default(Candidate);

            for (int i = 0; i < allCandidates.Count; i++)
            {
                Candidate current = allCandidates[i];
                if (current.Role != role || IsSelected(current.Part))
                    continue;
                if (current.LateralDistance < CenterLateralThreshold)
                    continue;

                float score = current.RadialDistance * 0.35f
                    - current.LateralDistance * 0.60f;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                candidate = current;
            }

            return candidate.Part != null;
        }

        private static void AddSelectedCandidate(Candidate candidate)
        {
            if (candidate.Part == null || IsSelected(candidate.Part))
                return;

            selectedCandidates.Add(candidate);
        }

        private static bool IsSelected(Part part)
        {
            if (part == null)
                return false;

            for (int i = 0; i < selectedCandidates.Count; i++)
            {
                if (selectedCandidates[i].Part == part)
                    return true;
            }

            return false;
        }

        private static string BuildCandidateSummary()
        {
            if (allCandidates.Count == 0)
                return "none";

            int count = Mathf.Min(allCandidates.Count, 8);
            candidateSummaryBuilder.Length = 0;
            for (int i = 0; i < count; i++)
            {
                Candidate candidate = allCandidates[i];
                if (candidateSummaryBuilder.Length > 0)
                    candidateSummaryBuilder.Append(" / ");

                candidateSummaryBuilder.Append(candidate.SideSign >= 0f ? "R" : "L");
                candidateSummaryBuilder.Append("-");
                candidateSummaryBuilder.Append(WingtipAnchorRoleUtil.GetShortName(candidate.Role));
                candidateSummaryBuilder.Append(":");
                candidateSummaryBuilder.Append(candidate.Part != null && candidate.Part.partInfo != null
                    ? candidate.Part.partInfo.name
                    : "unknown");
                candidateSummaryBuilder.Append("(");
                candidateSummaryBuilder.Append(candidate.WeightedScore.ToString("F2", CultureInfo.InvariantCulture));
                candidateSummaryBuilder.Append(",x=");
                candidateSummaryBuilder.Append(candidate.LateralDistance.ToString("F2", CultureInfo.InvariantCulture));
                candidateSummaryBuilder.Append(",r=");
                candidateSummaryBuilder.Append(candidate.RadialDistance.ToString("F2", CultureInfo.InvariantCulture));
                candidateSummaryBuilder.Append(",f=");
                candidateSummaryBuilder.Append(candidate.ForwardOffset.ToString("F2", CultureInfo.InvariantCulture));
                candidateSummaryBuilder.Append(")");
            }

            return candidateSummaryBuilder.ToString();
        }

        private static bool TryGetAnchorRole(Part part, out WingtipAnchorRole role)
        {
            role = WingtipAnchorRole.None;
            if (part == null)
                return false;

            string name = part.partInfo != null ? part.partInfo.name : part.name;
            if (string.IsNullOrEmpty(name))
                return false;

            if (KerbalFxUtil.ContainsAnyToken(name, DenyTokens))
                return false;

            bool hasControlSurface = part.FindModuleImplementing<ModuleControlSurface>() != null;
            bool hasLiftSurface = part.FindModuleImplementing<ModuleLiftingSurface>() != null
                || part.FindModuleImplementing<ModuleAeroSurface>() != null;
            if (!hasControlSurface && !hasLiftSurface)
                return false;

            return TryClassifyRoleFromName(name, out role);
        }

        private static bool TryClassifyRoleFromName(string name, out WingtipAnchorRole role)
        {
            role = WingtipAnchorRole.None;
            if (string.IsNullOrEmpty(name))
                return false;

            if (roleByPartName.TryGetValue(name, out role))
                return role != WingtipAnchorRole.None;

            if (KerbalFxUtil.ContainsAnyToken(name, DenyTokens))
            {
                roleByPartName[name] = WingtipAnchorRole.None;
                return false;
            }

            if (KerbalFxUtil.ContainsAnyToken(name, CanardTokens))
                role = WingtipAnchorRole.Canard;
            else if (KerbalFxUtil.ContainsAnyToken(name, TailTokens))
                role = WingtipAnchorRole.Tail;
            else if (KerbalFxUtil.ContainsAnyToken(name, ControlTokens))
                role = WingtipAnchorRole.Control;
            else if (KerbalFxUtil.ContainsAnyToken(name, MainWingTokens))
                role = WingtipAnchorRole.MainWing;

            roleByPartName[name] = role;
            return role != WingtipAnchorRole.None;
        }

        private static bool IsPreferredTailCandidate(Candidate candidate, Candidate outerCandidate)
        {
            if (candidate.Role != WingtipAnchorRole.Tail)
                return false;

            if (IsFinLikePart(candidate.Part))
                return false;

            if (candidate.ForwardOffset > outerCandidate.ForwardOffset - 0.05f)
                return false;

            if (candidate.LateralDistance > outerCandidate.LateralDistance + 0.05f)
                return false;

            return true;
        }

        private static bool IsTailplaneLikePart(Part part)
        {
            string name = GetPartLookupName(part);
            return !string.IsNullOrEmpty(name) && KerbalFxUtil.ContainsAnyToken(name, TailplaneTokens);
        }

        private static bool IsFinLikePart(Part part)
        {
            string name = GetPartLookupName(part);
            return !string.IsNullOrEmpty(name) && KerbalFxUtil.ContainsAnyToken(name, FinTokens);
        }

        private static string GetPartLookupName(Part part)
        {
            if (part == null)
                return null;

            return part.partInfo != null ? part.partInfo.name : part.name;
        }

        private static float GetRoleScoreBias(WingtipAnchorRole role)
        {
            switch (role)
            {
                case WingtipAnchorRole.MainWing:
                    return 0.70f;
                case WingtipAnchorRole.Tail:
                    return 1.25f;
                case WingtipAnchorRole.Control:
                    return 0.10f;
                case WingtipAnchorRole.Canard:
                    return -0.35f;
                default:
                    return 0f;
            }
        }

        private static float GetOuterRoleBias(WingtipAnchorRole role)
        {
            switch (role)
            {
                case WingtipAnchorRole.MainWing:
                    return 0.90f;
                case WingtipAnchorRole.Tail:
                    return 0.25f;
                case WingtipAnchorRole.Control:
                    return 0.10f;
                case WingtipAnchorRole.Canard:
                    return -0.20f;
                default:
                    return 0f;
            }
        }

        private static float EvaluateTailRoleScore(Candidate candidate, Candidate outerCandidate)
        {
            float aft01 = Mathf.InverseLerp(0.05f, 2.50f, -candidate.ForwardOffset);
            float center01 = 1f - Mathf.InverseLerp(0.20f, outerCandidate.LateralDistance, candidate.LateralDistance);
            float lateralPenalty = Mathf.InverseLerp(0.50f, outerCandidate.LateralDistance, candidate.LateralDistance);
            float roleTypeBias = IsFinLikePart(candidate.Part) ? -1.10f : (IsTailplaneLikePart(candidate.Part) ? 0.55f : 0f);
            return roleTypeBias
                + aft01 * 3.20f
                + center01 * 5.00f
                - lateralPenalty * 4.50f;
        }

        private static float EvaluatePreferredTailScore(Candidate candidate, Candidate outerCandidate)
        {
            float aftDelta = Mathf.Max(0f, outerCandidate.ForwardOffset - candidate.ForwardOffset);
            float aftDelta01 = Mathf.Clamp01(aftDelta / 0.90f);
            float center01 = 1f - Mathf.InverseLerp(0.20f, outerCandidate.LateralDistance, candidate.LateralDistance);
            float lateralPenalty = Mathf.InverseLerp(0.50f, outerCandidate.LateralDistance, candidate.LateralDistance);
            const float roleBias = 0.85f;
            return roleBias
                + aftDelta01 * 2.50f
                + center01 * 5.00f
                - lateralPenalty * 4.50f;
        }

        private static float EvaluateCanardRoleScore(Candidate candidate, float outerRadial)
        {
            float forward01 = Mathf.InverseLerp(0.05f, 2.50f, candidate.ForwardOffset);
            float center01 = 1f - Mathf.InverseLerp(0.20f, outerRadial, candidate.LateralDistance);
            float lateralPenalty = Mathf.InverseLerp(0.50f, outerRadial, candidate.LateralDistance);
            return forward01 * 2.60f
                + center01 * 4.00f
                - lateralPenalty * 3.50f;
        }

        private static float EvaluateAftFallbackScore(Candidate candidate, float outerRadial)
        {
            float aft01 = Mathf.InverseLerp(0.05f, 2.50f, -candidate.ForwardOffset);
            float center01 = 1f - Mathf.InverseLerp(0.20f, outerRadial, candidate.LateralDistance);
            float lateralPenalty = Mathf.InverseLerp(0.50f, outerRadial, candidate.LateralDistance);
            return aft01 * 3.00f
                + center01 * 5.00f
                - lateralPenalty * 4.50f;
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
            Vector3 rightAxis,
            WingtipAnchorRole role,
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
                    if (meshFilter == null || meshFilter.sharedMesh == null || meshFilter.transform == null)
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
                if (meshFilter == null || meshFilter.sharedMesh == null || meshFilter.transform == null)
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

        private static float EvaluateSupportPointScore(
            WingtipAnchorRole role,
            float lateralDist,
            float radialDist,
            float forwardOffset,
            float heightAboveCom)
        {
            switch (role)
            {
                case WingtipAnchorRole.MainWing:
                    return lateralDist * 3.00f + radialDist * 0.25f - forwardOffset * 0.05f;
                case WingtipAnchorRole.Control:
                case WingtipAnchorRole.Canard:
                    return radialDist * 2.20f + lateralDist * 0.35f - forwardOffset * 0.08f;
                case WingtipAnchorRole.Tail:
                    return heightAboveCom * 1.80f + radialDist * 1.00f + lateralDist * 0.20f - forwardOffset * 0.12f;
                default:
                    return radialDist * 1.95f - forwardOffset * 0.20f;
            }
        }

        private static float EvaluateSupportPointFallbackScore(
            WingtipAnchorRole role,
            float lateralDist,
            float radialDist)
        {
            switch (role)
            {
                case WingtipAnchorRole.MainWing:
                    return lateralDist * 2.50f + radialDist * 0.30f;
                case WingtipAnchorRole.Control:
                case WingtipAnchorRole.Canard:
                    return radialDist * 1.90f + lateralDist * 0.40f;
                case WingtipAnchorRole.Tail:
                    return radialDist * 1.55f + lateralDist * 0.25f;
                default:
                    return radialDist * 1.60f;
            }
        }
    }
}
