#ifndef RTX_MATERIAL_INCLUDE
#define RTX_MATERIAL_INCLUDE

struct RTXMaterialData
{
    float3 normalWS;
    float3 scatteredDir;    //散射方向
    float3 kd;
    float3 ks;
    float diffVal;
    float specVal;
};

#endif
