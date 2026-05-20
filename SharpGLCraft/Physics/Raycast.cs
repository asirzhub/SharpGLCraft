using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpGLCraft.Physics
{
    public class Raycast
    {
        public static bool RaycastSolidBlock(
        ChunkManager cm,
        Vector3 origin,
        Vector3 dir,
        float maxDist,
        int maxSteps,
        out Vector3i hitBlock,
        out Vector3i lastBlockBeforeHit
        )
        {
            hitBlock = default;

            // Start cell
            int x = (int)MathF.Floor(origin.X);
            int y = (int)MathF.Floor(origin.Y);
            int z = (int)MathF.Floor(origin.Z);

            int stepX = dir.X > 0f ? 1 : (dir.X < 0f ? -1 : 0);
            int stepY = dir.Y > 0f ? 1 : (dir.Y < 0f ? -1 : 0);
            int stepZ = dir.Z > 0f ? 1 : (dir.Z < 0f ? -1 : 0);

            float tDeltaX = stepX == 0 ? float.PositiveInfinity : MathF.Abs(1f / dir.X);
            float tDeltaY = stepY == 0 ? float.PositiveInfinity : MathF.Abs(1f / dir.Y);
            float tDeltaZ = stepZ == 0 ? float.PositiveInfinity : MathF.Abs(1f / dir.Z);

            // Distance to first boundary
            float nextVoxelBoundaryX = stepX > 0 ? (x + 1) : x;
            float nextVoxelBoundaryY = stepY > 0 ? (y + 1) : y;
            float nextVoxelBoundaryZ = stepZ > 0 ? (z + 1) : z;

            // infinities are protection against edge case where pointing exactly along an axis
            float tMaxX = stepX == 0 ? float.PositiveInfinity : (nextVoxelBoundaryX - origin.X) / dir.X;
            float tMaxY = stepY == 0 ? float.PositiveInfinity : (nextVoxelBoundaryY - origin.Y) / dir.Y;
            float tMaxZ = stepZ == 0 ? float.PositiveInfinity : (nextVoxelBoundaryZ - origin.Z) / dir.Z;

            // no negatives
            if (tMaxX < 0f) tMaxX = 0f;
            if (tMaxY < 0f) tMaxY = 0f;
            if (tMaxZ < 0f) tMaxZ = 0f;

            float t = 0f;

            Vector3i lastMove = (0, 0, 0);

            for (int i = 0; i < maxSteps && t <= maxDist; i++)
            {
                // Check current cell
                if (cm.TryGetBlockAtWorldPosition(new Vector3i(x, y, z), out var b) && b.IsSolid)
                {
                    hitBlock = new Vector3i(x, y, z);
                    lastBlockBeforeHit = hitBlock - lastMove;
                    return true;
                }

                // Step to next cell
                if (tMaxX <= tMaxY && tMaxX <= tMaxZ)
                {
                    x += stepX;
                    t = tMaxX;
                    tMaxX += tDeltaX;
                    lastMove = (stepX, 0, 0);
                }
                else if (tMaxY <= tMaxZ)
                {
                    y += stepY;
                    t = tMaxY;
                    tMaxY += tDeltaY;
                    lastMove = (0, stepY, 0);
                }
                else
                {
                    z += stepZ;
                    t = tMaxZ;
                    tMaxZ += tDeltaZ;
                    lastMove = (0, 0, stepZ);
                }

            }

            lastBlockBeforeHit = lastMove;
            return false;
        }
    }
}
