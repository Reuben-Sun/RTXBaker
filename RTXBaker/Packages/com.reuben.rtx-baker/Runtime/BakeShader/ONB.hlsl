#ifndef ONB_INCLUDE
#define ONB_INCLUDE

//以 normal为 w向量的单位正交基
struct ONB {
    float3 u;
    float3 v;
    float3 w;   //normal
};

void ONBBuildFromW(inout ONB uvw, float3 n) {
    uvw.w = n;
    float3 a;
    if (abs(uvw.w.x) > 0.0f)
        a = float3(0.0f, 1.0f, 0.0f);
    else
        a = float3(1.0f, 0.0f, 0.0f);
    uvw.v = normalize(cross(uvw.w, a));
    uvw.u = cross(uvw.w, uvw.v);
}

float3 ONBLocal(inout ONB uvw, float3 a) {
    return a.x * uvw.u + a.y * uvw.v + a.z * uvw.w;
}

float ScatteringPDF(float3 inOrigin, float3 inDirection, float inT, float3 hitNormal, float3 scatteredDir)
{
    float cosine = dot(hitNormal, scatteredDir);
    return max(0.0f, cosine / M_PI);
}
#endif
