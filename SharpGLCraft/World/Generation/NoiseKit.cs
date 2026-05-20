namespace SharpGLCraft.World.Generation
{
    public class NoiseKit
    {
        private int[] permutation;

        /// Perlin Noise I got chatgpt to write for me
        public NoiseKit(int seed = 0)
        {
            Random rand = new Random(seed);
            permutation = new int[512];
            int[] p = new int[256];

            for (int i = 0; i < 256; i++) p[i] = i;
            for (int i = 0; i < 256; i++)
            {
                int j = rand.Next(256);
                (p[i], p[j]) = (p[j], p[i]);
            }

            for (int i = 0; i < 512; i++)
            {
                permutation[i] = p[i % 256];
            }
        }

        public float Noise(float x, float y)
        {
            int xi = (int)MathF.Floor(x) & 255;
            int yi = (int)MathF.Floor(y) & 255;

            float xf = x - MathF.Floor(x);
            float yf = y - MathF.Floor(y);

            float u = Fade(xf);
            float v = Fade(yf);

            int aa = permutation[permutation[xi] + yi];
            int ab = permutation[permutation[xi] + yi + 1];
            int ba = permutation[permutation[xi + 1] + yi];
            int bb = permutation[permutation[xi + 1] + yi + 1];

            float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
            float x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);

            return (Lerp(x1, x2, v) + 1) / 2f; // normalize to [0,1]
        }

        public float Noise(float x, float y, float z)
        {
            int X = (int)MathF.Floor(x) & 255, Y = (int)MathF.Floor(y) & 255, Z = (int)MathF.Floor(z) & 255;
            float xf = x - MathF.Floor(x), yf = y - MathF.Floor(y), zf = z - MathF.Floor(z);

            float u = Fade(xf);
            float v = Fade(yf);
            float w = Fade(zf);

            int aaa = permutation[permutation[permutation[X] + Y] + Z];
            int aba = permutation[permutation[permutation[X] + Y + 1] + Z];
            int baa = permutation[permutation[permutation[X + 1] + Y] + Z];
            int bba = permutation[permutation[permutation[X + 1] + Y + 1] + Z];

            int aab = permutation[permutation[permutation[X] + Y] + Z + 1];
            int abb = permutation[permutation[permutation[X] + Y + 1] + Z + 1];
            int bab = permutation[permutation[permutation[X + 1] + Y] + Z + 1];
            int bbb = permutation[permutation[permutation[X + 1] + Y + 1] + Z + 1];

            float x1 = Lerp(Grad(aaa, xf, yf, zf), Grad(baa, xf - 1, yf, zf), u);
            float x2 = Lerp(Grad(aba, xf, yf - 1, zf), Grad(bba, xf - 1, yf - 1, zf), u);
            float x3 = Lerp(Grad(aab, xf, yf, zf - 1), Grad(bab, xf - 1, yf, zf - 1), u);
            float x4 = Lerp(Grad(abb, xf, yf - 1, zf - 1), Grad(bbb, xf - 1, yf - 1, zf - 1), u);

            float y1 = Lerp(x1, x2, v);
            float y2 = Lerp(x3, x4, v);
            return (Lerp(y1, y2, w) + 1) / 2f;
        }

        private static float Fade(float t) =>
                t * t * t * (t * (t * 6 - 15) + 10);

        private static float Lerp(float a, float b, float t) =>
            a + t * (b - a);

        private static float Grad(int hash, float x, float y)
        {
            int h = hash & 7;
            float u = h < 4 ? x : y;
            float v = h < 4 ? y : x;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        private static float Grad(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : h == 12 || h == 14 ? x : z;
            return ((h & 1) == 0 ? u : -u) +
                   ((h & 2) == 0 ? v : -v);
        }

        // 2D FBM
        public float Fbm2D(float x, float y, int octaves = 4, float lacunarity = 2f, float gain = 0.5f)
        {
            float total = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float max = 0f;
            for (int i = 0; i < octaves; i++)
            {
                total += (Noise(x * frequency, y * frequency) * 2f - 1f) * amplitude; // center around 0
                max += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }
            // normalize back to [0,1]
            return (total / max) * 0.5f + 0.5f;
        }

        // 3D FBM
        public float Fbm3D(float x, float y, float z, int octaves = 3, float lacunarity = 2f, float gain = 0.5f)
        {
            float total = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float max = 0f;
            for (int i = 0; i < octaves; i++)
            {
                total += (Noise(x * frequency, y * frequency, z * frequency) * 2f - 1f) * amplitude;
                max += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }
            return (total / max) * 0.5f + 0.5f;
        }
    }
}
