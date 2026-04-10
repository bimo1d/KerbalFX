using System.Globalization;
using KSP.Localization;
using UnityEngine;

namespace KerbalFX.ImpactPuffs
{
    internal sealed class TouchdownBurstEmitter
    {
        private struct TouchdownContext
        {
            public Vector3 Point;
            public Vector3 Normal;
            public Collider Collider;
            public bool Splash;
            public float QualityNorm;
            public float BodyVisibility;
            public float BurstStrength;
            public float TouchdownEnergy01;
            public float BurstLightScale;
            public Color Color;
            public int FxLayer;
        }

        private readonly Vessel vessel;
        private bool wasGrounded;
        private float cooldown;
        private static readonly RaycastHit[] BurstHits = new RaycastHit[32];
        private static readonly Gradient BurstCoreGradient = CreateBurstCoreGradient();
        private static readonly Gradient BurstHazeGradient = CreateBurstHazeGradient();
        private static readonly Gradient RingShockGradient = CreateRingShockGradient();
        private static readonly AnimationCurve BurstCoreSizeCurve = CreateBurstCoreSizeCurve();
        private static readonly AnimationCurve BurstHazeSizeCurve = CreateBurstHazeSizeCurve();
        private static readonly AnimationCurve RingShockSizeCurve = CreateRingShockSizeCurve();

        public TouchdownBurstEmitter(Vessel vessel)
        {
            this.vessel = vessel;
            wasGrounded = vessel != null && (vessel.Landed || vessel.situation == Vessel.Situations.PRELAUNCH);
        }

        public void Tick(float dt)
        {
            if (vessel == null || !vessel.loaded || vessel.packed)
            {
                return;
            }

            cooldown = Mathf.Max(0f, cooldown - dt);

            bool grounded = vessel.Landed
                || vessel.Splashed
                || vessel.situation == Vessel.Situations.PRELAUNCH
                || vessel.situation == Vessel.Situations.SPLASHED;
            float descentSpeed = Mathf.Max(0f, (float)(-vessel.verticalSpeed));
            bool useSimplified = ImpactPuffsConfig.UseSimplifiedEffects;
            float touchdownSpeedThreshold = useSimplified
                ? Mathf.Max(ImpactPuffsRuntimeConfig.TouchdownMinSpeed * 1.45f, 3.2f)
                : Mathf.Max(ImpactPuffsRuntimeConfig.TouchdownMinSpeed * 1.20f, 2.8f);
            if (!wasGrounded && grounded && cooldown <= 0f && descentSpeed >= touchdownSpeedThreshold)
            {
                if (useSimplified)
                {
                    SpawnBurst(descentSpeed);
                }
                else
                {
                    SpawnRingShock(descentSpeed);
                }
                cooldown = useSimplified ? 0.45f : 0.38f;
            }

            wasGrounded = grounded;
        }

        public void Dispose()
        {
        }

        private static float GetTouchdownEnergy01(float impactSpeed)
        {
            float cappedSpeed = Mathf.Clamp(impactSpeed, 0f, 30f);
            float speed01 = cappedSpeed / 30f;
            return speed01 * speed01;
        }

        private static void PlayIfExists(ParticleSystem particleSystem)
        {
            if (particleSystem != null)
            {
                particleSystem.Play(true);
            }
        }

        private bool TryBuildTouchdownContext(
            float impactSpeed,
            bool ringShockMode,
            float baseWhiten,
            float splashWhiten,
            out TouchdownContext context)
        {
            context = default(TouchdownContext);
            if (vessel == null || vessel.mainBody == null)
            {
                return false;
            }

            if (!TryGetTouchdownGroundSurface(out context.Point, out context.Normal, out context.Collider))
            {
                return false;
            }

            context.Splash = vessel.Splashed || vessel.situation == Vessel.Situations.SPLASHED;
            float quality = EngineGroundPuffEmitter.GetModeQualityScale();
            context.QualityNorm = Mathf.InverseLerp(0.25f, 2.0f, quality);
            context.BodyVisibility = ImpactPuffsRuntimeConfig.GetBodyVisibilityMultiplier(vessel.mainBody.bodyName);
            context.BurstStrength = Mathf.Clamp01(impactSpeed / (ringShockMode ? 10.5f : 11.5f));
            context.TouchdownEnergy01 = GetTouchdownEnergy01(impactSpeed);

            float burstLight = ringShockMode
                ? EngineGroundPuffEmitter.GetTouchdownLightFactor(vessel, context.Point, context.Normal)
                : EngineGroundPuffEmitter.GetSunLightFactor(vessel, context.Point, context.Normal);
            float burstLightVisibility = Mathf.Pow(Mathf.Clamp01(burstLight), ringShockMode ? 0.90f : 0.88f);
            float touchdownLightFloor = Mathf.Lerp(
                ringShockMode ? 0.03f : 0.02f,
                ringShockMode ? 0.12f : 0.09f,
                context.TouchdownEnergy01);
            context.BurstLightScale = Mathf.Lerp(touchdownLightFloor, 1f, burstLightVisibility);

            context.Color = ResolveTouchdownDustColor(context.Collider, context.Splash, baseWhiten, splashWhiten);
            context.FxLayer = vessel.rootPart != null ? vessel.rootPart.gameObject.layer : 0;
            return true;
        }

        private bool TryGetTouchdownGroundSurface(out Vector3 point, out Vector3 normal, out Collider collider)
        {
            point = Vector3.zero;
            normal = Vector3.up;
            collider = null;

            if (!TryGetGroundPoint(out point, out normal, out collider))
            {
                return false;
            }

            if (EngineGroundPuffEmitter.IsInKerbinLaunchsiteZone(vessel))
            {
                return false;
            }

            if (EngineGroundPuffEmitter.IsLaunchsiteExcludedSurface(collider))
            {
                return false;
            }

            return true;
        }

        private Color ResolveTouchdownDustColor(Collider collider, bool splash, float baseWhiten, float splashWhiten)
        {
            Color color = ImpactPuffsSurfaceColor.GetBaseDustColor(vessel);
            Color colliderColor;
            if (ImpactPuffsSurfaceColor.TryGetColliderColor(collider, out colliderColor))
            {
                color = ImpactPuffsSurfaceColor.BlendWithColliderColor(color, colliderColor);
            }

            color = ImpactPuffsSurfaceColor.NormalizeDustTone(color);
            color = Color.Lerp(color, new Color(0.90f, 0.90f, 0.89f), baseWhiten);
            if (splash)
            {
                color = Color.Lerp(color, new Color(0.93f, 0.94f, 0.95f), splashWhiten);
            }

            return color;
        }

        private void LogRingShockDebug(
            float impactSpeed,
            int ringDustCount,
            float bodyVisibility,
            int ringEdgeCount,
            int ringMistCount,
            float radiusBase,
            float lateralBase,
            float liftBase,
            float surfaceOffset,
            float slopeFactor,
            float ringAlpha,
            float burstLightScale,
            float touchdownEnergy01)
        {
            if (!ImpactPuffsConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel)
            {
                return;
            }

            ImpactPuffsLog.DebugLog(Localizer.Format(
                ImpactPuffsLoc.LogBurst,
                impactSpeed.ToString("F2"),
                ringDustCount,
                bodyVisibility.ToString("F2")
            ));
            ImpactPuffsLog.DebugLog(Localizer.Format(
                ImpactPuffsLoc.LogRingShockDebug,
                impactSpeed.ToString("F2", CultureInfo.InvariantCulture),
                ringDustCount.ToString(CultureInfo.InvariantCulture),
                ringEdgeCount.ToString(CultureInfo.InvariantCulture),
                ringMistCount.ToString(CultureInfo.InvariantCulture),
                radiusBase.ToString("F2", CultureInfo.InvariantCulture),
                lateralBase.ToString("F2", CultureInfo.InvariantCulture),
                liftBase.ToString("F3", CultureInfo.InvariantCulture),
                surfaceOffset.ToString("F3", CultureInfo.InvariantCulture),
                slopeFactor.ToString("F2", CultureInfo.InvariantCulture),
                ringAlpha.ToString("F2", CultureInfo.InvariantCulture),
                burstLightScale.ToString("F2", CultureInfo.InvariantCulture),
                touchdownEnergy01.ToString("F2", CultureInfo.InvariantCulture)
            ));
        }

        private void LogSimplifiedBurstDebug(
            float impactSpeed,
            int burstCount,
            float bodyVisibility,
            int coreCount,
            int hazeCount,
            float radiusBase,
            float lateralBase,
            float liftBase,
            float coreAlpha,
            float hazeAlpha,
            float burstLightScale,
            float touchdownEnergy01)
        {
            if (!ImpactPuffsConfig.DebugLogging || vessel != FlightGlobals.ActiveVessel)
            {
                return;
            }

            ImpactPuffsLog.DebugLog(Localizer.Format(
                ImpactPuffsLoc.LogBurst,
                impactSpeed.ToString("F2"),
                burstCount,
                bodyVisibility.ToString("F2")
            ));
            ImpactPuffsLog.DebugLog(Localizer.Format(
                ImpactPuffsLoc.LogBurstDebug,
                impactSpeed.ToString("F2", CultureInfo.InvariantCulture),
                burstCount.ToString(CultureInfo.InvariantCulture),
                coreCount.ToString(CultureInfo.InvariantCulture),
                hazeCount.ToString(CultureInfo.InvariantCulture),
                radiusBase.ToString("F2", CultureInfo.InvariantCulture),
                lateralBase.ToString("F2", CultureInfo.InvariantCulture),
                liftBase.ToString("F3", CultureInfo.InvariantCulture),
                coreAlpha.ToString("F2", CultureInfo.InvariantCulture),
                hazeAlpha.ToString("F2", CultureInfo.InvariantCulture),
                burstLightScale.ToString("F2", CultureInfo.InvariantCulture),
                touchdownEnergy01.ToString("F2", CultureInfo.InvariantCulture)
            ));
        }

        private void SpawnRingShock(float impactSpeed)
        {
            TouchdownContext context;
            if (!TryBuildTouchdownContext(impactSpeed, true, 0.08f, 0.14f, out context))
            {
                return;
            }

            float energyRadiusScale = Mathf.Lerp(1.00f, 1.12f, context.TouchdownEnergy01);
            float energySpreadScale = Mathf.Lerp(1.00f, 1.16f, context.TouchdownEnergy01);
            float energyLiftScale = Mathf.Lerp(1.00f, 1.10f, context.TouchdownEnergy01);
            float energyCountScale = Mathf.Lerp(1.00f, 1.32f, context.TouchdownEnergy01);
            float radiusBase = Mathf.Lerp(0.86f, 2.22f, context.QualityNorm)
                * (context.Splash ? 1.24f : 1.00f)
                * ImpactPuffsRuntimeConfig.RadiusScaleMultiplier
                * ImpactPuffsRuntimeConfig.SharedRadiusScaleMultiplier
                * 0.48f
                * energyRadiusScale;
            float lateralScale = Mathf.Lerp(
                0.72f,
                1.24f,
                Mathf.InverseLerp(1.0f, 3.0f, ImpactPuffsRuntimeConfig.LateralSpreadMultiplier)
            );
            float lateralBase = Mathf.Lerp(0.90f, 2.80f, context.BurstStrength)
                * Mathf.Lerp(0.92f, 1.16f, context.QualityNorm)
                * lateralScale
                * (context.Splash ? 1.12f : 1.00f)
                * energySpreadScale;
            float liftBase = Mathf.Lerp(0.03f, 0.12f, context.BurstStrength)
                * ImpactPuffsRuntimeConfig.VerticalLiftMultiplier
                * (context.Splash ? 0.74f : 1.00f)
                * energyLiftScale;
            float densityScale = Mathf.Lerp(0.96f, 1.14f, context.QualityNorm);
            Vector3 bodyUp = (vessel.CoM - vessel.mainBody.position).normalized;
            float slopeFactor = 1f - Mathf.Clamp01(Vector3.Dot(context.Normal, bodyUp));
            float surfaceOffset = (context.Splash ? 0.018f : 0.026f) + slopeFactor * 0.055f + Mathf.Clamp(radiusBase * 0.004f, 0f, 0.018f);

            GameObject root = new GameObject("KerbalFX_ImpactRingShock");
            root.transform.position = context.Point + context.Normal * surfaceOffset;
            root.transform.rotation = Quaternion.FromToRotation(Vector3.forward, context.Normal);
            root.layer = context.FxLayer;

            float ringAlpha = Mathf.Lerp(context.Splash ? 0.26f : 0.30f, context.Splash ? 0.36f : 0.43f, context.QualityNorm) * context.BurstLightScale * densityScale * 0.90f;
            float ringBase =
                (140f + 460f * context.BurstStrength)
                * ImpactPuffsRuntimeConfig.TouchdownBurstMultiplier
                * (context.Splash ? 1.08f : 1.00f)
                * context.BodyVisibility
                * 1.15f
                * energyCountScale;
            int ringDustCount = Mathf.RoundToInt(Mathf.Clamp(ringBase, 360f, 4600f));
            int ringEdgeCount = Mathf.Clamp(Mathf.RoundToInt(ringDustCount * 0.62f), 180, 2800);
            int ringMistCount = Mathf.Clamp(ringDustCount - ringEdgeCount, 120, 1800);

            Material dustMaterial = ImpactPuffsAssets.GetSharedMaterial();
            if (dustMaterial == null)
            {
                dustMaterial = ImpactPuffsAssets.GetBurstMaterial();
            }

            ParticleSystem ringEdge = CreateTouchdownLayer(
                "RingEdge",
                root.transform,
                context.FxLayer,
                context.Color,
                ringAlpha,
                0.16f * Mathf.Lerp(0.94f, 1.18f, context.QualityNorm) * context.BodyVisibility,
                0.64f * Mathf.Lerp(0.94f, 1.18f, context.QualityNorm) * context.BodyVisibility,
                0.54f,
                (context.Splash ? 1.48f : 1.34f) * Mathf.Lerp(0.96f, 1.14f, context.QualityNorm),
                0.00f,
                0.10f * Mathf.Lerp(0.96f, 1.12f, context.QualityNorm),
                0.020f,
                ringEdgeCount,
                Mathf.Lerp(context.Splash ? 76f : 74f, context.Splash ? 84f : 82f, context.QualityNorm),
                radiusBase * 0.98f,
                lateralBase * 0.06f,
                context.Splash ? liftBase * 0.03f : liftBase * 0.06f,
                context.Splash ? liftBase * 0.10f : liftBase * 0.18f,
                lateralBase * 1.46f,
                lateralBase * 2.70f,
                Mathf.Lerp(0.06f, 0.18f, context.BurstStrength) * ImpactPuffsRuntimeConfig.TurbulenceMultiplier,
                Mathf.Lerp(0.05f, 0.14f, context.BurstStrength),
                Mathf.Lerp(0.01f, 0.05f, context.BurstStrength),
                RingShockGradient,
                RingShockSizeCurve,
                Mathf.Lerp(0.78f, 0.98f, context.QualityNorm),
                2.2f,
                0.00f,
                ParticleSystemRenderMode.Billboard,
                dustMaterial,
                true
            );

            ParticleSystem ringMist = CreateTouchdownLayer(
                "RingMist",
                root.transform,
                context.FxLayer,
                Color.Lerp(context.Color, Color.white, 0.06f),
                ringAlpha * 0.58f,
                0.28f * Mathf.Lerp(0.96f, 1.22f, context.QualityNorm) * context.BodyVisibility,
                1.04f * Mathf.Lerp(0.96f, 1.22f, context.QualityNorm) * context.BodyVisibility,
                0.80f,
                (context.Splash ? 2.15f : 1.96f) * Mathf.Lerp(0.98f, 1.18f, context.QualityNorm),
                0.01f,
                0.08f * Mathf.Lerp(0.96f, 1.12f, context.QualityNorm),
                0.022f,
                ringMistCount,
                Mathf.Lerp(context.Splash ? 72f : 70f, context.Splash ? 80f : 78f, context.QualityNorm),
                radiusBase * 0.88f,
                lateralBase * 0.05f,
                context.Splash ? liftBase * 0.04f : liftBase * 0.08f,
                context.Splash ? liftBase * 0.12f : liftBase * 0.22f,
                lateralBase * 1.04f,
                lateralBase * 2.00f,
                Mathf.Lerp(0.04f, 0.14f, context.BurstStrength) * ImpactPuffsRuntimeConfig.TurbulenceMultiplier,
                Mathf.Lerp(0.04f, 0.12f, context.BurstStrength),
                Mathf.Lerp(0.00f, 0.04f, context.BurstStrength),
                RingShockGradient,
                RingShockSizeCurve,
                Mathf.Lerp(0.92f, 1.18f, context.QualityNorm),
                2.0f,
                0.08f,
                ParticleSystemRenderMode.Billboard,
                dustMaterial,
                true
            );

            PlayIfExists(ringEdge);
            PlayIfExists(ringMist);

            float ringLifeMax = context.Splash ? 2.55f : 2.30f;
            UnityEngine.Object.Destroy(root, ringLifeMax + 0.90f);

            LogRingShockDebug(
                impactSpeed,
                ringDustCount,
                context.BodyVisibility,
                ringEdgeCount,
                ringMistCount,
                radiusBase,
                lateralBase,
                liftBase,
                surfaceOffset,
                slopeFactor,
                ringAlpha,
                context.BurstLightScale,
                context.TouchdownEnergy01);
        }

        private void SpawnBurst(float impactSpeed)
        {
            TouchdownContext context;
            if (!TryBuildTouchdownContext(impactSpeed, false, 0.06f, 0.12f, out context))
            {
                return;
            }

            GameObject burstRoot = new GameObject("KerbalFX_ImpactBurst");
            burstRoot.transform.position = context.Splash ? (context.Point - context.Normal * 0.14f) : (context.Point - context.Normal * 0.08f);
            burstRoot.transform.rotation = Quaternion.FromToRotation(Vector3.up, context.Normal);
            float energyRadiusScale = Mathf.Lerp(1.00f, 1.10f, context.TouchdownEnergy01);
            float energySpreadScale = Mathf.Lerp(1.00f, 1.14f, context.TouchdownEnergy01);
            float energyLiftScale = Mathf.Lerp(1.00f, 1.10f, context.TouchdownEnergy01);
            float energyCountScale = Mathf.Lerp(1.00f, 1.28f, context.TouchdownEnergy01);
            float burstBase =
                (90f + 420f * context.BurstStrength)
                * ImpactPuffsRuntimeConfig.TouchdownBurstMultiplier
                * (context.Splash ? 1.08f : 1.00f)
                * context.BodyVisibility
                * energyCountScale;
            float burstParticleMultiplier = Mathf.Lerp(1.12f, 1.52f, context.BurstStrength);
            int burstCount = Mathf.RoundToInt(
                Mathf.Clamp(
                    burstBase * burstParticleMultiplier,
                    140f,
                    5200f
                )
            );
            float splashRadiusScale = context.Splash ? 1.34f : 1.00f;
            Material material = ImpactPuffsAssets.GetBurstMaterial();
            burstRoot.layer = context.FxLayer;

            int coreCount = Mathf.Clamp(Mathf.RoundToInt(burstCount * 0.58f), 80, 3000);
            int hazeCount = Mathf.Clamp(burstCount - coreCount, 64, 2300);

            float radiusBase = Mathf.Lerp(0.58f, 1.58f, context.QualityNorm) * splashRadiusScale
                * ImpactPuffsRuntimeConfig.RadiusScaleMultiplier
                * ImpactPuffsRuntimeConfig.SharedRadiusScaleMultiplier
                * energyRadiusScale;
            float lateralBase = Mathf.Lerp(0.78f, 2.85f, context.BurstStrength) * ImpactPuffsRuntimeConfig.LateralSpreadMultiplier * (context.Splash ? 1.08f : 1.00f) * energySpreadScale;
            float liftBase = Mathf.Lerp(0.015f, 0.14f, context.BurstStrength) * ImpactPuffsRuntimeConfig.VerticalLiftMultiplier * (context.Splash ? 0.05f : 0.16f) * energyLiftScale;
            float alphaDensityScale = Mathf.Lerp(1.04f, 1.36f, context.QualityNorm);
            float coreAlpha = Mathf.Lerp(context.Splash ? 0.62f : 0.58f, context.Splash ? 0.82f : 0.78f, context.QualityNorm) * context.BurstLightScale * alphaDensityScale;
            float hazeAlpha = coreAlpha * 0.76f * alphaDensityScale;
            float coreMaxParticleLife = (context.Splash ? 2.30f : 2.20f) * Mathf.Lerp(0.98f, 1.20f, context.QualityNorm);
            float hazeMaxParticleLife = (context.Splash ? 3.00f : 2.70f) * Mathf.Lerp(0.98f, 1.20f, context.QualityNorm);

            ParticleSystem burstCore = CreateTouchdownLayer(
                "Core",
                burstRoot.transform,
                context.FxLayer,
                context.Color,
                coreAlpha,
                0.058f * Mathf.Lerp(0.92f, 1.18f, context.QualityNorm) * context.BodyVisibility * 1.45f,
                0.19f * Mathf.Lerp(0.92f, 1.18f, context.QualityNorm) * context.BodyVisibility * 1.45f,
                0.46f,
                coreMaxParticleLife,
                0.06f,
                0.76f * Mathf.Lerp(0.98f, 1.20f, context.QualityNorm),
                0.030f,
                coreCount,
                Mathf.Lerp(context.Splash ? 86f : 82f, context.Splash ? 94f : 92f, context.QualityNorm),
                radiusBase * 0.92f,
                lateralBase * 1.06f,
                context.Splash ? 0f : liftBase * 0.022f,
                context.Splash ? liftBase * 0.015f : liftBase * 0.065f,
                lateralBase * 0.62f,
                lateralBase * 1.40f,
                Mathf.Lerp(0.60f, 1.40f, context.BurstStrength) * ImpactPuffsRuntimeConfig.TurbulenceMultiplier,
                Mathf.Lerp(0.26f, 0.72f, context.BurstStrength),
                Mathf.Lerp(0.10f, 0.48f, context.BurstStrength),
                BurstCoreGradient,
                BurstCoreSizeCurve,
                Mathf.Lerp(0.72f, 1.04f, context.QualityNorm),
                2.6f,
                0.82f,
                ParticleSystemRenderMode.Billboard,
                material
            );

            ParticleSystem burstHaze = CreateTouchdownLayer(
                "Haze",
                burstRoot.transform,
                context.FxLayer,
                Color.Lerp(context.Color, Color.white, 0.02f),
                hazeAlpha,
                0.090f * Mathf.Lerp(0.94f, 1.20f, context.QualityNorm) * context.BodyVisibility * 1.42f,
                0.30f * Mathf.Lerp(0.94f, 1.20f, context.QualityNorm) * context.BodyVisibility * 1.42f,
                0.72f,
                hazeMaxParticleLife,
                0.01f,
                0.42f * Mathf.Lerp(0.96f, 1.14f, context.QualityNorm),
                0.022f,
                hazeCount,
                Mathf.Lerp(context.Splash ? 90f : 88f, context.Splash ? 98f : 96f, context.QualityNorm),
                radiusBase * 1.86f,
                lateralBase * 1.18f,
                context.Splash ? 0f : liftBase * 0.015f,
                context.Splash ? liftBase * 0.022f : liftBase * 0.040f,
                lateralBase * 1.02f,
                lateralBase * 2.05f,
                Mathf.Lerp(0.46f, 1.04f, context.BurstStrength) * ImpactPuffsRuntimeConfig.TurbulenceMultiplier,
                Mathf.Lerp(0.20f, 0.60f, context.BurstStrength),
                Mathf.Lerp(0.08f, 0.38f, context.BurstStrength),
                BurstHazeGradient,
                BurstHazeSizeCurve,
                Mathf.Lerp(0.80f, 1.08f, context.QualityNorm),
                2.3f,
                0.10f,
                ParticleSystemRenderMode.HorizontalBillboard,
                material
            );

            PlayIfExists(burstCore);
            PlayIfExists(burstHaze);

            float burstTail = 0.30f;
            float maxLife = Mathf.Max(coreMaxParticleLife + burstTail, hazeMaxParticleLife + burstTail) + 0.80f;
            UnityEngine.Object.Destroy(burstRoot, maxLife);

            LogSimplifiedBurstDebug(
                impactSpeed,
                burstCount,
                context.BodyVisibility,
                coreCount,
                hazeCount,
                radiusBase,
                lateralBase,
                liftBase,
                coreAlpha,
                hazeAlpha,
                context.BurstLightScale,
                context.TouchdownEnergy01);
        }

        private static ParticleSystem CreateTouchdownLayer(
            string name,
            Transform parent,
            int layer,
            Color color,
            float alpha,
            float minSize,
            float maxSize,
            float minLife,
            float maxLife,
            float minSpeed,
            float maxSpeed,
            float gravity,
            int burstCount,
            float shapeAngle,
            float shapeRadius,
            float lateral,
            float liftMin,
            float liftMax,
            float radialMin,
            float radialMax,
            float noiseStrength,
            float noiseFrequency,
            float noiseScroll,
            Gradient gradient,
            AnimationCurve sizeCurve,
            float maxParticleSize,
            float sortingFudge,
            float shapeRadiusThickness,
            ParticleSystemRenderMode renderMode,
            Material material,
            bool useCircleShape = false
        )
        {
            GameObject go = new GameObject("KerbalFX_ImpactBurst_" + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.layer = layer;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(-3.14159f, 3.14159f);
            main.startColor = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.gravityModifier = gravity;
            main.maxParticles = Mathf.RoundToInt(Mathf.Clamp(36000f * ImpactPuffsRuntimeConfig.MaxParticlesMultiplier * ImpactPuffsRuntimeConfig.SharedMaxParticlesMultiplier, 6000f, 360000f));

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = true;
            short burstPrimary = (short)Mathf.Clamp(Mathf.RoundToInt(burstCount * 0.40f), 1, short.MaxValue);
            short burstSecondary = (short)Mathf.Clamp(Mathf.RoundToInt(burstCount * 0.28f), 1, short.MaxValue);
            short burstTertiary = (short)Mathf.Clamp(Mathf.RoundToInt(burstCount * 0.20f), 1, short.MaxValue);
            short burstQuaternary = (short)Mathf.Clamp(Mathf.RoundToInt(burstCount * 0.12f), 1, short.MaxValue);
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, burstPrimary),
                new ParticleSystem.Burst(0.04f, burstSecondary),
                new ParticleSystem.Burst(0.11f, burstTertiary),
                new ParticleSystem.Burst(0.21f, burstQuaternary)
            });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = useCircleShape ? ParticleSystemShapeType.Circle : ParticleSystemShapeType.Cone;
            shape.angle = useCircleShape ? 0f : shapeAngle;
            shape.radius = shapeRadius;
            shape.radiusThickness = Mathf.Clamp01(shapeRadiusThickness);

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-lateral, lateral);
            velocity.y = new ParticleSystem.MinMaxCurve(liftMin, liftMax);
            velocity.z = new ParticleSystem.MinMaxCurve(-lateral, lateral);
            velocity.radial = new ParticleSystem.MinMaxCurve(radialMin, radialMax);

            ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = ps.limitVelocityOverLifetime;
            limitVelocity.enabled = true;
            limitVelocity.space = ParticleSystemSimulationSpace.Local;
            limitVelocity.separateAxes = false;
            float maxRadial = Mathf.Max(Mathf.Abs(radialMin), Mathf.Abs(radialMax));
            float speedCap = Mathf.Max(0.20f, Mathf.Max(maxSpeed, maxRadial) * 1.16f);
            limitVelocity.limit = new ParticleSystem.MinMaxCurve(speedCap);
            limitVelocity.dampen = 0.46f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = noiseStrength;
            noise.frequency = noiseFrequency;
            noise.scrollSpeed = noiseScroll;
            noise.damping = true;

            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = renderMode;
            renderer.sortingFudge = sortingFudge;
            renderer.maxParticleSize = maxParticleSize;
            if (material != null)
            {
                renderer.material = material;
            }

            return ps;
        }

        private static Gradient CreateBurstCoreGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.98f, 0f),
                    new GradientAlphaKey(0.72f, 0.20f),
                    new GradientAlphaKey(0.42f, 0.52f),
                    new GradientAlphaKey(0.18f, 0.82f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            return gradient;
        }

        private static Gradient CreateBurstHazeGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.76f, 0f),
                    new GradientAlphaKey(0.52f, 0.26f),
                    new GradientAlphaKey(0.30f, 0.58f),
                    new GradientAlphaKey(0.10f, 0.86f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            return gradient;
        }

        private static AnimationCurve CreateBurstCoreSizeCurve()
        {
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.26f);
            curve.AddKey(0.28f, 0.86f);
            curve.AddKey(0.64f, 1.06f);
            curve.AddKey(1f, 0.24f);
            return curve;
        }

        private static AnimationCurve CreateBurstHazeSizeCurve()
        {
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.32f);
            curve.AddKey(0.44f, 0.94f);
            curve.AddKey(0.78f, 1.14f);
            curve.AddKey(1f, 0.30f);
            return curve;
        }

        private static Gradient CreateRingShockGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.68f, 0f),
                    new GradientAlphaKey(0.46f, 0.16f),
                    new GradientAlphaKey(0.22f, 0.52f),
                    new GradientAlphaKey(0.06f, 0.82f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            return gradient;
        }

        private static AnimationCurve CreateRingShockSizeCurve()
        {
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.44f);
            curve.AddKey(0.20f, 0.94f);
            curve.AddKey(0.62f, 1.34f);
            curve.AddKey(1f, 0.60f);
            return curve;
        }

        private bool TryGetGroundPoint(out Vector3 point, out Vector3 normal, out Collider collider)
        {
            point = Vector3.zero;
            normal = Vector3.up;
            collider = null;

            if (vessel == null || vessel.mainBody == null)
            {
                return false;
            }

            Vector3 up = vessel.CoM - vessel.mainBody.position;
            if (up.sqrMagnitude < 0.0001f)
            {
                up = Vector3.up;
            }
            up.Normalize();

            Vector3 origin = vessel.CoM + up * 3.0f;
            Vector3 direction = -up;

            RaycastHit hit;
            if (!TryRayDown(origin, direction, 14f, out hit))
            {
                return false;
            }

            point = hit.point;
            normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : up;
            collider = hit.collider;
            return true;
        }

        private bool TryRayDown(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit bestHit)
        {
            bestHit = new RaycastHit();
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction.normalized,
                BurstHits,
                maxDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );

            if (hitCount <= 0)
            {
                return false;
            }

            float bestDistance = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = BurstHits[i];
                if (candidate.collider == null)
                {
                    continue;
                }

                Part hitPart = candidate.collider.GetComponentInParent<Part>();
                if (hitPart != null && hitPart.vessel == vessel)
                {
                    continue;
                }

                if (vessel != null && vessel.rootPart != null)
                {
                    Transform rootTransform = vessel.rootPart.transform;
                    if (rootTransform != null && candidate.collider.transform != null && candidate.collider.transform.IsChildOf(rootTransform))
                    {
                        continue;
                    }
                }

                Rigidbody hitBody = candidate.rigidbody;
                if (hitBody != null)
                {
                    Part rbPart = hitBody.GetComponentInParent<Part>();
                    if (rbPart != null && rbPart.vessel == vessel)
                    {
                        continue;
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
    }
}


