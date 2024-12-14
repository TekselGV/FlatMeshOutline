Shader "Unlit/OutlineExtrudedMesh"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _OutlineWidth ("Outline Width", Range(0, 2)) = 0.2
        [Header (Color Setup)]
        [Space(7)]
        _OutlineColorInner ("Outline Color Inner", Color) = (0,0,0,1)
        _OutlineColorOuter ("Outline Color Outer", Color) = (1,1,1,0)
        [Space(2)]
        _OutlineColorEdge ("Outline Color Edge", Range(0, 1)) = 0.5
        _OutlineColorEdgeSmooth ("Outline Color Edge Smooth", Range(0.001, 1)) = 0.2
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
            
            Zwrite Off // if big outline width causing self-intersecting Z-fighting artifacts - try 'Zwrite On' 
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _MainTex_ST;
                half4 _OutlineColorInner;
                half4 _OutlineColorOuter;
                half _OutlineWidth;
                half _OutlineColorEdge;
                half _OutlineColorEdgeSmooth;
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

                float3 objectScale = float3(
                    length(GetObjectToWorldMatrix()._m00_m10_m20),
                    length(GetObjectToWorldMatrix()._m01_m11_m21),
                    length(GetObjectToWorldMatrix()._m02_m12_m22)
                );

                half3 scaleFactor = 1 / objectScale; // To make outline width independent from object transform scale
                input.positionOS += input.normalOS * input.uv.z * _OutlineWidth * scaleFactor;
                
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

                half step1 = saturate(_OutlineColorEdge + _OutlineColorEdgeSmooth);
                half step2 = saturate(_OutlineColorEdge - _OutlineColorEdgeSmooth);
                half outlineGradient = smoothstep(step1, step2, input.gradient);
                
                half4 outlineColor = lerp(_OutlineColorInner, _OutlineColorOuter, outlineGradient);
                color = lerp(color, outlineColor, step(input.gradient, .99));
                
                return color;
            }
            ENDHLSL
        }
    }
}
