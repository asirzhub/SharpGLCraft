// Index Buffer Object

using OpenTK.Graphics.OpenGL4;

namespace SharpGLCraft.Graphics.Buffers
{
    public class IBO
    {
        public int ID;
        public int length;

        public void Bind() => GL.BindBuffer(BufferTarget.ElementArrayBuffer, ID);
        public void UnBind() => GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
        public void Dispose() => GL.DeleteBuffer(ID);

        /// <summary>
        /// Generates, binds, and uploads index buffer object data in one go.
        /// </summary>
        public IBO(List<uint> data)
        {
            ID = GL.GenBuffer();
            Bind();
            GL.BufferData(BufferTarget.ElementArrayBuffer, data.Count * sizeof(uint), data.ToArray(), BufferUsageHint.StaticDraw);
            length = data.Count * sizeof(uint);
        }
    }
}
