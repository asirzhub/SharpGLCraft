using OpenTK.Mathematics;

namespace SharpGLCraft.Camera
{
    public interface ICamera
    {
        Vector3 CameraPosition();
        Matrix4 GetViewMatrix();
        Matrix4 GetProjectionMatrix();
    }
}
