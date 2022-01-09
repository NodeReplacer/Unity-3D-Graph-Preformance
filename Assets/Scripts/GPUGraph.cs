using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GPUGraph : MonoBehaviour
{
    /*
    We've written a kernel that calculates and stores positions for our graph's points. (It's in FunctionLibrary).
    But we need access to the compute shader to actually do our work.
    IN CASE YOU FORGET:
    Compute shader is a shader stage that is used solely for computing arbitrary information.
    Which is good because we're about to drop some arbitrary information right on there.
    */
    [SerializeField]
    ComputeShader computeShader;
    /*
    IMPORTANT NOTE:
    Which kernel index is what is NOT discovered here. It is done in FunctionLibrary.compute
    This serializeField will accept FunctionLibrary.compute, but it will honestly accept any compute shader.
    So yeah if you mix the file placed there around we're gonna really feel it.
    */
    
    /*
    Now that we have the positions existant on the GPU (through FunctionLibrary.compute and UpdateFunctionOnGPU) 
    we don't need the CPU to track them anymore.
    We don't even need Game Objects anymore, just tell the GPU to draw a mesh we give it.
    */
    [SerializeField]
	Material material;
	[SerializeField]
	Mesh mesh;
    
    const int maxResolution = 1000;
    [SerializeField, Range(10,maxResolution)]
    int resolution = 10;
    
    [SerializeField]
    FunctionLibrary.FunctionName function;
    
    public enum TransitionMode { Cycle, Random }

	[SerializeField]
	TransitionMode transitionMode;
    
    [SerializeField, Min(0f)]
	float functionDuration = 1f, transitionDuration = 1f;
    
    //We need to set a few properties to our shader so we need the ID to y'know... find them.
    static readonly int 
        positionsId = Shader.PropertyToID("_Positions"),
        resolutionId = Shader.PropertyToID("_Resolution"),
		stepId = Shader.PropertyToID("_Step"),
		timeId = Shader.PropertyToID("_Time"),
        transitionProgressId = Shader.PropertyToID("_TransitionProgress");
    
    float duration; //How much time has passed. This will be compared to TransitionDuration. 
    //Like duration/TransitionDuration. Eventually that number will hit 1.
    
    bool transitioning;
    
    FunctionLibrary.FunctionName transitionFunction;
    
    //IN CASE YOU FORGET: Compute SHADERS need arbitrary information to both read in and write out.
    //Compute Buffers are that arbitrary information.
    ComputeBuffer positionsBuffer; 
    
    //Things in OnEnable survive hot reloads (code changes while in play mode).
	void OnEnable () {
		//Pass the number of elements as an argument for positionsBuffer
        //Just like the positions array from Graph, this is resolution squared.
        //The second argument is the size of each element.
        //3 points = 3 floats (so 3 items of 4 bytes each)
        positionsBuffer = new ComputeBuffer(maxResolution * maxResolution, 3 * 4);
        //The resolution has been changed to maxResolution. No matter the resolution we allocate the max amount we need.
        //This lets us change resolution on the fly without needing to reallocate our buffer size.
	}
    void OnDisable() {
        //If this component is disabled release what you are holding instead of letting the garbage collector eat it.
        positionsBuffer.Release();
        positionsBuffer = null;
    }
    
    void Update() {
        duration += Time.deltaTime;
        
        //If we are transitioning then we have to check whether we did it for too long.
        //So subtract the transition duration from the current duration then switch back to normal mode.
        if (transitioning) {
            if (duration >= transitionDuration) {
                duration -= transitionDuration;
                transitioning = false;
            }
        }
        else if (duration >= functionDuration) {
            duration -= functionDuration;
            transitioning = true;
            transitionFunction = function;
            //function = FunctionLibrary.GetNextFunctionName(function);
            PickNextFunction();
        }
        UpdateFunctionOnGPU();
    }
    
    void PickNextFunction() {
        function = transitionMode == TransitionMode.Cycle ?
			FunctionLibrary.GetNextFunctionName(function) :
			FunctionLibrary.GetRandomFunctionNameOtherThan(function);
    }
    
    //Calculate the step size, set resolution, step, and time properties.
    void UpdateFunctionOnGPU() {
        //Procedural drawing doesn't use game objects. Unity doesn't know where in the scene
        //the drawing happens. We use a bounding box to tell it.
        //But points have a size as well, half of which can poke out our box, so on top of needing to scale
        //our boxes by two (because that's the size we want) we also need to scale it by half of the number
        //of points we have (2f / resolution)
        var bounds = new Bounds(Vector3.zero, Vector3.one * (2f + 2f / resolution));
        
        float step = 2f / resolution;
		computeShader.SetInt(resolutionId, resolution);
		computeShader.SetFloat(stepId, step);
		computeShader.SetFloat(timeId, Time.time);
        //If we are transitioning (determined far above) 
        if (transitioning) {
            //This should be expected to run multiple times.
            //Graph.cs does the same thing. It takes the currentDuration/totalDuration
            //(a.k.a. how far along we are through a transition) then hands
            //that off to the functionLibrary to actually use.
            //In our case we smoothstep it, so instead of a cold number that doesn't pay attentions
            //to the rules of animation and evenly transitions from one to the other,
            //we receive a modified timescale that smoothly steps from one to the other.
			computeShader.SetFloat(transitionProgressId, Mathf.SmoothStep(0f, 1f, duration / transitionDuration));
		}
        
        //I should get a chart for this. 
        //We need to land on the correct kernel (which itself is a function). I already mentioned that
        //function B + function A * 5
        var kernelIndex = 
            (int)function + (int)(transitioning ? transitionFunction : function) * FunctionLibrary.FunctionCount;
        //kernelIndex used to be 0 which corresponded to wave. Now it changes.
        computeShader.SetBuffer(kernelIndex, positionsId, positionsBuffer);
        //Because of our fixed group size (8 by 8) the number of groups we need will be our resolution divided by eight.
        //But rounded up.
        int groups = Mathf.CeilToInt(resolution / 8f);
        //The first number is the kernel index and the other three are the groups to run.
        //Split by dimension.
        //In the beginning the kernel index was 0, which corresponded to the wave function.
        //For more info FunctionLibrary.compute lists each kernel. 0 = WaveKernel, etc.
        computeShader.Dispatch(kernelIndex, groups, groups, 1);
        //At this point we have only established a buffer
        
        material.SetBuffer(positionsId, positionsBuffer);
		material.SetFloat(stepId, step);
        
        //The Procedural drawing occurs here. We give a mesh, index for a sub-mesh, and material as the first 3 arguments.
        //The sub-mesh index is for when a mesh has many parts to it. We don't have that problem so let's give it a 0.
        //The fourth argument is our bounding box established above.
        //The last argument is how many instances we should draw. That is the same as the number of elements in position buffer.
        Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, resolution * resolution);
        //The last argument has been changed from positionsBuffer.count into the current resolution squared.
    }
}
