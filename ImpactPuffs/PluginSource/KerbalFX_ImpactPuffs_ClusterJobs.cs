using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace KerbalFX.ImpactPuffs
{
    internal struct ImpactPuffClusterInput
    {
        public float3 ClusterPosition;
    }

    internal static class ImpactPuffClusterJobs
    {
        public static void Build(
            NativeArray<ImpactPuffClusterInput> input,
            int frameCount,
            float linkRadiusSqr,
            NativeArray<int> clusterIds,
            NativeArray<int> clusterCount)
        {
            if (!input.IsCreated || !clusterIds.IsCreated || !clusterCount.IsCreated || frameCount <= 0)
                return;

            var job = new EngineClusterJob
            {
                Input = input,
                FrameCount = math.min(frameCount, input.Length),
                LinkRadiusSqr = linkRadiusSqr,
                ClusterIds = clusterIds,
                ClusterCount = clusterCount
            };
            job.Schedule().Complete();
        }

        public static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        [BurstCompile]
        private struct EngineClusterJob : IJob
        {
            [ReadOnly]
            public NativeArray<ImpactPuffClusterInput> Input;

            public NativeArray<int> ClusterIds;
            public NativeArray<int> ClusterCount;
            public int FrameCount;
            public float LinkRadiusSqr;

            public void Execute()
            {
                for (int i = 0; i < FrameCount; i++)
                    ClusterIds[i] = -1;

                int clusterCount = 0;
                for (int i = 0; i < FrameCount; i++)
                {
                    if (ClusterIds[i] >= 0)
                        continue;

                    int clusterIndex = clusterCount++;
                    ClusterIds[i] = clusterIndex;

                    bool expanded;
                    do
                    {
                        expanded = false;
                        for (int j = 0; j < FrameCount; j++)
                        {
                            if (ClusterIds[j] >= 0)
                                continue;
                            if (!IsLinkedToCluster(j, clusterIndex))
                                continue;

                            ClusterIds[j] = clusterIndex;
                            expanded = true;
                        }
                    }
                    while (expanded);
                }

                ClusterCount[0] = clusterCount;
            }

            private bool IsLinkedToCluster(int frameIndex, int clusterIndex)
            {
                float3 position = Input[frameIndex].ClusterPosition;
                for (int i = 0; i < FrameCount; i++)
                {
                    if (ClusterIds[i] != clusterIndex)
                        continue;

                    float3 delta = position - Input[i].ClusterPosition;
                    if (math.lengthsq(delta) <= LinkRadiusSqr)
                        return true;
                }
                return false;
            }
        }
    }
}
