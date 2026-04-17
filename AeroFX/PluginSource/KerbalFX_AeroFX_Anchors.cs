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
        public Part TrackingPart;
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

    internal static partial class AeroTrailAnchors
    {
        private struct Candidate
        {
            public Part Part;
            public Part TrackingPart;
            public Vector3 Point;
            public Vector3 RadialGroupPoint;
            public Vector3 Outward;
            public float Score;
            public float WeightedScore;
            public float SideSign;
            public float LateralDistance;
            public float RadialDistance;
            public float ForwardOffset;
            public float VisualSize;
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
            WingtipAnchorRole.Tail,
            WingtipAnchorRole.MainWing,
            WingtipAnchorRole.Canard,
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
        private const float StabilizerForwardBandTolerance = 0.35f;
        private const float AircraftMainWingForwardLimit = -0.75f;
        private const float RadialStabilizerAftBand = 1.15f;
        private const float RadialStabilizerMinRadius = 0.35f;
        private const float RadialStabilizerMinDirectionDot = 0.70f;
        private const int RadialStabilizerMaxDirections = 4;
        private const float CenterMinTopHeight = 0.30f;

        private static Vector3 cachedUpAxis;
        private static Vector3 cachedForwardAxis;
        private static Vector3 cachedRightAxis;
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
            cachedRightAxis = rightAxis;
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
                    TrackingPart = part,
                    Point = supportPoint,
                    RadialGroupPoint = boundsCenter,
                    Outward = outward,
                    Score = score,
                    WeightedScore = score + GetRoleScoreBias(role),
                    SideSign = lateral >= 0f ? 1f : -1f,
                    LateralDistance = Mathf.Abs(Vector3.Dot(supportOffset, rightAxis)),
                    RadialDistance = supportRadialOffset.magnitude,
                    ForwardOffset = Vector3.Dot(offset, forward),
                    VisualSize = bounds.size.magnitude,
                    Role = role
                };

                allCandidates.Add(candidate);
                candidateCount++;
            }

            if (allCandidates.Count == 0)
                return 0;

            allCandidates.Sort(weightedScoreComparer);
            candidateSummary = BuildCandidateSummary();

            if (ShouldUseRadialStabilizerMode())
            {
                SelectRadialStabilizerCandidates(resultLimit);
            }
            else
            {
                SelectPrimaryOuterPair(resultLimit);
                SelectCenterCandidate(resultLimit);
                SelectSecondaryPriorityPairs(resultLimit);
                FillRemainingCandidates(resultLimit);
                EnforcePairSymmetry();
                ExtendPrimaryTipsToVisualExtremity(vessel.parts, centerOfMass, forward, rightAxis);
            }

            int count = Mathf.Min(selectedCandidates.Count, resultLimit);
            for (int i = 0; i < count; i++)
            {
                Candidate c = selectedCandidates[i];
                Part trackingPart = c.TrackingPart != null ? c.TrackingPart : c.Part;
                WingtipRibbonAnchor anchor;
                anchor.Part = c.Part;
                anchor.TrackingPart = trackingPart;
                anchor.WorldPoint = c.Point;
                anchor.Outward = c.Outward;
                anchor.LocalPoint = trackingPart.transform.InverseTransformPoint(c.Point);
                anchor.LocalOutward = trackingPart.transform.InverseTransformDirection(c.Outward);
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
            if (TryGetBestPrimaryMainWingCandidate(sideSign, out candidate))
                return true;
            if (TryGetBestPrimaryStabilizerCandidate(sideSign, out candidate))
                return true;
            if (TryGetBestPrimaryFallbackCandidate(sideSign, false, out candidate))
                return true;
            return TryGetBestPrimaryFallbackCandidate(sideSign, true, out candidate);
        }

        private static bool ShouldUseRadialStabilizerMode()
        {
            bool hasAircraftMainWing = false;
            float aftBandLimit;
            if (!TryGetRadialStabilizerAftBandLimit(out aftBandLimit))
                return false;

            int directionCount = 0;
            Vector3[] usedDirections = new Vector3[RadialStabilizerMaxDirections];
            for (int i = 0; i < allCandidates.Count; i++)
            {
                Candidate candidate = allCandidates[i];
                if (IsAircraftMainWingCandidate(candidate))
                {
                    hasAircraftMainWing = true;
                    break;
                }

                if (!IsRadialStabilizerCandidate(candidate) || candidate.ForwardOffset > aftBandLimit)
                    continue;

                Vector3 direction;
                if (!TryGetRadialDirection(candidate.RadialGroupPoint, out direction)
                    || IsRadialDirectionUsed(direction, usedDirections, directionCount))
                {
                    continue;
                }

                if (directionCount < usedDirections.Length)
                    usedDirections[directionCount] = direction;
                directionCount++;
            }

            return !hasAircraftMainWing && directionCount >= 2;
        }

        private static bool IsAircraftMainWingCandidate(Candidate candidate)
        {
            return candidate.Role == WingtipAnchorRole.MainWing
                && candidate.ForwardOffset > AircraftMainWingForwardLimit
                && candidate.VisualSize > 0.85f;
        }

        private static bool TryGetRadialStabilizerAftBandLimit(out float aftBandLimit)
        {
            aftBandLimit = 0f;
            float mostAft = float.MaxValue;
            bool found = false;
            for (int i = 0; i < allCandidates.Count; i++)
            {
                Candidate candidate = allCandidates[i];
                if (!IsRadialStabilizerCandidate(candidate))
                    continue;

                if (candidate.ForwardOffset < mostAft)
                    mostAft = candidate.ForwardOffset;
                found = true;
            }

            if (!found)
                return false;

            aftBandLimit = mostAft + RadialStabilizerAftBand;
            return true;
        }

        private static bool IsRadialStabilizerCandidate(Candidate candidate)
        {
            if (candidate.RadialDistance < RadialStabilizerMinRadius)
                return false;
            if (candidate.ForwardOffset > AircraftMainWingForwardLimit)
                return false;
            return candidate.Role == WingtipAnchorRole.Tail
                || candidate.Role == WingtipAnchorRole.MainWing;
        }

        private static void SelectRadialStabilizerCandidates(int resultLimit)
        {
            float aftBandLimit;
            if (!TryGetRadialStabilizerAftBandLimit(out aftBandLimit))
                return;

            while (selectedCandidates.Count < resultLimit)
            {
                Candidate candidate;
                if (!TryGetBestRadialStabilizerCandidate(aftBandLimit, out candidate))
                    break;

                AddSelectedCandidate(candidate);
            }
        }

        private static bool TryGetBestRadialStabilizerCandidate(
            float aftBandLimit,
            out Candidate candidate)
        {
            candidate = default(Candidate);
            float bestScore = float.MinValue;
            for (int i = 0; i < allCandidates.Count; i++)
            {
                Candidate current = allCandidates[i];
                if (IsSelected(current.Part))
                    continue;
                if (!IsRadialStabilizerCandidate(current) || current.ForwardOffset > aftBandLimit)
                    continue;

                RecomputeAsRadialOuterPoint(ref current);
                if (IsSelectedRadialDirection(current))
                    continue;

                float score = EvaluateRadialStabilizerScore(current);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                candidate = current;
            }

            return candidate.Part != null;
        }

        private static bool IsSelectedRadialDirection(Candidate candidate)
        {
            Vector3 direction;
            if (!TryGetRadialDirection(candidate.RadialGroupPoint, out direction))
                return false;

            for (int i = 0; i < selectedCandidates.Count; i++)
            {
                Vector3 selectedDirection;
                if (TryGetRadialDirection(selectedCandidates[i].RadialGroupPoint, out selectedDirection)
                    && Vector3.Dot(direction, selectedDirection) > RadialStabilizerMinDirectionDot)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetRadialDirection(Vector3 point, out Vector3 direction)
        {
            direction = Vector3.zero;
            Vector3 radialOffset = Vector3.ProjectOnPlane(point - cachedCenterOfMass, cachedForwardAxis);
            if (radialOffset.sqrMagnitude < 0.01f)
                return false;

            direction = radialOffset.normalized;
            return true;
        }

        private static bool IsRadialDirectionUsed(Vector3 direction, Vector3[] usedDirections, int directionCount)
        {
            int count = Mathf.Min(directionCount, usedDirections.Length);
            for (int i = 0; i < count; i++)
            {
                if (Vector3.Dot(direction, usedDirections[i]) > RadialStabilizerMinDirectionDot)
                    return true;
            }

            return false;
        }

        private static bool TryGetBestPrimaryMainWingCandidate(float sideSign, out Candidate candidate)
        {
            candidate = default(Candidate);
            for (int i = 0; i < allCandidates.Count; i++)
            {
                Candidate current = allCandidates[i];
                if (current.SideSign != sideSign || current.Role != WingtipAnchorRole.MainWing || IsSelected(current.Part))
                    continue;
                if (current.LateralDistance < CenterLateralThreshold)
                    continue;

                if (IsBetterPrimaryOuterCandidate(current, candidate))
                    candidate = current;
            }

            return candidate.Part != null;
        }

        private static bool TryGetBestPrimaryStabilizerCandidate(float sideSign, out Candidate candidate)
        {
            candidate = default(Candidate);
            float bestLateral = float.MinValue;
            float bestScore = float.MinValue;

            for (int i = 0; i < allCandidates.Count; i++)
            {
                Candidate current = allCandidates[i];
                if (current.SideSign != sideSign || current.Role != WingtipAnchorRole.Tail || IsSelected(current.Part))
                    continue;
                if (current.LateralDistance < CenterLateralThreshold)
                    continue;

                float score = EvaluatePrimaryStabilizerScore(current);
                if (score < bestScore - SupportPointForwardTieEpsilon)
                    continue;

                if (score > bestScore + SupportPointForwardTieEpsilon
                    || current.LateralDistance > bestLateral + PrimaryOuterLateralEpsilon)
                {
                    bestScore = score;
                    bestLateral = current.LateralDistance;
                    candidate = current;
                }
            }

            return candidate.Part != null;
        }

        private static bool TryGetBestPrimaryFallbackCandidate(float sideSign, bool includeControl, out Candidate candidate)
        {
            candidate = default(Candidate);
            for (int i = 0; i < allCandidates.Count; i++)
            {
                Candidate current = allCandidates[i];
                if (current.SideSign != sideSign || IsSelected(current.Part))
                    continue;
                if (current.LateralDistance < CenterLateralThreshold)
                    continue;
                if (!includeControl && current.Role == WingtipAnchorRole.Control)
                    continue;
                if (current.Role == WingtipAnchorRole.MainWing || current.Role == WingtipAnchorRole.Tail)
                    continue;

                if (IsBetterPrimaryOuterCandidate(current, candidate))
                    candidate = current;
            }

            return candidate.Part != null;
        }

        private static bool IsBetterPrimaryOuterCandidate(Candidate current, Candidate best)
        {
            if (best.Part == null)
                return true;
            if (current.LateralDistance > best.LateralDistance + PrimaryOuterLateralEpsilon)
                return true;
            if (current.LateralDistance < best.LateralDistance - PrimaryOuterLateralEpsilon)
                return false;
            if (current.RadialDistance > best.RadialDistance + SupportPointRadialTieEpsilon)
                return true;
            if (current.RadialDistance < best.RadialDistance - SupportPointRadialTieEpsilon)
                return false;
            return current.WeightedScore > best.WeightedScore;
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
                if (current.Role == WingtipAnchorRole.Tail && IsForwardOfSelectedStabilizerBand(current))
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
                if (IsForwardOfSelectedStabilizerBand(current))
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

            if (candidate.Part != null && role == WingtipAnchorRole.MainWing)
                RecomputeAftMainWingAsTopPoint(ref candidate);
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
                if (current.Role == WingtipAnchorRole.Tail && IsForwardOfSelectedStabilizerBand(current))
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

        private static bool IsForwardOfSelectedStabilizerBand(Candidate candidate)
        {
            if (candidate.Role != WingtipAnchorRole.Tail)
                return false;

            for (int i = 0; i < selectedCandidates.Count; i++)
            {
                Candidate selected = selectedCandidates[i];
                if (selected.SideSign != candidate.SideSign)
                    continue;
                if (selected.LateralDistance < CenterLateralThreshold)
                    continue;
                if (selected.Role != WingtipAnchorRole.Tail && selected.Role != WingtipAnchorRole.MainWing)
                    continue;
                if (selected.ForwardOffset + StabilizerForwardBandTolerance < candidate.ForwardOffset)
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

    }
}
