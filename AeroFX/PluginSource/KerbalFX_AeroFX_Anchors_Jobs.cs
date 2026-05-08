using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace KerbalFX.AeroFX
{
    internal static partial class AeroTrailAnchors
    {
        internal struct AnchorCandidateMetricInput
        {
            public float3 BoundsCenter;
            public float3 SupportPoint;
            public float Score;
            public float RoleBias;
            public float VisualSize;
        }

        internal struct AnchorCandidateMetricOutput
        {
            public float WeightedScore;
            public float SideSign;
            public float LateralDistance;
            public float RadialDistance;
            public float ForwardOffset;
            public float VisualSize;
        }

        private static void EvaluateCandidateMetrics(
            Vector3 centerOfMass,
            Vector3 forward,
            Vector3 rightAxis,
            PartBoundsCache cache)
        {
            int count = allCandidates.Count;
            if (count <= 0 || cache == null)
                return;

            cache.EnsureMetricCapacity(count);
            NativeArray<AnchorCandidateMetricInput> input = cache.MetricInput;
            NativeArray<AnchorCandidateMetricOutput> output = cache.MetricOutput;
            if (!input.IsCreated || !output.IsCreated)
                return;

            for (int i = 0; i < count; i++)
            {
                Candidate candidate = allCandidates[i];
                input[i] = new AnchorCandidateMetricInput
                {
                    BoundsCenter = ToJobFloat3(candidate.RadialGroupPoint),
                    SupportPoint = ToJobFloat3(candidate.Point),
                    Score = candidate.Score,
                    RoleBias = GetRoleScoreBias(candidate.Role),
                    VisualSize = candidate.VisualSize
                };
            }

            var job = new AnchorCandidateMetricJob
            {
                Input = input,
                Output = output,
                CenterOfMass = ToJobFloat3(centerOfMass),
                Forward = ToJobFloat3(forward),
                RightAxis = ToJobFloat3(rightAxis)
            };
            job.Schedule(count, 16).Complete();

            for (int i = 0; i < count; i++)
            {
                Candidate candidate = allCandidates[i];
                AnchorCandidateMetricOutput metrics = output[i];
                candidate.WeightedScore = metrics.WeightedScore;
                candidate.SideSign = metrics.SideSign;
                candidate.LateralDistance = metrics.LateralDistance;
                candidate.RadialDistance = metrics.RadialDistance;
                candidate.ForwardOffset = metrics.ForwardOffset;
                candidate.VisualSize = metrics.VisualSize;
                allCandidates[i] = candidate;
            }
        }

        private static float3 ToJobFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        [BurstCompile]
        private struct AnchorCandidateMetricJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<AnchorCandidateMetricInput> Input;

            [WriteOnly]
            public NativeArray<AnchorCandidateMetricOutput> Output;

            public float3 CenterOfMass;
            public float3 Forward;
            public float3 RightAxis;

            public void Execute(int index)
            {
                AnchorCandidateMetricInput candidate = Input[index];
                float3 offset = candidate.BoundsCenter - CenterOfMass;
                float3 supportOffset = candidate.SupportPoint - CenterOfMass;
                float lateral = math.dot(supportOffset, RightAxis);
                float forwardOffset = math.dot(offset, Forward);
                float3 supportRadial = supportOffset - Forward * math.dot(supportOffset, Forward);

                Output[index] = new AnchorCandidateMetricOutput
                {
                    WeightedScore = candidate.Score + candidate.RoleBias,
                    SideSign = lateral >= 0f ? 1f : -1f,
                    LateralDistance = math.abs(lateral),
                    RadialDistance = math.length(supportRadial),
                    ForwardOffset = forwardOffset,
                    VisualSize = candidate.VisualSize
                };
            }
        }
    }
}
