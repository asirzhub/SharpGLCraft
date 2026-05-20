using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace SharpGLCraft.Camera
{
    public enum PerspectiveMode
    {
        PERSPECTIVE,
        ORTHOGRAPHIC
    }

    /// <summary>
    /// Create a camera with a specific width/height (for aspect ratio) and location in worldspace
    /// </summary>
    public class Camera(float width, float height, Vector3 position) : ICamera
    {
        // camera properties
        private float speed = 10f;
        float boostSpeed = 40f;
        float defaultSpeed = 10f;
        public float screenwidth = width;
        public float screenheight = height;
        private float sensitivity = 10f;

        public Vector3 position = position;

        public Vector3 right = Vector3.UnitX;
        public Vector3 up = Vector3.UnitY; // we define Y as going up, not Z. but you can.
        public Vector3 forward = -Vector3.UnitZ;
        public float fovY = 65;

        private float pitch;
        private float yaw;

        public bool firstMove = true;
        public Vector2 lastPos;

        public Vector3 CameraPosition() => position;

        public Matrix4 GetViewMatrix() =>
            Matrix4.LookAt(position, position + forward, up);

        public Matrix4 GetProjectionMatrix() =>
            Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(fovY), screenwidth / screenheight, 0.01f, 2000f);

        private void UpdateVectors()
        { // copied straight out of the tutorial lol

            if (pitch > 85f) pitch = 85f;
            if (pitch < -85f) pitch = -85f;

            forward.X = MathF.Cos(MathHelper.DegreesToRadians(pitch)) * MathF.Cos(MathHelper.DegreesToRadians(yaw));
            forward.Y = MathF.Sin(MathHelper.DegreesToRadians(pitch));
            forward.Z = MathF.Cos(MathHelper.DegreesToRadians(pitch)) * MathF.Sin(MathHelper.DegreesToRadians(yaw));

            forward = Vector3.Normalize(forward);

            right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
            up = Vector3.Normalize(Vector3.Cross(right, forward));
        }

        // old freecam movement code
        public void InputController(KeyboardState keyboard, MouseState mouse, FrameEventArgs e)
        {
            var forward_dir = Vector3.Normalize(new Vector3(forward.X, 0, forward.Z));
            if (keyboard.IsKeyDown(Keys.W)) { position += forward_dir * speed * (float)e.Time; }
            if (keyboard.IsKeyDown(Keys.A)) { position -= right * speed * (float)e.Time; }
            if (keyboard.IsKeyDown(Keys.S)) { position -= forward_dir * speed * (float)e.Time; }
            if (keyboard.IsKeyDown(Keys.D)) { position += right * speed * (float)e.Time; }
            if (keyboard.IsKeyDown(Keys.Space)) { position.Y += speed * (float)e.Time; }
            if (keyboard.IsKeyDown(Keys.LeftControl)) { position.Y -= speed * (float)e.Time; }
            if (keyboard.IsKeyDown(Keys.LeftShift)) { speed = boostSpeed; } else { speed = defaultSpeed; }

            if (firstMove)
            {
                lastPos = new Vector2(mouse.X, mouse.Y);
                firstMove = false;
            }
            else
            {
                var deltaX = mouse.X - lastPos.X;
                var deltaY = mouse.Y - lastPos.Y;
                lastPos = new Vector2(mouse.X, mouse.Y);

                yaw += deltaX * sensitivity * (float)e.Time;
                pitch -= deltaY * sensitivity * (float)e.Time;
            }

            UpdateVectors();
        }

        public void Update(KeyboardState keyboard, MouseState mouse, FrameEventArgs e)
        {
            InputController(keyboard, mouse, e);
        }

        public void UpdateResolution(float width, float height)
        {
            this.screenwidth = width;
            this.screenheight = height;
        }

        public float AspectRatio()
        {
            return screenwidth / screenheight;
        }
    }
}
