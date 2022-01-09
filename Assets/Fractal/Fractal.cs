//The 4th way is the job system, which uses Unity burst.
//The job system just uses the parallel processes of the CPU
//To the best of its ability.
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;
//using float3x4 = Unity.Mathematics.float3x4;
using quaternion = Unity.Mathematics.quaternion;

public class Fractal : MonoBehaviour
{
    [SerializeField, Range(1,8)]
    int depth = 4; //How deep the fractal goes.
    
    //PREFACE:
    //Our CPU heavy approach is too heavy. We need to rethink our approach for making
    //The objects and creating each section because recursively it is too much of a burden
    //on the cpu.
    //Also Unity will also have to handle each game object individually.
    //But we can control the entire fractal shape from the root object
    
    //We removed the mesh and renderer and are deciding them
    //using SerializeFields.
    [SerializeField]
	Mesh mesh;
	[SerializeField]
	Material material;
    
    //If we control all of the objects through one root object, we need a way
    //to keep track of them through the root object.
    struct FractalPart {
		public float3 direction, worldPosition;
		public quaternion rotation, worldRotation;
        public float spinAngle; //Third pass. If we let the function run, our little inaccuracies will
        //build up to eventually throw us into error town.
        //But that only happens if we adjust localRotation. Storing the angle in a separate field
        //will leave us clear. 
        
        //We're positioning, rotating, and scaling the object.
        //So we need access to the transform aspects.
        //This is for the root object method
        /*
        public Transform transform;
        */
	}
    
    //Part of the Jobs system. I know, the name of its extension is weird.
    //But it's a Job Interface. The most flexible one too.
    //It's called that way because of naming conventions.
    //I is interface and specifically one that is used for functionality that runs inside for loops.
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)] 
    //Despite all the changes we have to explicitly compile using the
    //burst compiler to utilize Unity's parallel processing.
    //CompileSynchronously does the same thing in the shader as well.
    //Burst compiling is like the shader in that it compiles asynchronously at runtime. (on-demand)
    
    //FloatMode = fast rearranges operations for speed. a+b*c -> b*c+a
    
    struct UpdateFractalLevelJob : IJobFor {
        public float spinAngleDelta;
		public float scale;
        [ReadOnly]
		public NativeArray<FractalPart> parents;
		public NativeArray<FractalPart> parts;
        [WriteOnly]
		public NativeArray<float3x4> matrices;
        //IJobFor requires a method Execute that returns nothing 
        //and has an integer parameter.
        //We're going to use the Job here to replace the innermost loop
        //of our Update function.
        public void Execute (int i) {
            //And if it replaces the innermost loop of the UPdate function,
            //no surprises on how its gonna look.
            FractalPart parent = parents[i / 5]; //Exact same as above, I know.
			FractalPart part = parts[i]; //But this i is meant for the jobs system.
            part.spinAngle += spinAngleDelta;
			part.worldRotation = mul(parent.worldRotation,
				mul(part.rotation, quaternion.RotateY(part.spinAngle))
			);
			part.worldPosition =
				parent.worldPosition +
				mul(parent.worldRotation, 1.5f * scale * part.direction);
			parts[i] = part;
            
			float3x3 r = float3x3(part.worldRotation) * scale;
			matrices[i] = float3x4(r.c0, r.c1, r.c2, part.worldPosition);
        }
    }
    //We're just declaring things out the ass aren't we?
    //Jobs don't work with arrays, just simple values and structs.
    //Native arrays are structs containing a pointer to native machine memory.
    //To put another way: They sidestep the default memory management overhead.
    //I feel like I've just gotten really close to the metal...
    //These arrays are replacing the parts and matrices below.
    //So if you write the code back make sure to find all NativeArrays.
    NativeArray<FractalPart>[] parts;
	NativeArray<float3x4>[] matrices;
    
    //because we took it out of the struct above.
    //Transform's true form is that of a matrix.
    ComputeBuffer[] matricesBuffers; //Third pass. We need to send the matrices to the GPU.
    //Third pass
    //Remember for the graph, we needed the ID of the property _Matrices.
    //You'll find it in the FractalGPU.hlsl file
    static readonly int matricesId = Shader.PropertyToID("_Matrices");
    
    //The draw command gets queued and executed later. But the buffer is loaded.
    //This means whatever buffer was set last is the one that gets used by 
    //ALL of the draw commands.
    //Therefore only the last depth gets drawn.
    
    //Linking each buffer to a draw command using MaterialPropertyBlock
    //Will fix this.
    //Basically, we are pairing buffers and their draw commands.
    //Telling the GPU what to draw and when.
    static MaterialPropertyBlock propertyBlock;
    
    static float3[] directions = {
		up(), right(), left(), forward(), back()
	};
    
    //Mathematics works with radians instead of degrees, so we have to change it.
	static quaternion[] rotations = {
		quaternion.identity,
		quaternion.RotateZ(-0.5f * PI), quaternion.RotateZ(0.5f * PI),
		quaternion.RotateX(0.5f * PI), quaternion.RotateX(-0.5f * PI)
	};
    
    //void Awake() {//second pass
    void OnEnable() { //Third pass. Compute buffers need onEnable to survive hot reloads.
        //Each level of depth gets its own array.
        //For now each the size of the first array is 5
        //Then the next layer holds 25 FractalPart structs
        //And then multiplied by another 5.
        //Yeah, it gets pretty big.
        
        //These arrays were made for the jobs system.
        //They replace the arrays in the comments below, and also
        //further down in the for loop.
        parts = new NativeArray<FractalPart>[depth];
		matrices = new NativeArray<float3x4>[depth];
        
        matricesBuffers = new ComputeBuffer[depth];
		int stride = 12 * 4; //4x4 matrices have 16 float values of size 4 bytes each.
        
        //??= basically says if not THEN
        //if (propertyBlock == null) {
		propertyBlock ??= new MaterialPropertyBlock();
		//}
        
        //int length = 1;
		for (int i = 0, length = 1; i < parts.Length; i++, length *= 5) {
            //When declaring a native array you pass two arguments.
            //The size
            //How long you expect that array exist.
            //Anyway we're not done yet, there's also the parts creation loop below to deal with
            //Look for CreatePart(0), the creation of the root object to find it.
			parts[i] = new NativeArray<FractalPart>(length, Allocator.Persistent);
			matrices[i] = new NativeArray<float3x4>(length, Allocator.Persistent);
            matricesBuffers[i] = new ComputeBuffer(length, stride);
			//length *= 5;
		}
        //Create the whole fractal arrangement.
        //float scale = 1f; //The root part is the biggest. Part of the second pass
        //but we'll be changing the scale as we go down the tree.
        //parts[0][0] = CreatePart(0, 0, scale); //The root part second pass
        parts[0][0] = CreatePart(0);
        //We're nesting loops. The first loop goes through the "depth" variable.
        //li means length iterator.
        for (int li = 1; li < parts.Length; li++) {
            //scale *= 0.5f; //Part of the second pass
            //This is a reference to the parts array.
            //FULLER EXPLANATION: parts is a 2d array. The number of 2nd arrays is
            //determined by length *= 5 above. So a lot.
            NativeArray<FractalPart> levelParts = parts[li];
            //We take our references above and use them below.
            //They're just pointers anyways so we aren't making any unnecessary space.
            //The second loop actually creates the current layer.
            //fpi means fractal part iterator
			for (int fpi = 0; fpi < levelParts.Length; fpi+=5) {
				//We jump in this weird way for naming purposes.
                //Instead of having childINdex got up to like 500 we skip five and let the
                //ci only loop up to 5.
                for (int ci = 0; ci < 5; ci++) {
					//levelParts[fpi+ci] = CreatePart(li, ci, scale); //Part of the second pass
                    levelParts[fpi + ci] = CreatePart(ci);
				}
			}
        }
    }
    void OnDisable () { //Third pass. We need to disable things made in OnEnable to tidy things.
    //I don't care to let the garbage collector handle this.
		for (int i = 0; i < matricesBuffers.Length; i++) {
			matricesBuffers[i].Release();
            //Part 4
            parts[i].Dispose();
			matrices[i].Dispose();
		}
        parts = null;
		matrices = null;
		matricesBuffers = null;
	}
    
    //Third pass. OnValidate activates when a change has been made to the component
    //via the inspector.
    void OnValidate () {
        //Make sure to check what you're working with exists first.
        //OnValidate would get invoked if we toggled the active state.
        //Like if we disabled it through the inspector it would get disabled twice
        //Then re-enable in a weird way.
		if (parts != null && enabled) {
			OnDisable();
			OnEnable();
		}
	}
    
    void Update () {
        //To make the fractal animate we need to rotate again.
        //deltaRotation is the animating rotation.
        //If you recognize the 22.5f thing it's also in the cpu heavy part.
        //Quaternion deltaRotation = Quaternion.Euler(0f, 22.5f * Time.deltaTime, 0f); //Second pass
        float spinAngleDelta = 0.125f * PI * Time.deltaTime; //And yes, still working with radians instead
        //of degrees.
        FractalPart rootPart = parts[0][0];
        //rotation is usually set in stone but with this it'll be changed.
        //and it will be changed in a way that follows deltaRotation's rules.
		//rootPart.rotation *= deltaRotation; //second pass
        rootPart.spinAngle += spinAngleDelta; //Third pass. These rotation modifications needed
        //to be done on the struct but are now done on the root part.
        
        //And then we save it to the transform.
        //remember, just because its called rotation doesn't mean we've actually
        //changed the TRANSFORM'S rotation.
        //rootPart.transform.localRotation = rootPart.rotation;//Part of the second pass
        
        
        rootPart.worldRotation = mul(transform.rotation,
			mul(rootPart.rotation, quaternion.RotateY(rootPart.spinAngle))
		);
		rootPart.worldPosition = transform.position; 
        
        //this seems redudndant. It's not. It's a struct.
        //Changing a local variable won't keep if we don't copy the changes over.
        parts[0][0] = rootPart; //which we do here.
        
        //Third pass.
        //Simplest way to make a transformation matrix is using Matrix4x4.TRS
        //It stands for Translation-Rotation Scale. Which are the arguments too.
        //Translation in this case means to reposition or offset.
        float objectScale = transform.lossyScale.x;
        //What's a lossy scale?
        //A lossy scale indicates non-affine transformation.
        //It preps the transformation to be ready for that.
        //Sometimes scaling is not uniform and scaling things properly will
        //screw shit up.
		float3x3 r = float3x3(rootPart.worldRotation) * objectScale;
		matrices[0][0] = float3x4(r.c0, r.c1, r.c2, rootPart.worldPosition);
        
		float scale = objectScale;
        //PREFACE:
        //The update function will be changing the position of each ball.
        //But the positions are relative to each other (the scale goes down so
        //we need to pack them in tighter as we go down the depth).
        
		//Iterating over each part.
        //We don't need to do that triple loop thing because we actually know what
        //level we are on.
        //float scale = 1f; //Third pass
        JobHandle jobHandle = default;
        for (int li = 1; li < parts.Length; li++) {
			scale *= 0.5f; //Third pass
            //To get a realtive position we need the parent's position.
            
            
            //The old way we did it we have a var job = instead of
            //jobHandle. Then we scheduled later by enacting .Schedule on that
            //job variable.
            //But we don't need to store it in var job. Just schedule everything
            //and store the return value of .Schedule.
            jobHandle = new UpdateFractalLevelJob {
				spinAngleDelta = spinAngleDelta,
				scale = scale,
				parents = parts[li - 1],
				parts = parts[li],
				matrices = matrices[li]
			}.ScheduleParallel(parts[li].Length, 4, jobHandle);
            //Below is just another way to make a job do a for loop.
            
            //Schedule doesn't work immediately, it uhh schedules it for later.
            //So we need to have .Complete(); to wait for the completion of the
            //scheduled jobs.
            //But putting it in the loop means we'll be waiting EVERY time
            //we hit this statement.
            //So we schedule it now, but complete it later. We have the
            //return value stored in jobHandle after all.
            //jobHandle = job.Schedule(parts[li].Length, jobHandle);
            
			//for (int fpi = 0; fpi < parts[li].Length; fpi++) {
			//	job.Execute(fpi);
			//}

			//NativeArray<FractalPart> parentParts = parts[li - 1];
			//NativeArray<FractalPart> levelParts = parts[li];
			//NativeArray<Matrix4x4> levelMatrices = matrices[li];
            
            /*
            for (int fpi = 0; fpi < levelParts.Length; fpi++) {
                //Transform parentTransform = parentParts[fpi / 5].transform; //second pass
                FractalPart parent = parentParts[fpi / 5]; //third pass
				FractalPart part = levelParts[fpi];
                
                //You know what overwriting rotation does to it when modified by deltaRotation.
                //part.rotation *= deltaRotation; //second pass
                //Quaternion multiplication order matters.
                //This quaternion represent a rotation found by performing the rotation
                //of the second quaternion followed by the first quaternion.
                //Child's rotation is performed first then parent.
                //so it's parent->child multiplaction order.
                //part.transform.localRotation = parentTransform.localRotation * part.rotation;//Second pass
                
                //Third pass
                part.spinAngle += spinAngleDelta;
				part.worldRotation =
					parent.worldRotation *
					(part.rotation * Quaternion.Euler(0f, part.spinAngle, 0f));
                
                //First the local posiiton is equal to its parent's position + a little bit.
                //"a little bit" is 150% of the direction multipled by the scale.
                //direction is part of the struct, but it's also Vector3.right/up/down/etc.
                //Which has a value of 1. 1 is incidentally our maximum scale.
                //But without 150% multiplier we are using the scale of the smaller sphere.
                //So we'll clip into the bigger parent sphere without a boost.
                
                //Also if I'm going to rotate then that will affect the position too.
                part.transform.localPosition =
					parentTransform.localPosition +
					parentTransform.localRotation *
						(1.5f * part.transform.localScale.x * part.direction);
                
                /*
                //Third pass
                part.worldPosition =
					parent.worldPosition +
					parent.worldRotation * (1.5f * scale * part.direction);
                
                   
                levelParts[fpi] = part;
                //Third pass. Everytime we create a part we have to give it a matrix too.
                //Though we are using the scale for these because that is actually changing
                //as we go through the fractal lives.
                levelMatrices[fpi] = Matrix4x4.TRS(
					part.worldPosition, part.worldRotation, scale * Vector3.one
				);
			}
            */
            
		}
        jobHandle.Complete();
        
        //Third pass
        var bounds = new Bounds(rootPart.worldPosition, 3f * objectScale * Vector3.one);
        for (int i = 0; i < matricesBuffers.Length; i++) {
            ComputeBuffer buffer = matricesBuffers[i];
			buffer.SetData(matrices[i]);
            //Below isn't writing any big info, it links the
            //buffer and the kernel.
            //We are drawing through the GPU using DrawMeshInstancedProcedural. 
            //The material needs to go through.
            
            //But now we set the buffer to the propertyBlock.
            //and then we pass it as an extra argument.
            //Unity will copy the configuration that the propertyBlock has at the time.
            //Overruling whatever was set as the material.
            propertyBlock.SetBuffer(matricesId,buffer); 
            Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, buffer.count, propertyBlock);
		}
	}
    
    //This creates a new fractal part. If you want to remember how to programtically do so:
    //Reference this function.
    //If we want to animate and move the FractalPart we need its information.
    //So we need to return the piece we made.
    /*
    FractalPart CreatePart(int levelIndex, int childIndex, float scale) {
    */
    FractalPart CreatePart(int childIndex) => new FractalPart {
        /*
        var go = new GameObject("Fractal Part L" + levelIndex + " C" + childIndex);
        go.transform.localScale = scale * Vector3.one;
        
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().mesh = mesh;
        go.AddComponent<MeshRenderer>().material = material;
        */
        
        //Third pass change
        //return new FractalPart() {
            direction = directions[childIndex],
			rotation = rotations[childIndex] //,
			//transform = go.transform
    };
    
    //PREFACE:
    //The below method is CPU heavy and recursive. We can do this another way, 
    //but it is good to know the process to see it first.
    //We also removed some important components. The MeshFilter and MeshRenderer.
    //IN SUMMARY: BE CERTAIN TO USE THIS ONLY WITH CPU HEAVY FRACTAL Game Object
    /*
    void Start () {
        //Check the depth unless I WANT to spawn sphere's in a recursive loop forever
        name = "Test " + depth;
        
        if (depth <= 1) {
			return;
		}
        
        //We only want each child to have two children of their own.
        Fractal childA = CreateChild(Vector3.up, Quaternion.identity);
		Fractal childB = CreateChild(Vector3.right, Quaternion.Euler(0f, 0f, -90f));
        Fractal childC = CreateChild(Vector3.left, Quaternion.Euler(0f, 0f, 90f));
        Fractal childD = CreateChild(Vector3.forward, Quaternion.Euler(90f, 0f, 0f));
		Fractal childE = CreateChild(Vector3.back, Quaternion.Euler(-90f, 0f, 0f));
        
        
        //So we setup the parent-child relationship after the creation of both children.
        //The old way was create1->setparent, create2->setparent
        //so the second clone will inherit the children which means the thing is too fractal.
        //But only in the direction of the second child. 
		childA.transform.SetParent(transform, false);
		childB.transform.SetParent(transform, false);
        childC.transform.SetParent(transform, false);
        childD.transform.SetParent(transform, false);
		childE.transform.SetParent(transform, false);
        
    }
    void Update () {
		//Spin 'em. Every child will do this spinning.
        //Each update we rotate by a step: 22.5f.
        transform.Rotate(0f, 22.5f * Time.deltaTime, 0f);
    }
    Fractal CreateChild (Vector3 direction, Quaternion rotation) {
        //Child sphere created here.
        Fractal child = Instantiate(this);
		child.depth = depth - 1; //Made for the depth check if statement.
        child.transform.localRotation = rotation; //Adjust the direction the fractal grow out.
        child.transform.localPosition = 0.75f * direction; //With our modified scale we need to shrink distance
        //To let the spheres touch.
        child.transform.localScale = 0.5f * Vector3.one; //We're modifying the scale here.
        return child;
    }
    */
}
