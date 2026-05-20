using SharpGLCraft.Graphics;
using SharpGLCraft.World.Blocks;
using OpenTK.Mathematics;
using System.Collections.Concurrent;

namespace SharpGLCraft.World.Chunks
{
    // Chunk State Machine states
    public enum ChunkState
    {
        BIRTH,
        GENERATING,
        GENERATED,
        FEATURING,
        FEATURED,
        MESHING,
        MESHED,
        VISIBLE,
        INVISIBLE,
        DIRTY
    };

    public class Chunk(byte lod, ChunkState state = ChunkState.BIRTH)
    {
        public byte LOD = lod;
        public const int SIZE = 64; // same size in all coordinates
        private ChunkState state = state;

        public ChunkState GetState() { return state; } // protect the state from being directly modified
        
        public bool NeighborReady => !((state == ChunkState.BIRTH) || (state == ChunkState.GENERATING));// can the neighbor query this block for block existence?

        public ConcurrentDictionary<Vector3i, BlockType> pendingBlocks; // blocks that the chunk needs to update with
            
        // is the chunk empty?
        public bool IsEmpty { 
            get => isEmpty; 
            set => isEmpty = value; }

        // limits unique blocks in the game to 256 which is fine
        public byte[] blocks;
        private bool isEmpty = true;

        // Chunks store their mesh data
        public MeshData solidMesh;
        public MeshData transparentMesh;

        // if a chunk is updated, it must be marked dirty (for a re-mesh)
        public bool TryMarkDirty()
        {
            if(state == ChunkState.DIRTY) return true;
            if (LegalTransitions[ChunkState.DIRTY].Contains(state))
            {
                SetState(ChunkState.DIRTY);
                return true;
            }
            return false;
        }

        public void AddPendingBlock(Vector3i pos, BlockType newType)
        {
            if (pendingBlocks == null)
                pendingBlocks = new();

            if (pendingBlocks.TryGetValue(pos, out var oldType))
            {
                if (oldType.Equals(newType))
                    return;
            }

            else
                pendingBlocks.TryAdd(pos, newType);
        }

        // really need a better function name
        public async Task<List<Vector3i>?> ProcessPendingBlocksAndGetDirtyNeighbors()
        {
            if (pendingBlocks == null || pendingBlocks.IsEmpty)
                return null;

            var result = await Task.Run(async () =>
            {
                List<Vector3i> dirtyNeighbors = new();
                foreach (var kvp in pendingBlocks.ToArray())
                {
                    var neighbors = await SetBlockAsyncAndGetDirtyNeighbors(kvp.Key, kvp.Value);
                    foreach(var neighbor in neighbors) if (!dirtyNeighbors.Contains(neighbor)) dirtyNeighbors.Add(neighbor);
                    pendingBlocks.TryRemove(kvp.Key, out _);
                }
                return dirtyNeighbors;
            });

            return result;
        }

        // practice chunk safety: enforce state transitions
        // {result state , beginning state(s) allowed}
        public static readonly Dictionary<ChunkState, List<ChunkState>> LegalTransitions = new()
        {
            { ChunkState.GENERATING, new(){ ChunkState.BIRTH } },
            { ChunkState.GENERATED, new() { ChunkState.GENERATING} },
            { ChunkState.FEATURING, new() { ChunkState.GENERATED} },
            { ChunkState.FEATURED, new() { ChunkState.FEATURING} },
            { ChunkState.MESHING, new(){ ChunkState.DIRTY, ChunkState.FEATURED, ChunkState.MESHING } },
            { ChunkState.MESHED, new() { ChunkState.MESHING} },
            { ChunkState.VISIBLE, new(){ ChunkState.MESHED, ChunkState.INVISIBLE, ChunkState.VISIBLE} },
            { ChunkState.INVISIBLE, new(){ChunkState.MESHED, ChunkState.VISIBLE, ChunkState.INVISIBLE } },
            { ChunkState.DIRTY, new(){ ChunkState.VISIBLE, ChunkState.MESHED} }
        };
        // Safe chunk state management to enforce legal state transitions
        public void SetState(ChunkState NewState)
        {
            if (!LegalTransitions[NewState].Contains(state))
                throw new Exception($"nuh uh, can't go from {state} -> {NewState}!");
            else
                state = NewState;
        }

        // returns a block at the gievn local coordinate
        public Block GetBlock(int x, int y, int z)
        {
            if (IsEmpty) return new Block(BlockType.AIR);
            
            BlockType type = BlockType.AIR;

            if(blocks!=null) // added this check since there's a random error here sometimes that blocks[] is null... but the debugger clearly shows blocks exists. idfk.
                type = (BlockType)blocks[(y * SIZE + z) * SIZE + x];

            return new Block(type);
        }

        // sets the block at a given local coordinate. this should only be used on creation, everything else on async method
        public void SetBlock(int x, int y, int z, BlockType type)
        {
            if (IsEmpty && type == BlockType.AIR) return;

            if (IsEmpty)
            {
                IsEmpty = false;
                blocks = new byte[SIZE * SIZE * SIZE];
            }
            blocks[(y * SIZE + z) * SIZE + x] = (byte)type;

            TryMarkDirty();
        }

        public Task<List<Vector3i>> SetBlockAsyncAndGetDirtyNeighbors(Vector3i pos, BlockType type)
        {
            if (IsEmpty && type == BlockType.AIR) return Task.FromResult(new List<Vector3i>());

            if (IsEmpty)
            {
                IsEmpty = false;
                blocks = new byte[SIZE * SIZE * SIZE];
            }

            blocks[(pos.Y * SIZE + pos.Z) * SIZE + pos.X] = (byte)type;

            TryMarkDirty();
            return Task.FromResult(NeighborDirtyAlert(pos));
        }

        // helper function to determine if neighbors must be marked dirty also (border block updates)
        public List<Vector3i> NeighborDirtyAlert(Vector3i localCoord)
        {
            List<Vector3i> result = new();

            if (localCoord.X == 0) result.Add(new Vector3i(-1, 0, 0));
            if (localCoord.Y == 0) result.Add(new Vector3i(0, -1, 0));
            if (localCoord.Z == 0) result.Add(new Vector3i(0, 0, -1));
            if (localCoord.X == SIZE - 1) result.Add(new Vector3i(1, 0, 0));
            if (localCoord.Y == SIZE - 1) result.Add(new Vector3i(0, 1, 0));
            if (localCoord.Z == SIZE - 1) result.Add(new Vector3i(0, 0, 1));

            return result;
        }

        // clean up meshes when removing from memory
        public void DisposeMeshes()
        {
            solidMesh?.Dispose();
            transparentMesh?.Dispose();
        }
    }
}