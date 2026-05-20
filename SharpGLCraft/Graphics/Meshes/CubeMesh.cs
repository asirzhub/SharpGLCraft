using OpenTK.Mathematics;
using SharpGLCraft.Graphics;
using static SharpGLCraft.Graphics.Buffers.VBO;

namespace SharpGLCraft.Graphics.Meshes
{
    // Literally just information on how to make a cube
    public class CubeMesh : MeshData
    {
        // english to track faces
        public enum Face
        {
            FRONT,
            BACK,
            LEFT,
            RIGHT,
            TOP,
            BOTTOM
        }

        // (0,1,2) & (2,3,0)
        // 01 11 10 &

        // PackedVertex arrays for each face (x, y, z, u, v, normalIdx, brightness)
        private static readonly PackedVertex[] PackedFaceFront = new PackedVertex[] {
            new PackedVertex(0, 0, 1, 0f, 0f, 0, 255),
            new PackedVertex(1, 0, 1, 1f, 0f, 0, 255),
            new PackedVertex(1, 1, 1, 1f, 1f, 0, 255),
            new PackedVertex(0, 1, 1, 0f, 1f, 0, 255),
        };

        private static readonly PackedVertex[] PackedFaceBack = new PackedVertex[] {
            new PackedVertex(1, 0, 0, 0f, 0f, 1, 255),
            new PackedVertex(0, 0, 0, 1f, 0f, 1, 255),
            new PackedVertex(0, 1, 0, 1f, 1f, 1, 255),
            new PackedVertex(1, 1, 0, 0f, 1f, 1, 255),
        };

        private static readonly PackedVertex[] PackedFaceLeft = new PackedVertex[] {
            new PackedVertex(0, 0, 0, 0f, 0f, 2, 255),
            new PackedVertex(0, 0, 1, 1f, 0f, 2, 255),
            new PackedVertex(0, 1, 1, 1f, 1f, 2, 255),
            new PackedVertex(0, 1, 0, 0f, 1f, 2, 255),
        };

        private static readonly PackedVertex[] PackedFaceRight = new PackedVertex[] {
            new PackedVertex(1, 0, 1, 0f, 0f, 3, 255),
            new PackedVertex(1, 0, 0, 1f, 0f, 3, 255),
            new PackedVertex(1, 1, 0, 1f, 1f, 3, 255),
            new PackedVertex(1, 1, 1, 0f, 1f, 3, 255),
        };

        private static readonly PackedVertex[] PackedFaceTop = new PackedVertex[] {
            new PackedVertex(0, 1, 1, 0f, 0f, 4, 255),
            new PackedVertex(1, 1, 1, 1f, 0f, 4, 255),
            new PackedVertex(1, 1, 0, 1f, 1f, 4, 255),
            new PackedVertex(0, 1, 0, 0f, 1f, 4, 255),
        };

        private static readonly PackedVertex[] PackedFaceBottom = new PackedVertex[] {
            new PackedVertex(0, 0, 0, 0f, 0f, 5, 255),
            new PackedVertex(1, 0, 0, 1f, 0f, 5, 255),
            new PackedVertex(1, 0, 1, 1f, 1f, 5, 255),
            new PackedVertex(0, 0, 1, 0f, 1f, 5, 255),
        };

        public static readonly Dictionary<Face, PackedVertex[]> PackedFaceVertices = new()
        {
            { Face.FRONT, PackedFaceFront },
            { Face.BACK, PackedFaceBack },
            { Face.LEFT, PackedFaceLeft },
            { Face.RIGHT, PackedFaceRight },
            { Face.TOP, PackedFaceTop },
            { Face.BOTTOM, PackedFaceBottom }
        };
    }
}
