using SharpGLCraft.Camera;
using SharpGLCraft.Graphics;
using SharpGLCraft.World.Blocks;
using SharpGLCraft.World.Generation;
using SharpGLCraft.World.Chunks;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Collections.Concurrent;
using System.Diagnostics;

public class ChunkManager
{
    public ChunkRenderer renderer = new ChunkRenderer();
    public ChunkGenerator generator = new ChunkGenerator();
    public ChunkMesher mesher = new ChunkMesher();
    public WorldGenerator worldGenerator = new WorldGenerator(11);

    public Vector3i currentChunkIndex = new();
    public Vector3i lastChunkIndex = new();
    public int radius = 10;
    public int maxChunkTasks;
    public bool chunkTasksHalved = false;
    public float chunkTaskHalftime = 20f;// after 20 seconds, half the chunk tasks (prioritize speed at first, then smoothness)
    public float expiryTime = 2f; // how long a chunk can be "inactive" (seconds) before disposed from ram?

    public byte[] LODCascades = new byte[] { 6, 9, 12 }; // dist 0-6:LOD1, dist 6-9:LOD2, etc.
    public byte[] LODValues = new byte[] {1, 2, 4 };

    // independant lists to keep track of chunks that actually matter
    List<Vector3i> ActiveChunksIndices = new List<Vector3i>();
    List<Vector3i> LastActivationChunksIndices = new List<Vector3i>();

    // link every chunk to its index
    ConcurrentDictionary<Vector3i, Chunk> ActiveChunks = new ConcurrentDictionary<Vector3i, Chunk>();
    
    // also store a list for the indexes of chunks that are about to be deleted from memory due to being too far (hysterisis/thrashing management)
    ConcurrentDictionary<Vector3i, float> ExpiredChunkLifetimes = new ConcurrentDictionary<Vector3i, float>();

    // worker threads deposit results in these
    ConcurrentQueue<ChunkGenerator.CompletedChunkBlocks> CompletedBlocksQueue = new(); // terrain generated and featured chunk data
    ConcurrentQueue<ChunkMesher.CompletedMesh> CompletedMeshQueue = new(); // mesh data from block data

    // worker tasks list to control the number of worker threads
    ConcurrentDictionary<Vector3i, Task> RunningTasks = new();
    ConcurrentDictionary<Vector3i, CancellationTokenSource> RunningTasksCTS = new();

    // global block queue that delivers pending blocks to their chunks eventually
    ConcurrentDictionary<Vector3i, BlockType> PendingBlocks = new();

    public int taskCount => RunningTasks.Count;

    public ChunkManager(AerialCameraRig camera)
    {
        // idk if this is the best way might change later
        maxChunkTasks = (int)(Environment.ProcessorCount -1);

        currentChunkIndex = WorldPosToChunkIndex((
                                (int)MathF.Floor(camera.CameraPosition().X),
                                (int)MathF.Floor(camera.CameraPosition().Y),
                                (int)MathF.Floor(camera.CameraPosition().Z)), out _); ; // first index in the list is always the center index  

        noiseWatch.Start();
    }

    bool shadowFrame = true;
    Stopwatch noiseWatch = new Stopwatch();
    
    public void Update(AerialCameraRig camera, float frameTime, float time, int frameCount, Vector3 sunDirection, SkyRender skyRender)
    {
        // reduce chunk tasks after first burst 
        if (!chunkTasksHalved)
        {
            chunkTaskHalftime -= frameTime;
            if (chunkTaskHalftime < 0)
            {
                chunkTasksHalved = true;
                maxChunkTasks /= 2;
            }
        }

        // deliver pending blocks to their chunks if possible
        var oldPendingBlocks = PendingBlocks;
        foreach (var kvp in oldPendingBlocks)
        {
            Vector3i worldPos = kvp.Key;
            if (ActiveChunks.TryGetValue(WorldPosToChunkIndex(worldPos, out var localCoord), out var chunk))
            {
                var state = chunk.GetState();
                if (state == ChunkState.VISIBLE || state == ChunkState.INVISIBLE || state == ChunkState.MESHED) // be cautious but not overly about updating the pending chunk blocks
                {
                    chunk.AddPendingBlock(localCoord, kvp.Value);
                    PendingBlocks.TryRemove(kvp.Key, out _);
                }
            }
        }

        bool updateIndices = currentChunkIndex != lastChunkIndex; // refresh active chunks list when the camera moves between two chunks

        if (updateIndices)
            ActiveChunksIndices = ListActiveChunksIndices(camera.focusPoint, radius, radius / 3);

        currentChunkIndex = WorldPosToChunkIndex((
                                (int)MathF.Floor(camera.focusPoint.X),
                                (int)MathF.Floor(camera.focusPoint.Y),
                                (int)MathF.Floor(camera.focusPoint.Z)), out _); ;

        // for each CompletedChunkBlocks, move data from the queue into the respective chunk. MARK STATE
        while (CompletedBlocksQueue.TryDequeue(out var completedBlocks))
        {
            Chunk targetChunk = ActiveChunks[completedBlocks.index];
            targetChunk.blocks = completedBlocks.blocks;
            targetChunk.IsEmpty = completedBlocks.isEmpty;
            RunningTasks.TryRemove(completedBlocks.index, out _);
            RunningTasksCTS.TryRemove(completedBlocks.index, out _);

            // also store the spilled blocks from features
            if(completedBlocks.pendingBlocks != null)
                foreach(var kvp in completedBlocks.pendingBlocks)
                {
                    PendingBlocks.TryAdd(kvp.Key, kvp.Value);
                }

            // if the chunk's block data both generated AND featured, mark it so
            if (completedBlocks.featured)
            {
                targetChunk.SetState(ChunkState.FEATURED);
            }
            else // otherwise this queue item is only generated and not featured. need to mark it for a feature task
            {
                targetChunk.SetState(ChunkState.GENERATED);
            }

        }

        // for each CompletedMesh, move data from the queue into the respective chunk. MARK STATE
        while (CompletedMeshQueue.TryDequeue(out var resultChunk))
        {
            Chunk targetChunk = ActiveChunks[resultChunk.index];
            targetChunk.solidMesh = resultChunk.solidMesh;
            targetChunk.transparentMesh = resultChunk.liquidMesh;
            targetChunk.SetState(ChunkState.MESHED);
            RunningTasks.TryRemove(resultChunk.index, out _);
            RunningTasksCTS.TryRemove(resultChunk.index, out _);
        }


        // for every chunk that's still relevant
        foreach (var idx in ActiveChunksIndices)
        {
            byte lodLevel = DetermineLOD(currentChunkIndex, idx);

            // if the chunk has existed already
            if (ActiveChunks.TryGetValue(idx, out var chunk))
            {
                var state = chunk.GetState();

                // un-expire chunks that were expired but need to be brought back before expiry
                if (ExpiredChunkLifetimes.ContainsKey(idx))
                    ExpiredChunkLifetimes.TryRemove(idx, out _);

                // determine if the chunk is in view or not (for culling)
                if(state == ChunkState.VISIBLE || state == ChunkState.INVISIBLE)
                        chunk.SetState(IsChunkInView(camera, idx)? ChunkState.VISIBLE : ChunkState.INVISIBLE);

                // different instructions for different chunk states
                switch (state)
                {
                    case ChunkState.BIRTH:
                        // if theres room to add a generation job for a new chunk, do it
                        if (RunningTasks.Count < maxChunkTasks)
                        {
                            chunk.SetState(ChunkState.GENERATING);
                            CancellationTokenSource cts = new CancellationTokenSource();
                            RunningTasksCTS.TryAdd(idx, cts);
                            RunningTasks.TryAdd(idx, generator.GenerationTask(idx, cts, worldGenerator, CompletedBlocksQueue));
                        }

                        break;
                    case ChunkState.GENERATED:
                        // if the chunk has terrain blocks and theres room for a task, start feature task for it
                        if (AreNeighborsGenerated(idx) && RunningTasks.Count < maxChunkTasks)
                        {
                            chunk.SetState(ChunkState.FEATURING);
                            CancellationTokenSource cts = new CancellationTokenSource();
                            RunningTasksCTS.TryAdd(idx, cts);
                            RunningTasks.TryAdd(idx, generator.FeatureTask(new ChunkGenerator.CompletedChunkBlocks(idx, chunk), cts, worldGenerator, CompletedBlocksQueue, this));
                        }
                        break;
                    case ChunkState.FEATURED:
                        // if the chunk has terrain + feature blocks, neighbors have blocks, and theres room for a task, make mesh for it
                        if (AreNeighborsGenerated(idx) && RunningTasks.Count < maxChunkTasks)
                        {
                            chunk.SetState(ChunkState.MESHING);
                            CancellationTokenSource cts = new CancellationTokenSource();
                            RunningTasksCTS.TryAdd(idx, cts);
                            RunningTasks.TryAdd(idx, mesher.MeshTask(idx, this, cts, CompletedMeshQueue, ActiveChunks, LOD: 1));
                        }
                        break;
                    case ChunkState.MESHED:
                        chunk.SetState(ChunkState.VISIBLE);

                        // mark chunks with no mesh (all air) as invisible
                        if (chunk.IsEmpty)
                            chunk.SetState(ChunkState.INVISIBLE);
                                                
                        break;

                    case ChunkState.VISIBLE:
                        if (lodLevel != chunk.LOD)
                        {
                            if(chunk.TryMarkDirty())
                                chunk.LOD = lodLevel;
                        }

                        var dirtyNeighbors = chunk.ProcessPendingBlocksAndGetDirtyNeighbors();

                        if (dirtyNeighbors.Result !=null && dirtyNeighbors.Result.Count > 0)
                            foreach(var direction in dirtyNeighbors.Result)
                                if (ActiveChunks.TryGetValue(idx + direction, out var dirtyChunk))
                                    dirtyChunk.TryMarkDirty();
                        break;

                    case ChunkState.DIRTY:
                        // dirty chunks need to get re-meshed
                        if (AreNeighborsGenerated(idx) && RunningTasks.Count < maxChunkTasks)
                        {
                            chunk.SetState(ChunkState.MESHING);
                            CancellationTokenSource cts = new CancellationTokenSource();
                            RunningTasksCTS.TryAdd(idx, cts);
                            RunningTasks.TryAdd(idx, mesher.MeshTask(idx, this, cts, CompletedMeshQueue, ActiveChunks, LOD: lodLevel));
                        }
                        break;
                    default:
                        break;
                }
            }
            else
                ActiveChunks.TryAdd(idx, new Chunk(lodLevel)); // adding a brand new chunk to the system
        }

        // assign expired chunks to expiry list if they're not in the current list
        foreach (var idx in LastActivationChunksIndices)
        {
            if (!ActiveChunksIndices.Contains(idx))
                MarkExpired(idx);
        }

        // for each chunk in the expiry list, check if it's expired. if it is, dispose it from ram
        foreach (var kvp in ExpiredChunkLifetimes)
        {
            if (kvp.Value > expiryTime)
            {
                ExpiredChunkLifetimes.TryRemove(kvp.Key, out _);
                DisposeChunk(kvp.Key);
            }
            // unexpired chunks will wait a little longer
            else
                ExpiredChunkLifetimes[kvp.Key] = kvp.Value + frameTime;
        }

        // important to update the old and current lists of indexes
        if (updateIndices)
        {
            LastActivationChunksIndices = new List<Vector3i>(ActiveChunksIndices);
            lastChunkIndex = currentChunkIndex;
        }

        if (shadowFrame)
        {
            renderer.RenderShadowMapPass(camera, time, ActiveChunks, skyRender);
            shadowFrame = false;
        }
        else shadowFrame = true;

        // check for expired noise cache entries every three seconds
        if (noiseWatch.ElapsedMilliseconds > 10000f)
        {
            worldGenerator.Update(frameCount);
            noiseWatch.Restart();
        }

        // render chunks
        renderer.RenderLightingPass(camera, time, ActiveChunks, skyRender, worldGenerator.seaLevel);
    }

    // naive frustrum culling using a cone frustrum
    public bool IsChunkInView(AerialCameraRig camera, Vector3i idx)
    {
        Vector3 chunkWorldCoord = idx * Chunk.SIZE + Vector3.One * Chunk.SIZE/2; // center of the chunk
        Vector3 chunkToCamera = chunkWorldCoord - camera.CameraPosition();

        if (chunkToCamera.LengthFast < 2 * Chunk.SIZE) // if the chunk is too close, exit out with true
            return true;

        float angle = Vector3.CalculateAngle(camera.forward, chunkToCamera);

        if (angle < 1.2f) // if within view, true
            return true;

        return false;
    }

    public byte DetermineLOD(Vector3 center, Vector3 target)
        => ChunkMath.DetermineLOD(center, target, LODCascades, LODValues);

    // idk why i thought this was easier but whatever might need it later on
    public void MarkExpired(Vector3i idx)
    {
        ExpiredChunkLifetimes.TryAdd(idx, 0f);
    }

    // returns true if the 6 adjacent chunks are at least at the generated state (false if birth/generating)
    public bool AreNeighborsGenerated(Vector3i centerIndex)
    {
        Vector3i[] directions = { Vector3i.UnitX, -Vector3i.UnitX,
                                Vector3i.UnitY, -Vector3i.UnitY,
                                Vector3i.UnitZ, -Vector3i.UnitZ,};

        foreach (var dir in directions)
        {
            bool exists = TryGetChunkAtIndex(centerIndex + dir, out var c);
            if (!exists || !c.NeighborReady)
                return false;
        }

        return true;
    }

    // chunk disposal involves deleting all the chunk information from ram (blocks, meshes, tasks)
    public void DisposeChunk(Vector3i index)
    {
        if (ActiveChunks.TryRemove(index, out var chunk))
        {
            chunk.blocks = null;  // release block storage
            chunk.IsEmpty = true;
            chunk.DisposeMeshes();

            if (RunningTasksCTS.TryGetValue(index, out var cts))
                cts.CancelAsync();
            if (RunningTasks.TryGetValue(index, out var task))
                task.Dispose();
        }
    }

    // generates the list of chunk indices that need to be ready. ordered to start at the center index and spiral out.
    public List<Vector3i> ListActiveChunksIndices(Vector3 centerWorldPos, int horizontalRadius, int verticalRadius)
    {
        List<Vector3i> result = new();
        Vector3i centerIndex = currentChunkIndex;

        HashSet<Vector3i> visited = new();
        Queue<Vector3i> queue = new();

        // Start from center
        queue.Enqueue(centerIndex);
        visited.Add(centerIndex);

        Vector3i[] directions =
        {   new Vector3i( 1, 0, 0),
            new Vector3i(-1, 0, 0),
            new Vector3i( 0, 1, 0),
            new Vector3i( 0,-1, 0),
            new Vector3i( 0, 0, 1),
            new Vector3i( 0, 0,-1),
        };

        // BFS chunk selector uses the queue
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            foreach (var dir in directions)
            {
                var next = current + dir;
                Vector3i delta = next - centerIndex;

                // Bound check
                if (Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z) < horizontalRadius &&
                    Math.Abs(delta.Y) <= verticalRadius &&
                    //ActiveChunks.TryGetValue(next, out var c)  &&
                    visited.Add(next)) // only enqueue if not seen
                {
                    //if(!c.IsEmpty)
                        queue.Enqueue(next);
                }
            }
        }
        
        return result;
    }

    // attempts to get a chunk from the list
    public bool TryGetChunkAtIndex(Vector3i index, out Chunk result)
    {
        result = default;
        var exists = ActiveChunks.TryGetValue(index, out var chunk);

        if (exists)
        {
            result = chunk;
            return true;
        }
        return false;
    }

    public Vector3i WorldPosToChunkIndex(Vector3i worldIndex, out Vector3i localBlockPos, int chunkSize = Chunk.SIZE)
        => ChunkMath.WorldPosToChunkIndex(worldIndex, out localBlockPos, chunkSize);

    // also self explanatory
    public bool TryGetBlockAtWorldPosition(Vector3i worldIndex, out Block result, int chunkSize = Chunk.SIZE)
    {
        result = default;

        Vector3i chunkIndex = WorldPosToChunkIndex(worldIndex, out Vector3i localBlockPos, chunkSize);

        if (TryGetChunkAtIndex(chunkIndex, out Chunk targetChunk))
        {
            result = targetChunk.GetBlock(localBlockPos.X, localBlockPos.Y, localBlockPos.Z);
            return true;
        }
        return false;
    }

    public bool TrySetBlockAtWorldPosition(Vector3i worldIndex, BlockType type, int chunkSize = Chunk.SIZE)
    {
        Vector3i chunkIndex = WorldPosToChunkIndex(worldIndex, out Vector3i localBlockPos, chunkSize);

        if (TryGetChunkAtIndex(chunkIndex, out Chunk targetChunk))
        {
            targetChunk.AddPendingBlock(localBlockPos, type);
            return true;
        }
        return false;
    }
}
