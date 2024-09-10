#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"//use it to retrieve the light data.
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"

//天空盒采样
TEXTURECUBE(unity_SpecCube0);
SAMPLER(samplerunity_SpecCube0);

//Lightmap 贴图和采样器
TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);

//ShadowMask 贴图和采样器
TEXTURE2D(unity_ShadowMask);
SAMPLER(samplerunity_ShadowMask);

//LightProbeProxyVolume
TEXTURE3D_FLOAT(unity_ProbeVolumeSH);
SAMPLER(samplerunity_ProbeVolumeSH);

struct GI
{
    //间接光来自所有方向，因此只能用于漫反射照明，不能用于镜面反射。
    float3 diffuse;
    //镜面环境反射通常通过反射探针提供，屏幕空间反射是另一种选择。
    float3 specular;
    ShadowMask shadowMask;
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
            TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
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
        return max(0.0, SampleSH9(coefficients, surfaceWS.normal));
    }

    #endif
}

float4 SampleBakedShadows(float2 lightMapUV, Surface surfaceWS)
{
    #if defined(LIGHTMAP_ON)
        return SAMPLE_TEXTURE2D(unity_ShadowMask, samplerunity_ShadowMask, lightMapUV);
    #else
        if (unity_ProbeVolumeParams.x)
        {
            //ShadowMask LPPV
            return SampleProbeOcclusion(
                TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
                surfaceWS.position, unity_ProbeVolumeWorldToObject,
                unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
                unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
            );
        }
        else
        {
            return unity_ProbesOcclusion;
        }
    #endif
}

//采样天空盒
float3 SampleEnvironment(Surface surfaceWS,BRDF brdf)
{
    //立方体贴图的采样使用的是方向而非坐标，这里使用相机方向与表面反射的方向
    float3 uvw = reflect(-surfaceWS.viewDirection,surfaceWS.normal);
    //使用库函数计算mip级别
    float mip = PerceptualRoughnessToMipmapLevel(brdf.perceptualRoughness);
    //参数：贴图、采样器状态、UVW 坐标 、 mip 级别
    float4 environment = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0,samplerunity_SpecCube0,uvw,mip);
    return DecodeHDREnvironment(environment, unity_SpecCube0_HDR);;
}

GI GetGI(float2 lightMapUV, Surface surfaceWS,BRDF brdf)
{
    GI gi;
    gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(surfaceWS);
    gi.specular = SampleEnvironment(surfaceWS,brdf);
    gi.shadowMask.always = false;
    gi.shadowMask.distance = false;
    gi.shadowMask.shadows = 1.0;

    #if defined(_SHADOW_MASK_ALWAYS)
        gi.shadowMask.always = true;
        gi.shadowMask.shadows = SampleBakedShadows(lightMapUV,surfaceWS);
    #elif defined(_SHADOW_MASK_DISTANCE)
        gi.shadowMask.distance = true;//这会使distance成为编译时常量，因此它的使用不会导致动态分支。
        gi.shadowMask.shadows = SampleBakedShadows(lightMapUV,surfaceWS);
    #endif
    return gi;
}


#endif
