// URP replacement lit shader for dynamic entities (mobs) that supports a
// hurt-flash tint. Built for opaque materials; property names are kept
// compatible with URP/Lit (_BaseMap/_BaseColor/_Metallic/_Smoothness) so a
// runtime material swap keeps the source texture. The flash is applied to the
// albedo BEFORE lighting (vanilla style): shaded faces stay shaded while the
// whole body reads as red. Render layers drive _FlashAmount (0..1) per shell;
// the shader itself is passive and stateless.
Shader "Entity/HurtFlash"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        _Cutoff ("Alpha Cutout", Range(0,1)) = 0.5
        // Per-source culling: 0=Off (double-sided glTF layers) 2=Back (default)
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _FlashColor ("Hurt Flash Color", Color) = (1, 0.15, 0.15, 1)
        [HideInInspector] _FlashAmount ("Hurt Flash Amount", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        // LerpWhiteTo (Shadows.hlsl shadow-attenuation math) lives here; the
        // official ShadowCaster/DepthOnly pass templates assume it was pulled
        // in by LitInput.hlsl, which this shader does not include.
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // All passes must share one cbuffer layout or SRP Batcher batching breaks.
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4  _BaseColor;
        half   _Cutoff;
        half   _Metallic;
        half   _Smoothness;
        half4  _FlashColor;
        half   _FlashAmount;
        CBUFFER_END
        ENDHLSL

        // Main forward pass: main directional light + shadows + SH ambient +
        // Blinn-Phong specular + fog. A deliberate subset of URP/Lit - enough
        // for mob shells without dragging in the full additional-lights matrix.
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float  fogCoord   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                #ifdef _ALPHATEST_ON
                    clip(base.a - _Cutoff);   // cutout sources (glTF hair/hat layers): transparent pixels drop out
                #endif
                // Flash tints the albedo pre-lighting so the silhouette keeps
                // its shading (vanilla-style reddening, not a flat emissive).
                base.rgb = lerp(base.rgb, _FlashColor.rgb, saturate(_FlashAmount));

                float3 normalWS = normalize(input.normalWS);
                float3 positionWS = input.positionWS;

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
                float3 viewDir = GetWorldSpaceNormalizeViewDir(positionWS);
                float3 halfDir = normalize(viewDir + mainLight.direction);

                half NoL = saturate(dot(normalWS, mainLight.direction));
                half NoH = saturate(dot(normalWS, halfDir));
                half visibility = NoL * mainLight.shadowAttenuation * mainLight.distanceAttenuation;

                half gloss = exp2(_Smoothness * 10.0 + 1.0);
                half3 specColor = lerp(half3(0.04, 0.04, 0.04), base.rgb, _Metallic);
                half3 specular = specColor * pow(NoH, gloss) * mainLight.color * visibility;

                half3 ambient = SampleSH(normalWS) * base.rgb;
                half3 direct = base.rgb * mainLight.color * visibility;
                half3 color = direct + ambient + specular;

                color = MixFog(color, input.fogCoord);
                return half4(color, base.a);
            }
            ENDHLSL
        }

        // Standard URP shadow caster: writes the depth of the lit silhouette.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile _ _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // Depth pre-pass (camera depth texture): keeps the entity visible in
        // depth-based effects exactly like URP/Lit did.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile _ _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
}
