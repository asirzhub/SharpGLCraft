namespace SharpGLCraft
{
    class Program
    {
        static void Main(string[] args)
        {
            Game G = new(1280, 720, "game");
            G.Run();
        }
    }
}
