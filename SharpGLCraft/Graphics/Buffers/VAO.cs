// Vertex Array Object

using OpenTK.Graphics.OpenGL4;

namespace SharpGLCraft.Graphics.Buffers
{
    public class VAO
    {
        public int ID;

        public void Bind() => GL.BindVertexArray(ID);
        public void UnBind() => GL.BindVertexArray(0);
        public void Dispose() => GL.DeleteVertexArray(ID);

        // <summary>
        /// Create and bind a VAO
        /// </summary>
        public VAO()
        {
            ID = GL.GenVertexArray();
            Bind();
        }

        // <summary>
        /// Enable the vertex attrib, and fill it in with correct info.
        /// </summary>
        public void LinkToVAO(int location, int size, VBO vbo, int stride, int offset)
        {
            Bind();
            vbo.Bind();
            GL.EnableVertexAttribArray(location);
            GL.VertexAttribPointer(location, size, VertexAttribPointerType.Float, false, stride, offset);
            UnBind();
        }

        public void LinkToVAOInt(int location, int size, VBO vbo, int stride, int offset)
        {
            Bind();
            vbo.Bind();
            GL.EnableVertexAttribArray(location);
            GL.VertexAttribIPointer(location, size, VertexAttribIntegerType.UnsignedInt, stride, offset);
            UnBind();
        }
    }
}
