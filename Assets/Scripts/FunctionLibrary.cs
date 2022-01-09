using UnityEngine;

using static UnityEngine.Mathf;

public static class FunctionLibrary {
    
    public delegate Vector3 Function (float u, float v, float t);
    
    public enum FunctionName { Wave, MultiWave, Ripple, Sphere, Torus }
    
    static Function[] functions = { Wave, MultiWave, Ripple, Sphere, Torus };
    
    //Below are the many ways that we can turn functions.Length (which used to be a hardcoded 5)
    //into a property of FunctionLibrary itself.
    /*
    public static int GetFunctionCount () {
		return functions.Length;
	}
    
    //So usually the getter conversion looks like this. Take out the () and put this get thing in there.
    public static int FunctionCount {
		get {
			return functions.Length;
		}
	}
    
    //But we can simplify the above even further.
    public static int FunctionCount {
		get => functions.Length;
	}
    */
    //And then simplify it even further. Just remove the {}
    public static int FunctionCount => functions.Length;
    
    //See above, GetFunction is turning into a property now.
    /*
    public static Function GetFunction (FunctionName name) {
		return functions[(int)name];
    }
    */
    public static Function GetFunction (FunctionName name) => functions[(int)name];
    
    //HIPPITY HOPPITY
    /*
    public static FunctionName GetNextFunctionName (FunctionName name) {
		if ((int)name < functions.Length - 1) {
			return name + 1;
		}
		else {
			return 0;
		}
	}
    */
    public static FunctionName GetNextFunctionName (FunctionName name) =>
		(int)name < functions.Length - 1 ? name + 1 : 0;
    
    public static FunctionName GetRandomFunctionNameOtherThan (FunctionName name) {
		var choice = (FunctionName)Random.Range(1, functions.Length);
		return choice == name ? 0 : choice;
	}
    
    //LET'S SETTLE WHAT LERP AND SMOOTHSTEP DO HERE AND NOW.
    //lerp gives a SINGLE Vector3 point. It will be a point of linear interpolation between
    //function "from" and function "to". How far along the interpolation we are is determined by the third
    //argument time.
    //Smoothstep does a smooth transition between two numbers. ONLY. From arg 1 = minimum -> arg2 = maximum.
    //The third argument is also time, just like in lerp.
    
    //Time only goes from 0 to 1.
    //"progress" only shows up in Graph.cs. It is the currentDuration/totalDuration. A decimal going from 0 to 1.
    //It tracks how far along we are from one function to another.
    //Smoothstep translates this. Let's say currDur/totDur was 0.5, will take that and return 0.6 to speed things along.
    //So that when we get to the end where currDur/totDur was 0.8, smoothstep can slow down and start repeatedly giving 0.9s
    //or whatever.
    
    //Either way this morph will be run many times and currentDuration/totalDuration will be recalculated every time.
    //This updating display is what causes the animation.
    public static Vector3 Morph (float u, float v, float t, Function from, Function to, float progress) {
        return Vector3.LerpUnclamped(from(u, v, t), to(u, v, t), SmoothStep(0f, 1f, progress));
    }
    
    public static Vector3 Wave (float u, float v, float t) {
        Vector3 p;
        p.x = u;
        p.y = Sin(PI * (u + v + t));
        p.z = v;
        return p;
    }
    public static Vector3 MultiWave (float u, float v, float t) {
        Vector3 p;
        p.x = u;
        p.y = Sin(PI * (u + 0.5f * t));
        p.y += Sin(2f * PI * (u + t)) * 0.5f;
        p.y += Sin(PI * (u + v + 0.25f * t));
        p.y *= (1f/2.5f);
        p.z = v;
        return p;
    }
    public static Vector3 Ripple (float u, float v, float t) {
		float d = Sqrt(u * u + v * v);
        Vector3 p;
        p.x = u;
		p.y = Sin(PI * (4f * d - t));
        p.y /= 1f + 10f * d;
        p.z = v;
        return p;
	}
    public static Vector3 Sphere (float u, float v, float t) {
        float r = 0.9f + 0.1f * Sin(PI * (6f * u + 4f * v + t));
        float s = r * Cos(0.5f * PI * v);
        Vector3 p;
		p.x = s * Sin(PI * u);
		p.y = r * Sin(PI * 0.5f * v);
		p.z = s * Cos(PI * u);
		return p;
    }
    public static Vector3 Torus (float u, float v, float t) {
        //float r = 1f;
		float r1 = 0.7f + 0.1f * Sin(PI * (6f * u + 0.5f * t));
		float r2 = 0.15f + 0.05f * Sin(PI * (8f * u + 4f * v + 2f * t));
        float s = r1 + r2 * Cos(PI * v);
		Vector3 p;
		p.x = s * Sin(PI * u);
		p.y = r2 * Sin(PI * v);
		p.z = s * Cos(PI * u);
		return p;
    }
}