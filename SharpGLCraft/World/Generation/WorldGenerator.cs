using SharpGLCraft.World.Blocks;
using SharpGLCraft.World.Chunks;
using OpenTK.Mathematics;
using System.Collections.Concurrent;

namespace SharpGLCraft.World.Generation
{
    public enum NoiseLayer
    {
        BASE,
        HEIGHT,
        MOUNTAINBLEND,
        DETAIL,
        TALLGRASS,
        TREE,
    }

    // bundle noise generation parameters
    public struct NoiseParams
    {
        public float scale;
        public int octaves;
        public float lacunarity;
        public float gain;

        public NoiseParams(float scale, int octaves, float lacunarity, float gain)
        {
            this.scale = scale;
            this.octaves = octaves;
            this.lacunarity = lacunarity;
            this.gain = gain;
        }
    }

    public struct NoiseCacheEntry
    {
        public float value;
        public int framesSinceUse;

        public NoiseCacheEntry(float value, int framesSinceUse)
        {
            this.value = value;
            this.framesSinceUse = framesSinceUse;
        }
    }

    // manages world generation...
    public class WorldGenerator
    {
        // World bounds
        public int seaLevel = 0;
        public int minHeight = -128;
        public int maxHeight = 256;

        // Continents (FBM)
        public NoiseParams baseNoiseParams = new NoiseParams(scale: 0.0007f, octaves: 5, lacunarity: 2.6f, gain: 0.5f);

        public FeatureGenerator features;

        public int seaFloorDepth = 32;
        public float seaFloorBlend = 0.2f;   // sea floor flattening
        public int beachHalfWidth = 3;      // band around sea level for sand

        public int topsoilDepth = 1;
        public int subsoilDepth = 3;

        public NoiseParams mountainBlendNoise = new NoiseParams(scale: 0.002f, octaves: 3, lacunarity: 2.4f, gain: 0.8f);
        public int mountainHeightStart = 128;
        public int snowLineOffset = 32;
        public float mountainBoost = 10.0f;

        // Noise
        public readonly NoiseKit noise;
        public int seed = 0;

        // noise caches
        ConcurrentDictionary<NoiseLayer, ConcurrentDictionary<Vector2i, NoiseCacheEntry>> noiseCaches;
        Dictionary<NoiseLayer, NoiseParams> noiseParams = new();

        // height cache
        ConcurrentDictionary<Vector2i, NoiseCacheEntry> heightCache = new();

        float noiseCacheLifetime = 180; // 30 frames may pass without noise cache access

        readonly Vector2[] controlPoints;

        public WorldGenerator(int seed = 0)
        {
            this.seed = seed;
            noise = new NoiseKit(seed);
            noiseParams.Add(NoiseLayer.BASE, baseNoiseParams);
            noiseParams.Add(NoiseLayer.MOUNTAINBLEND, mountainBlendNoise);
            noiseCaches = new();
            features = new FeatureGenerator(this);
            noiseParams.Add(NoiseLayer.TALLGRASS, features.tallgrassNoiseParams);
            noiseParams.Add(NoiseLayer.TREE, features.treeNoiseParams);

            float s = seaLevel;
            float m = mountainHeightStart;

            controlPoints = new Vector2[]{
                (minHeight, minHeight),
                (s, s),
                (m, m),
                (maxHeight, maxHeight) }; // needs to be in order along x
        }

        // piecewise function to flatten/exaggerate cliffs and stuff idk
        public float heightRemapper(float h, Vector2[] controlPoints)
        {
            float result = h;

            for (int i = 0; i < controlPoints.Length - 1; i++)
            {
                var point = controlPoints[i];
                var nextPoint = controlPoints[i + 1];

                // find the zone it falls under
                if (h >= point.X && h < nextPoint.X)
                {
                    result = Lerp(point.Y, nextPoint.Y, (h - point.X) / (nextPoint.X - point.X));
                }
            }

            return result;
        }

        public float GetNoiseAt(NoiseLayer layer, int x, int z)
        {
            float result;

            if (layer == NoiseLayer.HEIGHT)
            {
                // height has some extra stuff to do
                float baseN = GetNoiseAt(NoiseLayer.BASE, x, z);
                float height = minHeight + (baseN * baseN) * (maxHeight - minHeight);
                height += GetNoiseAt(NoiseLayer.MOUNTAINBLEND, x, z) * mountainBoost;

                result = heightRemapper(height, controlPoints);
                result = Clamp(result, minHeight + 1, maxHeight - 1);
            }
            else
            {
                // Regular FBM for other layers
                noiseParams.TryGetValue(layer, out var p);
                result = noise.Fbm2D(x * p.scale, z * p.scale, p.octaves, p.lacunarity, p.gain);
            }

            return result;
        }

        // This is the function to generate terrain, as well as mark locations to place trees
        public BlockType GetBlockAtWorldPos(Vector3i pos)
        {
            int x = pos.X, y = pos.Y, z = pos.Z;

            float hF = GetNoiseAt(NoiseLayer.HEIGHT, x, z);
            int h = (int)MathF.Floor(hF);

            // calculate gradients
            float dx = (GetNoiseAt(NoiseLayer.HEIGHT, x + 1, z) - GetNoiseAt(NoiseLayer.HEIGHT, x - 1, z)) / 3;
            float dz = (GetNoiseAt(NoiseLayer.HEIGHT, x, z + 1) - GetNoiseAt(NoiseLayer.HEIGHT, x, z - 1)) / 3;

            float absdx = MathF.Abs(dx);
            float absdz = MathF.Abs(dz);

            float slope = MathF.Sqrt(absdx * absdx + absdz * absdz);

            // Air / water
            if (y > h)
            {
                if (y <= seaLevel) return BlockType.WATER;
                if (y == h + 1 && y > seaLevel + beachHalfWidth + 1)
                {

                }
                return BlockType.AIR;
            }

            // beaches
            if (h >= seaLevel - beachHalfWidth && h <= seaLevel + beachHalfWidth)
            {
                if (y >= h - topsoilDepth + 1) return BlockType.SAND;
                if (y >= h - subsoilDepth) return BlockType.SAND;
            }

            // normal surface
            if (y == h)
            {
                // mountain
                if (h >= mountainHeightStart)
                {
                    if (h > seaLevel + 1 && y >= mountainHeightStart + snowLineOffset) return BlockType.SNOW;
                    return BlockType.STONE;
                }

                // normal grass surface
                if (h > seaLevel + 1) return BlockType.GRASS;

                // underwater
                if (slope < 0.5) return BlockType.SAND;
                else if (slope < 0.6) return BlockType.DIRT;
                return BlockType.STONE;
            }

            // subsoil
            if (y >= h - subsoilDepth && y < mountainHeightStart) return BlockType.DIRT;

            return BlockType.STONE;
        }

        public ChunkGenerator.CompletedChunkBlocks GrowFlora(ChunkGenerator.CompletedChunkBlocks blocks, ChunkManager manager)
            => features.GrowFlora(blocks, manager);

        int frameCount = -10;

        public async void Update(int frameCount)
        {
            if (frameCount - this.frameCount < 15) return; // avoid double-calls

            Console.WriteLine($"Searching for expired noise entries at frame: {frameCount}");
            int removed = 0;
            int total = 0;

            // remove expired noise cache entries
            this.frameCount = frameCount;
            await Task.Run(() =>
            {
                foreach (var (layer, dict) in noiseCaches)
                {
                    foreach (var kv in dict) // kv is KeyValuePair<Vector2i, NoiseCacheEntry>
                    {
                        total++;
                        if (frameCount - kv.Value.framesSinceUse > noiseCacheLifetime)
                        {
                            if (dict.TryRemove(kv.Key, out _)) removed++;
                        }
                    }
                }
                Console.WriteLine($"Removed {removed} entries - {100 * (float)removed / total}%");
            });
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t; // (fixed)
    }
}
