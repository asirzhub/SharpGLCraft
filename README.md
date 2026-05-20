# SharpGLCraft

This is an OpenGL Minecraft clone that uses C#. It is made possible via OpenTK, which is a C# binding layer for OpenGL. 

## Screenshots
![https://github.com/asirzhub/MinecraftClone/daytime.png](https://github.com/asirzhub/MinecraftClone/blob/main/daytime.png "Daytime screenshot.")
![https://github.com/asirzhub/MinecraftClone/daytime.png](https://github.com/asirzhub/MinecraftClone/blob/main/godrays.png "Closeup of godrays.")
![https://github.com/asirzhub/MinecraftClone/daytime.png](https://github.com/asirzhub/MinecraftClone/blob/main/sunset.png "Sunsets are stunning.")

## Development Roadmap
1. ~~Base-layer absolute minimum engine with chunk generation, camera, and rendering of chunks.~~
2. ~~Improve chunk meshing to massively increase performance~~
3. ~~Dynamic loading and unloading of chunks~~ (kinda... meshing seems buggy and doesnt cull enough faces)
4. ~~Improve terrain generation to make it more interesting/realistic~~
5. ~~Async world generation~~
6. ~~Async chunk loading~~
7. ~~Separate solid blocks vs water/transparent render passes~~
8. ~~Vertex-based baked lighting (ambient occlusion)~~
9. ~~Frustrum culling~~
10. ~~Surface features (trees, grass, flowers)~~
11. ~~Shadow mapping~~
12. ~~Block placement and destruction~~
13. ~~Volumetric fog~~ and clouds
14. Tree parameterization
15. Yet another terrain generation upgrade
16. User interface (text and images)
17. Intelligent chunk loading (instead of moving volume, use chunk data and occlusion info)
18. Post processing (tonemapping, bloom, SSR)
19. Caching generated chunks to disk (save/load worlds)

## Lessons I've learned
### Separate data, logic, and rendering into unique scripts
And use a manager to control the data flow between these three aspects of whatever game feature. For example, re-writing my meshing and dynamically generating chunks had me stuck for a whole week. The biggest mistake that I finally stopped making: The manager does not hold ANY information - It knows nothing in specific about whatever it's managing, it just tells each part what it should do, when it should do it, and carts information from one part to another. Additionally, data has no idea what the logic is or what the rendering is - it's just information, nothing else. Logic is given information by the data, and it does what it's told to, to prepare the data for rendering. But logic doesn't know anything about rendering either. And rendering, all it knows is what vertices to mesh and render - it doesn't know why it's rendering what it is, it just knows what to do once given vertices.
### Async programming: Never set states off the main thread.
The chunks drove me insane. I spent a few weeks to think about it, and ended up with a design that sees the main thread kick off worker threads, who deliver their products into concurrent queues. the main thread is responsible for moving the data from the queues to their respective chunks, and any calls for setting chunk states happens on the main thread. This prevents race conditions and chunks being incorrectly assigned states. 
