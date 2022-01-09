Shader "Fractal/Fractal Surface GPU" {
    
    Properties {
		_BaseColor ("Base Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
	}
    
    SubShader {
        CGPROGRAM
        #pragma surface ConfigureSurface Standard fullforwardshadows addshadow
        //Procedural rendering works like GPU instancing but we need to specify the option in pragma.
        //Usually this is only the draw pass, to give it the shadow pass we need to addshadow above and
        //some other things.
        #pragma instancing_options assumeuniformscaling procedural:ConfigureProcedural 
        //If you remember those cyan blocks that take a moment to load. That is unique to the editor mode.
        //It's because shaders are compiled asynchronously. Usually. But because we use proc drawing the dummy shader
        //will be in for a hell of a time. Specifically it will slow down. I'm about to attempt 1 million points.
        //If I get caught asynchronously I'll be dead in the water.
        //The option editor_sync_compilation will make sure we do it all at the same time.
        //Goodbye cyan blocks.
        #pragma editor_sync_compilation
        #pragma target 4.5 //We need OpenGL 4.5
        
        #include "FractalGPU.hlsl"
        
        struct Input {
            float3 worldPos;
        };
        
        float4 _BaseColor;
        float _Smoothness;
        
        void ConfigureSurface (Input input, inout SurfaceOutputStandard surface) {
            surface.Albedo = _BaseColor.rgb;
            surface.Smoothness = _Smoothness;
        }
        ENDCG
    }
    
    FallBack "Diffuse"
    
}