#ifndef COMMON_INCLUDE
#define COMMON_INCLUDE

#include "UnityRayTracingMeshUtils.cginc"

#define CBUFFER_START(name) cbuffer name {
#define CBUFFER_END };

// 顶点插值
#define INTERPOLATE_RAYTRACING_ATTRIBUTE(A0, A1, A2, BARYCENTRIC_COORDINATES) (A0 * BARYCENTRIC_COORDINATES.x + A1 * BARYCENTRIC_COORDINATES.y + A2 * BARYCENTRIC_COORDINATES.z)

// 采样函数
#define SAMPLE_TEXTURE2D_LOD(textureName, samplerName, coord2, lod) textureName.SampleLevel(samplerName, coord2, lod)
#define TEXTURE2D(textureName) Texture2D textureName
#define SAMPLER(samplerName) SamplerState samplerName

CBUFFER_START(CameraBuffer)
float4x4 _InvCameraViewProj;
float3 _WorldSpaceCameraPos;
float _CameraFarDistance;
CBUFFER_END

struct RayIntersection
{
    float4 color;
    uint4 PRNGStates;   //随机数产生器
    int remainingDepth;
};

struct AttributeData
{
    float2 barycentrics;
};


RaytracingAccelerationStructure _AccelerationStructure;

inline void GenerateCameraRay(out float3 origin, out float3 direction)
{
    //center
    float2 xy = DispatchRaysIndex().xy + 0.5f;
    float2 screenPos = xy / DispatchRaysDimensions().xy * 2.0f - 1.0f;
    
    float4 world = mul(_InvCameraViewProj, float4(screenPos, 0, 1));

    world.xyz /= world.w;
    origin = _WorldSpaceCameraPos.xyz;
    direction = normalize(world.xyz - origin);
}

inline void GenerateCameraRayWithOffset(out float3 origin, out float3 direction, float2 offset)
{
    float2 xy = DispatchRaysIndex().xy + offset;
    float2 screenPos = xy / DispatchRaysDimensions().xy * 2.0f - 1.0f;
    
    float4 world = mul(_InvCameraViewProj, float4(screenPos, 0, 1));

    world.xyz /= world.w;
    origin = _WorldSpaceCameraPos.xyz;
    direction = normalize(world.xyz - origin);
}
#endif
