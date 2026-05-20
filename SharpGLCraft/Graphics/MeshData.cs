using System.Runtime.InteropServices;
using SharpGLCraft.Graphics.Buffers;
using static SharpGLCraft.Graphics.Buffers.VBO;

namespace SharpGLCraft.Graphics
{
    public class MeshData
    {
        public List<PackedVertex> Vertices = new();
        public List<uint> Indices = new();

        public VAO vao;
        public VBO vbo;
        public IBO ibo;

        public int GetIBOLength() => ibo.length;

        public int stride = -1;

        public void Upload()
        {
            if (vao == null) { vao = new VAO(); }
            if (vbo == null) { vbo = new VBO(FlattenPackedVertices(Vertices)); }
            if (ibo == null) { ibo = new IBO(Indices); }

            if(stride == -1) stride = Marshal.SizeOf(typeof(PackedVertex));

            vao.LinkToVAOInt(0, 1, vbo, stride, 0);
            vao.LinkToVAO(1, 2, vbo, stride, 4);
        }

        public void Bind()
        {
            vao.Bind();
            vbo.Bind();
            ibo.Bind();
        }

        public void AddVertex(VBO.PackedVertex v)
        {
            Vertices.Add(v);
        }

        public void Dispose()
        {
            vao?.UnBind();
            vao?.Dispose();
            vbo?.UnBind();
            vbo?.Dispose();
            ibo?.UnBind();
            ibo?.Dispose();

            Vertices.Clear();
            Indices.Clear();
        }

        /// <summary>
        /// Merge the new mesh data into this current mesh's data
        /// </summary>
        /// <param name="meshToMerge">Mesh to merge</param>
        public void MergeMesh(MeshData meshToMerge)
        {
            
        }
    }
}
