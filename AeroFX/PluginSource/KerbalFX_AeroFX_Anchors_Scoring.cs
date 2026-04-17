using UnityEngine;

namespace KerbalFX.AeroFX
{
    internal static partial class AeroTrailAnchors
    {
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
                    return -0.55f;
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
                    return -0.45f;
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

        private static float EvaluatePrimaryStabilizerScore(Candidate candidate)
        {
            float aft01 = Mathf.InverseLerp(0.05f, 2.50f, -candidate.ForwardOffset);
            float forwardPenalty = Mathf.InverseLerp(0.25f, 2.50f, candidate.ForwardOffset);
            float size01 = Mathf.InverseLerp(0.25f, 2.20f, candidate.VisualSize);
            float roleTypeBias = IsFinLikePart(candidate.Part) ? -0.20f : (IsTailplaneLikePart(candidate.Part) ? 0.35f : 0f);
            return roleTypeBias
                + aft01 * 4.50f
                + size01 * 1.35f
                + candidate.LateralDistance * 1.10f
                + candidate.RadialDistance * 0.75f
                - forwardPenalty * 2.75f;
        }

        private static float EvaluateRadialStabilizerScore(Candidate candidate)
        {
            float aft01 = Mathf.InverseLerp(0.05f, 3.00f, -candidate.ForwardOffset);
            float size01 = Mathf.InverseLerp(0.25f, 2.20f, candidate.VisualSize);
            float roleBias = candidate.Role == WingtipAnchorRole.Tail ? 0.25f : 0f;
            return roleBias
                + candidate.RadialDistance * 3.00f
                + candidate.LateralDistance * 0.35f
                + aft01 * 1.60f
                + size01 * 0.90f;
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
            return forward01 * 2.15f
                + center01 * 3.60f
                - lateralPenalty * 4.10f;
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
