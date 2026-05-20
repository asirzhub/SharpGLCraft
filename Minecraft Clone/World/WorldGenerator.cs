using Minecraft_Clone.World.Chunks;
using OpenTK.Mathematics;
using System.Collections.Concurrent;
using static Minecraft_Clone.Graphics.CubeMesh;

namespace Minecraft_Clone.World
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

        public NoiseCacheEntry(float value, int framesSinceUse) {
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

        // feature generation
        public NoiseParams tallgrassNoiseParams = new NoiseParams(scale: 0.12f, octaves: 3, lacunarity: 2.5f, gain: 0.5f);
        float tallgrassThreshold = 0.05f;// grass half-band around 0.5f

        public NoiseParams treeNoiseParams = new NoiseParams(scale: 1.5f, octaves: 2, lacunarity: 2.5f, gain: 0.8f);
        float treeThreshold = 0.75f; // tree noise greater than this value means a tree is placed

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

        public struct TreeParams {
            public int trunkThickness;  
            public int minHeight;
            public int maxHeight;
            public int leafStartHeight;  // local Y where leaves begin
            public int maxLeafRadius;    // furthest radial leaf distance
        }

        public TreeParams treeParams = new TreeParams()
        {
            trunkThickness = 1,
            minHeight = 8,
            maxHeight = 18,
            leafStartHeight = 3,
            maxLeafRadius = 3
        };

        public WorldGenerator(int seed = 0)
        {
            this.seed = seed;
            noise = new NoiseKit(seed);
            noiseParams.Add(NoiseLayer.BASE, baseNoiseParams);
            noiseParams.Add(NoiseLayer.MOUNTAINBLEND, mountainBlendNoise);
            noiseParams.Add(NoiseLayer.TALLGRASS, tallgrassNoiseParams);
            noiseParams.Add(NoiseLayer.TREE, treeNoiseParams);
            noiseCaches = new();
            
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
            // if (!noiseCaches.TryGetValue(layer, out var cache))
            // {
            //     cache = new ConcurrentDictionary<Vector2i, NoiseCacheEntry>();
            //     noiseCaches.TryAdd(layer, cache);
            // }

            // var key = new Vector2i(x, z);

            // if (cache.TryGetValue(key, out var entry))
            // {
            //     cache[key] = new NoiseCacheEntry(entry.value, frameCount);
            //     return entry.value;
            // }

            float result;

            if (layer == NoiseLayer.HEIGHT)
            {
                // height has some extra stuff to do
                float baseN = GetNoiseAt(NoiseLayer.BASE, x, z);
                float height = minHeight + (baseN * baseN) * (maxHeight-minHeight);
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

            //New entries should have a fresh timestamp
            //cache[key] = new NoiseCacheEntry(result, frameCount);
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

        private int GetTreeHeight(float treeNoiseValue)
        {
            if (treeParams.maxHeight <= treeParams.minHeight)
                return treeParams.minHeight;

            // Stronger tree noise => taller tree
            float normalized = Clamp((treeNoiseValue - treeThreshold) / (1f - treeThreshold), 0f, 1f);
            return treeParams.minHeight + (int)MathF.Round(normalized * (treeParams.maxHeight - treeParams.minHeight));
        }

        private void PlaceProceduralTree(ref ChunkGenerator.CompletedChunkBlocks blocks, int baseX, int baseY, int baseZ, float treeNoiseValue)
        {
            int trunkHeight = GetTreeHeight(treeNoiseValue);

            int trunkMinOffset = -(treeParams.trunkThickness / 2);
            int trunkMaxOffset = trunkMinOffset + treeParams.trunkThickness - 1;

            // Build trunk
            for (int ty = 0; ty <= trunkHeight; ty++)
            {
                for (int tx = trunkMinOffset; tx <= trunkMaxOffset; tx++)
                {
                    for (int tz = trunkMinOffset; tz <= trunkMaxOffset; tz++)
                    {
                        blocks.SetBlock((baseX + tx, baseY + ty, baseZ + tz), BlockType.LOG);
                    }
                }
            }

            int leafStart = treeParams.leafStartHeight;

            // Let canopy extend a little above the trunk
            int leafTop = trunkHeight+1;
            int canopyHeight = Math.Max(1, leafTop - leafStart);

            // Build a cone tree
            for (int ly = leafStart; ly <= leafTop; ly+=2)
            {
                float normalizedHeight = (ly - leafStart) / (float)canopyHeight;
                float widthFactor = 1f - (normalizedHeight * normalizedHeight);
                float layerRadius = MathF.Max(1f, treeParams.maxLeafRadius * widthFactor);

                for (int dx = -treeParams.maxLeafRadius; dx <= treeParams.maxLeafRadius; dx++)
                {
                    for (int dz = -treeParams.maxLeafRadius; dz <= treeParams.maxLeafRadius; dz++)
                    {
                        float dist = MathF.Sqrt(dx * dx + dz * dz);
                        if (dist > layerRadius + 0.2f) continue;

                        bool insideTrunkFootprint =
                            dx >= trunkMinOffset && dx <= trunkMaxOffset &&
                            dz >= trunkMinOffset && dz <= trunkMaxOffset;

                        // Don't overwrite trunk blocks below the top
                        if (insideTrunkFootprint && ly < trunkHeight)
                            continue;

                        blocks.SetBlock((baseX + dx, baseY + ly, baseZ + dz), BlockType.LEAVES);
                    }
                }
            }
        }

        // grows trees
        public ChunkGenerator.CompletedChunkBlocks GrowFlora(ChunkGenerator.CompletedChunkBlocks blocks, ChunkManager manager)
        {
            var worldOffset = blocks.index * Chunk.SIZE;

            for (byte x = 0; x < Chunk.SIZE; x++)
            {
                for (byte y = 0; y < Chunk.SIZE; y++)
                {
                    for (byte z = 0; z < Chunk.SIZE; z++)
                    {
                        var treePos = worldOffset + (x, y, z);

                        bool growableSurface = manager.TryGetBlockAtWorldPosition(worldOffset + (x, y - 1, z), out var result) && result.Type == BlockType.GRASS;

                        // determine if there's tallgrass here
                        if (growableSurface)
                        {

                            float t = GetNoiseAt(NoiseLayer.TREE, worldOffset.X + x, worldOffset.Z + z);
                            if (t > treeThreshold && growableSurface)
                            {
                                PlaceProceduralTree(ref blocks, x, y, z, t);
                                blocks.SetBlock((x, y - 1, z), BlockType.DIRT);
                                continue;
                            }

                            float f = GetNoiseAt(NoiseLayer.TALLGRASS, worldOffset.X + x, worldOffset.Z + z);
                            if (f > 0.5f - tallgrassThreshold && f < 0.5f + tallgrassThreshold)
                            {
                                blocks.SetBlock((x, y, z), BlockType.TALLGRASS);
                            }
                        }
                    }
                }
            }

            return blocks;
        }

        public Vector3i WorldPosToChunkIndex(Vector3i worldIndex, int chunkSize = Chunk.SIZE)
        {
            int chunkX = (int)Math.Floor(worldIndex.X / (double)chunkSize);
            int chunkY = (int)Math.Floor(worldIndex.Y / (double)chunkSize);
            int chunkZ = (int)Math.Floor(worldIndex.Z / (double)chunkSize);

            return (chunkX, chunkY, chunkZ);
        }

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
                            if(dict.TryRemove(kv.Key, out _)) removed++;
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
