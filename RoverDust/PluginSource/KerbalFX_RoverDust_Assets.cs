using UnityEngine;

namespace KerbalFX.RoverDust
{
    internal static class RoverDustAssets
    {
        private static Material sharedMaterial;
        private static Texture2D sharedDustTexture;

        public static Material GetSharedMaterial()
        {
            if (sharedMaterial != null)
            {
                return sharedMaterial;
            }

            sharedMaterial = KerbalFxUtil.CreateParticleMaterial(
                "RoverDustFXMaterial",
                GetOrCreateDustTexture(),
                false,
                false,
                false);
            return sharedMaterial;
        }

        private static Texture2D GetOrCreateDustTexture()
        {
            if (sharedDustTexture != null)
            {
                return sharedDustTexture;
            }

            const int size = 64;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size) * 2f - 1f;
                    float ny = ((y + 0.5f) / size) * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float t = Mathf.Clamp01(1f - r);
                    float soft = Mathf.Pow(t, 1.35f);
                    float noise = Mathf.PerlinNoise(x * 0.11f, y * 0.11f);
                    float alpha = Mathf.Clamp01(soft * (0.90f + 0.10f * noise));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            sharedDustTexture = KerbalFxUtil.CreateProceduralTexture(size, size, pixels);
            return sharedDustTexture;
        }
    }
}
