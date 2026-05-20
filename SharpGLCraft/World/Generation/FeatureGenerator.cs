using OpenTK.Mathematics;
using SharpGLCraft.World.Blocks;
using SharpGLCraft.World.Chunks;

namespace SharpGLCraft.World.Generation
{
    public class FeatureGenerator
    {
        public struct TreeParams
        {
            public int trunkThickness;
            public int minHeight;
            public int maxHeight;
            public int leafStartHeight;  // local Y where leaves begin
            public int maxLeafRadius;    // furthest radial leaf distance
        }

        public NoiseParams tallgrassNoiseParams = new NoiseParams(scale: 0.12f, octaves: 3, lacunarity: 2.5f, gain: 0.5f);
        float tallgrassThreshold = 0.05f; // grass half-band around 0.5f

        public NoiseParams treeNoiseParams = new NoiseParams(scale: 1.5f, octaves: 2, lacunarity: 2.5f, gain: 0.8f);
        float treeThreshold = 0.75f; // tree noise greater than this value means a tree is placed

        public TreeParams treeParams = new TreeParams()
        {
            trunkThickness = 1,
            minHeight = 8,
            maxHeight = 18,
            leafStartHeight = 3,
            maxLeafRadius = 3
        };

        private readonly WorldGenerator worldGenerator;

        public FeatureGenerator(WorldGenerator worldGenerator)
        {
            this.worldGenerator = worldGenerator;
        }

        private int GetTreeHeight(float treeNoiseValue)
        {
            if (treeParams.maxHeight <= treeParams.minHeight)
                return treeParams.minHeight;

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

            int leafTop = trunkHeight + 1;
            int canopyHeight = Math.Max(1, leafTop - leafStart);

            // Build a cone tree
            for (int ly = leafStart; ly <= leafTop; ly += 2)
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

                        if (insideTrunkFootprint && ly < trunkHeight)
                            continue;

                        blocks.SetBlock((baseX + dx, baseY + ly, baseZ + dz), BlockType.LEAVES);
                    }
                }
            }
        }

        public ChunkGenerator.CompletedChunkBlocks GrowFlora(ChunkGenerator.CompletedChunkBlocks blocks, ChunkManager manager)
        {
            var worldOffset = blocks.index * Chunk.SIZE;

            for (byte x = 0; x < Chunk.SIZE; x++)
            {
                for (byte y = 0; y < Chunk.SIZE; y++)
                {
                    for (byte z = 0; z < Chunk.SIZE; z++)
                    {
                        bool growableSurface = manager.TryGetBlockAtWorldPosition(worldOffset + (x, y - 1, z), out var result) && result.Type == BlockType.GRASS;

                        if (growableSurface)
                        {
                            float t = worldGenerator.GetNoiseAt(NoiseLayer.TREE, worldOffset.X + x, worldOffset.Z + z);
                            if (t > treeThreshold)
                            {
                                PlaceProceduralTree(ref blocks, x, y, z, t);
                                blocks.SetBlock((x, y - 1, z), BlockType.DIRT);
                                continue;
                            }

                            float f = worldGenerator.GetNoiseAt(NoiseLayer.TALLGRASS, worldOffset.X + x, worldOffset.Z + z);
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

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}
