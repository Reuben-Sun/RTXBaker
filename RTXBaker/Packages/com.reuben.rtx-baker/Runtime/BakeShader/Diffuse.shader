Shader "RT/Diffuse"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _BaseColorMap ("BaseColor Map", 2D) = "white" {}
        _Metallic ("Metallic", float) = 0.04
        _Roughness ("Roughness", float) = 0.5
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
            #pragma enable_d3d11_debug_symbols

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
            float _Metallic;
            float _Roughness;
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

                //材质信息
                RTXMaterialData mtlData;
                // 法线
                float3 normalOS = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.normalOS, v1.normalOS, v2.normalOS, barycentricCoordinates);
                float3x3 objectToWorld = (float3x3)ObjectToWorld3x4();
                mtlData.normalWS = normalize(mul(objectToWorld, normalOS));
                // BaseColor Map
                float2 texCoord0 = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.uv, v1.uv, v2.uv, barycentricCoordinates);
                mtlData.kd = _Color * SAMPLE_TEXTURE2D_LOD(_BaseColorMap, sampler_BaseColorMap, texCoord0, 0);
                
                float4 color = float4(0, 0, 0, 1);
                if (rayIntersection.remainingDepth > 0)
                {
                    // Get position in world space.
                    float3 origin = WorldRayOrigin();
                    float3 direction = WorldRayDirection();
                    float t = RayTCurrent();
                    float3 positionWS = origin + direction * t;

                    // 构建单位正交基
                    ONB uvw;
                    ONBBuildFromW(uvw, mtlData.normalWS);
                    // pdf
                    float pdf;
                    bool computeDirect = GetRandomValue(rayIntersection.PRNGStates) < 0.5f; //是否直接计算光照
                    if(!computeDirect)
                    {
                        //50%的概率采材质（求间接光）
                        float3 metalDir = reflect(direction, mtlData.normalWS);
                        float3 diffDir = ONBLocal(uvw, GetRandomCosineDirection(rayIntersection.PRNGStates));
                        if(dot(metalDir, mtlData.normalWS) < 0.0f)
                        {
                            metalDir = diffDir;
                        }
                        
                        mtlData.scatteredDir = lerp(diffDir, metalDir, _Metallic);
                        pdf = dot(mtlData.normalWS, mtlData.scatteredDir) / M_PI;   //这个 pdf不对，后效 PBR再改
                    }
                    else
                    {
                        //50%的概率采光源（求直接光）
                        RTXLight dirLight = GetDirectionalLight();
                        float NoL = dot(dirLight.direction, mtlData.normalWS);
                        if (NoL < 0.0f)
                        {
                            mtlData.scatteredDir = ONBLocal(uvw, GetRandomCosineDirection(rayIntersection.PRNGStates));
                            pdf = dot(mtlData.normalWS, mtlData.scatteredDir) / M_PI;
                        }
                        else
                        {
                            //漫反射着色
                            mtlData.diffVal = NoL * dirLight.color;
                            //高光反射着色

                            //向光源方向散射，之后要采阴影
                            mtlData.scatteredDir = dirLight.direction;
                            pdf = 1;
                        }
                    }
                    
                    // Make reflection ray.
                    RayDesc rayDescriptor;
                    rayDescriptor.Origin = positionWS + 0.001f * mtlData.normalWS;  //向外挤出一点点
                    rayDescriptor.Direction = mtlData.scatteredDir;
                    rayDescriptor.TMin = 1e-5f;
                    rayDescriptor.TMax = _CameraFarDistance;

                    // Tracing reflection.
                    RayIntersection reflectionRayIntersection;  //下一次追踪结果
                    reflectionRayIntersection.remainingDepth = rayIntersection.remainingDepth - 1;
                    reflectionRayIntersection.PRNGStates = rayIntersection.PRNGStates;
                    reflectionRayIntersection.color = float4(0.0f, 0.0f, 0.0f, 0.0f);

                    TraceRay(_AccelerationStructure, RAY_FLAG_CULL_BACK_FACING_TRIANGLES, RAYTRACINGRENDERERFLAG_ALL, 0, 1, 0, rayDescriptor, reflectionRayIntersection);

                    rayIntersection.PRNGStates = reflectionRayIntersection.PRNGStates;
                    color = float4(mtlData.diffVal * mtlData.kd, 1);    //直接光
                    color += float4(mtlData.kd, 1.0f) * ScatteringPDF(origin, direction, t, mtlData.normalWS, mtlData.scatteredDir) * reflectionRayIntersection.color / pdf;   //间接光
                    color = max(float4(0,0,0,0), color);
                }

                rayIntersection.color = color;
            }
            ENDHLSL
        }
    }
}