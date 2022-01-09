using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Graph : MonoBehaviour
{
    [SerializeField]
    Transform pointPrefab;
    
    [SerializeField, Range(10,100)]
    int resolution = 10;
    
    [SerializeField]
    FunctionLibrary.FunctionName function;
    
    public enum TransitionMode { Cycle, Random }

	[SerializeField]
	TransitionMode transitionMode;
    
    [SerializeField, Min(0f)]
	float functionDuration = 1f, transitionDuration = 1f;
    
    
    Transform[] points;
    float duration; //How much time has passed before the duration is up.
    
    bool transitioning;
    
    FunctionLibrary.FunctionName transitionFunction; //We are transitioning AWAY from this function.
    //So the current function should find it's way into this holder every time.
    
    void Awake () {
        float step = 2f / resolution;
        //var position = Vector3.zero;
        var scale = Vector3.one * step;
        
        points = new Transform[resolution * resolution];
        
        //This loop keeps track of the "grid" in both directions.
        //Without x and z it would be a 2d graph.
        for (int i = 0; i < points.Length; ++i) {
            //if (x == resolution) {
                //x = 0;
                //z+=1;
			//}
            Transform point = points[i] = Instantiate(pointPrefab);
			//point.localPosition = Vector3.right * ((i + 0.5f) / 5f - 1f);
            //position.x = (x + 0.5f) * step - 1f;
			//position.z = (z + 0.5f) * step - 1f;
            //point.localPosition = position;
            point.localScale = scale;
            point.SetParent(transform, false);
            points[i] = point;
		}
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
        
        if (transitioning) {
            UpdateFunctionTransition();
        }
        else {
            UpdateFunction();
        }
        
    }
    
    void PickNextFunction() {
        function = transitionMode == TransitionMode.Cycle ?
			FunctionLibrary.GetNextFunctionName(function) :
			FunctionLibrary.GetRandomFunctionNameOtherThan(function);
    }
    
    void UpdateFunction() {
        FunctionLibrary.Function f = FunctionLibrary.GetFunction(function);
        float time = Time.time;
        float step = 2f / resolution;
        float v = 0.5f * step - 1f;
        for (int i = 0, x = 0, z = 0; i < points.Length; ++i, ++x) {
            if (x == resolution) {
				x = 0;
				z += 1;
                v = (z + 0.5f) * step - 1f;
			}
            float u = (x + 0.5f) * step - 1f;
			//float v = (z + 0.5f) * step - 1f;
			points[i].localPosition = f(u, v, time);
        }
    }
    
    void UpdateFunctionTransition() {
        FunctionLibrary.Function from = FunctionLibrary.GetFunction(transitionFunction);
        FunctionLibrary.Function to = FunctionLibrary.GetFunction(function);
        float progress = duration / transitionDuration;
        float time = Time.time;
        float step = 2f / resolution;
        float v = 0.5f * step - 1f;
        for (int i = 0, x = 0, z = 0; i < points.Length; ++i, ++x) {
            if (x == resolution) {
				x = 0;
				z += 1;
                v = (z + 0.5f) * step - 1f;
			}
            float u = (x + 0.5f) * step - 1f;
			//float v = (z + 0.5f) * step - 1f;
			points[i].localPosition = FunctionLibrary.Morph(u, v, time, from, to, progress);
        }
    }
}
