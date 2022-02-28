# Unity 3d Performance
 Animated Graph but with some serious performance enhancing drugs.

A better explanation:

There are 4 objects/scenes of note:

Graph

GPUGraph

CPU Heavy Fractal

And Fractal

Graph is the original shape maker. It can create up to 5 shapes using a bunch of cubes as pseudo voxels.

It looks great actually.

GPUGraph pushes the calculation for each cube in world space (and their transforms) onto a Compute Shader and lets the GPU's specialized computer handle
the heavy lifting.

This enables us to have up to one million points in the wave. It's less attractive because moiré patterns show up all over it. But it runs reasonably well. (The original Graph cannot run even close to this number of cubes before the framerate starts dying. Using the profiler will reveal that the bottleneck is on the CPU end)

CPU Heavy Fractal is a whole new animal. It creates a fractal shape, but its taxing on the CPU. DO NOT GO OVER DEPTH 5 OR 6. I've tried to and it crashes out.

Fractal is the big one, it pushes the generation of the fractal shape onto the ComputeShader and procedurally draws everything. This takes the load off of the CPU in a big way.
Also uses Unity's burst compiling to utilize the CPUs parallelization. You can safely go over depth 6 on the game object, bear in mind that this will still be taxing on the GPU and CPU. This is simply due to the nature of fractal shapes.

There's a bit of freezing up at the start when I try to run this in the editor. It's most likely because I had the burst compiling perform synchronously instead of its usual on-demand shader esque compiling.

Honestly I don't what use any of this has. But it looks nice
