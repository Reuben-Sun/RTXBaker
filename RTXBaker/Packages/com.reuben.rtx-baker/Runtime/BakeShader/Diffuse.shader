Shader "RT/Diffuse"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _BaseColorMap ("BaseColor Map", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float3 normal : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD1;
            };

            sampler2D _BaseColorMap;
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColorMap_ST;
            half4 _Color;
            CBUFFER_END

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.uv = TRANSFORM_TEX(v.uv, _BaseColorMap);
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 col = _Color * tex2D(_BaseColorMap, i.uv);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
            }
        }

        SubShader
        {
            Pass
            {
                Name "RayTracing"
                Tags
            {
                "LightMode" = "RayTracing"
            }

            HLSLPROGRAM

            #pragma raytracing test

            #include "Common.hlsl"
            #include "PRNG.hlsl"
            #include "ONB.hlsl"

            struct IntersectionVertex
            {
                float3 normalOS;
                float2 uv;
            };

            TEXTURE2D(_BaseColorMap);
            SAMPLER(sampler_BaseColorMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColorMap_ST;
            float4 _Color;
            CBUFFER_END

            //获得顶点信息
            void FetchIntersectionVertex(uint vertexIndex, out IntersectionVertex outVertex)
            {
                outVertex.normalOS = UnityRayTracingFetchVertexAttribute3(vertexIndex, kVertexAttributeNormal);
                outVertex.uv = UnityRayTracingFetchVertexAttribute2(vertexIndex, kVertexAttributeTexCoord0);
            }

            [shader("closesthit")]
            void ClosestHitShader(inout RayIntersection rayIntersection : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
            {
                // 得到命中三角形
                uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());

                // 三角形三个顶点
                IntersectionVertex v0, v1, v2;
                FetchIntersectionVertex(triangleIndices.x, v0);
                FetchIntersectionVertex(triangleIndices.y, v1);
                FetchIntersectionVertex(triangleIndices.z, v2);

                // 顶点插值
                float3 barycentricCoordinates = float3(1.0 - attributeData.barycentrics.x - attributeData.barycentrics.y, attributeData.barycentrics.x, attributeData.barycentrics.y);

                // 法线
                float3 normalOS = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.normalOS, v1.normalOS, v2.normalOS, barycentricCoordinates);
                float3x3 objectToWorld = (float3x3)ObjectToWorld3x4();
                float3 normalWS = normalize(mul(objectToWorld, normalOS));

                // BaseColor Map
                float2 texCoord0 = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.uv, v1.uv, v2.uv, barycentricCoordinates);
                float4 texColor = _Color * SAMPLE_TEXTURE2D_LOD(_BaseColorMap, sampler_BaseColorMap, texCoord0, 0);
                
                float4 color = float4(0, 0, 0, 1);
                if (rayIntersection.remainingDepth > 0)
                {
                    // Get position in world space.
                    float3 origin = WorldRayOrigin();
                    float3 direction = WorldRayDirection();
                    float t = RayTCurrent();
                    float3 positionWS = origin + direction * t;

                    //pdf
                    ONB uvw;
                    ONBBuildFromW(uvw, normalWS);
                    float3 scatteredDir;    //散射方向
                    float pdf;
                    if(GetRandomValue(rayIntersection.PRNGStates) < 0.5f)
                    {
                        //50%的概率采半球（法线正方向）
                        scatteredDir = ONBLocal(uvw, GetRandomCosineDirection(rayIntersection.PRNGStates));
                        pdf = dot(normalWS, scatteredDir) / M_PI;
                    }
                    else
                    {
                        //50%的概率采自定义的光源
                        const float3 _FakeLightMin = float3(-106.5f, 554.0f, -52.5f);
                        const float3 _FakeLightMax = float3( 106.5f, 554.0f,  52.5f);
                        float3 onLight = float3(
                          _FakeLightMin.x + GetRandomValue(rayIntersection.PRNGStates) * (_FakeLightMax.x - _FakeLightMin.x),
                          _FakeLightMin.y,
                          _FakeLightMin.z + GetRandomValue(rayIntersection.PRNGStates) * (_FakeLightMax.z - _FakeLightMin.z));
                        float3 toLight = onLight - positionWS;
                        float distanceSquared = toLight.x * toLight.x + toLight.y * toLight.y + toLight.z * toLight.z;
                        toLight = normalize(toLight);
                        if (dot(toLight, normalWS) < 0.0f)
                        {
                          scatteredDir = ONBLocal(uvw, GetRandomCosineDirection(rayIntersection.PRNGStates));
                          pdf = dot(normalWS, scatteredDir) / M_PI;
                        }
                        float lightArea = (_FakeLightMax.x - _FakeLightMin.x) * (_FakeLightMax.z - _FakeLightMin.z);
                        float lightConsin = abs(toLight.y);
                        if (lightConsin < 1e-5f)
                        {
                          scatteredDir = ONBLocal(uvw, GetRandomCosineDirection(rayIntersection.PRNGStates));
                          pdf = dot(normalWS, scatteredDir) / M_PI;
                        }
                        scatteredDir = toLight;
                        pdf = distanceSquared / (lightConsin * lightArea);
                    }
                    
                    // Make reflection ray.
                    RayDesc rayDescriptor;
                    rayDescriptor.Origin = positionWS + 0.001f * normalWS;  //向外挤出一点点
                    rayDescriptor.Direction = scatteredDir;
                    rayDescriptor.TMin = 1e-5f;
                    rayDescriptor.TMax = _CameraFarDistance;

                    // Tracing reflection.
                    RayIntersection reflectionRayIntersection;
                    reflectionRayIntersection.remainingDepth = rayIntersection.remainingDepth - 1;
                    reflectionRayIntersection.PRNGStates = rayIntersection.PRNGStates;
                    reflectionRayIntersection.color = float4(0.0f, 0.0f, 0.0f, 0.0f);

                    TraceRay(_AccelerationStructure, RAY_FLAG_CULL_BACK_FACING_TRIANGLES, 0xFF, 0, 1, 0, rayDescriptor, reflectionRayIntersection);

                    rayIntersection.PRNGStates = reflectionRayIntersection.PRNGStates;
                    color = ScatteringPDF(origin, direction, t, normalWS, scatteredDir) * reflectionRayIntersection.color / pdf;
                    color = max(float4(0,0,0,0), color);
                }

                rayIntersection.color = texColor * 0.5f * color;
            }
            ENDHLSL
        }
    }
}