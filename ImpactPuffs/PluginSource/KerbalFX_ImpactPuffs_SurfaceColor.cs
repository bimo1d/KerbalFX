using UnityEngine;

namespace KerbalFX.ImpactPuffs
{
    internal static class ImpactPuffsSurfaceColor
    {
        public static Color GetBaseDustColor(Vessel vessel)
        {
            if (vessel == null || vessel.mainBody == null || string.IsNullOrEmpty(vessel.mainBody.bodyName))
            {
                return new Color(0.70f, 0.66f, 0.58f);
            }

            string key = vessel.mainBody.bodyName.ToLowerInvariant();

            if (key.Contains("minmus")) return new Color(0.73f, 0.80f, 0.74f);
            if (key.Contains("mun")) return new Color(0.76f, 0.74f, 0.70f);
            if (key.Contains("duna")) return new Color(0.72f, 0.48f, 0.33f);
            if (key.Contains("eve")) return new Color(0.77f, 0.71f, 0.60f);
            if (key.Contains("moho")) return new Color(0.63f, 0.56f, 0.50f);
            if (key.Contains("gilly")) return new Color(0.62f, 0.58f, 0.52f);
            if (key.Contains("bop")) return new Color(0.60f, 0.52f, 0.45f);
            if (key.Contains("pol")) return new Color(0.66f, 0.64f, 0.62f);
            if (key.Contains("tylo")) return new Color(0.67f, 0.67f, 0.66f);
            if (key.Contains("vall")) return new Color(0.70f, 0.72f, 0.74f);
            if (key.Contains("eeloo")) return new Color(0.74f, 0.75f, 0.77f);
            if (key.Contains("kerbin")) return new Color(0.67f, 0.61f, 0.53f);

            return new Color(0.70f, 0.66f, 0.58f);
        }

        public static bool TryGetColliderColor(Collider collider, out Color color)
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

        public static Color BlendWithColliderColor(Color baseColor, Color colliderColor)
        {
            float h;
            float s;
            float v;
            Color.RGBToHSV(colliderColor, out h, out s, out v);

            s = Mathf.Clamp(s, 0.05f, 0.34f);
            v = Mathf.Clamp(v, 0.20f, 0.90f);

            if (h > 0.20f && h < 0.45f)
            {
                h = Mathf.Lerp(h, 0.11f, 0.50f);
                s *= 0.44f;
                v *= 0.92f;
            }

            Color tunedCollider = Color.HSVToRGB(h, s, v);
            return Color.Lerp(baseColor, tunedCollider, 0.14f);
        }

        public static Color NormalizeDustTone(Color input)
        {
            float h;
            float s;
            float v;
            Color.RGBToHSV(input, out h, out s, out v);

            s = Mathf.Clamp(s, 0.10f, 0.34f);
            v = Mathf.Clamp(v, 0.22f, 0.88f);

            if (h > 0.20f && h < 0.45f)
            {
                h = Mathf.Lerp(h, 0.12f, 0.33f);
                s *= 0.70f;
            }

            return Color.HSVToRGB(h, s, v);
        }
    }
}
