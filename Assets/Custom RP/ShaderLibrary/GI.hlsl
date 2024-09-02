#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"//use it to retrieve the light data.

//LightMap采样
TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);

struct GI
{
    //间接光来自所有方向，因此只能用于漫反射照明，不能用于镜面反射。
    //镜面环境反射通常通过反射探针提供，屏幕空间反射是另一种选择。
    float3 diffuse;
};

float3 SampleLightMap(float2 lightMapUV)
{
    //EntityLighting.hlsl中的采样函数
    //传递：纹理和采样器状态/UV坐标/应用缩放和平移/bool光照是否经过压缩/float4包含解码指令
    #if defined(LIGHTMAP_ON)
    return SampleSingleLightmap(
            TEXTURE2D_ARGS(unity_Lightmap,
            samplerunity_Lightmap),
            lightMapUV,
            float4(1.0, 1.0, 0.0, 0.0),
            #if defined(UNITY_LIGHTMAP_FULL_HDR)
                false,
            #else
                true,
            #endif
            float4(LIGHTMAP_HDR_MULTIPLIER,LIGHTMAP_HDR_EXPONENT,0.0,0.0)
            );
    #else
        return 0.0;
    #endif
}

GI GetGI(float2 lightMapUV)
{
    GI gi;
    gi.diffuse = SampleLightMap(lightMapUV);
    return gi;
}


#endif