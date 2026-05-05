using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace KerbalFX
{
    internal struct KerbalFxLightSample
    {
        public float PerceivedBrightness;
        public float DirectSun;
        public float CosSun;
        public float SunLuma;
        public float Ambient;
        public float LocalLights;
        public Color DominantColor;
        public bool IsTwilight;
        public bool IsShadowed;

        public static KerbalFxLightSample Daylight
        {
            get
            {
                return new KerbalFxLightSample
                {
                    PerceivedBrightness = 1f,
                    DirectSun = 1f,
                    CosSun = 1f,
                    SunLuma = 1f,
                    Ambient = 0.30f,
                    LocalLights = 0f,
                    DominantColor = Color.white,
                    IsTwilight = false,
                    IsShadowed = false
                };
            }
        }
    }

    internal struct KerbalFxLightAwareEntry
    {
        public float DarkScale;
        public float BrightScale;
        public float TwilightFloor;
        public float MinPerceived;
        public float ColorTintStrength;

        public static KerbalFxLightAwareEntry Default
        {
            get
            {
                return new KerbalFxLightAwareEntry
                {
                    DarkScale = 0.20f,
                    BrightScale = 1f,
                    TwilightFloor = 0.42f,
                    MinPerceived = 0.06f,
                    ColorTintStrength = 0.30f
                };
            }
        }
    }

    internal static class KerbalFxLightingCore
    {
        public const float NominalSunLuma = 1.0f;
        public const float ShadowProbeDistance = 35f;
        public const float ShadowProbeNormalOffset = 0.40f;
        public const float ShadowProbeMinCos = 0.25f;
        public const float ShadowResidualBleed = 0.08f;
        public const float ShadowConeAngleDeg = 3.5f;
        public const float TwilightDirectThreshold = 0.05f;
        public const float TwilightPerceivedFloor = 0.10f;
        public const float TwilightPerceivedCeiling = 0.50f;
        public const float AmbientNoiseFloor = 0.025f;
        public const float PerceptualGammaExponent = 0.62f;
        public const float RgbDimFloor = 0.08f;
        public const float LocalLightWeight = 0.18f;
        public const float LocalLightMaxRangeSq = 60f * 60f;
        public const float SurfaceBoostFloor = 0.08f;
        public const float SurfaceBoostExponent = 0.22f;
        public const float AtmoDimExponent = 1.85f;
        public const float RateLightMinMultiplier = 0.50f;
        private const float LumaR = 0.2126f;
        private const float LumaG = 0.7152f;
        private const float LumaB = 0.0722f;
        private const int ScaledSceneryLayer = 10;
        private static readonly int ShadowProbeLayerMask = Physics.DefaultRaycastLayers & ~(1 << ScaledSceneryLayer);
        private static readonly RaycastHit[] ShadowHits = new RaycastHit[8];

        public static int ShadowProbeMask { get { return ShadowProbeLayerMask; } }

        public static float Luminance(Color c)
        {
            return Mathf.Max(0f, c.r) * LumaR + Mathf.Max(0f, c.g) * LumaG + Mathf.Max(0f, c.b) * LumaB;
        }

        public static float PerceptualFromLinear(float linearLuma)
        {
            return Mathf.Pow(Mathf.Max(0f, linearLuma), PerceptualGammaExponent);
        }

        public static float RemapVisualAlpha(float alphaMultiplier, float floor, float exponent)
        {
            float clamped = Mathf.Clamp01(alphaMultiplier);
            float safeExponent = Mathf.Max(0.0001f, exponent);
            if (floor > 0.0001f)
            {
                float t = Mathf.InverseLerp(floor, 1f, clamped);
                return Mathf.Pow(t, safeExponent);
            }
            return Mathf.Pow(clamped, safeExponent);
        }

        public static float ApplySurfaceBoost(float alphaMultiplier)
        {
            return RemapVisualAlpha(alphaMultiplier, SurfaceBoostFloor, SurfaceBoostExponent);
        }

        public static float ApplyAtmoDim(float alphaMultiplier)
        {
            return RemapVisualAlpha(alphaMultiplier, 0f, AtmoDimExponent);
        }

        public static float ApplyRateCap(float visualMultiplier)
        {
            return Mathf.Lerp(RateLightMinMultiplier, 1f, Mathf.Clamp01(visualMultiplier));
        }

        public static Vector3 GetSunDirection(Vector3 worldPoint, Vector3 fallback)
        {
            Light sunLight = RenderSettings.sun;
            if (sunLight != null && sunLight.enabled && sunLight.isActiveAndEnabled)
            {
                Vector3 forward = sunLight.transform.forward;
                if (forward.sqrMagnitude > 0.0001f)
                    return -forward.normalized;
            }

            Light brightestDirectional = FindBrightestDirectionalLight();
            if (brightestDirectional != null)
            {
                Vector3 forward = brightestDirectional.transform.forward;
                if (forward.sqrMagnitude > 0.0001f)
                    return -forward.normalized;
            }

            if (Planetarium.fetch != null && Planetarium.fetch.Sun != null)
            {
                Vector3 toSun = (Vector3)Planetarium.fetch.Sun.position - worldPoint;
                if (toSun.sqrMagnitude > 0.0001f)
                    return toSun.normalized;
            }
            return fallback;
        }

        private static Light FindBrightestDirectionalLight()
        {
            List<Light> directional = KerbalFxSceneLightCache.SnapshotDirectional();
            if (directional.Count == 0)
                return null;

            float bestLuma = 0f;
            Light best = null;
            for (int i = 0; i < directional.Count; i++)
            {
                Light light = directional[i];
                if (light == null || !light.enabled || !light.isActiveAndEnabled)
                    continue;
                float l = Luminance(light.color * Mathf.Max(0f, light.intensity));
                if (l <= bestLuma)
                    continue;
                bestLuma = l;
                best = light;
            }
            return best;
        }

        public static bool TryGetSunLight(out Color color, out float luminance)
        {
            color = Color.white;
            luminance = 0f;

            Light sun = RenderSettings.sun;
            if (sun != null && sun.enabled && sun.isActiveAndEnabled)
            {
                Color c = sun.color * Mathf.Max(0f, sun.intensity);
                float l = Luminance(c);
                if (l > 0.0001f)
                {
                    luminance = l;
                    color = c / l;
                    return true;
                }
            }

            List<Light> directional = KerbalFxSceneLightCache.SnapshotDirectional();
            if (directional.Count == 0)
                return false;

            float bestLuma = 0f;
            Color bestColor = Color.white;
            for (int i = 0; i < directional.Count; i++)
            {
                Light light = directional[i];
                if (light == null || !light.enabled || !light.isActiveAndEnabled)
                    continue;
                Color c = light.color * Mathf.Max(0f, light.intensity);
                float l = Luminance(c);
                if (l <= bestLuma)
                    continue;
                bestLuma = l;
                bestColor = l > 0.0001f ? c / l : Color.white;
            }

            if (bestLuma <= 0.0001f)
                return false;

            color = bestColor;
            luminance = bestLuma;
            return true;
        }

        public static bool RaycastShadowProbe(Vector3 origin, Vector3 direction)
        {
            Vector3 right = Vector3.Cross(direction, Vector3.up);
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.Cross(direction, Vector3.right);
            right.Normalize();
            Vector3 up = Vector3.Cross(direction, right).normalized;

            float angle = ShadowConeAngleDeg * Mathf.Deg2Rad;
            Vector3 offsetA = direction + right * Mathf.Tan(angle);
            Vector3 offsetB = direction + up * Mathf.Tan(angle);
            offsetA.Normalize();
            offsetB.Normalize();

            return RayHitsValidBlocker(origin, direction)
                && RayHitsValidBlocker(origin, offsetA)
                && RayHitsValidBlocker(origin, offsetB);
        }

        private static bool RayHitsValidBlocker(Vector3 origin, Vector3 direction)
        {
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                ShadowHits,
                ShadowProbeDistance,
                ShadowProbeLayerMask,
                QueryTriggerInteraction.Ignore);
            if (hitCount <= 0)
                return false;

            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = ShadowHits[i].collider;
                if (collider == null)
                    continue;
                if (collider.GetComponentInParent<Part>() != null)
                    continue;
                return true;
            }
            return false;
        }
    }

    internal static class KerbalFxSceneLightCache
    {
        private const float RefreshInterval = 0.5f;
        private const float DirectionalRefreshInterval = 0.25f;
        private static readonly List<Light> cachedPointSpot = new List<Light>(32);
        private static readonly List<Light> cachedDirectional = new List<Light>(8);
        private static float lastUpdateTime = -10f;
        private static float lastDirectionalUpdateTime = -10f;

        public static List<Light> Snapshot()
        {
            float now = Time.time;
            if (now - lastUpdateTime < RefreshInterval)
                return cachedPointSpot;

            lastUpdateTime = now;
            cachedPointSpot.Clear();
            CollectInto(cachedPointSpot, LightType.Point);
            CollectInto(cachedPointSpot, LightType.Spot);
            return cachedPointSpot;
        }

        public static List<Light> SnapshotDirectional()
        {
            float now = Time.time;
            if (now - lastDirectionalUpdateTime < DirectionalRefreshInterval)
                return cachedDirectional;

            lastDirectionalUpdateTime = now;
            cachedDirectional.Clear();
            CollectInto(cachedDirectional, LightType.Directional);
            return cachedDirectional;
        }

        private static void CollectInto(List<Light> target, LightType type)
        {
            Light[] lights = Light.GetLights(type, 0);
            if (lights == null)
                return;
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light != null && light.enabled && light.isActiveAndEnabled)
                    target.Add(light);
            }
        }
    }

    internal sealed class KerbalFxLightAwareProfile
    {
        private readonly string configNodeName;
        private readonly Func<string> pathProvider;
        private readonly Action<Dictionary<string, KerbalFxLightAwareEntry>> seedDefaults;
        private readonly Dictionary<string, KerbalFxLightAwareEntry> entries;
        private readonly KerbalFxLightAwareEntry moduleDefaults;
        private DateTime lastConfigWriteUtc = DateTime.MinValue;

        public KerbalFxLightAwareProfile(
            string configNodeName,
            Func<string> pathProvider,
            Action<Dictionary<string, KerbalFxLightAwareEntry>> seedDefaults,
            KerbalFxLightAwareEntry moduleDefaults)
        {
            this.configNodeName = configNodeName;
            this.pathProvider = pathProvider;
            this.seedDefaults = seedDefaults;
            this.moduleDefaults = moduleDefaults;
            this.entries = new Dictionary<string, KerbalFxLightAwareEntry>(16, StringComparer.OrdinalIgnoreCase);
        }

        public int Count { get { return entries.Count; } }

        public KerbalFxLightAwareEntry ModuleDefaults { get { return moduleDefaults; } }

        public void Refresh()
        {
            SeedDefaults();
            if (GameDatabase.Instance != null)
            {
                ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes(configNodeName);
                if (nodes != null)
                    for (int i = 0; i < nodes.Length; i++)
                        if (nodes[i] != null)
                            KerbalFxUtil.LoadLightAware(nodes[i], entries, moduleDefaults);
            }
            KerbalFxUtil.PrimeConfigFileStamp(GetPath(), ref lastConfigWriteUtc);
        }

        public bool TryHotReloadFromDisk(out string failure)
        {
            failure = null;
            string path = GetPath();
            if (!KerbalFxUtil.HasConfigFileChanged(path, ref lastConfigWriteUtc))
                return false;

            SeedDefaults();
            try
            {
                if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                {
                    ConfigNode root = ConfigNode.Load(path);
                    if (root != null)
                    {
                        ConfigNode[] nodes = root.GetNodes(configNodeName);
                        if (nodes != null)
                            for (int i = 0; i < nodes.Length; i++)
                                if (nodes[i] != null)
                                    KerbalFxUtil.LoadLightAware(nodes[i], entries, moduleDefaults);
                    }
                }
            }
            catch (Exception ex)
            {
                failure = ex.Message;
            }
            return true;
        }

        public KerbalFxLightAwareEntry Get(string bodyName)
        {
            if (!string.IsNullOrEmpty(bodyName) && entries.TryGetValue(bodyName.Trim(), out var entry))
                return entry;
            return moduleDefaults;
        }

        private string GetPath()
        {
            return pathProvider != null ? pathProvider() : null;
        }

        private void SeedDefaults()
        {
            entries.Clear();
            if (seedDefaults != null)
                seedDefaults(entries);
        }
    }

    internal sealed class KerbalFxLightAwareSampler
    {
        private readonly float refreshInterval;
        private readonly float smoothingSpeed;
        private readonly bool useShadowProbe;
        private readonly bool sampleLocalLights;
        private readonly bool useAerialHorizon;

        private KerbalFxLightSample target;
        private KerbalFxLightSample smoothed;
        private float refreshTimer;
        private bool hasSample;

        public KerbalFxLightAwareSampler(
            float refreshInterval,
            float smoothingSpeed,
            bool useShadowProbe,
            bool sampleLocalLights,
            bool useAerialHorizon = false)
        {
            this.refreshInterval = Mathf.Max(0.05f, refreshInterval);
            this.smoothingSpeed = Mathf.Max(0.5f, smoothingSpeed);
            this.useShadowProbe = useShadowProbe;
            this.sampleLocalLights = sampleLocalLights;
            this.useAerialHorizon = useAerialHorizon;
            this.target = KerbalFxLightSample.Daylight;
            this.smoothed = KerbalFxLightSample.Daylight;
        }

        public KerbalFxLightSample Current { get { return smoothed; } }

        public KerbalFxLightSample TargetSample { get { return target; } }

        public bool HasSample { get { return hasSample; } }

        public bool IsReset { get { return !hasSample; } }

        public void Reset()
        {
            target = KerbalFxLightSample.Daylight;
            smoothed = KerbalFxLightSample.Daylight;
            refreshTimer = 0f;
            hasSample = false;
        }

        public void Tick(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal, float dt)
        {
            refreshTimer -= dt;
            if (refreshTimer <= 0f || !hasSample)
            {
                refreshTimer = refreshInterval;
                target = ResolveSample(vessel, worldPoint, surfaceNormal);
                if (!hasSample)
                {
                    smoothed = target;
                    hasSample = true;
                    return;
                }
            }

            float lerpSpeed = Mathf.Clamp01(dt * smoothingSpeed);
            smoothed = LerpSample(smoothed, target, lerpSpeed);
        }

        public void ApplySnapshot(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal)
        {
            target = ResolveSample(vessel, worldPoint, surfaceNormal);
            smoothed = target;
            refreshTimer = refreshInterval;
            hasSample = true;
        }

        public float GetAlphaMultiplier(KerbalFxLightAwareEntry entry, float strength)
        {
            float baseMultiplier = ComputeBaseMultiplier(entry);
            return Mathf.Lerp(1f, baseMultiplier, Mathf.Clamp01(strength));
        }

        public Color ApplyColorTint(Color baseColor, KerbalFxLightAwareEntry entry, float strength)
        {
            return ApplyColorTint(baseColor, entry, strength, GetAlphaMultiplier(entry, strength));
        }

        public Color ApplyColorTint(Color baseColor, KerbalFxLightAwareEntry entry, float strength, float alphaMultiplier)
        {
            float blend = Mathf.Clamp01(entry.ColorTintStrength * strength);
            Color tinted = baseColor;
            if (blend > 0.0001f)
            {
                tinted = new Color(
                    Mathf.Lerp(baseColor.r, baseColor.r * smoothed.DominantColor.r, blend),
                    Mathf.Lerp(baseColor.g, baseColor.g * smoothed.DominantColor.g, blend),
                    Mathf.Lerp(baseColor.b, baseColor.b * smoothed.DominantColor.b, blend),
                    baseColor.a);
            }

            float rgbDim = Mathf.Lerp(KerbalFxLightingCore.RgbDimFloor, 1f, Mathf.Clamp01(alphaMultiplier));
            return new Color(tinted.r * rgbDim, tinted.g * rgbDim, tinted.b * rgbDim, tinted.a);
        }

        private float ComputeBaseMultiplier(KerbalFxLightAwareEntry entry)
        {
            float minPerceived = Mathf.Max(0f, entry.MinPerceived);
            float t = Mathf.InverseLerp(minPerceived, KerbalFxLightingCore.NominalSunLuma, smoothed.PerceivedBrightness);
            float dark = entry.DarkScale;
            if (smoothed.IsTwilight && entry.TwilightFloor > dark)
                dark = entry.TwilightFloor;
            float curved = t * t * (3f - 2f * t);
            return Mathf.Lerp(dark, entry.BrightScale, curved);
        }

        private KerbalFxLightSample ResolveSample(Vessel vessel, Vector3 worldPoint, Vector3 surfaceNormal)
        {
            CelestialBody body = vessel != null ? vessel.mainBody : null;
            Vector3 bodyUp = Vector3.up;
            if (body != null)
            {
                Vector3 toBody = (Vector3)body.position - worldPoint;
                if (toBody.sqrMagnitude > 0.0001f)
                    bodyUp = -toBody.normalized;
            }

            Vector3 normal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : bodyUp;
            Vector3 sunDir = KerbalFxLightingCore.GetSunDirection(worldPoint, bodyUp);

            Color sunColor;
            float sunLuma;
            bool hasSun = KerbalFxLightingCore.TryGetSunLight(out sunColor, out sunLuma);
            float rawCosSun = Vector3.Dot(normal, sunDir);
            float cosSun = Mathf.Clamp01(rawCosSun);
            if (useAerialHorizon && body != null && vessel != null)
            {
                double radius = Math.Max(1.0, body.Radius);
                double altitude = Math.Max(0.0, vessel.radarAltitude);
                if (altitude > 1.0)
                {
                    double r = radius / (radius + altitude);
                    float horizonDip = Mathf.Sqrt(Mathf.Clamp01((float)(1.0 - r * r)));
                    cosSun = Mathf.Clamp01(Mathf.InverseLerp(-horizonDip, 1f, rawCosSun));
                }
            }
            float directSun = hasSun ? cosSun * sunLuma : 0f;

            bool shadowed = false;
            if (useShadowProbe && hasSun && cosSun > KerbalFxLightingCore.ShadowProbeMinCos)
            {
                Vector3 origin = worldPoint + normal * KerbalFxLightingCore.ShadowProbeNormalOffset;
                if (KerbalFxLightingCore.RaycastShadowProbe(origin, sunDir))
                {
                    shadowed = true;
                    directSun *= KerbalFxLightingCore.ShadowResidualBleed;
                }
            }

            Color ambient = RenderSettings.ambientLight;
            float ambientLuma = KerbalFxLightingCore.Luminance(ambient);
            float effectiveAmbientLuma = Mathf.Max(0f, ambientLuma - KerbalFxLightingCore.AmbientNoiseFloor);

            float localLuma = 0f;
            Color localAccum = Color.black;
            if (sampleLocalLights)
                AccumulateLocalLights(worldPoint, normal, ref localLuma, ref localAccum);

            float linearLuma = effectiveAmbientLuma + directSun + localLuma;
            float perceived = KerbalFxLightingCore.PerceptualFromLinear(linearLuma);

            bool hasAtmosphere = body != null && body.atmosphere;
            bool isTwilight = hasAtmosphere
                && directSun < KerbalFxLightingCore.TwilightDirectThreshold
                && perceived > KerbalFxLightingCore.TwilightPerceivedFloor
                && perceived < KerbalFxLightingCore.TwilightPerceivedCeiling;

            Color dominant = sunColor * directSun + ambient + localAccum;
            float dominantLuma = KerbalFxLightingCore.Luminance(dominant);
            if (dominantLuma > 0.0001f)
                dominant = new Color(dominant.r / dominantLuma, dominant.g / dominantLuma, dominant.b / dominantLuma, 1f);
            else
                dominant = Color.white;

            return new KerbalFxLightSample
            {
                PerceivedBrightness = perceived,
                DirectSun = Mathf.Clamp01(directSun),
                CosSun = cosSun,
                SunLuma = hasSun ? sunLuma : 0f,
                Ambient = Mathf.Clamp01(ambientLuma),
                LocalLights = Mathf.Clamp01(localLuma),
                DominantColor = dominant,
                IsTwilight = isTwilight,
                IsShadowed = shadowed
            };
        }

        private static void AccumulateLocalLights(Vector3 worldPoint, Vector3 normal, ref float luma, ref Color accum)
        {
            List<Light> lights = KerbalFxSceneLightCache.Snapshot();
            for (int i = 0; i < lights.Count; i++)
            {
                Light light = lights[i];
                if (light == null || !light.enabled || !light.isActiveAndEnabled)
                    continue;

                float range = light.range;
                if (range <= 0.5f)
                    continue;

                Vector3 lightPos = light.transform.position;
                Vector3 toPoint = worldPoint - lightPos;
                float sqrDist = toPoint.sqrMagnitude;
                if (sqrDist > KerbalFxLightingCore.LocalLightMaxRangeSq || sqrDist > range * range)
                    continue;

                float dist = Mathf.Sqrt(sqrDist);
                float falloff = 1f - Mathf.Clamp01(dist / range);
                falloff *= falloff;

                if (light.type == LightType.Spot)
                {
                    Vector3 toPointNorm = sqrDist > 0.0001f ? toPoint / dist : light.transform.forward;
                    float spotCos = Vector3.Dot(light.transform.forward, toPointNorm);
                    float spotMin = Mathf.Cos(light.spotAngle * 0.5f * Mathf.Deg2Rad);
                    if (spotCos < spotMin)
                        continue;
                    float spotEdge = Mathf.InverseLerp(spotMin, 1f, spotCos);
                    falloff *= spotEdge;
                }

                Vector3 lightDir = sqrDist > 0.0001f ? -toPoint / dist : Vector3.up;
                float incidence = Mathf.Clamp(Vector3.Dot(normal, lightDir), 0.10f, 1f);

                Color contrib = light.color * (Mathf.Max(0f, light.intensity) * falloff * incidence);
                luma += KerbalFxLightingCore.Luminance(contrib) * KerbalFxLightingCore.LocalLightWeight;
                accum += contrib * KerbalFxLightingCore.LocalLightWeight;
            }
        }

        private static KerbalFxLightSample LerpSample(KerbalFxLightSample a, KerbalFxLightSample b, float t)
        {
            return new KerbalFxLightSample
            {
                PerceivedBrightness = Mathf.Lerp(a.PerceivedBrightness, b.PerceivedBrightness, t),
                DirectSun = Mathf.Lerp(a.DirectSun, b.DirectSun, t),
                CosSun = Mathf.Lerp(a.CosSun, b.CosSun, t),
                SunLuma = Mathf.Lerp(a.SunLuma, b.SunLuma, t),
                Ambient = Mathf.Lerp(a.Ambient, b.Ambient, t),
                LocalLights = Mathf.Lerp(a.LocalLights, b.LocalLights, t),
                DominantColor = Color.Lerp(a.DominantColor, b.DominantColor, t),
                IsTwilight = b.IsTwilight,
                IsShadowed = b.IsShadowed
            };
        }
    }

    internal static class KerbalFxLightFormat
    {
        public static string Describe(KerbalFxLightSample sample, string prefix)
        {
            return (string.IsNullOrEmpty(prefix) ? string.Empty : prefix)
                + "PB=" + sample.PerceivedBrightness.ToString("F2", CultureInfo.InvariantCulture)
                + " sun=" + sample.DirectSun.ToString("F2", CultureInfo.InvariantCulture)
                + " cos=" + sample.CosSun.ToString("F2", CultureInfo.InvariantCulture)
                + " sLm=" + sample.SunLuma.ToString("F2", CultureInfo.InvariantCulture)
                + " amb=" + sample.Ambient.ToString("F2", CultureInfo.InvariantCulture)
                + " loc=" + sample.LocalLights.ToString("F2", CultureInfo.InvariantCulture)
                + " col=" + KerbalFxSurfaceColorCore.FormatColor(sample.DominantColor)
                + (sample.IsShadowed ? " shadowed" : string.Empty)
                + (sample.IsTwilight ? " twilight" : string.Empty);
        }
    }

    internal struct KerbalFxLightDebugReport
    {
        public string Module;
        public string Emitter;
        public KerbalFxLightSample Sample;
        public float AlphaMultiplier;
        public float Strength;
        public float ReportTime;
    }

    internal static class KerbalFxLightDebugReporter
    {
        public const float StaleAfter = 2.0f;
        private const int MaxEntries = 32;
        private static readonly Dictionary<string, KerbalFxLightDebugReport> entries =
            new Dictionary<string, KerbalFxLightDebugReport>(32, StringComparer.Ordinal);

        public static void Report(string module, string emitter, KerbalFxLightSample sample, float alphaMul, float strength)
        {
            if (string.IsNullOrEmpty(module) || string.IsNullOrEmpty(emitter))
                return;

            string key = module + "/" + emitter;
            if (entries.Count >= MaxEntries && !entries.ContainsKey(key))
                PruneStale(Time.time);
            if (entries.Count >= MaxEntries && !entries.ContainsKey(key))
                return;

            entries[key] = new KerbalFxLightDebugReport
            {
                Module = module,
                Emitter = emitter,
                Sample = sample,
                AlphaMultiplier = alphaMul,
                Strength = strength,
                ReportTime = Time.time
            };
        }

        public static IEnumerable<KerbalFxLightDebugReport> Snapshot()
        {
            return entries.Values;
        }

        public static int CountFresh(float now)
        {
            int count = 0;
            foreach (var pair in entries)
                if (now - pair.Value.ReportTime <= StaleAfter)
                    count++;
            return count;
        }

        public static void Clear()
        {
            entries.Clear();
        }

        private static void PruneStale(float now)
        {
            List<string> remove = null;
            foreach (var pair in entries)
            {
                if (now - pair.Value.ReportTime > StaleAfter)
                {
                    if (remove == null) remove = new List<string>();
                    remove.Add(pair.Key);
                }
            }
            if (remove != null)
                for (int i = 0; i < remove.Count; i++)
                    entries.Remove(remove[i]);
        }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    internal class KerbalFxLightDebugOverlay : MonoBehaviour
    {
        private const int PanelWidth = 720;
        private const int PanelMaxHeight = 540;
        private GUIStyle headerStyle;
        private GUIStyle rowStyle;
        private Vector2 scroll;
        private Rect panelRect = new Rect(12f, 60f, PanelWidth, 240f);

        private void OnGUI()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            float now = Time.time;
            int fresh = KerbalFxLightDebugReporter.CountFresh(now);
            if (fresh <= 0)
                return;

            EnsureStyles();
            panelRect.height = Mathf.Min(PanelMaxHeight, 60f + fresh * 22f + 30f);
            panelRect = GUILayout.Window(0x4B46_4C44, panelRect, DrawWindow, "KerbalFX Light-aware Debug");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("Module/Emitter | PB | sun | cos | sLm | amb | loc | aMul | flags", headerStyle);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(PanelMaxHeight - 60f));
            float now = Time.time;
            foreach (KerbalFxLightDebugReport r in KerbalFxLightDebugReporter.Snapshot())
            {
                if (now - r.ReportTime > KerbalFxLightDebugReporter.StaleAfter)
                    continue;

                string flags = (r.Sample.IsShadowed ? "shd " : "")
                    + (r.Sample.IsTwilight ? "twi" : "");
                string row = string.Format(CultureInfo.InvariantCulture,
                    "{0}/{1}  PB={2:F2} sun={3:F2} cos={4:F2} sLm={5:F2} amb={6:F2} loc={7:F2} aMul={8:F2} {9}",
                    r.Module,
                    Truncate(r.Emitter, 28),
                    r.Sample.PerceivedBrightness,
                    r.Sample.DirectSun,
                    r.Sample.CosSun,
                    r.Sample.SunLuma,
                    r.Sample.Ambient,
                    r.Sample.LocalLights,
                    r.AlphaMultiplier,
                    flags);
                GUILayout.Label(row, rowStyle);
            }
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void EnsureStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label);
                headerStyle.fontStyle = FontStyle.Bold;
                headerStyle.normal.textColor = new Color(0.95f, 0.95f, 0.70f);
            }
            if (rowStyle == null)
            {
                rowStyle = new GUIStyle(GUI.skin.label);
                rowStyle.normal.textColor = Color.white;
                rowStyle.fontSize = 11;
                rowStyle.wordWrap = false;
            }
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
                return value;
            return value.Substring(0, max);
        }
    }
}
