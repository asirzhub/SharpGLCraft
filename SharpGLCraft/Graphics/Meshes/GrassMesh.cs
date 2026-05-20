using SharpGLCraft.Graphics;
using static SharpGLCraft.Graphics.Buffers.VBO;

namespace SharpGLCraft.Graphics.Meshes
{
    public class GrassMesh : MeshData
    {
        // made of two faces to form an X
        public static readonly PackedVertex[] PackedFaceLeft = new PackedVertex[] {
            new PackedVertex(0, 0, 0, 0f, 0f, 4, 255),
            new PackedVertex(1, 0, 1, 1f, 0f, 4, 255),
            new PackedVertex(1, 1, 1, 1f, 1f, 4, 255),
            new PackedVertex(0, 1, 0, 0f, 1f, 4, 255),
        };

        public static readonly PackedVertex[] PackedFaceRight = new PackedVertex[] {
            new PackedVertex(0, 0, 1, 0f, 0f, 4, 255),
            new PackedVertex(1, 0, 0, 1f, 0f, 4, 255),
            new PackedVertex(1, 1, 0, 1f, 1f, 4, 255),
            new PackedVertex(0, 1, 1, 0f, 1f, 4, 255),
        };

        public static readonly PackedVertex[][] packedVertices = new PackedVertex[][] { PackedFaceLeft, PackedFaceRight };
    }
}
