# Unity-Grass
Grass rendering experiment in Unity. Uses an HLSL compute shader and graphics shader to draw procedurally generated blades on the GPU.

Essentially an implementation of the approach used in Ghost of Tsushima, detailed in this incredible talk: [Procedural Grass in 'Ghost of Tsushima](https://www.youtube.com/watch?v=Ibe1JBF5i5Y)' .

The grass is quite performant (although there are crucial optimisations that should be added, like LODing). The look and movement of the grass is highly customisable and can be changed using various parameters.

![alt text](https://github.com/blakSou1/Unity-Grass/blob/main/ReadmeDataMedia/photo_2025-09-18_22-27-54.jpg?raw=true)

# Key Features:
- **Shape of blades** determined by cubic Bezier curves
- **Wind animation** driven by scrolling 2D perlin noise inputted to a sin-based function that modulates various parameters of the grass
- **Clumping**: Grass can be grouped into Voronoi clumps that share the same parameters, for a less uniform look
- **Lighting**: Phong shading, with gloss map, and fake ambient occlusion based on length of blade
- **Grass color**: combination of color gradient along length, clump color, and albedo texture
- **Heightmap terrain**: blades placed on the surface of a heightmap terrain
- **GPU instancing**, allowing for fast rendering of millions of blades
- **Frustum culling**, Blades outside of the viewing frustum are not rendered
- **Distance culling**, Fewer blades are rendered at distance, with a smooth transition between near and far
- **Chunking**, chunk-based rejection on the CPU
- **RenderPipeline**, URP support
- **Mask-Based Vegetation Spawning**, uses the Terrain mask to determine where to cut the grass

# To-do
- LODing
- Do transparency fade of distance culled grass
- Receive shadows from shadow map
- Cast shadows onto terrain (do not actually include grass in shadow pass, but maybe fake it using Tsushima's method)
- Have grass deform based on player movement
- Occlusion culling
- Spend verts for single blade on multiple blades when grass is short (Tsushima does this)

# Bugs
- if there is a large amount of grass on the screen, the buffer will overflow and some of the grass will be excluded from the render.
- Terrain Width and Terrain Height must be identical. The height map is not stretched correctly and only works with a square.
- I advise you to use the number of chunks, the value of which is a multiple of 4

# Resources
I used a lot of online resources to learn the techniques used. I don't remember everything that I referenced but I'll list the main ones:
- [Procedural Grass in 'Ghost of Tsushima](https://www.youtube.com/watch?v=Ibe1JBF5i5Y)'
- [Acerola - How Do Games Render So Much Grass?](https://www.youtube.com/watch?v=Y0Ko0kvwfgA)
- https://github.com/GarrettGunnell/Grass
- https://github.com/harlan0103/Grass-Rendering-in-Modern-Game-Engine
- https://github.com/cainrademan/Unity-Grass
- https://github.com/Youssef-Afella/UnityURP-InfiniteGrass
- https://github.com/EricHu33/UnityGrassIndirectRenderingExample
