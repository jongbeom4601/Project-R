// Drawn on a card just behind each window pane.
// It shows the night panorama by view direction, so the sky looks infinitely far away,
// and it writes depth, so neighbouring apartments behind the window stay hidden.
Shader "ProjectR/Window Sky"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Panorama (lat-long)", 2D) = "black" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Exposure ("Exposure", Range(0, 4)) = 0.85
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "WindowSky"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Exposure;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Same latitude-longitude mapping as the built-in Skybox/Panoramic shader.
                float3 direction = normalize(input.positionWS - GetCameraPositionWS());
                float latitude = acos(clamp(direction.y, -1.0, 1.0));
                float longitude = atan2(direction.z, direction.x);
                float2 uv = float2(0.5, 1.0) - float2(longitude * 0.5 / PI, latitude / PI);
                half3 color = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).rgb;
                return half4(color * _Tint.rgb * _Exposure, 1.0);
            }
            ENDHLSL
        }

        // Depth passes so the card occludes correctly with depth priming and SSAO.
        UsePass "Universal Render Pipeline/Unlit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Unlit/DEPTHNORMALSONLY"
    }
}
