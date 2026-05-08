using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace KerbalFX.AeroFX
{
    internal struct AeroRibbonPathRequest
    {
        public int InputOffset;
        public int OutputOffset;
        public int BaseCount;
        public int SegmentSubdivisions;
        public float4x4 LocalToWorld;
    }

    internal static class AeroRibbonPathBuilder
    {
        public static void BuildBatch(
            NativeArray<float3> bodyLocalPoints,
            NativeArray<AeroRibbonPathRequest> requests,
            int requestCount,
            NativeArray<float3> output)
        {
            if (!bodyLocalPoints.IsCreated || !requests.IsCreated || !output.IsCreated || requestCount <= 0)
                return;

            int clampedCount = math.min(requestCount, requests.Length);
            var job = new RibbonPathBatchJob
            {
                BodyLocalPoints = bodyLocalPoints,
                Requests = requests,
                Output = output
            };
            job.Schedule(clampedCount, 1).Complete();
        }

        public static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        public static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        public static int ComputeRenderCount(int baseCount, int segmentSubdivisions)
        {
            if (baseCount <= 0)
                return 0;
            if (baseCount == 1)
                return 1;
            return (baseCount - 1) * segmentSubdivisions + 1;
        }

        public static float4x4 ToFloat4x4(Matrix4x4 value)
        {
            Vector4 c0 = value.GetColumn(0);
            Vector4 c1 = value.GetColumn(1);
            Vector4 c2 = value.GetColumn(2);
            Vector4 c3 = value.GetColumn(3);
            return new float4x4(
                new float4(c0.x, c0.y, c0.z, c0.w),
                new float4(c1.x, c1.y, c1.z, c1.w),
                new float4(c2.x, c2.y, c2.z, c2.w),
                new float4(c3.x, c3.y, c3.z, c3.w));
        }

        [BurstCompile]
        private struct RibbonPathBatchJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<float3> BodyLocalPoints;

            [ReadOnly]
            public NativeArray<AeroRibbonPathRequest> Requests;

            [WriteOnly]
            public NativeArray<float3> Output;

            public void Execute(int index)
            {
                AeroRibbonPathRequest request = Requests[index];
                if (request.BaseCount <= 0 || request.SegmentSubdivisions <= 0)
                    return;

                if (request.BaseCount == 1)
                {
                    Output[request.OutputOffset] = TransformPoint(BodyLocalPoints[request.InputOffset], request.LocalToWorld);
                    return;
                }

                float3 p0 = TransformPoint(BodyLocalPoints[request.InputOffset], request.LocalToWorld);
                float3 p1 = p0;
                float3 p2 = TransformPoint(BodyLocalPoints[request.InputOffset + 1], request.LocalToWorld);
                int writeIndex = request.OutputOffset;

                for (int i = 0; i < request.BaseCount - 1; i++)
                {
                    float3 p3 = i + 2 < request.BaseCount
                        ? TransformPoint(BodyLocalPoints[request.InputOffset + i + 2], request.LocalToWorld)
                        : p2;
                    bool isTail = i == request.BaseCount - 2;
                    for (int step = 0; step < request.SegmentSubdivisions; step++)
                    {
                        float u = (float)step / request.SegmentSubdivisions;
                        Output[writeIndex++] = isTail
                            ? math.lerp(p1, p2, u)
                            : CatmullRom(p0, p1, p2, p3, u);
                    }

                    p0 = p1;
                    p1 = p2;
                    p2 = p3;
                }

                Output[writeIndex] = p1;
            }

            private static float3 TransformPoint(float3 value, float4x4 localToWorld)
            {
                return math.mul(localToWorld, new float4(value, 1f)).xyz;
            }

            private static float3 CatmullRom(float3 p0, float3 p1, float3 p2, float3 p3, float t)
            {
                float t2 = t * t;
                float t3 = t2 * t;
                return 0.5f * (
                    (2f * p1)
                    + (-p0 + p2) * t
                    + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                    + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
            }
        }
    }
}
