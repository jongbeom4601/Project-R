Shader "ProjectR/Apartment Doorway"
{
    Properties { _DoorView ("Doorway view", 2D) = "black" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_DoorView);
            SAMPLER(sampler_DoorView);

            struct Varyings
            {
                float4 position : SV_POSITION;
                float4 screen : TEXCOORD0;
            };

            Varyings Vert(float3 position : POSITION)
            {
                Varyings output;
                output.position = TransformObjectToHClip(position);
                output.screen = ComputeScreenPos(output.position);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_DoorView, sampler_DoorView, input.screen.xy / input.screen.w);
            }
            ENDHLSL
        }
    }
}
