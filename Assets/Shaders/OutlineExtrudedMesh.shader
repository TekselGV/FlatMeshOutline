Shader "Unlit/OutlineExtrudedMesh"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _OutlineWidth ("Outline Width", Range(0, 10)) = 0.2
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineColor2 ("Outline Color 2", Color) = (1,1,1,1)
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

        Pass
        {
            Tags
            {
                "LightMode" = "SRPDefaultUnlit"
            }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            //ZWrite On
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _MainTex_ST;
                half4 _OutlineColor;
                half4 _OutlineColor2;
                half _OutlineWidth;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            
            struct VertexInput
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 uv : TEXCOORD0;
                half4 vertexColor : COLOR;
            };

            struct VertexOutput
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half gradient : TEXCOORD1;
                half4 vertexColor : COLOR;
            };


            VertexOutput vert (VertexInput input)
            {
                VertexOutput output = (VertexOutput)0;

                input.positionOS += input.normalOS * input.uv.z * _OutlineWidth + half3(0, -0.2, 0) * input.uv.z;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS);

                output.positionCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.vertexColor = input.vertexColor;
                output.gradient = 1 - input.uv.z;
                
                return output;
            }

            half4 frag (VertexOutput input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                color *= input.vertexColor;
                color = lerp(color, lerp(_OutlineColor, _OutlineColor2, input.gradient), step(input.gradient, .99));
                

                color.a *= input.gradient;
                
                return color;
            }
            ENDHLSL
        }
    }
}
