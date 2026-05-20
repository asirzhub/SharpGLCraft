using OpenTK.Graphics.OpenGL4;

namespace SharpGLCraft.Graphics
{
    public class FBOShadowMap
    {
        public int ID;
        public int depthTexture;
        public int width;
        public int height;

        public void Bind()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, ID);
            GL.BindTexture(TextureTarget.Texture2D, depthTexture);
        }
        public void UnBind()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }
        public void Dispose()
        {
            GL.DeleteFramebuffer(ID);
            GL.DeleteTexture(depthTexture);
        }

        public FBOShadowMap(int width, int height)
        {
            this.width = width;
            this.height = height;

            ID = GL.GenFramebuffer();
            Bind();

            // create shadowmap texture
            depthTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, depthTexture);

            GL.TexImage2D(TextureTarget.Texture2D, 0,
            PixelInternalFormat.DepthComponent24,
            width, height, 0,
            PixelFormat.DepthComponent,
            PixelType.UnsignedInt,
            IntPtr.Zero);


            // attach depth texture to this framebuffer, specifically in the depth slot of this framebuffer
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,
                FramebufferAttachment.DepthAttachment,
                TextureTarget.Texture2D,
                depthTexture, 0);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (float)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (float)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (float)TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (float)TextureWrapMode.ClampToBorder);

            GL.TextureParameter(depthTexture, TextureParameterName.TextureBorderColor, new float[] { 1f, 1f, 1f, 1f });
            // ^ this is so stupid why are TexParameter and TextureParameter different

            // Tell GL this depth texture will be used for shadow comparisons
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, (int)TextureCompareMode.CompareRefToTexture);

            // Choose compare function (LESS or LEQUAL are typical)
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareFunc, (int)DepthFunction.Lequal);


            var fboStatus = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (fboStatus != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine($"ShadowMap FBO Error: {fboStatus.ToString()}");

            // no drawing or reading colours since this is depth-only
            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(ReadBufferMode.None);

            UnBind();
        }
    }
}