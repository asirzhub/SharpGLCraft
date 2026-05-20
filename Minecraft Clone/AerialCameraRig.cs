using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Minecraft_Clone
{
    public class AerialCameraRig(float width, float height, Vector3 focusPointLoc)
    {
        public Vector3 focusPoint = focusPointLoc;
        public float armDistance = 100f;
        public float speed = 30f;
        public float viewSize = 60f; // the "size" of the projection box
        public float smoothing = 0.1f;

        public Vector3 right = Vector3.UnitX;
        public Vector3 up = Vector3.UnitY; // we define Y as going up, not Z. but you can.
        public Vector3 forward = -Vector3.UnitZ;

        public float screenWidth = width;
        public float screenHeight = height;

        private float pitch;
        private float yaw;

        public bool firstMove = true;
        Vector2 lastPos = new();
        float sensitivity = 15f;
        public float fovY = 60f;

        public Matrix4 GetViewMatrix() =>
            Matrix4.LookAt(CameraPosition(), focusPoint , up);

        public Matrix4 GetProjectionMatrix() => Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(fovY), screenWidth / screenHeight, 0.1f, 2000f);
            //Matrix4.CreateOrthographic(screenWidth/screenHeight * viewSize, viewSize, 0.1f, 2000f);

        public Vector3 CameraPosition() => focusPoint - forward * armDistance;

        bool dirty = false;

        private void UpdateVectors()
        { 
            if (!dirty) return;
            
            if (pitch > 20f) pitch = 20f; // prevent looking "upward"
            if (pitch < -85f) pitch = -85f;

            forward.X = MathF.Cos(MathHelper.DegreesToRadians(pitch)) * MathF.Cos(MathHelper.DegreesToRadians(yaw));
            forward.Y = MathF.Sin(MathHelper.DegreesToRadians(pitch));
            forward.Z = MathF.Cos(MathHelper.DegreesToRadians(pitch)) * MathF.Sin(MathHelper.DegreesToRadians(yaw));

            forward = Vector3.Normalize(forward);

            right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
            up = Vector3.Normalize(Vector3.Cross(right, forward));
        }

        public void UpdateResolution(float width, float height)
        {
            screenWidth = width;
            screenHeight = height;
        }

        public void Update(KeyboardState keyboard, MouseState mouse, FrameEventArgs e) {

            if (keyboard.IsKeyDown(Keys.W))
            {
                focusPoint += Vector3.Normalize((forward.X, 0, forward.Z)) * (float)e.Time * speed;
            }

            if (keyboard.IsKeyDown(Keys.A))
            {
                focusPoint -= right * (float)e.Time * speed;
            }

            if (keyboard.IsKeyDown(Keys.S)) {
                focusPoint -= Vector3.Normalize((forward.X, 0, forward.Z)) * (float)e.Time * speed;
            }

            if (keyboard.IsKeyDown(Keys.D))
            {
                focusPoint += right * (float)e.Time * speed;
            }

            armDistance += mouse.ScrollDelta.Y * -5f;

            if (armDistance < 25f) armDistance = 25f;
            if (armDistance > 150f) armDistance = 150f;

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

                if(MathF.Abs(deltaX) > 0.01f || MathF.Abs(deltaY) > 0.01f ) dirty = true;

                yaw += deltaX * sensitivity * (float)e.Time;
                pitch -= deltaY * sensitivity * (float)e.Time;
            }
            UpdateVectors();
        }
    }
}
