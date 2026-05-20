using OpenTK.Mathematics;

namespace SharpGLCraft.World.Chunks
{
    public static class ChunkMath
    {
        public static Vector3i WorldPosToChunkIndex(Vector3i worldIndex, out Vector3i localBlockPos, int chunkSize = Chunk.SIZE)
        {
            int chunkX = (int)Math.Floor(worldIndex.X / (double)chunkSize);
            int chunkY = (int)Math.Floor(worldIndex.Y / (double)chunkSize);
            int chunkZ = (int)Math.Floor(worldIndex.Z / (double)chunkSize);

            localBlockPos = new Vector3i(
                worldIndex.X - chunkX * chunkSize,
                worldIndex.Y - chunkY * chunkSize,
                worldIndex.Z - chunkZ * chunkSize
            );

            return new Vector3i(chunkX, chunkY, chunkZ);
        }

        public static byte DetermineLOD(Vector3 center, Vector3 target, byte[] lodCascades, byte[] lodValues)
        {
            int dist = (int)MathF.Floor(Vector3.Distance(center, target));
            for (int i = 0; i < lodCascades.Length; i++)
            {
                if (dist < lodCascades[i])
                    return lodValues[i];
            }
            return 8;
        }
    }
}
