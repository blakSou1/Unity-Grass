Shader "Custom/Plane2"
{
    Properties
    {
        _NearColor ("_NearColor", Color) = (1,1,1,1)
        _FarColor ("_FarColor", Color) = (1,1,1,1)
        _ColDistanceBlendStart ("_ColDistanceBlendStart", Float) = 1
        _ColDistanceBlendEnd ("_ColDistanceBlendEnd", Float) = 1
        _Brightness ("_Brightness", Float) = 1
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Heightmap ("Heightmap", 2D) = "white" {}
        _HeightMul ("Height multiplier", Float) = 1
        _Offset ("_Offset", Float) = 0.01
        _NormalMap ("Norm", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // URP features
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            // URP includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            // Material properties
            CBUFFER_START(UnityPerMaterial)
                float4 _NearColor;
                float4 _FarColor;
                float _ColDistanceBlendStart;
                float _ColDistanceBlendEnd;
                float _Brightness;
                float _Glossiness;
                float _Metallic;
                float _HeightMul;
                float _Offset;
                float4 _MainTex_ST;
                float4 _Heightmap_ST;
                float4 _NormalMap_ST;
            CBUFFER_END
            
            // Textures and samplers
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_Heightmap);
            SAMPLER(sampler_Heightmap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            
            // Vertex input
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };
            
            // Vertex output
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 customColor : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };
            
            // Helper function to transform vertex with heightmap
            float4 GetTransformedVertex(float4 positionOS)
            {
                // Transform to world space
                float3 positionWS = TransformObjectToWorld(positionOS.xyz);
                
                // Calculate world UV for heightmap
                float2 worldUV = positionWS.xz;
                worldUV = worldUV * (1.0 / _Heightmap_ST.xy) + _Heightmap_ST.zw;
                
                // Sample heightmap
                float height = SAMPLE_TEXTURE2D_LOD(_Heightmap, sampler_Heightmap, float4(worldUV, 0, 0), 0).r;
                
                // Apply height displacement
                float3 raisedPositionWS = positionWS + float3(0, 1, 0) * height * _HeightMul;
                
                // Transform back to object space
                return float4(TransformWorldToObject(raisedPositionWS), positionOS.w);
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // Transform vertex with heightmap
                float4 raisedVertexOS = GetTransformedVertex(input.positionOS);
                float3 raisedPositionWS = TransformObjectToWorld(raisedVertexOS.xyz);
                
                // Calculate normals using finite differences
                float3 bitangentOS = float3(1, 0, 0);
                float3 tangentOS = float3(0, 0, 1);
                
                float4 vertexBitangentOS = GetTransformedVertex(input.positionOS + float4(bitangentOS, 0) * _Offset);
                float4 vertexTangentOS = GetTransformedVertex(input.positionOS + float4(tangentOS, 0) * _Offset);
                
                float3 vertexBitangentWS = TransformObjectToWorld(vertexBitangentOS.xyz);
                float3 vertexTangentWS = TransformObjectToWorld(vertexTangentOS.xyz);
                
                float3 newBitangentWS = vertexBitangentWS - raisedPositionWS;
                float3 newTangentWS = vertexTangentWS - raisedPositionWS;
                
                float3 normalWS = normalize(cross(newTangentWS, newBitangentWS));
                
                // Calculate distance-based color
                float d = distance(raisedPositionWS, _WorldSpaceCameraPos);
                float colBlendVal = smoothstep(_ColDistanceBlendStart, _ColDistanceBlendEnd, d);
                float3 customColor = lerp(_NearColor.rgb, _FarColor.rgb, colBlendVal) * _Brightness;
                
                // Output data
                output.positionCS = TransformWorldToHClip(raisedPositionWS);
                output.positionWS = raisedPositionWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normalWS = normalWS;
                output.customColor = customColor;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Sample main texture
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // Convert to luminance (grayscale)
                half luminance = c.r * 0.3 + c.g * 0.59 + c.b * 0.11;
                
                // Apply custom color
                half3 albedo = luminance * input.customColor;
                
                // Create surface data for lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                
                // FIXED: Правильное получение shadow coordinates
                #if defined(_MAIN_LIGHT_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif
                
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SampleSH(inputData.normalWS);
                
                // Create surface description
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.alpha = 1.0;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Glossiness;
                surfaceData.emission = half3(0, 0, 0);
                surfaceData.occlusion = 1.0;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.clearCoatMask = 0.0;
                surfaceData.clearCoatSmoothness = 0.0;
                
                // Apply URP lighting
                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                
                // Apply fog
                color.rgb = MixFog(color.rgb, input.fogFactor);
                
                return color;
            }
            ENDHLSL
        }
        
        // Shadow pass
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float4 _Heightmap_ST;
                float _HeightMul;
            CBUFFER_END
            
            TEXTURE2D(_Heightmap);
            SAMPLER(sampler_Heightmap);
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };
            
            float3 ApplyHeightDisplacement(float3 positionWS)
            {
                float2 worldUV = positionWS.xz;
                worldUV = worldUV * (1.0 / _Heightmap_ST.xy) + _Heightmap_ST.zw;
                float height = SAMPLE_TEXTURE2D_LOD(_Heightmap, sampler_Heightmap, float4(worldUV, 0, 0), 0).r;
                return positionWS + float3(0, height * _HeightMul, 0);
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 displacedPositionWS = ApplyHeightDisplacement(positionWS);
                
                output.positionCS = TransformWorldToHClip(displacedPositionWS);
                return output;
            }
            
            half4 frag(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
        
        // Depth only pass
        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}
            
            ZWrite On
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float4 _Heightmap_ST;
                float _HeightMul;
            CBUFFER_END
            
            TEXTURE2D(_Heightmap);
            SAMPLER(sampler_Heightmap);
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };
            
            float3 ApplyHeightDisplacement(float3 positionWS)
            {
                float2 worldUV = positionWS.xz;
                worldUV = worldUV * (1.0 / _Heightmap_ST.xy) + _Heightmap_ST.zw;
                float height = SAMPLE_TEXTURE2D_LOD(_Heightmap, sampler_Heightmap, float4(worldUV, 0, 0), 0).r;
                return positionWS + float3(0, height * _HeightMul, 0);
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 displacedPositionWS = ApplyHeightDisplacement(positionWS);
                
                output.positionCS = TransformWorldToHClip(displacedPositionWS);
                return output;
            }
            
            half4 frag(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Lit"
}