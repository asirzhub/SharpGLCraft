using SharpGLCraft.Camera;
using SharpGLCraft.Graphics;
using SharpGLCraft.World.Blocks;
using SharpGLCraft.World.Generation;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SharpGLCraft.Physics;

namespace SharpGLCraft
{
    class Game : GameWindow
    {    
        AerialCameraRig aerialCamera;
        public bool camOrbiting = true;

        private Vector2i savedWindowedSize;
        private Vector2i savedWindowedLocation;
        private bool isFullscreen = false;

        public ChunkManager chunkManager;
        SkyRender skyRender;

        // window-specific variables
        private int width;
        private int height;
        private double frameTimeAccumulator = 0.0;
        private int shortFrameCount = 0;
        private int totalFrameCount = 0;
        public float timeElapsed = 0;

        float timeMult = 0.01f;


        // Game Constructor not much to say
        public Game(int width, int height, string title) : base(GameWindowSettings.Default, new NativeWindowSettings()
        {
            ClientSize = (width, height),
            Title = title,
            API = ContextAPI.OpenGL,
            APIVersion = new Version(3, 3),
            DepthBits = 24,
        })
        {
            aerialCamera = new AerialCameraRig(width, height, (0f,0f,0f));
            this.width = width;
            this.height = height;
            skyRender = new SkyRender((1f, 1f, 0f));
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            GL.ClearColor(0.0f, 0.0f, 1.0f, 1.0f);

            CursorState = CursorState.Grabbed;

            //VSync = VSyncMode.On; // only needed when i dont want my laptop to turn into a jet engine at the library
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            //GL.Enable(EnableCap.Multisample);
            //GL.Enable(EnableCap.)
            GL.Enable(EnableCap.CullFace);
            GL.FrontFace(FrontFaceDirection.Ccw);

            chunkManager = new ChunkManager(aerialCamera);

            skyRender.InitializeSky();
        }

        // render stuff for each frame 
        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);

            skyRender.SetSunDirection(Vector3.Transform(skyRender.sunDirection, new Quaternion((float)args.Time * timeMult, 0f, 0f)));
            
            chunkManager.Update(aerialCamera, (float)args.Time, timeElapsed, totalFrameCount, skyRender.sunDirection.Normalized(), skyRender);

            SwapBuffers();

            // track fps
            frameTimeAccumulator += args.Time;
            shortFrameCount++;
            totalFrameCount++;

            if (frameTimeAccumulator >= 0.5)
            {
                Title = $"game - FPS: {shortFrameCount * 2} | " +
                    $"Position: {aerialCamera.CameraPosition()} | " +
                    $"Chunk: {chunkManager.currentChunkIndex} | " +
                    $"Chunk Tasks: {chunkManager.taskCount}/{chunkManager.maxChunkTasks} | " +
                    $"Render Distance: {chunkManager.radius} | " +
                    $"Shader: {chunkManager.renderer.ShaderModeName} [R]";
                frameTimeAccumulator = 0.0;
                shortFrameCount = 0;
            }
        }

        protected override void OnFramebufferResize(FramebufferResizeEventArgs e)
        {
            base.OnFramebufferResize(e);

            width = e.Width;
            height = e.Height;

            GL.Viewport(0, 0, width, height);
            aerialCamera.UpdateResolution(width, height);
        }

        // logic, non-rendering frame-by-frame stuff
        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            MouseState mouse = MouseState;
            KeyboardState input = KeyboardState;

            base.OnUpdateFrame(args);

            timeElapsed += (float)args.Time;

            // press escape to close this window or release mouse
            if (KeyboardState.IsKeyPressed(Keys.Escape)) Close();

            camOrbiting = MouseState.IsButtonDown(MouseButton.Right);

            if (camOrbiting)
            {
                CursorState = CursorState.Grabbed;
                aerialCamera.Update(input, mouse, args);
                aerialCamera.firstMove = false;
            }
            else
            {
                CursorState = CursorState.Normal;
                aerialCamera.firstMove = true;
            }

            if (KeyboardState.IsKeyPressed(Keys.Period)) chunkManager.radius++;            

            if (KeyboardState.IsKeyPressed(Keys.Comma)) chunkManager.radius--;

            Vector3 focusPoint = new(aerialCamera.focusPoint);
            float targetFocusHeight = chunkManager.worldGenerator.GetNoiseAt(NoiseLayer.HEIGHT, (int)focusPoint.X, (int)focusPoint.Z);
            aerialCamera.focusPoint.Y = Lerp(aerialCamera.focusPoint.Y, targetFocusHeight, aerialCamera.smoothing);

            if (MouseState.IsButtonPressed(MouseButton.Left))
            {
                var origin = aerialCamera.CameraPosition();   // more stable than focusPoint

                float aspect = aerialCamera.screenWidth / aerialCamera.screenHeight;

                float tanHalfFovY = MathF.Tan(MathHelper.DegreesToRadians(aerialCamera.fovY * 0.5f));
                float tanHalfFovX = tanHalfFovY * aspect;

                Vector2 ndc = mouse.Position;
                ndc.X = (ndc.X / aerialCamera.screenWidth) * 2f - 1f;
                ndc.Y = 1f - (ndc.Y / aerialCamera.screenHeight) * 2f;

                Vector3 dir =
                    aerialCamera.forward +
                    aerialCamera.right * (ndc.X * tanHalfFovX) +
                    aerialCamera.up * (ndc.Y * tanHalfFovY);

                dir = Vector3.Normalize(dir);

                if (Raycast.RaycastSolidBlock(chunkManager, origin, dir, maxDist: 256f, maxSteps: 256, out var hit, out var place))
                    chunkManager.TrySetBlockAtWorldPosition(hit, BlockType.AIR);
            }

            if (KeyboardState.IsKeyPressed(Keys.F)) ToggleFullscreen();

            if (KeyboardState.IsKeyPressed(Keys.R)) chunkManager.renderer.CycleShader();
        }
        private void ToggleFullscreen()
        {
            if (!isFullscreen)
            {
                // Save windowed state
                savedWindowedSize = Size;
                savedWindowedLocation = Location;

                WindowState = WindowState.Fullscreen;
                isFullscreen = true;
            }
            else
            {
                WindowState = WindowState.Normal;

                // Restore previous size and position
                Size = savedWindowedSize;
                Location = savedWindowedLocation;

                isFullscreen = false;
            }
        }

        protected override void OnUnload()
        {
            base.OnUnload();
            skyRender.Dispose();
        }

        static float Lerp(float x, float y, float t)
        {
            return y * t + x * (1 - t);
        }
    }

}