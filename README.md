# Unity 3d Performance


https://user-images.githubusercontent.com/80176553/206288950-a5f84fe0-e85e-40e2-9e38-da1de25deec7.mp4

A repeat of Animated Graph (A previous project) but with some serious performance enhancing drugs.

A better explanation:

There are 4 objects/scenes of note:

- Graph

- GPUGraph

- CPU Heavy Fractal

- And Fractal

Graph is a copy of Animated-Graph which is a different repository. It can create up to 5 shapes using cubes as pseudo voxels. Transitioning between shapes can be decided in order or chosen at random. The change in shape is not a hard transition like a movie cut, but a smooth transition involving linear interpolation.

It looks great actually.

GPUGraph pushes the calculation for each cube in world space (and their transforms) onto a Compute Shader and lets the GPU's specialized computer handle the heavy lifting concerning the position of the cube due to the mathematical nature of each animation. They are just multiple waves interacting with each other.

This enables us to have up to one million points (cubes) in the wave. It's less attractive because moiré patterns show up at that resolution. But it runs reasonably well. (The original Graph cannot run even close to this number of cubes before the framerate starts dying. Using the Unity profiler will reveal that the bottleneck is on the CPU end.)

CPU Heavy Fractal is a whole new animal separate from the above two graphs. It creates a fractal shape out of sphere, but its taxing on the CPU. DO NOT GO OVER DEPTH 5 OR 6. I've tried to and it just crashes on my machine.

Fractal is the big one, it pushes the generation of the fractal shape onto the ComputeShader and procedurally draws everything. This takes the load off of the CPU.
Also uses Unity's burst compiling to utilize the CPUs parallelization. You can safely go over depth 6 on the game object, bear in mind that this will still be taxing on the GPU and CPU at depths 7 and 8. The depth afterward has been limited to no more beyond that.

There's a bit of freezing up at the start when I try to run this in the editor. It's most likely because I had the burst compiling perform synchronously instead of its usual on-demand shader esque compiling. Just to make sure that burst compiler acts reliably instead of the usual bug chasing headaches that occur with asynchronous program running.
