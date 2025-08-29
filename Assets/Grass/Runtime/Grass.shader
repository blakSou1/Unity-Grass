Shader "Unlit/Grass"
{
    Properties
    {
        _ClumpColorBlend("_ClumpColorBlend", Range (0, 1)) = 1
        [Header(Albedo)]
        _GrassAlbedo("Grass albedo", 2D) = "white" {}
        _AlbedoScale("Albedo Scale", Float) = 0
        _AlbedoStrength("Albedo Strength", Float) = 0
        [Header(Gloss)]
        _GrassGloss("Grass gloss", 2D) = "white" {}
        _GlossScale("Gloss Scale", Float) = 0
        [Header(Shape)]
        _TaperAmount ("Taper Amount", Float) = 0
        _CurvedNormalAmount("Curved Normal Amount", Float) = 1
        _p1Flexibility ("p1Flexibility", Float) = 1
        _p2Flexibility ("p2Flexibility", Float) = 1
        [Header(Animation)]
        _WaveAmplitude("Wave Amplitude", Float) = 1
        _WaveSpeed("Wave Speed", Float) = 1
        _WavePower("Wave Power", Float) = 1
        _SinOffsetRange("Sin OffsetRange", Float) = 1
        _PushTipOscillationForward("_PushTipOscillationForward", Float) = 1
        [Header(Shading)]
        _Kspec("Specular Strength", Float) = 0
        _Kd("Diffuse Strength", Float) = 0
        _Kamb("Ambient Strength", Float) = 0
        _ShininessLower("Lower Shininess", Float) = 1
        _ShininessUpper("Upper Shininess", Float) = 1
        _SpecularLengthAtten("Specular Length Atten", Float) = 1
        _TipCol ("Tip Color", Color) = (.25, .5, .5, 1)
        _TipColLowerDist("_TipColLowerDist", Float) = 0.8
        _TipColUpperDist("_TipColUpperDist", Float) = 1
        _TopColor ("Top Color", Color) = (.25, .5, .5, 1)
        _BottomColor ("Bottom Color", Color) = (.25, .5, .5, 1)
        _LengthShadingStrength("Length shading multiplier", Float) = 1
        _LengthShadingBaseLuminance("Length shading offset", Float) = 1
        [Header(Distance shading)]
        _BlendSurfaceNormalDistLower("Start Distance (Blend Surface Normal)", Float) = 1
        _BlendSurfaceNormalDistUpper("End Distance (Blend Surface Normal)", Float) = 1
        _DistantDiff("Distant Diffuse Strength", Float) = 1
        _DistantSpec("Distant Specular Strength", Float) = 1
        _BottomColDistanceBrightness ("_BottomColDistanceBrightness", Float) = 1
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        
        LOD 100
        Cull Off
        
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
            
            // Custom features
            #pragma multi_compile_local __ USE_CLUMP_COLORS
            
            // URP includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            // Structures
            struct GrassBlade
            {
                float3 position;
                float2 rotData;      
                float2 size;         
                float2 shape;        
                float3 color;
                float windForce;
            };
            
            // GPU buffers
            StructuredBuffer<GrassBlade> _GrassBlades;
            StructuredBuffer<int> Triangles;
            StructuredBuffer<float3> Positions;
            StructuredBuffer<float4> Colors;
            StructuredBuffer<float2> Uvs;
            
            // Material properties in CBUFFER for SRP Batcher
            CBUFFER_START(UnityPerMaterial)
                float _ClumpColorBlend;
                float _AlbedoScale;
                float _AlbedoStrength;
                float _GlossScale;
                float _TaperAmount;
                float _CurvedNormalAmount;
                float _p1Flexibility;
                float _p2Flexibility;
                float _WaveAmplitude;
                float _WaveSpeed;
                float _WavePower;
                float _SinOffsetRange;
                float _PushTipOscillationForward;
                float _Kspec;
                float _Kd;
                float _Kamb;
                float _ShininessLower;
                float _ShininessUpper;
                float _SpecularLengthAtten;
                float4 _TipCol;
                float _TipColLowerDist;
                float _TipColUpperDist;
                float4 _TopColor;
                float4 _BottomColor;
                float _LengthShadingStrength;
                float _LengthShadingBaseLuminance;
                float _BlendSurfaceNormalDistLower;
                float _BlendSurfaceNormalDistUpper;
                float _DistantDiff;
                float _DistantSpec;
                float _BottomColDistanceBrightness;
                float _WindControl;
            CBUFFER_END
            
            // Textures and samplers
            TEXTURE2D(_GrassAlbedo);
            SAMPLER(sampler_GrassAlbedo);
            TEXTURE2D(_GrassGloss);
            SAMPLER(sampler_GrassGloss);
            
            // Vertex input
            struct Attributes
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };
            
            // Vertex output / Fragment input
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
                float3 curvedNorm : TEXCOORD3;
                float3 originalNorm : TEXCOORD4;
                float3 surfaceNorm : TEXCOORD5;
                float4 vertexColor : TEXCOORD6;
                float4 color : COLOR0;
                float fogFactor : TEXCOORD7;
            };
            
            // Helper functions
            float3x3 AngleAxis3x3(float angle, float3 axis)
            {
                float c, s;
                sincos(angle, s, c);
                float t = 1 - c;
                float x = axis.x;
                float y = axis.y;
                float z = axis.z;
                
                return float3x3(
                    t * x * x + c, t * x * y - s * z, t * x * z + s * y,
                    t * x * y + s * z, t * y * y + c, t * y * z - s * x,
                    t * x * z - s * y, t * y * z + s * x, t * z * z + c
                );
            }
            
            float3 cubicBezier(float3 p0, float3 p1, float3 p2, float3 p3, float t)
            {
                float3 a = lerp(p0, p1, t);
                float3 b = lerp(p2, p3, t);
                float3 c = lerp(p1, p2, t);
                float3 d = lerp(a, c, t);
                float3 e = lerp(c, b, t);
                return lerp(d, e, t);
            }
            
            float3 bezierTangent(float3 p0, float3 p1, float3 p2, float3 p3, float t)
            {
                float omt = 1 - t;
                float omt2 = omt * omt;
                float t2 = t * t;
                
                float3 tangent = 
                    p0 * (-omt2) +
                    p1 * (3 * omt2 - 2 * omt) +
                    p2 * (-3 * t2 + 2 * t) +
                    p3 * (t2);
                
                return normalize(tangent);
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                // Extract vertex data
                int positionIndex = Triangles[input.vertexID];
                float3 positionOS = Positions[positionIndex];
                float4 vertColor = Colors[positionIndex];
                float2 uv = Uvs[positionIndex];
                
                float t = vertColor.r;
                float side = vertColor.g;
                side = (side * 2) - 1;
                
                // Get blade data
                GrassBlade blade = _GrassBlades[input.instanceID];
                
                // Bezier curve calculations (your existing code)
                float height = blade.size.x;
                float tilt = blade.shape.x;
                float bend = blade.shape.y;
                float hash = blade.rotData.y;
                float width = blade.size.y * (1 - _TaperAmount * t);
                float angle = blade.rotData.x;

                float3 p0 = float3(0, 0, 0);
                float p3y = tilt * height;
                float p3x = sqrt(height * height - p3y * p3y);
                float3 p3 = float3(-p3x, p3y, 0);

                float3 bladeDir = normalize(p3);
                float3 bezCtrlOffsetDir = normalize(cross(bladeDir, float3(0, 0, 1)));
                
                float3 p1 = 0.33 * p3;
                float3 p2 = 0.66 * p3;
                
                p1 += bezCtrlOffsetDir * bend * _p1Flexibility;
                p2 += bezCtrlOffsetDir * bend * _p2Flexibility;
                
                // Wind animation (your existing code)
                float windForce = blade.windForce;
                float waveAmplitude = lerp(0, _WaveAmplitude, _WindControl);
                float waveSpeed = lerp(0, _WaveSpeed, _WindControl);
                float mult = 1 - bend;
                
                float p2ffset = pow(0.66, _WavePower) * (waveAmplitude / 100) * 
                    sin((_Time.y + hash * 2 * PI) * waveSpeed + 0.66 * 2 * PI * _SinOffsetRange) * windForce;
                float p3ffset = pow(1.0, _WavePower) * (waveAmplitude / 100) * 
                    sin((_Time.y + hash * 2 * PI) * waveSpeed + 1.0 * 2 * PI * _SinOffsetRange) * windForce;
                
                p3ffset = p3ffset - _PushTipOscillationForward * mult * (pow(1.0, _WavePower) * waveAmplitude / 100) / 2;
                
                p2 += bezCtrlOffsetDir * p2ffset;
                p3 += bezCtrlOffsetDir * p3ffset;
                
                // Evaluate Bezier curve
                float3 newPos = cubicBezier(p0, p1, p2, p3, t);
                float3 midPoint = newPos;
                float3 tangent = normalize(bezierTangent(p0, p1, p2, p3, t));
                float3 normal = normalize(cross(tangent, float3(0, 0, 1)));
                
                float3 curvedNormal = normal;
                curvedNormal.z += side * pow(_CurvedNormalAmount, 1);
                curvedNormal = normalize(curvedNormal);
                
                newPos.z += side * width;
                
                // Apply rotation
                float sideBend = _WindControl * blade.windForce * _p1Flexibility;
                
                float3x3 rotMat = AngleAxis3x3(-angle, float3(0, 1, 0));
                float3x3 sideRot = AngleAxis3x3(sideBend, normalize(tangent));
                
                newPos = newPos - midPoint;
                normal = mul(sideRot, normal);
                curvedNormal = mul(sideRot, curvedNormal);
                newPos = mul(sideRot, newPos);
                newPos = newPos + midPoint;
                
                normal = mul(rotMat, normal);
                curvedNormal = mul(rotMat, curvedNormal);
                newPos = mul(rotMat, newPos);
                
                newPos += blade.position;
                
                // Transform to world space
                float3 positionWS = newPos;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.worldPos = positionWS;
                
                // Calculate view direction
                float3 cameraPosWS = _WorldSpaceCameraPos;
                output.viewDir = GetWorldSpaceNormalizeViewDir(positionWS);
                
                // Pass through other data
                output.uv = uv;
                output.curvedNorm = curvedNormal;
                output.originalNorm = normal;
                output.surfaceNorm = float3(0,1,0);
                output.vertexColor = float4(vertColor.xyz, 1.0);
                output.color = float4(blade.color, 1);
                
                // Fog
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                
                return output;
            }
            
            half4 frag(Varyings input, half facing : VFACE) : SV_Target
            {
                // Normal calculation
                float3 curvedNorm = normalize(input.curvedNorm);
                float3 originalNorm = normalize(input.originalNorm);
                float3 n;
                
                if (facing > 0)
                {
                    n = curvedNorm;
                }
                else
                {
                    n = -reflect(-curvedNorm, originalNorm);
                }
                
                // Distance-based normal blending
                float distToCam = distance(input.worldPos, _WorldSpaceCameraPos);
                float surfaceNormalBlend = smoothstep(
                    _BlendSurfaceNormalDistLower, 
                    _BlendSurfaceNormalDistUpper, 
                    distToCam
                );
                
                n = lerp(n, normalize(input.surfaceNorm), surfaceNormalBlend);
                n = normalize(n);
                
                // Lighting calculations using URP functions
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.worldPos));
                float3 lightDir = mainLight.direction;
                float3 lightColor = mainLight.color;
                
                // Specular
                float gloss = SAMPLE_TEXTURE2D(_GrassGloss, sampler_GrassGloss, input.uv * _GlossScale).r;
                float3 viewDir = normalize(input.viewDir);
                float3 reflectDir = reflect(-lightDir, n);
                
                float shininess = lerp(_ShininessLower, _ShininessUpper, gloss);
                float specIntensity = lerp(_Kspec, _DistantSpec, surfaceNormalBlend);
                float spec = specIntensity * pow(saturate(dot(reflectDir, viewDir)), shininess) * 
                           pow(input.vertexColor.r, _SpecularLengthAtten);
                
                // Diffuse
                float diffIntensity = lerp(_Kd, _DistantDiff, surfaceNormalBlend);
                float diff = diffIntensity * saturate(dot(n, lightDir));
                
                // Ambient
                float3 ambient = SampleSH(n) * _Kamb;
                
                // Combined lighting
                float3 lighting = ambient + (diff + spec) * lightColor;
                lighting *= saturate(input.vertexColor.r * _LengthShadingStrength + _LengthShadingBaseLuminance);
                
                // Albedo
                float grassAlbedo = SAMPLE_TEXTURE2D(_GrassAlbedo, sampler_GrassAlbedo, input.uv * _AlbedoScale).r;
                float noAlbedoMask = floor(grassAlbedo);
                grassAlbedo = saturate(grassAlbedo * _AlbedoStrength + noAlbedoMask);
                
                // Color blending
                float bottomColBrightness = lerp(1, _BottomColDistanceBrightness, surfaceNormalBlend);
                half4 grassCol = lerp(_BottomColor * bottomColBrightness, _TopColor, input.vertexColor.r);
                
                float sstep = smoothstep(_TipColLowerDist, _TipColUpperDist, input.vertexColor.r);
                grassCol = lerp(grassCol, _TipCol, sstep);
                
                // Final color with clump colors
                half4 finalColor;
                #ifdef USE_CLUMP_COLORS
                    half4 clumpCol = lerp(grassCol, input.color * grassCol, _ClumpColorBlend * input.vertexColor.w);
                    finalColor = half4(lighting * grassAlbedo * clumpCol.rgb, 1);
                #else
                    finalColor = half4(lighting * grassAlbedo * grassCol.rgb, 1);
                #endif
                
                // Apply fog
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);
                
                return finalColor;
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Simple Lit"
}