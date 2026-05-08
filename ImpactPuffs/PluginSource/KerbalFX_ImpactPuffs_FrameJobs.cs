using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace KerbalFX.ImpactPuffs
{
    internal struct ImpactPuffFrameInput
    {
        public float3 GroundPoint;
        public float3 GroundNormal;
        public float3 ExhaustDirection;
        public float3 VesselCoM;
        public float3 VesselSurfaceVelocity;
        public float3 PartRight;
        public float CurrentThrust;
        public float NormalizedThrust;
        public float DistanceFactor;
        public float BodyVisibility;
        public float QualityScale;
        public float ImpactQualityScale;
        public float EmissionMultiplier;
        public float SharedEmissionMultiplier;
        public float ThrustPowerReference;
        public float ThrustPowerExponent;
        public float ThrustPowerMinScale;
        public float ThrustPowerMaxScale;
        public float EngineCountExponent;
        public float EngineCountMinScale;
        public float LightAwareStrength;
        public float LightDarkScale;
        public float LightBrightScale;
        public float LightTwilightFloor;
        public float LightMinPerceived;
        public float LightPerceivedBrightness;
        public int LightIsTwilight;
        public int UseLightAware;
        public int EngineClusterCount;
    }

    internal struct ImpactPuffFrameOutput
    {
        public float3 Position;
        public float3 StableNormal;
        public float3 OutwardHint;
        public float TargetRate;
        public float Pressure;
        public float QualityNorm;
        public float LightAlphaMultiplier;
    }

    internal static class ImpactPuffFrameJobs
    {
        public static void Build(
            NativeArray<ImpactPuffFrameInput> input,
            int frameCount,
            NativeArray<ImpactPuffFrameOutput> output)
        {
            if (!input.IsCreated || !output.IsCreated || frameCount <= 0)
                return;

            var job = new EngineFrameJob
            {
                Input = input,
                Output = output,
                FrameCount = math.min(frameCount, math.min(input.Length, output.Length))
            };
            job.Schedule(job.FrameCount, 16).Complete();
        }

        public static ImpactPuffFrameOutput BuildManaged(ImpactPuffFrameInput input)
        {
            return EngineFrameJob.BuildFrame(input);
        }

        public static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        public static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        [BurstCompile]
        private struct EngineFrameJob : IJobParallelFor
        {
            [ReadOnly]
            public NativeArray<ImpactPuffFrameInput> Input;

            [WriteOnly]
            public NativeArray<ImpactPuffFrameOutput> Output;

            public int FrameCount;

            public void Execute(int index)
            {
                if (index >= FrameCount)
                    return;
                Output[index] = BuildFrame(Input[index]);
            }

            public static ImpactPuffFrameOutput BuildFrame(ImpactPuffFrameInput input)
            {
                float quality = input.ImpactQualityScale;
                float qualityNorm = InverseLerp(0.25f, 2.0f, quality);
                float qualityRateScale = 1f + (math.pow(math.max(0.001f, quality), 1.50f) - 1f) * 1.35f;
                float thrustPowerScale = ComputeThrustPowerScale(input.CurrentThrust, input);
                float thrustPowerNorm = math.saturate(
                    (thrustPowerScale - input.ThrustPowerMinScale)
                    / math.max(0.01f, input.ThrustPowerMaxScale - input.ThrustPowerMinScale));

                float lowThrustBoost = math.lerp(1.42f, 1.00f, math.saturate(input.NormalizedThrust));
                float pressure = math.saturate(
                    input.NormalizedThrust
                    * lowThrustBoost
                    * math.lerp(0.52f, 1.95f, input.DistanceFactor)
                    * math.lerp(0.78f, 1.46f, thrustPowerNorm));

                float baseRate = (1320f + 14800f * pressure * pressure) * math.lerp(0.42f, 1.36f, input.DistanceFactor);
                float engineClusterScale = ComputeEngineClusterScale(input.EngineClusterCount, input);
                float visualAlpha = ComputeLightVisualAlpha(input);
                float rateLight = math.lerp(KerbalFxLightingCore.RateLightMinMultiplier, 1f, math.saturate(visualAlpha));
                float targetRate = baseRate
                    * qualityRateScale
                    * thrustPowerScale
                    * engineClusterScale
                    * input.EmissionMultiplier
                    * input.SharedEmissionMultiplier
                    * input.BodyVisibility
                    * 1.10f
                    * input.QualityScale
                    * rateLight;

                targetRate *= 1.42f;
                float rateCap = (input.EngineClusterCount > 1 ? 7500f : 18000f) * input.QualityScale;
                targetRate = math.clamp(targetRate, 0f, rateCap);

                float3 stableNormal = math.lengthsq(input.GroundNormal) > 0.0001f
                    ? math.normalize(input.GroundNormal)
                    : new float3(0f, 1f, 0f);
                float3 outwardHint;
                float3 position = ComputeEmitterPosition(input, pressure, stableNormal, out outwardHint);

                return new ImpactPuffFrameOutput
                {
                    Position = position,
                    StableNormal = stableNormal,
                    OutwardHint = outwardHint,
                    TargetRate = targetRate,
                    Pressure = pressure,
                    QualityNorm = qualityNorm,
                    LightAlphaMultiplier = visualAlpha
                };
            }

            private static float ComputeThrustPowerScale(float currentThrust, ImpactPuffFrameInput input)
            {
                float safeThrust = math.max(0f, currentThrust);
                float normalized = safeThrust / math.max(10f, input.ThrustPowerReference);
                float scaled = math.pow(math.max(0.001f, normalized), input.ThrustPowerExponent);
                return math.clamp(scaled, input.ThrustPowerMinScale, input.ThrustPowerMaxScale);
            }

            private static float ComputeEngineClusterScale(int clusterCount, ImpactPuffFrameInput input)
            {
                float count = math.max(1f, clusterCount);
                float scale = 1f / math.pow(count, input.EngineCountExponent);
                return math.clamp(scale, input.EngineCountMinScale, 1f);
            }

            private static float ComputeLightVisualAlpha(ImpactPuffFrameInput input)
            {
                if (input.UseLightAware == 0)
                    return 1f;

                float minPerceived = math.max(0f, input.LightMinPerceived);
                float t = InverseLerp(minPerceived, KerbalFxLightingCore.NominalSunLuma, input.LightPerceivedBrightness);
                float dark = input.LightDarkScale;
                if (input.LightIsTwilight != 0 && input.LightTwilightFloor > dark)
                    dark = input.LightTwilightFloor;
                float curved = t * t * (3f - 2f * t);
                float baseMultiplier = math.lerp(dark, input.LightBrightScale, curved);
                float alpha = math.lerp(1f, baseMultiplier, math.saturate(input.LightAwareStrength));
                return RemapVisualAlpha(alpha, KerbalFxLightingCore.SurfaceBoostFloor, KerbalFxLightingCore.SurfaceBoostExponent);
            }

            private static float3 ComputeEmitterPosition(
                ImpactPuffFrameInput input,
                float pressure,
                float3 stableNormal,
                out float3 outwardHint)
            {
                outwardHint = float3.zero;
                float surfaceOffset = -0.05f;
                float3 lateralOffset = float3.zero;
                float3 centerPlane = ProjectOnPlane(input.GroundPoint - input.VesselCoM, stableNormal);
                float centerPlaneMag = math.length(centerPlane);
                float underCenter = math.saturate(1f - centerPlaneMag / 0.85f);

                float3 outwardDir = centerPlane;
                if (math.lengthsq(outwardDir) < 0.0001f)
                    outwardDir = ProjectOnPlane(input.VesselSurfaceVelocity, stableNormal);
                if (math.lengthsq(outwardDir) < 0.0001f)
                    outwardDir = ProjectOnPlane(input.PartRight, stableNormal);

                if (math.lengthsq(outwardDir) > 0.0001f)
                {
                    outwardDir = math.normalize(outwardDir);
                    outwardHint = outwardDir;
                    float outwardShift = math.lerp(0.08f, 0.42f, pressure) * math.lerp(0.30f, 0.92f, underCenter);
                    if (input.EngineClusterCount > 1)
                    {
                        float clusterNorm = InverseLerp(2f, 6f, input.EngineClusterCount);
                        outwardShift *= math.lerp(1.10f, 1.50f, clusterNorm);
                    }
                    lateralOffset = outwardDir * outwardShift;
                }

                float3 finalPosition = input.GroundPoint + stableNormal * surfaceOffset + lateralOffset;
                float3 finalPlane = ProjectOnPlane(finalPosition - input.VesselCoM, stableNormal);
                float finalRadius = math.length(finalPlane);
                float minCoreRadius = math.lerp(0.55f, 1.95f, pressure);
                if (input.EngineClusterCount > 1)
                {
                    float clusterNorm = InverseLerp(2f, 6f, input.EngineClusterCount);
                    minCoreRadius *= math.lerp(1.00f, 1.40f, clusterNorm);
                }
                else
                {
                    minCoreRadius *= 0.30f;
                }

                if (finalRadius < minCoreRadius)
                {
                    float3 pushDir = outwardHint;
                    if (math.lengthsq(pushDir) < 0.0001f && math.lengthsq(finalPlane) > 0.0001f)
                        pushDir = math.normalize(finalPlane);
                    if (math.lengthsq(pushDir) > 0.0001f)
                        finalPosition += pushDir * (minCoreRadius - finalRadius);
                }

                return finalPosition;
            }

            private static float3 ProjectOnPlane(float3 value, float3 normal)
            {
                return value - normal * math.dot(value, normal);
            }

            private static float InverseLerp(float a, float b, float value)
            {
                float denom = b - a;
                if (math.abs(denom) < 0.0001f)
                    return 0f;
                return math.saturate((value - a) / denom);
            }

            private static float RemapVisualAlpha(float alphaMultiplier, float floor, float exponent)
            {
                float clamped = math.saturate(alphaMultiplier);
                float safeExponent = math.max(0.0001f, exponent);
                if (floor > 0.0001f)
                {
                    float t = InverseLerp(floor, 1f, clamped);
                    return math.pow(t, safeExponent);
                }
                return math.pow(clamped, safeExponent);
            }
        }
    }
}
