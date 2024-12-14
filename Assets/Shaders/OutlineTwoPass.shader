Shader "Unlit/OutlineTwoPass"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _OutlineWidth ("Outline Width", Range(0, 1)) = 0.2
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Unlit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass // Main
        {
            Tags
            {
                "LightMode" = "SRPDefaultUnlit"
            }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _MainTex_ST;
                half4 _OutlineColor;
                half _OutlineWidth;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            
            struct VertexInput
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 vertexColor : COLOR;
            };

            struct VertexOutput
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                half4 vertexColor : COLOR;
            };


            VertexOutput vert (VertexInput input)
            {
                VertexOutput output = (VertexOutput)0;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS);

                output.positionCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.vertexColor = input.vertexColor;
                
                return output;
            }

            half4 frag (VertexOutput input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                color *= input.vertexColor;
                
                return color;
            }
            ENDHLSL
        }

        Pass // Outline
        {
            Tags
            {
                "LightMode" = "Outline"
            }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _MainTex_ST;
                half4 _OutlineColor;
                half _OutlineWidth;
            CBUFFER_END

            struct VertexInput
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 uv : TEXCOORD0;
            };

            struct VertexOutput
            {
                float gradient : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            VertexOutput vert (VertexInput input)
            {
                VertexOutput output = (VertexOutput)0;

                input.positionOS += input.normalOS * _OutlineWidth * input.uv.z;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS);

                output.positionCS = positionInputs.positionCS;
                output.gradient = input.uv.z;
                
                return output;
            }

            half4 frag (VertexOutput input) : SV_Target
            {
                half4 color = _OutlineColor;
                
                return color;
            }
            ENDHLSL
        }
    }
}
