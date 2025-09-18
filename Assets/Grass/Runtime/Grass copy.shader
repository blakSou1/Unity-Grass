Shader "Unlit/Grass2"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Size ("Size", Range(0.1, 50)) = 1
        
        // Свойства цвета и эмиссии
        [HDR] _Color ("Base Color", Color) = (1,1,1,1)
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionStrength ("Emission Strength", Range(0, 10)) = 0
        [NoScaleOffset] _EmissionMap ("Emission Map", 2D) = "white" {}
        
        // Новые свойства для теней
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.7
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.2
    }
  
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue" = "Geometry"
            "LightMode" = "UniversalForward"
        }
      
        LOD 100
        Cull Off
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
                        
            #pragma multi_compile _ROTATIONTYPE_BLADE _ROTATIONTYPE_CUSTOM

            // Добавляем поддержку теней
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
          
            // Структура травинки (без изменений)
            struct GrassBlade
            {
                float3 position;
                float2 rotData;      
                float2 size;         
            };
            
            // Буферы (без изменений)
            StructuredBuffer<GrassBlade> _GrassBlades;
            StructuredBuffer<int> Triangles;
            StructuredBuffer<float3> Positions;
            StructuredBuffer<float2> Uvs;
          
            // Текстуры и параметры
            TEXTURE2D(_MainTex);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_MainTex);
            
            // Uniform-переменные
            CBUFFER_START(UnityPerMaterial)
            float _Size;
            float _ShadowStrength;
            float _AmbientStrength;

            float4 _Color;
            float4 _EmissionColor;
            float _EmissionStrength;
            CBUFFER_END

            // Входные и выходные структуры (без изменений)
            struct Attributes
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
                float3 normalOS : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
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

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                int positionIndex = Triangles[input.vertexID];
                float3 positionOS = Positions[positionIndex];
                float2 uv = Uvs[positionIndex];
                
                GrassBlade blade = _GrassBlades[input.instanceID];
                
                // Базовое позиционирование с учетом размера
                float3 newPos = positionOS * _Size;
                float width = blade.size.y * 0.1 * _Size;
                
                float rotAngle = blade.rotData.x;
                float3 rotationAxis = float3(0, 1, 0); // Поворот вокруг оси Y

                float3x3 rotationMatrix = AngleAxis3x3(rotAngle, rotationAxis);

                // Применение поворота к позиции травинки
                newPos = mul(rotationMatrix, newPos);

                // Применение позиции травинки
                newPos += blade.position;

                // Преобразование в мировое пространство
                float3 worldPos = TransformObjectToWorld(newPos);
                output.positionCS = TransformWorldToHClip(worldPos);
                
                // Нормали
                output.normalWS = TransformObjectToWorldNormal(float3(0, 1, 0));
                
                // Координаты для теней
                output.shadowCoord = TransformWorldToShadowCoord(worldPos);
                
                output.worldPos = worldPos;
                output.uv = uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                
                return output;
            }

            // Улучшенный фрагментный шейдер
            half4 frag(Varyings input) : SV_Target
            {
                // Семплирование текстур
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 emissionTex = SAMPLE_TEXTURE2D(_EmissionMap, sampler_MainTex, input.uv);
                
                // Получение основного света
                Light mainLight = GetMainLight(input.shadowCoord);
                
                // Расчет теней с плавным затуханием
                half shadowAttenuation = saturate(mainLight.shadowAttenuation + _AmbientStrength);
                shadowAttenuation = lerp(1 - _ShadowStrength, 1, shadowAttenuation);
                
                // Диффузное освещение
                half NdotL = saturate(dot(input.normalWS, mainLight.direction));
                half3 diffuse = mainLight.color * NdotL * shadowAttenuation;
                
                // Применение базового цвета и освещения
                half4 finalColor = texColor * _Color;
                finalColor.rgb *= diffuse + (_AmbientStrength * mainLight.color);
                
                // Добавление эмиссии
                half3 emission = _EmissionColor.rgb * emissionTex.rgb * _EmissionStrength;
                finalColor.rgb += emission;
                
                // Применение тумана
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);

                return finalColor;
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Unlit"
}
