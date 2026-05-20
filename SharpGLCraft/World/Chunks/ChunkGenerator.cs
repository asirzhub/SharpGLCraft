using OpenTK.Mathematics;
using System.Collections.Concurrent;
using SharpGLCraft.World.Blocks;
using SharpGLCraft.World.Generation;

namespace SharpGLCraft.World.Chunks
{
    public class ChunkGenerator
    {
        // chunk block generation delivery class. Very similar to Chunk class but includes featured flag and the chunk index which the data should go into
        public class CompletedChunkBlocks
        {
            public Vector3i index;
            public bool isEmpty;
            public bool featured;
            public byte[] blocks;

            public ConcurrentDictionary<Vector3i, BlockType> pendingBlocks;

            public CompletedChunkBlocks(Vector3i index, byte[] blocks, bool isEmpty)
            {
                this.index = index;
                this.blocks = blocks;
                this.isEmpty = isEmpty;
                featured = false;
            }

            public CompletedChunkBlocks(Vector3i index, Chunk c)
            {
                this.index = index;
                this.blocks = c.blocks;
                this.isEmpty = c.IsEmpty;
                this.featured = false;
            }

            public Block GetBlock(int x, int y, int z)
            {
                if (isEmpty) return new Block(BlockType.AIR);
                var type = (BlockType)blocks[(y * Chunk.SIZE + z) * Chunk.SIZE + x];
                return new Block(type);
            }

            // returns true if successfully placed in the chunk, false if it spilled over into a neighbor
            public bool SetBlock(Vector3i localPos, BlockType type)
            {
                if (blocks == null)
                    blocks = new byte[Chunk.SIZE * Chunk.SIZE * Chunk.SIZE];

                // if it's out of chunk bounds, store it for later
                if (localPos.X >= Chunk.SIZE || localPos.Y >= Chunk.SIZE || localPos.Z >= Chunk.SIZE ||
                    localPos.X < 0 || localPos.Y < 0 || localPos.Z < 0)
                {
                    if (pendingBlocks == null)
                        pendingBlocks = new();
                    pendingBlocks.TryAdd(index * Chunk.SIZE + localPos, type);
                    return false;
                }

                blocks[(localPos.Y * Chunk.SIZE + localPos.Z) * Chunk.SIZE + localPos.X] = (byte)type;
                return true;
            }

            public void Dispose()
            {
                Array.Clear(this.blocks);
            }
        }

        // chunk's block generation kick-off fxn
        public Task GenerationTask(Vector3i index, CancellationTokenSource cts, WorldGenerator worldGenerator, ConcurrentQueue<CompletedChunkBlocks> queue)
        {
            // async wrapper for the long part of the operation
            return Task.Run(async () =>
            {
                var result = await GenerateBlocks(index, cts.Token, worldGenerator);
                queue.Enqueue(result);
            });
        }

        // generates the blocks for a given chunk and world generator 
        async Task<CompletedChunkBlocks> GenerateBlocks(Vector3i chunkIndex, CancellationToken token, WorldGenerator worldGenerator)
        {
            var tempChunk = new Chunk(lod:1);

            for (int x = 0; x < Chunk.SIZE; x++)
            {
                for (int y = Chunk.SIZE - 1; y >= 0; y--)
                {
                    for (int z = 0; z < Chunk.SIZE; z++)
                    {
                        token.ThrowIfCancellationRequested();

                        int worldX = chunkIndex.X * Chunk.SIZE + x;
                        int worldY = chunkIndex.Y * Chunk.SIZE + y;
                        int worldZ = chunkIndex.Z * Chunk.SIZE + z;

                        tempChunk.SetBlock(x, y, z, worldGenerator.GetBlockAtWorldPos((worldX, worldY, worldZ)));
                    }
                }
            }

            return new CompletedChunkBlocks(chunkIndex, tempChunk.blocks, tempChunk.IsEmpty);
        }

        // chunk's block featuring (grass, trees, etc) kick-off fxn
        public Task FeatureTask(CompletedChunkBlocks chunkBlocks, CancellationTokenSource cts, WorldGenerator worldGenerator, ConcurrentQueue<CompletedChunkBlocks> queue, ChunkManager manager)
        {
            // async wrapper for the long part of the operation
            return Task.Run(async () =>
            {
                var result = await FeatureBlocks(chunkBlocks, cts.Token, worldGenerator, manager);
                queue.Enqueue(result);
            });
        }

        // places the features for a given chunk and world generator. world generation should house the functions for generating features
        async Task<CompletedChunkBlocks> FeatureBlocks(CompletedChunkBlocks chunkBlocks, CancellationToken token, WorldGenerator worldGenerator, ChunkManager manager)
        {
            var result = worldGenerator.GrowFlora(chunkBlocks, manager);

            result.featured = true;

            return result;
        }
    }
}
