using SharpGLCraft.Camera;
using SharpGLCraft.Graphics;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using System.Collections.Concurrent;

namespace SharpGLCraft.World.Chunks
{
    // renderer handles rendering side of things for chunks
    public class ChunkRenderer
    {
        public Texture blockTexture;

        private Shader[] blockShaders;
        public int shaderMode = 0;
        public Shader blockShader => blockShaders[shaderMode];

        static readonly string[] ShaderModeNames = { "Full", "FastFog", "Fastest" };
        public string ShaderModeName => ShaderModeNames[shaderMode];

        public void CycleShader()
        {
            shaderMode = (shaderMode + 1) % blockShaders.Length;
        }

        // some parameters
        public float waterOffset = 0.15f; // water surface offset from top edge of blocks
        public float waterWaveAmplitude = 0.04f; // how much water (and foliage...) deviates from origin
        public float waterWaveScale = 0.1f; // world-size scale of sine waves
        public float waterWaveSpeed = 0.5f; // speed at which oscillations travel

        List<ChunkState> lightingPassVisibleStates = new List<ChunkState>() { ChunkState.VISIBLE, ChunkState.MESHING, ChunkState.MESHED, ChunkState.DIRTY };
        List<ChunkState> shadowMapPassVisibleStates = new List<ChunkState>() { ChunkState.INVISIBLE, ChunkState.VISIBLE, ChunkState.MESHING, ChunkState.MESHED, ChunkState.DIRTY };


        public Shader shadowMapShader;
        FBOShadowMap fboShadowMap;
        int shadowMapResolution = 512;

        Matrix4 shadowMapViewMatrix = new();
        Matrix4 shadowMapProjMatrix = new();

        Random random = new Random();

        public ChunkRenderer()
        {
            blockShaders = new Shader[]
            {
                new Shader("PackedBlock.vert", "PackedBlock.frag"),
                new Shader("PackedBlock.vert", "PackedBlockFastFog.frag"),
            };
            blockTexture = new Texture("textures.png");

            shadowMapShader = new Shader("BlockShadowPass.vert", "BlockShadowPass.frag");
            fboShadowMap = new(shadowMapResolution, shadowMapResolution);
        }

        public void Bind()
        {
            blockShader.Bind();
            GL.ActiveTexture(TextureUnit.Texture0); // color channel
            blockTexture.Bind();
            blockShader.SetInt("albedoTexture", 0);

            GL.ActiveTexture(TextureUnit.Texture1); // shadowmap channel
            GL.BindTexture(TextureTarget.Texture2D, fboShadowMap.depthTexture);
            blockShader.SetInt("shadowMap", 1);
        }


        readonly List<Vector3i> visibleIndexes = new List<Vector3i>();

        public void RenderLightingPass(AerialCameraRig camera, float time, ConcurrentDictionary<Vector3i, Chunk> chunks, SkyRender skyRender, float seaLevel)
        {
            // clear screen, draw sky first
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Viewport(0, 0, (int)camera.screenWidth, (int)camera.screenHeight);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            skyRender.RenderSky(camera);

            // THEN render chunks
            Bind();

            visibleIndexes.Clear();

            blockShader.SetVector3("u_sunColor", skyRender.sunColor * 1.5f);
            blockShader.SetVector3("cameraPos", (camera.CameraPosition()));// + camera.focusPoint)/2.0f);
            blockShader.SetFloat("u_waterOffset", waterOffset);
            blockShader.SetFloat("u_waveAmplitude", waterWaveAmplitude);
            blockShader.SetFloat("u_waveScale", waterWaveScale);
            blockShader.SetFloat("u_hzLightMix", 0.2f);
            blockShader.SetFloat("u_time", time);
            blockShader.SetFloat("u_waveSpeed", waterWaveSpeed);
            blockShader.SetVector3("u_sunDirection", skyRender.sunDirection);

            if(shaderMode == 0) blockShader.SetInt("u_fogSamples", 12);

            blockShader.SetVector3("u_horizonColor", skyRender.finalH);
            blockShader.SetVector3("u_zenithColor", skyRender.finalZ);
            blockShader.SetVector3("u_sunsetColor", skyRender.sunsetColor);
            
            Matrix4 view = camera.GetViewMatrix();
            Matrix4 projection = camera.GetProjectionMatrix();
            blockShader.SetMatrix4("view", view);
            blockShader.SetMatrix4("projection", projection);

            // render all chunks non-transparent mesh
            foreach (var kvp in chunks)
            {
                var chunk = kvp.Value;
                var idx = kvp.Key;
                if(lightingPassVisibleStates.Contains(chunk.GetState()))
                {
                    RenderChunkLit(chunk.solidMesh, camera, idx, time, skyRender, seaLevel);
                    visibleIndexes.Add(idx);
                }
            }

            // render water with no depth mask, after all solids were rendered
            GL.DepthMask(false);
            foreach (var idx in visibleIndexes)
            {
                RenderChunkLit(chunks[idx].transparentMesh, camera, idx, time, skyRender, seaLevel);
            }
            GL.DepthMask(true);
        }

        bool RenderChunkLit(MeshData mesh, AerialCameraRig camera, Vector3i index, float time, SkyRender sky, float seaLevel)
        {
            // exit if there's no mesh data
            if (mesh == null || mesh.Vertices.Count == 0) return false;

            var sunDirection = sky.sunDirection;

            //with everything prepped, we can now render
            Matrix4 model = Matrix4.CreateTranslation(index*(Chunk.SIZE));

            blockShader.SetMatrix4("model", model);
            blockShader.SetMatrix4("lightProjMat", shadowMapProjMatrix);
            blockShader.SetMatrix4("lightViewMat", shadowMapViewMatrix);

            mesh.Upload();
            mesh.Bind();

            GL.DrawElements(
            PrimitiveType.Triangles,
            mesh.ibo.length,
            DrawElementsType.UnsignedInt,
            0
            );

            return true;
        }

        // shadowmap pass renders the scene depth-only from light's perspective
        public void RenderShadowMapPass(AerialCameraRig camera, float time, ConcurrentDictionary<Vector3i, Chunk> chunks, SkyRender skyRender)
        {
            // bind shadow stuff
            fboShadowMap.Bind();
            GL.Viewport(0, 0, shadowMapResolution, shadowMapResolution);
            GL.Clear(ClearBufferMask.DepthBufferBit);

            shadowMapShader.Bind();

            visibleIndexes.Clear();

            // position the light 500 units away in the direction of the sun, looking at the place the camera looks at. use ortho projection
            shadowMapViewMatrix = Matrix4.LookAt(camera.focusPoint + 500f * skyRender.sunDirection, 
                camera.focusPoint , 
                Vector3.UnitZ);
            shadowMapProjMatrix = Matrix4.CreateOrthographic(camera.armDistance*3, camera.armDistance * 2, 0.01f, 2000f);
            
            Matrix4 view = shadowMapViewMatrix;
            Matrix4 projection = shadowMapProjMatrix;
            shadowMapShader.SetMatrix4("view", view);
            shadowMapShader.SetMatrix4("projection", projection);

            shadowMapShader.SetFloat("u_waterOffset", waterOffset);
            shadowMapShader.SetFloat("u_waveAmplitude", waterWaveAmplitude);
            shadowMapShader.SetFloat("u_waveScale", waterWaveScale);

            // render all chunks non-transparent mesh
            foreach (var kvp in chunks)
            {
                var chunk = kvp.Value;
                var idx = kvp.Key;
                if (shadowMapPassVisibleStates.Contains(chunk.GetState()))
                {
                    RenderChunkShadowMap(chunk.solidMesh, idx, time, skyRender);
                    visibleIndexes.Add(idx);
                }
            }

            // render transparent with no depth mask, after all solids were rendered
            GL.DepthMask(false);
            foreach (var idx in visibleIndexes)
            {
                RenderChunkShadowMap(chunks[idx].transparentMesh, idx, time, skyRender);
            }
            GL.DepthMask(true);

        }

        bool RenderChunkShadowMap(MeshData mesh, Vector3i index, float time, SkyRender sky)
        {
            // exit if there's no mesh data
            if (mesh == null || mesh.Vertices.Count == 0) return false;        
            
            Matrix4 model = Matrix4.CreateTranslation(index * (Chunk.SIZE));

            shadowMapShader.SetMatrix4("model", model);

            mesh.Upload();
            mesh.Bind();

            GL.DrawElements(
            PrimitiveType.Triangles,
            mesh.ibo.length,
            DrawElementsType.UnsignedInt,
            0
            );

            return true;
        }
    }
}
