#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

TEXTURE2D(_BaseMap);
TEXTURE2D(_MaskMap);
TEXTURE2D(_EmissionMap);
//采样器包含了从纹理中采样时的过滤模式、寻址模式等信息。定义了如何从纹理中获取像素数据
SAMPLER(sampler_BaseMap);

TEXTURE2D(_DetailMap);
SAMPLER(sampler_DetailMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4,_DetailMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
    UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
    UNITY_DEFINE_INSTANCED_PROP(float, _Occlusion)
    UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
    UNITY_DEFINE_INSTANCED_PROP(float, _Fresnel)
    UNITY_DEFINE_INSTANCED_PROP(float, _DetailAlbedo)
    UNITY_DEFINE_INSTANCED_PROP(float, _DetailSmoothness)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

float2 TransformBaseUV(float2 baseUV)
{
    //ST:
    //baseUV是物体上的顶点对纹理映射的值，表示某个顶点在纹理的坐标(0~1,0~1)
    //baseST表示scale & translate，可以用来表示物体的光照纹理在Lightmap上的位置，也可以用缩放来重复显示纹理，也可以用平移量做动态变化的效果。
    float4 baseST = INPUT_PROP(_BaseMap_ST);
    return baseUV * baseST.xy + baseST.zw;
}

float2 TransformDetailUV(float2 detailUV)
{
    float4 detailST = INPUT_PROP(_DetailMap_ST);
    return detailUV * detailST.xy + detailST.zw;
}

//LitPass中不需要知道哪些属性用到Mask，而是将Mask写在各个属性的getter函数中。
float4 GetMask(float2 baseUV)
{
    return SAMPLE_TEXTURE2D(_MaskMap,sampler_BaseMap,baseUV);
}

float4 GetDetail (float2 detailUV)
{
    float4 map = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, detailUV);
    return map * 2.0 - 1.0;
}

float4 GetBase(float2 baseUV, float2 detailUV = 0.0)
{
    float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
    float4 color = INPUT_PROP(_BaseColor);

    float detail = GetDetail(detailUV).r* INPUT_PROP(_DetailAlbedo);
    float mask = GetMask(baseUV).b;
    map.rgb = lerp(sqrt(map.rgb), detail < 0.0 ? 0.0 : 1.0, abs(detail)* mask);
    map.rgb *= map.rgb;
    
    return map * color;
}

float GetCutoff(float2 baseUV)
{
    return INPUT_PROP(_Cutoff);
}

float GetMetallic(float2 baseUV)
{
    float metallic = INPUT_PROP(_Metallic);
    metallic *= GetMask(baseUV).r;
    return metallic;
}

float GetOcclusion (float2 baseUV)
{
    float strength = INPUT_PROP(_Occlusion);
    float occlusion = GetMask(baseUV).g;
    occlusion = lerp(occlusion, 1.0, strength);
    return occlusion;
}

float GetSmoothness(float2 baseUV, float2 detailUV = 0.0)
{
    float smoothness = INPUT_PROP(_Smoothness);
    smoothness *= GetMask(baseUV).a;

    float detail = GetDetail(detailUV).b * INPUT_PROP(_DetailSmoothness);
    float mask = GetMask(baseUV).b;
    smoothness = lerp(smoothness, detail < 0.0 ? 0.0 : 1.0, abs(detail) * mask);
    return smoothness;
}

float3 GetEmission(float2 baseUV)
{
    float4 map = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, baseUV);
    float4 color = INPUT_PROP(_EmissionColor);
    return map.rgb * color.rgb;
}

float GetFresnel (float2 baseUV) {
    return INPUT_PROP(_Fresnel);
}

#endif
