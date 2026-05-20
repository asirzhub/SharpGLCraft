// Vertex Buffer Object

using SharpGLCraft.World.Blocks;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Runtime.InteropServices;

namespace SharpGLCraft.Graphics.Buffers
{
    public class VBO
    {
        public int ID;

        public void Bind() => GL.BindBuffer(BufferTarget.ArrayBuffer, ID);
        public void UnBind() => GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        public void Dispose() => GL.DeleteBuffer(ID);

        // human-readable vertex info
        public struct Vertex
        {
            public Vector3 Position;
            public Vector2 TexCoord;
            public Vector3 Normal;
            public float brightness; // must be float!

            public Vertex(Vector3 position, Vector2 texCoord, Vector3 normal, float brightness = 1.0f)
            {
                this.Position = position;
                this.TexCoord = texCoord;
                this.Normal = normal;
                this.brightness = brightness;
            }
            // everything here is a float (4 bytes)
            // (3 + 2 + 3 + 1) * 4 = 36 bytes
        }

        public struct PackedVertex
        {
            public uint PositionNormalLighting; // 32 bits packed
            // 00WW LLLL NNNZ ZZZZ ZZYY YYYY YXXX XXXX

            public float TexU, TexV;

            public PackedVertex(byte posX, byte posY, byte posZ, float texU, float texV, byte normal, byte lightLevel, WiggleType wiggleType = WiggleType.NONE)
            {
                PositionNormalLighting = 0;

                PositionNormalLighting |= (uint)(posX & 0x7F);              // bits 0–6
                PositionNormalLighting |= (uint)((posY & 0x7F) << 7);       // bits 7–13
                PositionNormalLighting |= (uint)((posZ & 0x7F) << 14);      // bits 14–20

                PositionNormalLighting |= (uint)((normal & 0x7) << 21); // bits 21–23
                PositionNormalLighting |= (uint)((lightLevel & 0xF) << 24); // bits 24–27

                PositionNormalLighting |= (uint)(((wiggleType == WiggleType.OMNIDIRECTIONAL ? 1 : 0) & 0x1) << 28); // X-Z / omni
                PositionNormalLighting |= (uint)((((wiggleType == WiggleType.VERTICAL || wiggleType == WiggleType.OMNIDIRECTIONAL) ? 1 : 0) & 0x1) << 29); // Y / vertical

                TexU = texU;
                TexV = texV;
            }

            public Vector3i Position()
            {
                int x = (int)(PositionNormalLighting & 0x7F);      // bits 0–6
                int y = (int)((PositionNormalLighting >> 7) & 0x7F);      // bits 7–13
                int z = (int)((PositionNormalLighting >> 14) & 0x7F);     // bits 14–20
                return (x, y, z);
            }
        }

        public static byte[] FlattenPackedVertices(List<PackedVertex> verts)
        {
            // 4 bytes for uint + 4 for TexU + 4 for TexV = 12 bytes/vertex
            const int VERTEX_SIZE = 12;
            byte[] bytes = new byte[verts.Count * VERTEX_SIZE];

            for (int i = 0; i < verts.Count; i++)
            {
                int offset = i * VERTEX_SIZE;
                var v = verts[i];

                // full 32-bit uint
                var posNorBri = BitConverter.GetBytes(v.PositionNormalLighting);
                Array.Copy(posNorBri, 0, bytes, offset + 0, 4);

                // tex u and v coords
                var texU = BitConverter.GetBytes(v.TexU);
                Array.Copy(texU, 0, bytes, offset + 4, 4);

                var texV = BitConverter.GetBytes(v.TexV);
                Array.Copy(texV, 0, bytes, offset + 8, 4);
            }

            return bytes;
        }

        public static List<float> FlattenVertices(List<Vertex> vertices)
        {
            List<float> data = new List<float>();
            int stride = Marshal.SizeOf(typeof(Vertex));

            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                int baseIndex = i * stride;

                data.Add(v.Position.X);
                data.Add(v.Position.Y);
                data.Add(v.Position.Z);
                data.Add(v.TexCoord.X);
                data.Add(v.TexCoord.Y);
                data.Add(v.Normal.X);
                data.Add(v.Normal.Y);
                data.Add(v.Normal.Z);
                data.Add(v.brightness);
            }

            return data;
        }

        // <summary>
        /// VBO built with floats
        /// </summary>
        public VBO(byte[] data)
        {
            ID = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, ID);
            GL.BufferData(BufferTarget.ArrayBuffer, data.Length, data, BufferUsageHint.StaticDraw);
        }
    }
}
