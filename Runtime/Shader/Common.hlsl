#ifndef COMMON_INCLUDE
#define COMMON_INCLUDE

#define CBUFFER_START(name) cbuffer name {
#define CBUFFER_END };

CBUFFER_START(CameraBuffer)
float4x4 _InvCameraViewProj;
float3 _WorldSpaceCameraPos;
float _CameraFarDistance;
CBUFFER_END

struct RayIntersection
{
    float4 color;
};

struct AttributeData
{
    float2 barycentrics;
};

RaytracingAccelerationStructure _AccelerationStructure;

inline void GenerateCameraRay(out float3 origin, out float3 direction)
{
    // center in the middle of the pixel.
    float2 xy = DispatchRaysIndex().xy + 0.5f;
    float2 screenPos = xy / DispatchRaysDimensions().xy * 2.0f - 1.0f;

    // Un project the pixel coordinate into a ray.
    float4 world = mul(_InvCameraViewProj, float4(screenPos, 0, 1));

    world.xyz /= world.w;
    origin = _WorldSpaceCameraPos.xyz;
    direction = normalize(world.xyz - origin);
}

#endif
