using SharpGLCraft.Graphics;
using SharpGLCraft.Graphics.Buffers;
using SharpGLCraft.Graphics.Meshes;
using SharpGLCraft.World.Blocks;
using OpenTK.Mathematics;
using System.Collections.Concurrent;
using static SharpGLCraft.Graphics.Buffers.VBO;

namespace SharpGLCraft.World.Chunks
{
    public class ChunkMesher
    {
        public ChunkMesher()
        {

        }

        // chunk mesh delivery class
        public class CompletedMesh
        {
            public Vector3i index;
            public MeshData solidMesh;
            public MeshData liquidMesh;

            public CompletedMesh(Vector3i index, MeshData solidMesh, MeshData liquidMesh)
            {
                this.index = index;
                this.solidMesh = solidMesh;
                this.liquidMesh = liquidMesh;
            }

            public void Dispose()
            {
                this.solidMesh.Dispose();
                this.liquidMesh.Dispose();
            }
        };
        // chunk mesher kick-off fxn
        public Task MeshTask(Vector3i index, ChunkManager manager, 
            CancellationTokenSource cts, ConcurrentQueue<CompletedMesh> queue, 
            ConcurrentDictionary<Vector3i, Chunk> ActiveChunks,
            byte LOD = 1)
        {
            Chunk thisChunk = ActiveChunks[index];

            // skip meshing if it's empty
            if (thisChunk.IsEmpty)
            {
                CompletedMesh result = new CompletedMesh(index, new MeshData(), new MeshData());
                queue.Enqueue(result);
                return Task.CompletedTask;
            }

            return Task.Run(async () =>
            {

                var result = await BuildMesh(index, manager, cts.Token, LOD);

                queue.Enqueue(result);

                //RunningTasks.TryRemove(index, out _);
                //RunningTasksCTS.TryRemove(index, out _);
            });
        }

        // naive meshing
        Task<CompletedMesh> BuildMesh(Vector3i chunkIndex, ChunkManager manager, CancellationToken token, byte LOD)
        {
            manager.TryGetChunkAtIndex(chunkIndex, out var chunk);
            return Task.Run(async () =>
            {
                Vector3i localOrigin = chunkIndex * Chunk.SIZE;

                int seaLevel = manager.worldGenerator.seaLevel;

                MeshData solidResult = new MeshData();
                uint solidVertexOffset = 0;

                MeshData liquidResult = new MeshData();
                uint liquidVertexOffset = 0;

                // local chunk coordinate 
                for (byte x = 0; x < Chunk.SIZE; x+=LOD)
                {
                    if (token.IsCancellationRequested) break; // early exit

                    for (byte y = 0; y < Chunk.SIZE; y+=LOD)
                    {
                        for (byte z = 0; z < Chunk.SIZE; z+=LOD)
                        {

                            Block block = chunk.GetBlock(x, y, z);
                            if (block.Type == BlockType.AIR) // skip air
                                continue;

                            Vector3i blockWorldPos = localOrigin + (x, y, z);

                            // for each face
                            if (block.Type != BlockType.TALLGRASS)
                            {
                                // base-layer culling: hide faces hidden by solid blocks
                                bool[] occlusions = new bool[6];

                                // front
                                if (manager.TryGetBlockAtWorldPosition(blockWorldPos + new Vector3i(0, 0, +LOD), out var bF))
                                    occlusions[0] = bF.IsSolid || block.Type == bF.Type || (block.IsWater && bF.IsWater) ;

                                // back
                                if (manager.TryGetBlockAtWorldPosition(blockWorldPos + new Vector3i(0, 0, -LOD), out var bB))
                                    occlusions[1] = bB.IsSolid || block.Type == bB.Type || (block.IsWater && bB.IsWater);

                                // left
                                if (manager.TryGetBlockAtWorldPosition(blockWorldPos + new Vector3i(-LOD, 0, 0), out var bL))
                                    occlusions[2] = bL.IsSolid || block.Type == bL.Type || (block.IsWater && bL.IsWater);

                                // right
                                if (manager.TryGetBlockAtWorldPosition(blockWorldPos + new Vector3i(+LOD, 0, 0), out var bR))
                                    occlusions[3] = bR.IsSolid || block.Type == bR.Type || (block.IsWater && bR.IsWater);

                                // up
                                if (manager.TryGetBlockAtWorldPosition(blockWorldPos + new Vector3i(0, +LOD, 0), out var bU))
                                    occlusions[4] = bU.IsSolid || block.Type == bU.Type || (block.IsWater && bU.IsWater); ;

                                // down
                                if (manager.TryGetBlockAtWorldPosition(blockWorldPos + new Vector3i(0, -LOD, 0), out var bD))
                                    occlusions[5] = bD.IsSolid || block.Type == bD.Type || (block.IsWater && bD.IsWater);

                                foreach (CubeMesh.Face face in Enum.GetValues(typeof(CubeMesh.Face)))
                                {
                                    int faceIndex = (int)face;
                                    if (occlusions[faceIndex]) continue;              // skip hidden faces
                                    var faceVerts = CubeMesh.PackedFaceVertices[face];

                                    // Append 4 corners
                                    for (int i = 0; i < 4; i++)
                                    {
                                        var v = faceVerts[i];

                                        // Local position within chunk (0..16)
                                        var vPos = v.Position();
                                        byte lx = (byte)(x + vPos.X * LOD);
                                        byte ly = (byte)(y + vPos.Y * LOD);
                                        byte lz = (byte)(z + vPos.Z * LOD);

                                        // Compute UV
                                        var registryData = BlockRegistry.Types[block.Type];
                                        var faceUVs = registryData.FaceUVs;
                                        Vector2 tile = faceUVs[faceIndex];
                                        Vector2 uv = (tile + (v.TexU, v.TexV)) / 8f;
                                        uv.Y = 1f - uv.Y;

                                        byte normal = (byte)face;

                                        byte lightLevel = 15;

                                        if (blockWorldPos.Y < seaLevel && blockWorldPos.Y > seaLevel - 6)
                                        {
                                            lightLevel = (byte)(15 - (seaLevel - blockWorldPos.Y));
                                        }
                                        else if (blockWorldPos.Y <= seaLevel - 6)
                                        {
                                            lightLevel = (byte)9;
                                        }

                                        // check the edges in the direction of the vertex to do ambient occlusion with
                                        Vector3i[] AOCheckDirection = new Vector3i[4];
                                        AOCheckDirection[0] = ((int)(MathF.Round((vPos.X - 0.5f) * 2f)),
                                            0,
                                            (int)(MathF.Round((vPos.Z - 0.5f) * 2f)));

                                        AOCheckDirection[1] = ((int)(MathF.Round((vPos.X - 0.5f) * 2f)),
                                            (int)(MathF.Round((vPos.Y - 0.5f) * 2f)),
                                            0);

                                        AOCheckDirection[2] = (0,
                                            (int)(MathF.Round((vPos.Y - 0.5f) * 2f)),
                                            (int)(MathF.Round((vPos.Z - 0.5f) * 2f)));

                                        AOCheckDirection[3] = ((int)(MathF.Round((vPos.X - 0.5f) * 2f)),
                                            (int)(MathF.Round((vPos.Y - 0.5f) * 2f)),
                                            (int)(MathF.Round((vPos.Z - 0.5f) * 2f)));

                                        byte occluderCount = 0;
                                        foreach (var direction in AOCheckDirection)
                                        {
                                            if (lightLevel > 1)
                                            {
                                                manager.TryGetBlockAtWorldPosition(blockWorldPos + direction, out var b);
                                                if (b.IsSolid)
                                                {
                                                    occluderCount += (byte)1;
                                                }
                                            }
                                        }

                                        if (occluderCount >= 2) lightLevel -= occluderCount;//ambient occlusion can only happen with two or more occluders

                                        if (block.IsWater)
                                        {
                                            liquidResult.Vertices.Add(
                                                new PackedVertex(lx, ly, lz, uv.X, uv.Y, normal, lightLevel, registryData.wiggleType)
                                            );

                                            // Two triangles (0,1,2) & (2,3,0)
                                            liquidResult.Indices.Add(liquidVertexOffset + 0);
                                            liquidResult.Indices.Add(liquidVertexOffset + 1);
                                            liquidResult.Indices.Add(liquidVertexOffset + 2);
                                            liquidResult.Indices.Add(liquidVertexOffset + 2);
                                            liquidResult.Indices.Add(liquidVertexOffset + 3);
                                            liquidResult.Indices.Add(liquidVertexOffset + 0);

                                            liquidVertexOffset += 4;
                                        }
                                        else
                                        {
                                            solidResult.Vertices.Add(
                                                new PackedVertex(lx, ly, lz, uv.X, uv.Y, normal, lightLevel, registryData.wiggleType)
                                            );

                                            // Two triangles (0,1,2) & (2,3,0)
                                            solidResult.Indices.Add(solidVertexOffset + 0);
                                            solidResult.Indices.Add(solidVertexOffset + 1);
                                            solidResult.Indices.Add(solidVertexOffset + 2);
                                            solidResult.Indices.Add(solidVertexOffset + 2);
                                            solidResult.Indices.Add(solidVertexOffset + 3);
                                            solidResult.Indices.Add(solidVertexOffset + 0);

                                            solidVertexOffset += 4;
                                        }
                                    }
                                }
                            }
                            else if (block.Type == BlockType.TALLGRASS)
                            {
                                foreach (var face in GrassMesh.packedVertices)
                                {
                                    var faceUVs = BlockRegistry.Types[block.Type].FaceUVs;
                                    Vector2 tile = faceUVs[0];

                                    foreach (var vertex in face)
                                    {
                                        Vector3i vPos = vertex.Position();
                                        byte lx = (byte)(x + vPos.X * LOD);
                                        byte ly = (byte)(y + vPos.Y * LOD);
                                        byte lz = (byte)(z + vPos.Z * LOD);

                                        byte lightLevel = (byte)(14 + vPos.Y);

                                        Vector2 uv = (tile + (vertex.TexU, vertex.TexV)) / 8f;
                                        uv.Y = 1f - uv.Y;

                                        // 4 - normal pointed upward, matching the surface it's on
                                        var wiggleType = BlockRegistry.Types[block.Type].wiggleType;
                                        if (vPos.Y == 0) wiggleType = WiggleType.NONE;
                                        solidResult.Vertices.Add(
                                                new PackedVertex(lx, ly, lz, uv.X, uv.Y, 4, lightLevel, wiggleType)
                                            );

                                        // Two triangles (0,1,2) & (2,3,0)
                                        solidResult.Indices.Add(solidVertexOffset + 0);
                                        solidResult.Indices.Add(solidVertexOffset + 1);
                                        solidResult.Indices.Add(solidVertexOffset + 2);
                                        solidResult.Indices.Add(solidVertexOffset + 2);
                                        solidResult.Indices.Add(solidVertexOffset + 3);
                                        solidResult.Indices.Add(solidVertexOffset + 0);

                                        //solidVertexOffset += 4;
                                    }
                                }
                            }


                        }
                    }
                }
                return new CompletedMesh(chunkIndex, solidResult, liquidResult);
            });
        }
    }
}
