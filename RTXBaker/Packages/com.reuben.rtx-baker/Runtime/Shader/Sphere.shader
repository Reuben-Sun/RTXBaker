Shader "Sphere"
{
    Properties
    {
        _Color ("Main Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
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
            };

            struct v2f
            {
                UNITY_FOG_COORDS(0)
                float4 vertex : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 col = _Color;
                // apply fog
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

            //碰撞三角形信息
            struct IntersectionVertex
            {
                float3 normalOS;
            };

            void FetchIntersectionVertex(uint vertexIndex, out IntersectionVertex outVertex)
            {
                outVertex.normalOS = UnityRayTracingFetchVertexAttribute3(vertexIndex, kVertexAttributeNormal);
            }

            CBUFFER_START (UnityPerMaterial)
            float4 _Color;
            CBUFFER_END

            [shader("closesthit")]

            void ClosestHitShader(inout RayIntersection rayIntersection : SV_RayPayload,
                                  AttributeData attributeData : SV_IntersectionAttributes)
            {
                //得到碰撞三角形的索引值
                uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());
                //填充三角形顶点信息
                IntersectionVertex v0, v1, v2;
                FetchIntersectionVertex(triangleIndices.x, v0);
                FetchIntersectionVertex(triangleIndices.y, v1);
                FetchIntersectionVertex(triangleIndices.z, v2);
                //顶点插值
                float3 barycentricCoordinates = float3(1.0 - attributeData.barycentrics.x - attributeData.barycentrics.y, attributeData.barycentrics.x, attributeData.barycentrics.y);
                float3 normalOS = INTERPOLATE_RAYTRACING_ATTRIBUTE(v0.normalOS, v1.normalOS, v2.normalOS, barycentricCoordinates);
                //object to World
                float3x3 objectToWorld = (float3x3)ObjectToWorld3x4();
                float3 normalWS = normalize(mul(objectToWorld, normalOS));
                //final color
                rayIntersection.color = float4(0.5f * (normalWS + 1.0f), 0);
            }
            ENDHLSL
        }
    }
}