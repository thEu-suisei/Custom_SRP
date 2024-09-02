﻿#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"//use it to retrieve the light data.

//Lightmap 贴图和采样器
TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);

//LightProbeProxyVolume
TEXTURE3D_FLOAT(unity_ProbeVolumeSH);
SAMPLER(samplerunity_ProbeVolumeSH);

struct GI
{
    //间接光来自所有方向，因此只能用于漫反射照明，不能用于镜面反射。
    //镜面环境反射通常通过反射探针提供，屏幕空间反射是另一种选择。
    float3 diffuse;
};

//LightMap采样，返回采样结果
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

//LightProbe
float3 SampleLightProbe(Surface surfaceWS)
{
    //如果使用Lightmap方法则返回0
    #if defined(LIGHTMAP_ON)
        return 0.0;
    #else
        if (unity_ProbeVolumeParams.x)
        {
            return SampleProbeVolumeSH4(
                TEXTURE3D_ARGS(unity_ProbeVolumeSH,samplerunity_ProbeVolumeSH),
                surfaceWS.position,
                surfaceWS.normal,
                unity_ProbeVolumeWorldToObject,
                unity_ProbeVolumeParams.y,
                unity_ProbeVolumeParams.z,
                unity_ProbeVolumeMin.xyz,
                unity_ProbeVolumeSizeInv.xyz
                );
        }
        else
        {
            float4 coefficients[7];
            coefficients[0] = unity_SHAr;
            coefficients[1] = unity_SHAg;
            coefficients[2] = unity_SHAb;
            coefficients[3] = unity_SHBr;
            coefficients[4] = unity_SHBg;
            coefficients[5] = unity_SHBb;
            coefficients[6] = unity_SHC;
            //SampleSH9
            return max(0.0,SampleSH9(coefficients,surfaceWS.normal));
        }
    
    #endif
}

GI GetGI(float2 lightMapUV,Surface surfaceWS)
{
    GI gi;
    gi.diffuse = SampleLightMap(lightMapUV)+SampleLightProbe(surfaceWS);
    return gi;
}


#endif