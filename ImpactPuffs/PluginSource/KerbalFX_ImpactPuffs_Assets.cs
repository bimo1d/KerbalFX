using UnityEngine;

namespace KerbalFX.ImpactPuffs
{
    internal static class ImpactPuffsAssets
    {
        private static Material sharedMaterial;
        private static Material sharedBurstMaterial;
        private static Texture2D sharedTexture;
        private static Texture2D sharedBurstTexture;

        public static Material GetSharedMaterial()
        {
            if (sharedMaterial != null)
                return sharedMaterial;

            Shader shader = KerbalFxUtil.FindParticleShader();
            if (shader == null)
                return null;

            sharedMaterial = new Material(shader);
            sharedMaterial.name = "KerbalFX_ImpactPuffsMaterial";
            sharedMaterial.color = Color.white;
            sharedMaterial.mainTexture = GetSharedTexture();
            return sharedMaterial;
        }

        public static Material GetBurstMaterial()
        {
            if (sharedBurstMaterial != null)
                return sharedBurstMaterial;

            Shader shader = KerbalFxUtil.FindParticleShader();
            if (shader == null)
                return GetSharedMaterial();

            sharedBurstMaterial = new Material(shader);
            sharedBurstMaterial.name = "KerbalFX_ImpactPuffsBurstMaterial";
            sharedBurstMaterial.color = Color.white;
            sharedBurstMaterial.mainTexture = GetBurstTexture();
            return sharedBurstMaterial;
        }

        private static Texture2D GetSharedTexture()
        {
            if (sharedTexture != null)
            {
                return sharedTexture;
            }

            const int size = 96;
            sharedTexture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            sharedTexture.wrapMode = TextureWrapMode.Clamp;
            sharedTexture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size) * 2f - 1f;
                    float ny = ((y + 0.5f) / size) * 2f - 1f;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);

                    float radial = Mathf.Clamp01(1f - radius);
                    float soft = Mathf.Pow(radial, 1.45f);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.34f) * 2.8f);
                    float noiseA = Mathf.PerlinNoise(x * 0.095f, y * 0.095f);
                    float noiseB = Mathf.PerlinNoise(x * 0.185f + 12.3f, y * 0.185f + 3.7f);
                    float noise = Mathf.Lerp(noiseA, noiseB, 0.45f);

                    float alpha = Mathf.Clamp01((soft * 0.80f + ring * 0.20f) * (0.82f + 0.18f * noise));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            sharedTexture.SetPixels(pixels);
            sharedTexture.Apply(false, true);
            return sharedTexture;
        }

        private static Texture2D GetBurstTexture()
        {
            if (sharedBurstTexture != null)
            {
                return sharedBurstTexture;
            }

            const int size = 128;
            sharedBurstTexture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            sharedBurstTexture.wrapMode = TextureWrapMode.Clamp;
            sharedBurstTexture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size) * 2f - 1f;
                    float ny = ((y + 0.5f) / size) * 2f - 1f;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);

                    float radial = Mathf.Clamp01(1f - radius);
                    float softBody = Mathf.Pow(radial, 1.95f);
                    float centerCut = Mathf.Clamp01((radius - 0.06f) / 0.22f);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.42f) * 3.6f);
                    float feather = Mathf.Pow(Mathf.Clamp01(1f - radius * 0.84f), 1.35f);
                    float noiseA = Mathf.PerlinNoise(x * 0.060f + 5.1f, y * 0.060f + 2.7f);
                    float noiseB = Mathf.PerlinNoise(x * 0.125f + 17.2f, y * 0.125f + 9.4f);
                    float breakup = Mathf.Lerp(noiseA, noiseB, 0.42f);
                    float alphaBody = softBody * centerCut;
                    float alpha = Mathf.Clamp01((alphaBody * 0.38f + ring * 0.44f + feather * 0.18f) * (0.72f + 0.28f * breakup));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            sharedBurstTexture.SetPixels(pixels);
            sharedBurstTexture.Apply(false, true);
            return sharedBurstTexture;
        }
    }
}
