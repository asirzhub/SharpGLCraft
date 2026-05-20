using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace SharpGLCraft.Graphics
{
    public class Shader
    {
        public int ID;
        string pathPrefix = Path.Join(AppContext.BaseDirectory, "../../../Shaders/");

        /// <summary>
        /// Create a shader with vertex and fragment. Binds the shader to the graph
        /// </summary>
        public Shader(string vertexPath, string fragmentPath)
        {
            string VertexShaderSource = File.ReadAllText(pathPrefix + vertexPath);
            string FragmentShaderSource = File.ReadAllText(pathPrefix + fragmentPath);

            int VertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(VertexShader, VertexShaderSource);

            int FragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(FragmentShader, FragmentShaderSource);

            // compile the shaders we just made
            GL.CompileShader(VertexShader);
            // and check for errors
            GL.GetShader(VertexShader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(VertexShader);
                Console.WriteLine(infoLog);
            } else Console.WriteLine($"{vertexPath} compilation success");

            GL.CompileShader(FragmentShader);
            GL.GetShader(FragmentShader, ShaderParameter.CompileStatus, out int _success);
            if (_success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(FragmentShader);
                Console.WriteLine(infoLog);
            } else Console.WriteLine($"{fragmentPath} compilation success");

            // with the individual shaders compiled, we now create our "shader program" which is what runs on the GPU
            ID = GL.CreateProgram();

            GL.AttachShader(ID, VertexShader);
            GL.AttachShader(ID, FragmentShader);

            GL.LinkProgram(ID);
        
            // error-check
            GL.GetProgram(ID, GetProgramParameterName.LinkStatus, out int __success);
            if (__success == 0)
            {
                string infoLog = GL.GetProgramInfoLog(ID);
                Console.WriteLine(infoLog);
            }

            // since it's already stored in the shader program ID, we can toss out the raw shader code and the compiled stuff (cleanup)
            GL.DeleteShader(FragmentShader);
            GL.DeleteShader(VertexShader);
        }

        // how to use the shader
        public void Bind() => GL.UseProgram(ID);
        public void UnBind() => GL.UseProgram(0);
        public void Delete() => GL.DeleteShader(ID);

        Dictionary<string, int> uniformCache = [];
        int GetUniformLocationCached(string name)
        {            
            if(uniformCache.ContainsKey(name))
                return uniformCache[name];

            int loc = GL.GetUniformLocation(ID, name);
            uniformCache.Add(name, loc);
            return loc;            
        }

        // a series of functions to make it easier to pass values into the shader
        public void SetMatrix4(string name, Matrix4 value)
        {            
            int loc = GetUniformLocationCached(name);

            if (loc == -1)
                Console.WriteLine($"[Shader Warning] Uniform '{name}' not found in shader {ID}.");
            else
                GL.UniformMatrix4(loc, false, ref value);
        }

        public void SetMatrix3(string name, Matrix3 value)
        {
            int loc = GetUniformLocationCached(name);

            if (loc == -1)
                Console.WriteLine($"[Shader Warning] Uniform '{name}' not found in shader {ID}.");
            else
                GL.UniformMatrix3(loc, false, ref value);
        }

        public void SetFloat(string name, float value)
        {
            int loc = GetUniformLocationCached(name);

            if (loc == -1)
                Console.WriteLine($"[Shader Warning] Uniform '{name}' not found in shader {ID}.");
            else
                GL.Uniform1(loc, value);
        }
        public void SetInt(string name, int value)
        {
            int loc = GetUniformLocationCached(name);

            if (loc == -1)
                Console.WriteLine($"[Shader Warning] Uniform '{name}' not found in shader {ID}.");
            else
                GL.Uniform1(loc, value);
        }
        public void SetVector3(string name, Vector3 value)
        {
            int loc = GetUniformLocationCached(name);
            
            if (loc == -1)
                Console.WriteLine($"[Shader Warning] Uniform '{name}' not found in shader {ID}.");
            else
                GL.Uniform3(loc, value);
        }

        public void SetVector4(string name, Vector4 value)
        {
            int loc = GetUniformLocationCached(name);

            if (loc == -1)
                Console.WriteLine($"[Shader Warning] Uniform '{name}' not found in shader {ID}.");
            else
                GL.Uniform4(loc, value);
        }

        // Stuff that the openTK tutorial told me to do but i dont fully understand
        private bool disposedValue = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                GL.DeleteProgram(ID);
                disposedValue = true;
            }
        }

        ~Shader()
        {
            if (disposedValue == false)
            {
                Console.WriteLine("GPU Resource leak! Did you forget to call Dispose()?");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}