#ifndef RTX_LIGHT_INCLUDE
#define RTX_LIGHT_INCLUDE

CBUFFER_START(_RTXLight)
    float3 _DirectionalLightColor;
    float3 _DirectionalLightDirection;
CBUFFER_END

struct RTXLight
{
    float3 color;
    float3 direction;
};

// Distant Light
RTXLight GetDirectionalLight()
{
    RTXLight light;
    light.color = _DirectionalLightColor;
    light.direction = _DirectionalLightDirection;
    return light;
}
#endif