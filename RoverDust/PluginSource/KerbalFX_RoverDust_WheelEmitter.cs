using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoverDustFX
{
    internal sealed class WheelDustEmitter
    {
        private readonly Part part;
        private readonly WheelCollider wheel;
        private readonly GameObject root;
        private readonly ParticleSystem particleSystem;
        private readonly string debugId;

        private float smoothedRate;
        private float debugTimer;
        private float colorRefreshTimer;
        private float profileRefreshTimer;
        private float lightRefreshTimer;
        private float cachedLightFactor = 1f;
        private float wheelDustRateScale = 1f;
        private float wheelEffectiveRadius = 0.35f;
        private bool advancedQualityFeatures;
        private bool disposed;
        private bool colorInitialized;
        private bool lastSuppressed;
        private int appliedUiRevision = -1;
        private int appliedRuntimeRevision = -1;
        private string lastSuppressionKey = string.Empty;
        private int lastSurfaceColliderId = int.MinValue;
        private string lastSurfaceBody = string.Empty;
        private string lastSurfaceBiome = string.Empty;
        private Color currentColor = new Color(0.70f, 0.66f, 0.58f, 1f);

        private const float BaseDustAlpha = 0.82f;
        private static readonly string[] KscSurfaceTokens = { "runway", "launchpad", "launch_pad", "launch pad", "crawlerway", "launchsite", "launch_site" };
        private static readonly string[] KscMaterialTokens = { "runway", "launchpad", "crawlerway" };
        private static readonly string[] KerbalKonstructsTokens = { "kerbalkonstructs", "staticobject" };
        private static readonly string[] LampTokens = { "headlamp", "headlight", "floodlight", "spotlight", "searchlight", "lamp", "projector" };
        private static readonly string[] WheelLikeTokens = { "wheel", "tire", "track", "bogie", "roller" };
        private static readonly List<Light> sharedSceneLights = new List<Light>();
        private static readonly List<Component> sharedComponentBuffer = new List<Component>(24);
        private static float sharedSceneLightsRefreshAt;
        private static int sharedSceneLightsRefreshFrame = -1;
        private static Guid sharedSceneLightsActiveVesselId = Guid.Empty;

        public WheelDustEmitter(Part part, WheelCollider wheel)
        {
            this.part = part;
            this.wheel = wheel;
            debugId = part.partInfo.name + ":" + wheel.name;

            root = new GameObject("RoverDustFXEmitter");
            root.transform.parent = part.transform;
            root.transform.position = wheel.transform.position;
            root.layer = part.gameObject.layer;

            particleSystem = root.AddComponent<ParticleSystem>();

            ConfigureParticleSystemBase();
            ApplyRuntimeVisualProfile(true);
        }

        public void Tick(Vessel vessel, float dt)
        {
            if (disposed || wheel == null || part == null)
            {
                return;
            }

            profileRefreshTimer -= dt;
            if (profileRefreshTimer <= 0f
                || appliedUiRevision != RoverDustConfig.Revision
                || appliedRuntimeRevision != KerbalFxRuntimeConfig.Revision)
            {
                profileRefreshTimer = 0.33f;
                ApplyRuntimeVisualProfile(false);
            }

            WheelHit hit;
            bool hasHit = wheel.GetGroundHit(out hit) && hit.collider != null;
            if (!hasHit)
            {
                SetTargetRate(0f, dt);
                return;
            }

            string suppressionKey;
            bool suppressed = ShouldSuppressDustSurface(hit.collider, out suppressionKey);
            if (suppressed)
            {
                if ((!lastSuppressed || suppressionKey != lastSuppressionKey) && RoverDustConfig.DebugLogging && vessel == FlightGlobals.ActiveVessel)
                {
                    RoverDustLog.DebugLog(RoverDustLoc.Format(RoverDustLoc.LogSuppressed, debugId, suppressionKey));
                }

                lastSuppressed = true;
                lastSuppressionKey = suppressionKey;
                SetTargetRate(0f, dt);
                return;
            }

            lastSuppressed = false;
            lastSuppressionKey = string.Empty;

            float speed = Mathf.Abs((float)vessel.srfSpeed);
            float slip = Mathf.Abs(hit.forwardSlip) + Mathf.Abs(hit.sidewaysSlip);

            float speedFactor = Mathf.InverseLerp(0.7f, 20f, speed);
            float slipBoost = Mathf.Clamp01(slip * 2.4f);

            float quality = RoverDustConfig.QualityPercent / 100f;
            float qualityRateScale = 1f + (Mathf.Pow(quality, 1.60f) - 1f) * 0.75f;
            float bodyVisibility = GetBodyDustVisibilityMultiplier(vessel);
            float baseRate = (120f + 480f * speedFactor) * (0.45f + 0.55f * slipBoost) * KerbalFxRuntimeConfig.EmissionMultiplier;
            float targetRate = baseRate * qualityRateScale * wheelDustRateScale * bodyVisibility;
            bool useLightAware = advancedQualityFeatures && RoverDustConfig.UseLightAware;

            Vector3 stableNormal = GetStableGroundNormal(vessel, hit.point, hit.normal);
            RefreshLightingState(vessel, hit.point, stableNormal, dt);
            if (useLightAware)
            {
                float light = Mathf.Clamp01(cachedLightFactor);
                float lightRateFactor = Mathf.Pow(light, KerbalFxRuntimeConfig.LightRateExponent);
                if (light > 0.001f)
                {
                    lightRateFactor = Mathf.Lerp(KerbalFxRuntimeConfig.DaylightRateFloor, 1f, lightRateFactor);
                }
                targetRate *= lightRateFactor;
                if (light < 0.025f)
                {
                    targetRate = 0f;
                }
            }

            if (speed < 0.7f)
            {
                targetRate = 0f;
            }

            SetTargetRate(targetRate, dt);

            root.transform.position = hit.point + stableNormal * 0.04f;
            root.transform.rotation = Quaternion.LookRotation(part.transform.forward, stableNormal);

            colorRefreshTimer -= dt;
            if (colorRefreshTimer <= 0f)
            {
                colorRefreshTimer = 0.3f;
                UpdateSurfaceColor(vessel, hit);
            }

            if (RoverDustConfig.DebugLogging && vessel == FlightGlobals.ActiveVessel)
            {
                debugTimer -= dt;
                if (debugTimer <= 0f)
                {
                    debugTimer = 1.2f;
                    RoverDustLog.DebugLog(RoverDustLoc.Format(
                        RoverDustLoc.LogEmitter,
                        debugId,
                        hasHit,
                        speed.ToString("F2"),
                        slip.ToString("F3"),
                        smoothedRate.ToString("F1"),
                        currentColor.r.ToString("F2") + "," + currentColor.g.ToString("F2") + "," + currentColor.b.ToString("F2")
                        + " L=" + cachedLightFactor.ToString("F2")
                        + " W=" + wheelDustRateScale.ToString("F2")
                        + " R=" + wheelEffectiveRadius.ToString("F2")
                        + " B=" + bodyVisibility.ToString("F2")
                    ));
                }
            }
        }

        public void StopEmission()
        {
            SetTargetRate(0f, 0.12f);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (particleSystem != null)
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        private void ConfigureParticleSystemBase()
        {
            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(-3.14159f, 3.14159f);
            main.startColor = new Color(currentColor.r, currentColor.g, currentColor.b, BaseDustAlpha);

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 2f;

            Material material = RoverDustAssets.GetSharedMaterial();
            if (material != null)
            {
                renderer.material = material;
            }
        }

        private void ApplyRuntimeVisualProfile(bool force)
        {
            if (!force
                && appliedUiRevision == RoverDustConfig.Revision
                && appliedRuntimeRevision == KerbalFxRuntimeConfig.Revision)
            {
                return;
            }

            appliedUiRevision = RoverDustConfig.Revision;
            appliedRuntimeRevision = KerbalFxRuntimeConfig.Revision;

            int qualityPercent = Mathf.Clamp(RoverDustConfig.QualityPercent, 25, 200);
            float quality = qualityPercent / 100f;
            float qualityNorm = Mathf.InverseLerp(0.25f, 2.0f, quality);
            float qualityParticleScale = 1f + (Mathf.Pow(quality, 1.70f) - 1f) * 0.75f;
            float qualitySizeScale = Mathf.Lerp(0.76f, 1.36f, qualityNorm);
            float bodyVisibility = GetBodyDustVisibilityMultiplier(part != null ? part.vessel : null);
            float bodyBoostNorm = Mathf.Clamp01((bodyVisibility - 1f) / 0.5f);
            float bodyParticleScale = Mathf.Lerp(1f, 1.28f, bodyBoostNorm);
            float bodySizeScale = Mathf.Lerp(1f, 1.16f, bodyBoostNorm);
            advancedQualityFeatures = qualityPercent >= 100;
            wheelEffectiveRadius = GetEffectiveWheelRadius(wheel, part);
            wheelDustRateScale = advancedQualityFeatures ? GetWheelDustRateScale(wheelEffectiveRadius) : 1f;
            if (!advancedQualityFeatures)
            {
                cachedLightFactor = 1f;
            }

            ParticleSystem.MainModule main = particleSystem.main;
            float maxParticlesBase = 760f * qualityParticleScale * KerbalFxRuntimeConfig.MaxParticlesMultiplier;
            main.maxParticles = Mathf.RoundToInt(Mathf.Clamp(maxParticlesBase * wheelDustRateScale * bodyParticleScale, 220f, 4600f));

            float minSize = 0.036f * qualitySizeScale * bodySizeScale;
            float maxSize = 0.102f * qualitySizeScale * bodySizeScale;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);

            float minLifetime = 0.21f * Mathf.Lerp(0.98f, 1.18f, qualityNorm);
            float maxLifetime = 0.64f * Mathf.Lerp(0.98f, 1.20f, qualityNorm);
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.60f, Mathf.Lerp(1.9f, 3.4f, qualityNorm));
            main.gravityModifier = Mathf.Lerp(0.014f, 0.024f, qualityNorm);

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.angle = Mathf.Lerp(11.5f, 16.5f, qualityNorm);
            float wheelRadiusVisual = wheelEffectiveRadius;
            float radiusScale = advancedQualityFeatures ? Mathf.Clamp(Mathf.Lerp(0.90f, 1.70f, Mathf.InverseLerp(0.22f, 1.05f, wheelRadiusVisual)), 0.90f, 1.80f) * KerbalFxRuntimeConfig.RadiusScaleMultiplier : 1f;
            shape.radius = Mathf.Lerp(0.062f, 0.132f, qualityNorm) * radiusScale * bodySizeScale;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(Mathf.Lerp(0.66f, 0.84f, qualityNorm), 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = quality >= 0.50f;
            if (sizeOverLifetime.enabled)
            {
                AnimationCurve curve = new AnimationCurve();
                curve.AddKey(0f, 0.95f);
                curve.AddKey(0.55f, 1.40f);
                curve.AddKey(1f, 0.35f);
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);
            }

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.maxParticleSize = Mathf.Lerp(0.11f, 0.19f, qualityNorm);
            ApplyCurrentStartColor();

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            if (smoothedRate < 0.01f)
            {
                emission.rateOverTime = 0f;
            }

            if (RoverDustConfig.DebugLogging)
            {
                RoverDustLog.DebugLog(RoverDustLoc.Format(
                    RoverDustLoc.LogProfile,
                    debugId,
                    qualityPercent,
                    main.maxParticles,
                    minSize.ToString("F3"),
                    maxSize.ToString("F3")
                ));
            }
        }

        private void SetTargetRate(float targetRate, float dt)
        {
            if (particleSystem == null)
            {
                return;
            }

            float lerpSpeed = Mathf.Clamp01(dt * 6.5f);
            smoothedRate = Mathf.Lerp(smoothedRate, targetRate, lerpSpeed);

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = smoothedRate;

            if (smoothedRate > 0.18f)
            {
                if (!particleSystem.isPlaying)
                {
                    particleSystem.Play(true);
                }
            }
            else if (particleSystem.isPlaying)
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void RefreshLightingState(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal, float dt)
        {
            if (!advancedQualityFeatures || !RoverDustConfig.UseLightAware)
            {
                if (cachedLightFactor != 1f)
                {
                    cachedLightFactor = 1f;
                    ApplyCurrentStartColor();
                }

                return;
            }

            lightRefreshTimer -= dt;
            if (lightRefreshTimer > 0f)
            {
                return;
            }

            lightRefreshTimer = 0.20f;
            float newLightFactor = EvaluateCombinedLighting(vessel, worldPoint, surfaceNormal);
            if (Mathf.Abs(newLightFactor - cachedLightFactor) > 0.02f)
            {
                cachedLightFactor = newLightFactor;
                ApplyCurrentStartColor();
            }
        }

        private void ApplyCurrentStartColor()
        {
            if (particleSystem == null)
            {
                return;
            }

            float alpha = BaseDustAlpha;
            if (advancedQualityFeatures && RoverDustConfig.UseLightAware)
            {
                float light = Mathf.Clamp01(cachedLightFactor);
                if (light < 0.14f)
                {
                    alpha = 0f;
                }
                else
                {
                    float alphaLight = Mathf.Pow(light, KerbalFxRuntimeConfig.LightAlphaExponent);
                    alphaLight = Mathf.Lerp(KerbalFxRuntimeConfig.DaylightAlphaFloor, 1f, alphaLight);
                    alpha *= alphaLight;
                }
            }

            float bodyVisibility = GetBodyDustVisibilityMultiplier(part != null ? part.vessel : null);
            float bodyBoostNorm = Mathf.Clamp01((bodyVisibility - 1f) / 0.5f);
            alpha *= Mathf.Lerp(1f, 1.16f, bodyBoostNorm);
            alpha = Mathf.Clamp(alpha, 0f, 0.97f);

            ParticleSystem.MainModule main = particleSystem.main;
            main.startColor = new Color(currentColor.r, currentColor.g, currentColor.b, alpha);
        }

        private static float EvaluateCombinedLighting(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal)
        {
            RefreshSharedSceneLights();

            float sunLight = EvaluateSunLighting(vessel, worldPoint, surfaceNormal);
            float artificialLight = EvaluateNearbyArtificialLights(worldPoint, surfaceNormal);

            if (sunLight <= 0.001f && artificialLight < 0.055f)
            {
                return 0f;
            }

            float combined = Mathf.Max(sunLight, artificialLight);
            if (combined < KerbalFxRuntimeConfig.MinCombinedLight)
            {
                return 0f;
            }

            return Mathf.Clamp01(combined);
        }

        private static float EvaluateSunLighting(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal)
        {
            Vector3 safeNormal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.up;

            float directionalBest = 0f;
            for (int i = 0; i < sharedSceneLights.Count; i++)
            {
                float strength = EvaluateSingleDirectionalSunLight(sharedSceneLights[i], safeNormal);
                if (strength > directionalBest)
                {
                    directionalBest = strength;
                }
            }

            float geometricSun = 0f;
            Vector3 sunDirection;
            if (TryGetSunDirection(worldPoint, out sunDirection))
            {
                float sunDot = Vector3.Dot(safeNormal, sunDirection);
                if (sunDot > 0f)
                {
                    geometricSun = Mathf.Lerp(0.20f, 1f, Mathf.Clamp01(sunDot));
                }
            }

            bool isDayAtPoint = geometricSun > 0.001f;
            float best = Mathf.Max(directionalBest, geometricSun);

            if (best <= 0.01f && vessel != null && vessel.directSunlight)
            {
                best = 0.90f;
                isDayAtPoint = true;
            }

            if (!isDayAtPoint)
            {
                return 0f;
            }

            if (vessel != null && !vessel.directSunlight)
            {
                float shadowed = best * KerbalFxRuntimeConfig.ShadowLightFactor;
                float cloudyDayFloor = geometricSun * 0.22f;
                best = Mathf.Max(shadowed, cloudyDayFloor);
            }

            return Mathf.Clamp01(best);
        }

        private static bool TryGetSunDirection(Vector3 worldPoint, out Vector3 sunDirection)
        {
            sunDirection = Vector3.zero;

            CelestialBody sun = null;
            if (Planetarium.fetch != null)
            {
                sun = Planetarium.fetch.Sun;
            }

            if (sun == null && FlightGlobals.Bodies != null && FlightGlobals.Bodies.Count > 0)
            {
                for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
                {
                    CelestialBody body = FlightGlobals.Bodies[i];
                    if (body != null && !string.IsNullOrEmpty(body.bodyName) && body.bodyName.IndexOf("sun", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        sun = body;
                        break;
                    }
                }

                if (sun == null)
                {
                    sun = FlightGlobals.Bodies[0];
                }
            }

            if (sun == null)
            {
                return false;
            }

            Vector3d toSun = sun.position - (Vector3d)worldPoint;
            if (toSun.sqrMagnitude < 1e-8)
            {
                return false;
            }

            sunDirection = ((Vector3)toSun).normalized;
            return sunDirection.sqrMagnitude > 0.0001f;
        }

        private static float EvaluateNearbyArtificialLights(Vector3 worldPoint, Vector3 surfaceNormal)
        {
            float best = 0f;
            for (int i = 0; i < sharedSceneLights.Count; i++)
            {
                float strength = EvaluateSingleArtificialLight(sharedSceneLights[i], worldPoint, surfaceNormal);
                if (strength > best)
                {
                    best = strength;
                }
            }

            return Mathf.Clamp01(best);
        }

        private static float EvaluateSingleDirectionalSunLight(Light light, Vector3 surfaceNormal)
        {
            if (light == null || !light.enabled || light.intensity <= 0.03f || !light.gameObject.activeInHierarchy)
            {
                return 0f;
            }

            if (light.type != LightType.Directional || light.transform == null)
            {
                return 0f;
            }

            Vector3 toLightDir = (-light.transform.forward).normalized;
            float normalDot = Mathf.Clamp01(Vector3.Dot(surfaceNormal.normalized, toLightDir));
            float strength = light.intensity * Mathf.Lerp(0.35f, 1f, normalDot);
            return Mathf.Clamp01(strength / 1.05f);
        }

        private static void RefreshSharedSceneLights()
        {
            int frame = Time.frameCount;
            if (sharedSceneLightsRefreshFrame == frame)
            {
                return;
            }
            sharedSceneLightsRefreshFrame = frame;

            Vessel activeVessel = FlightGlobals.ActiveVessel;
            Guid activeVesselId = activeVessel != null ? activeVessel.id : Guid.Empty;
            if (activeVesselId != sharedSceneLightsActiveVesselId)
            {
                sharedSceneLightsActiveVesselId = activeVesselId;
                sharedSceneLightsRefreshAt = 0f;
            }

            if (!RoverDustConfig.UseLightAware)
            {
                if (sharedSceneLights.Count > 0)
                {
                    sharedSceneLights.Clear();
                }

                sharedSceneLightsRefreshAt = 0f;
                return;
            }

            if (Time.time < sharedSceneLightsRefreshAt)
            {
                return;
            }

            sharedSceneLights.Clear();

            Light[] foundLights = UnityEngine.Object.FindObjectsOfType<Light>();
            if (foundLights == null)
            {
                sharedSceneLightsRefreshAt = Time.time + 5.0f;
                return;
            }

            int kept = 0;
            for (int i = 0; i < foundLights.Length; i++)
            {
                Light light = foundLights[i];
                if (light == null || !light.enabled || !light.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (light.type == LightType.Directional)
                {
                    sharedSceneLights.Add(light);
                    kept++;
                    continue;
                }

                if ((light.type == LightType.Point || light.type == LightType.Spot)
                    && light.intensity > 0.01f
                    && light.range > 0.50f)
                {
                    sharedSceneLights.Add(light);
                    kept++;
                }
            }

            if (kept > 220)
            {
                sharedSceneLightsRefreshAt = Time.time + 7.0f;
            }
            else if (kept > 120)
            {
                sharedSceneLightsRefreshAt = Time.time + 6.0f;
            }
            else
            {
                sharedSceneLightsRefreshAt = Time.time + 4.5f;
            }
        }

        private static float EvaluateSingleArtificialLight(Light light, Vector3 worldPoint, Vector3 surfaceNormal)
        {
            if (light == null || !light.enabled || light.intensity <= 0.01f || !light.gameObject.activeInHierarchy)
            {
                return 0f;
            }

            if ((light.type != LightType.Point && light.type != LightType.Spot) || light.transform == null)
            {
                return 0f;
            }

            if (!IsLikelyIntentionalLampLight(light))
            {
                return 0f;
            }

            float range = light.range;
            if (range <= 0.01f)
            {
                return 0f;
            }

            Vector3 pointToLight = light.transform.position - worldPoint;
            float distance = pointToLight.magnitude;
            if (distance > range)
            {
                return 0f;
            }

            Vector3 toLightDir = distance > 0.001f ? pointToLight / distance : Vector3.up;
            float attenuation = Mathf.Clamp01(1f - distance / range);
            attenuation *= attenuation;

            float spotFactor = 1f;
            if (light.type == LightType.Spot)
            {
                float cosLimit = Mathf.Cos(light.spotAngle * 0.5f * Mathf.Deg2Rad);
                float cosAngle = Vector3.Dot(light.transform.forward, -toLightDir);
                if (cosAngle <= cosLimit)
                {
                    return 0f;
                }

                spotFactor = Mathf.InverseLerp(cosLimit, 1f, cosAngle);
            }

            float normalDot = Mathf.Clamp01(Vector3.Dot(surfaceNormal.normalized, toLightDir));
            float normalFactor = Mathf.Lerp(0.40f, 1f, normalDot);

            float strength = light.intensity * attenuation * spotFactor * normalFactor;
            return Mathf.Clamp01(strength / 1.45f);
        }

        private static bool IsLikelyIntentionalLampLight(Light light)
        {
            if (light == null || light.transform == null)
            {
                return false;
            }

            bool fromPart = light.GetComponentInParent<Part>() != null;
            if (fromPart)
            {
                if (light.range < 1.20f || light.intensity < 0.10f)
                {
                    return false;
                }

                if (ContainsAnyToken(light.name, LampTokens)
                    || ContainsAnyTokenInTransformHierarchy(light.transform, LampTokens, 12))
                {
                    return true;
                }

                return light.range <= 85f && light.intensity <= 8.5f;
            }

            if (light.range > 120f || light.intensity < 0.35f)
            {
                return false;
            }

            return ContainsAnyToken(light.name, LampTokens)
                || ContainsAnyTokenInTransformHierarchy(light.transform, LampTokens, 12);
        }

        private static Vector3 GetStableGroundNormal(Vessel vessel, Vector3 worldPoint, Vector3 hitNormal)
        {
            Vector3 normal = hitNormal;
            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = Vector3.up;
            }

            normal.Normalize();

            if (vessel != null && vessel.mainBody != null)
            {
                Vector3 bodyUp = (worldPoint - vessel.mainBody.position);
                if (bodyUp.sqrMagnitude > 0.0001f)
                {
                    bodyUp.Normalize();
                    normal = Vector3.Slerp(normal, bodyUp, 0.70f);
                }
            }

            return normal.normalized;
        }

        private static float GetWheelDustRateScale(float effectiveRadius)
        {
            float normalized = Mathf.Clamp(effectiveRadius / 0.30f, 0.9f, 3.8f);
            float baseScale = Mathf.Pow(normalized, KerbalFxRuntimeConfig.WheelBoostPower);
            float amplifiedScale = 1f + (baseScale - 1f) * 1.25f;
            return Mathf.Clamp(amplifiedScale, 1f, KerbalFxRuntimeConfig.WheelBoostMax * 1.25f);
        }

        private static float GetEffectiveWheelRadius(WheelCollider wheelCollider, Part sourcePart)
        {
            if (wheelCollider == null)
            {
                return 0.35f;
            }

            float radius = Mathf.Max(0.05f, wheelCollider.radius);
            if (wheelCollider.transform != null)
            {
                Vector3 scale = wheelCollider.transform.lossyScale;
                float axisScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                if (axisScale > 0.001f)
                {
                    radius = Mathf.Max(radius, wheelCollider.radius * axisScale);
                }
            }

            float visualRadius = EstimateVisualWheelRadius(sourcePart, wheelCollider.transform != null ? wheelCollider.transform.position : Vector3.zero);
            if (visualRadius > 0.01f)
            {
                radius = Mathf.Max(radius, visualRadius);
            }

            return Mathf.Clamp(radius, 0.05f, 2.4f);
        }

        private static float EstimateVisualWheelRadius(Part sourcePart, Vector3 wheelWorldPosition)
        {
            if (sourcePart == null)
            {
                return 0f;
            }

            Renderer[] renderers = sourcePart.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return 0f;
            }

            float bestScore = float.MaxValue;
            float bestRadius = 0f;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                float extent = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
                if (extent < 0.03f || extent > 2.8f)
                {
                    continue;
                }

                float dist = Vector3.Distance(bounds.center, wheelWorldPosition);
                bool wheelLike = ContainsAnyToken(renderer.name, WheelLikeTokens)
                    || (renderer.transform != null && ContainsAnyToken(renderer.transform.name, WheelLikeTokens));

                if (!wheelLike && dist > 1.15f)
                {
                    continue;
                }

                float score = dist + (wheelLike ? 0f : 0.75f);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestRadius = extent;
                }
            }

            return bestRadius;
        }

        private void UpdateSurfaceColor(Vessel vessel, WheelHit hit)
        {
            if (!RoverDustConfig.AdaptSurfaceColor)
            {
                ApplyColor(new Color(0.70f, 0.66f, 0.58f));
                return;
            }

            if (colorInitialized && IsSameSurfaceSignature(vessel, hit.collider))
            {
                return;
            }
            UpdateSurfaceSignature(vessel, hit.collider);

            Color baseColor = GuessDustColor(vessel);
            Color newColor = baseColor;
            Color colliderColor;
            if (TryGetColliderColor(hit.collider, out colliderColor))
            {
                newColor = BlendWithColliderColor(baseColor, colliderColor);
            }

            ApplyColor(newColor);
        }

        private void ApplyColor(Color color)
        {
            Color target = new Color(
                Mathf.Clamp01(color.r),
                Mathf.Clamp01(color.g),
                Mathf.Clamp01(color.b),
                1f
            );

            target = NormalizeDustTone(target);
            currentColor = colorInitialized ? Color.Lerp(currentColor, target, 0.45f) : target;
            colorInitialized = true;

            ApplyCurrentStartColor();
        }

        private static bool TryGetColliderColor(Collider collider, out Color color)
        {
            color = Color.white;
            if (collider == null)
            {
                return false;
            }

            Renderer renderer = collider.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null)
            {
                return false;
            }

            if (!renderer.sharedMaterial.HasProperty("_Color"))
            {
                return false;
            }

            color = renderer.sharedMaterial.color;
            return true;
        }

        private static bool ShouldSuppressDustSurface(Collider collider, out string reason)
        {
            reason = string.Empty;
            if (collider == null)
            {
                return false;
            }

            if (ContainsAnyToken(collider.name, KscSurfaceTokens)
                || ContainsAnyToken(collider.gameObject != null ? collider.gameObject.name : string.Empty, KscSurfaceTokens)
                || ContainsAnyTokenInTransformHierarchy(collider.transform, KscSurfaceTokens, 14))
            {
                reason = "KSC_Surface";
                return true;
            }

            Renderer renderer = collider.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                if (ContainsAnyToken(renderer.sharedMaterial.name, KscMaterialTokens))
                {
                    reason = "KSC_Material";
                    return true;
                }
            }

            if (IsKerbalKonstructsStatic(collider))
            {
                reason = "KerbalKonstructs_Static";
                return true;
            }

            return false;
        }

        private static bool IsKerbalKonstructsStatic(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            Transform t = collider.transform;
            int depth = 0;
            while (t != null && depth < 10)
            {
                sharedComponentBuffer.Clear();
                t.GetComponents<Component>(sharedComponentBuffer);
                if (ContainsAnyTokenInTypes(sharedComponentBuffer, KerbalKonstructsTokens))
                {
                    return true;
                }

                t = t.parent;
                depth++;
            }

            return false;
        }

        private static bool ContainsAnyTokenInTypes(List<Component> components, string[] tokens)
        {
            if (components == null || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < components.Count; i++)
            {
                Component c = components[i];
                if (c == null)
                {
                    continue;
                }

                Type type = c.GetType();
                if (type == null)
                {
                    continue;
                }

                string fullName = type.FullName;
                if (!string.IsNullOrEmpty(fullName) && ContainsAnyToken(fullName, tokens))
                {
                    return true;
                }

                if (ContainsAnyToken(type.Name, tokens))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsAnyTokenInTransformHierarchy(Transform transform, string[] tokens, int maxDepth)
        {
            if (transform == null || tokens == null || maxDepth <= 0)
            {
                return false;
            }

            Transform current = transform;
            int depth = 0;
            while (current != null && depth < maxDepth)
            {
                if (ContainsAnyToken(current.name, tokens))
                {
                    return true;
                }

                current = current.parent;
                depth++;
            }

            return false;
        }

        private static bool ContainsAnyToken(string text, string[] tokens)
        {
            if (string.IsNullOrEmpty(text) || tokens == null)
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token) && text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSameSurfaceSignature(Vessel vessel, Collider collider)
        {
            int colliderId = collider != null ? collider.GetInstanceID() : 0;
            string body = vessel.mainBody != null ? vessel.mainBody.bodyName : "UnknownBody";
            string biome = string.IsNullOrEmpty(vessel.landedAt) ? "UnknownBiome" : vessel.landedAt;
            return colliderId == lastSurfaceColliderId
                && string.Equals(body, lastSurfaceBody, StringComparison.Ordinal)
                && string.Equals(biome, lastSurfaceBiome, StringComparison.Ordinal);
        }

        private void UpdateSurfaceSignature(Vessel vessel, Collider collider)
        {
            lastSurfaceColliderId = collider != null ? collider.GetInstanceID() : 0;
            lastSurfaceBody = vessel.mainBody != null ? vessel.mainBody.bodyName : "UnknownBody";
            lastSurfaceBiome = string.IsNullOrEmpty(vessel.landedAt) ? "UnknownBiome" : vessel.landedAt;
        }

        private static float GetBodyDustVisibilityMultiplier(Vessel vessel)
        {
            if (vessel == null || vessel.mainBody == null || string.IsNullOrEmpty(vessel.mainBody.bodyName))
            {
                return 1f;
            }

            return KerbalFxRuntimeConfig.GetBodyVisibilityMultiplier(vessel.mainBody.bodyName);
        }

        private static Color GuessDustColor(Vessel vessel)
        {
            string key = vessel.mainBody != null
                ? vessel.mainBody.bodyName.ToLowerInvariant()
                : string.Empty;

            if (key.Contains("minmus"))
            {
                return new Color(0.73f, 0.80f, 0.74f);
            }
            if (key.Contains("mun"))
            {
                return new Color(0.75f, 0.73f, 0.69f);
            }
            if (key.Contains("duna"))
            {
                return new Color(0.70f, 0.46f, 0.31f);
            }
            if (key.Contains("eve"))
            {
                return new Color(0.77f, 0.71f, 0.60f);
            }
            if (key.Contains("moho"))
            {
                return new Color(0.63f, 0.56f, 0.50f);
            }
            if (key.Contains("gilly"))
            {
                return new Color(0.62f, 0.58f, 0.52f);
            }
            if (key.Contains("bop"))
            {
                return new Color(0.60f, 0.52f, 0.45f);
            }
            if (key.Contains("pol"))
            {
                return new Color(0.66f, 0.64f, 0.62f);
            }
            if (key.Contains("tylo"))
            {
                return new Color(0.67f, 0.67f, 0.66f);
            }
            if (key.Contains("vall"))
            {
                return new Color(0.70f, 0.72f, 0.74f);
            }
            if (key.Contains("eeloo"))
            {
                return new Color(0.74f, 0.75f, 0.77f);
            }
            if (key.Contains("kerbin"))
            {
                return new Color(0.67f, 0.61f, 0.53f);
            }

            return new Color(0.70f, 0.66f, 0.58f);
        }

        private static Color BlendWithColliderColor(Color baseColor, Color colliderColor)
        {
            float h;
            float s;
            float v;
            Color.RGBToHSV(colliderColor, out h, out s, out v);

            s = Mathf.Clamp(s, 0.05f, 0.35f);
            v = Mathf.Clamp(v, 0.20f, 0.86f);

            if (h > 0.20f && h < 0.45f)
            {
                h = Mathf.Lerp(h, 0.11f, 0.45f);
                s *= 0.45f;
                v *= 0.92f;
            }

            Color tunedCollider = Color.HSVToRGB(h, s, v);
            return Color.Lerp(baseColor, tunedCollider, 0.16f);
        }

        private static Color NormalizeDustTone(Color input)
        {
            float h;
            float s;
            float v;
            Color.RGBToHSV(input, out h, out s, out v);

            s = Mathf.Clamp(s, 0.12f, 0.40f);
            v = Mathf.Clamp(v, 0.24f, 0.88f);

            if (h > 0.20f && h < 0.45f)
            {
                h = Mathf.Lerp(h, 0.12f, 0.30f);
                s *= 0.72f;
            }

            return Color.HSVToRGB(h, s, v);
        }

    }
}

