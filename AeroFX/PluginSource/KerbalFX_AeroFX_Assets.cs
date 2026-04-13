using UnityEngine;

namespace KerbalFX.AeroFX
{
    internal static class AeroFxAssets
    {
        private static Material sharedTrailMaterial;
        private static Texture2D sharedTrailTexture;

        public static Material GetTrailMaterial()
        {
            if (sharedTrailMaterial != null)
                return sharedTrailMaterial;

            Shader shader = KerbalFxUtil.FindParticleShader();
            if (shader == null)
                shader = KerbalFxUtil.FindTransparentShader();
            if (shader == null)
                return null;

            sharedTrailMaterial = new Material(shader);
            sharedTrailMaterial.name = "KerbalFX_AeroTrailMaterial";
            sharedTrailMaterial.color = Color.white;
            if (sharedTrailMaterial.HasProperty("_TintColor"))
                sharedTrailMaterial.SetColor("_TintColor", Color.white);
            sharedTrailMaterial.mainTexture = GetTrailTexture();
            sharedTrailMaterial.mainTextureScale = Vector2.one;
            sharedTrailMaterial.renderQueue = 3000;
            return sharedTrailMaterial;
        }

        private static Texture2D GetTrailTexture()
        {
            if (sharedTrailTexture != null)
                return sharedTrailTexture;

            const int width = 128;
            const int height = 32;
            sharedTrailTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            sharedTrailTexture.wrapMode = TextureWrapMode.Clamp;
            sharedTrailTexture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float v = (y + 0.5f) / height;
                    float ny = v * 2f - 1f;
                    float vertical = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(ny)), 1.65f);
                    float head = Mathf.Lerp(0.96f, 0.68f, Mathf.Pow(u, 0.55f));
                    float tailFade = Mathf.Pow(Mathf.Clamp01(1f - u), 0.30f);
                    float noiseA = Mathf.PerlinNoise(u * 5.0f + 0.7f, v * 3.4f + 1.1f);
                    float noiseB = Mathf.PerlinNoise(u * 9.8f + 3.4f, v * 6.6f + 4.8f);
                    float breakup = Mathf.Lerp(noiseA, noiseB, 0.24f);
                    float alpha = Mathf.Clamp01(vertical * head * tailFade * (0.88f + 0.12f * breakup));
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            sharedTrailTexture.SetPixels(pixels);
            sharedTrailTexture.Apply(false, true);
            return sharedTrailTexture;
        }
    }
}
