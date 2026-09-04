Shader "MainGame/P2/Black Water Mirror"
{
    Properties
    {
        _DeepColor ("Deep Color", Color) = (0.003, 0.006, 0.009, 1)
        _EdgeColor ("Edge Color", Color) = (0.05, 0.11, 0.14, 1)
        _HighlightColor ("Highlight Color", Color) = (0.18, 0.32, 0.36, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.96
        _Smoothness ("Smoothness", Range(0, 1)) = 0.98
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3.2
        _RippleStrength ("Ripple Strength", Range(0, 0.05)) = 0.008
        _RippleScale ("Ripple Scale", Range(0.5, 12)) = 4.5
        _RippleSpeed ("Ripple Speed", Range(0, 4)) = 0.38
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            float4 _DeepColor;
            float4 _EdgeColor;
            float4 _HighlightColor;
            float _Alpha;
            float _Smoothness;
            float _FresnelPower;
            float _RippleStrength;
            float _RippleScale;
            float _RippleSpeed;

            struct Attributes
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.vertex.xyz;
                float waveA = sin((positionOS.x + positionOS.z * 1.7 + _Time.y * _RippleSpeed) * _RippleScale);
                float waveB = sin((positionOS.x * 2.1 - positionOS.z + _Time.y * _RippleSpeed * 0.73) * (_RippleScale * 0.63));
                positionOS.y += (waveA + waveB) * _RippleStrength;

                float4 world = mul(unity_ObjectToWorld, float4(positionOS, 1));
                output.positionWS = world.xyz;
                output.positionCS = UnityObjectToClipPos(float4(positionOS, 1));
                output.normalWS = UnityObjectToWorldNormal(input.normal);
                output.uv = input.uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                float fresnel = pow(saturate(1.0 - dot(normalWS, viewDir)), _FresnelPower);

                float rippleLine = sin((input.uv.x * 18.0 + input.uv.y * 11.0) + _Time.y * 0.8) * 0.5 + 0.5;
                rippleLine *= sin((input.uv.x * 7.0 - input.uv.y * 16.0) - _Time.y * 0.45) * 0.5 + 0.5;

                float3 color = lerp(_DeepColor.rgb, _EdgeColor.rgb, fresnel);
                color += _HighlightColor.rgb * rippleLine * fresnel * 0.18 * _Smoothness;
                return float4(color, _Alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
