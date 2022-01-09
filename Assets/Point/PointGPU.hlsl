//So far, this is section is copied straight from Points Surface GPU
//We'll be including it into Point Surface GPU as well, we're just moving it off to here.

//This structured buffer was declared in our compute shader. We only need to read from it now so
//skip RWStructuredBuffer, acquire StructuredBuffer
//Only do use StructuredBuffer if our shader variants specifically compiled for proc draw.
//There's actually something called UNITY_PROCEDURAL_INSTANCING_ENABLED macro label if it is defined.
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
	StructuredBuffer<float3> _Positions;
#endif

float _Step;

void ConfigureProcedural () {
	#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
		float3 position = _Positions[unity_InstanceID];
        //The object to world transformation matrix for one point.
        //Messing with the transform component of GPU Graph object won't do anything.
        //We aren't using it, after all.
		unity_ObjectToWorld = 0.0;
        //A basic column vector. This sets it as the fourth column of our Obj to World.
		unity_ObjectToWorld._m03_m13_m23_m33 = float4(position, 1.0);
		unity_ObjectToWorld._m00_m11_m22 = _Step;
	#endif
}
//The shader will use a Custom Function node to include this HLSL file.
//The node will invoke a function to do this. Which is why we have this do nothing function.
void ShaderGraphFunction_float (float3 In, out float3 Out) {
	Out = In;
}
//Shader graph has two precision modes. float or half. half is uh half the size of the former.
//Two instead of four bytes. Just to make sure we work with both precision modes in case somebody
//explicitly sets it: We do the same as above but for half.
//Now we go down to our Point URP GPU shader graph.
void ShaderGraphFunction_half (half3 In, out half3 Out) {
	Out = In;
}