using UnityEngine;

namespace KerbalFX.BlastFX
{
    internal static class BlastFxAssets
    {
        private static Material sparkMat;
        private static Material chunkMat;
        private static Material smokeMat;
        private static Mesh chunkMesh;
        private static Texture2D sparkTex;
        private static Texture2D chunkTex;
        private static Texture2D smokeTex;

        public static Material GetSparkMaterial()
        {
            if (sparkMat != null) return sparkMat;
            Shader sh = KerbalFxUtil.FindParticleShader(additive: true);
            if (sh == null) return null;
            sparkMat = new Material(sh);
            sparkMat.name = "KerbalFX_BlastFXSpark";
            sparkMat.color = Color.white;
            sparkMat.mainTexture = GetSparkTexture();
            return sparkMat;
        }

        public static Material GetSmokeMaterial()
        {
            if (smokeMat != null) return smokeMat;
            Shader sh = KerbalFxUtil.FindParticleShader();
            if (sh == null) return null;
            smokeMat = new Material(sh);
            smokeMat.name = "KerbalFX_BlastFXSmoke";
            smokeMat.color = Color.white;
            smokeMat.mainTexture = GetSmokeTexture();
            return smokeMat;
        }

        public static Material GetChunkMaterial()
        {
            if (chunkMat != null) return chunkMat;
            Shader sh = KerbalFxUtil.FindTransparentShader();
            if (sh == null) return null;
            chunkMat = new Material(sh)
            {
                name = "KerbalFX_BlastFXChunk",
                color = Color.white,
                mainTexture = GetChunkTexture()
            };
            return chunkMat;
        }

        public static Mesh GetChunkMesh()
        {
            if (chunkMesh != null) return chunkMesh;
            GameObject temp = null;
            try
            {
                temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                MeshFilter mf = temp.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    chunkMesh = UnityEngine.Object.Instantiate(mf.sharedMesh);
                    chunkMesh.name = "KerbalFX_BlastFXChunkMesh";
                }
            }
            finally
            {
                if (temp != null)
                {
                    UnityEngine.Object.Destroy(temp);
                }
            }

            return chunkMesh;
        }

        private static Texture2D GetSparkTexture()
        {
            if (sparkTex != null) return sparkTex;
            const int size = 48;
            sparkTex = new Texture2D(size, size, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size) * 2f - 1f;
                    float ny = ((y + 0.5f) / size) * 2f - 1f;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny)), 2.8f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            sparkTex.SetPixels(px);
            sparkTex.Apply(false, true);
            return sparkTex;
        }

        private static Texture2D GetSmokeTexture()
        {
            if (smokeTex != null) return smokeTex;
            const int size = 96;
            smokeTex = new Texture2D(size, size, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size) * 2f - 1f;
                    float ny = ((y + 0.5f) / size) * 2f - 1f;
                    float radial = Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny));
                    float n1 = Mathf.PerlinNoise(x * 0.11f, y * 0.11f);
                    float n2 = Mathf.PerlinNoise(x * 0.21f + 13.7f, y * 0.21f + 7.2f);
                    float a = Mathf.Clamp01(Mathf.Pow(radial, 1.6f) * (0.65f + Mathf.Lerp(n1, n2, 0.45f) * 0.35f));
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            smokeTex.SetPixels(px);
            smokeTex.Apply(false, true);
            return smokeTex;
        }

        private static Texture2D GetChunkTexture()
        {
            if (chunkTex != null) return chunkTex;
            const int size = 64;
            chunkTex = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color[] px = new Color[size * size];
            float angle = 0.66f;
            float ca = Mathf.Cos(angle);
            float sa = Mathf.Sin(angle);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size) * 2f - 1f;
                    float ny = ((y + 0.5f) / size) * 2f - 1f;

                    float rx = nx * ca - ny * sa;
                    float ry = nx * sa + ny * ca;
                    float diamond = Mathf.Clamp01(1f - (Mathf.Abs(rx) * 1.15f + Mathf.Abs(ry) * 0.88f));
                    float n = Mathf.PerlinNoise((x + 11f) * 0.19f, (y + 7f) * 0.19f);
                    float edge = Mathf.Clamp01(diamond * (0.70f + 0.55f * n));
                    float alpha = Mathf.Pow(edge, 1.65f);
                    px[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            chunkTex.SetPixels(px);
            chunkTex.Apply(false, true);
            return chunkTex;
        }
    }
}
