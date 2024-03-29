//用来采样阴影贴图
#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED


//使用Core RP的软阴影采样函数
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary//Shadow/ShadowSamplingTent.hlsl"//定义了不同filterMode的filter大小和采样设置


#if defined(_DIRECTIONAL_PCF3)
    #define DIRECTIONAL_FILTER_SAMPLES 4
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
    #define DIRECTIONAL_FILTER_SAMPLES 9
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
    #define DIRECTIONAL_FILTER_SAMPLES 16
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

//宏定义最大支持阴影的方向光源数，要与CPU端同步，为4
#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
//最大阴影级联数，要与CPU同步
#define MAX_CASCADE_COUNT 4

//接收CPU端传来的ShadowAtlas
//使用TEXTURE2D_SHADOW来明确我们接收的是阴影贴图
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
//阴影贴图只有一种采样方式，因此我们显式定义一个阴影采样器状态（不需要依赖任何纹理），其名字为sampler_linear_clamp_compare(使用宏定义它为SHADOW_SAMPLER)
//由此，对于任何阴影贴图，我们都可以使用SHADOW_SAMPLER这个采样器状态
//sampler_linear_clamp_compare这个取名十分有讲究，Unity会将这个名字翻译成使用Linear过滤、Clamp包裹的用于深度比较的采样器
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

//接收CPU端传来的每个Shadow Tile的阴影变换矩阵
CBUFFER_START(_CustonShadows)
    int _CascadeCount;
    float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
    float4 _CascadeData[MAX_CASCADE_COUNT];
    float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
    float4 _ShadowAtlasSize;
    float4 _ShadowDistanceFade;
CBUFFER_END

//每个方向光源的的阴影信息（包括不支持阴影的光源，不支持，其阴影强度就是0）
struct DirectionalShadowData
{
    float strength;
    int tileIndex;
    float normalBias;
};

struct ShadowData
{
    int cascadeIndex;
    //用于不同级联插值，表示渐变强度
    float cascadeBlend;
    //默认设置为1，如果我们结束了最后一个级联，设置为零。
    float strength;
};

//阴影自然消失强度函数
float FadedShadowStrength(float distance, float scale, float fade)
{
    return saturate((1.0 - distance * scale) * fade);
}

//计算给定片元将要使用的级联信息
ShadowData GetShadowData(Surface surfaceWS)
{
    ShadowData data;
    data.cascadeBlend = 1.0;
    //如果表面超出最大阴影深度，那么开始自然消失；
    data.strength = FadedShadowStrength(
        surfaceWS.depth,
        _ShadowDistanceFade.x,
        _ShadowDistanceFade.y
    );
    int i;
    for (i = 0; i < _CascadeCount; i++)
    {
        float4 sphere = _CascadeCullingSpheres[i];
        float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
        if (distanceSqr < sphere.w)
        {
            float fade = FadedShadowStrength(
                distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z
            );
            if (i == _CascadeCount - 1) {
                data.strength *= fade;
            }
            else {
                data.cascadeBlend = fade;
            }
            break;
        }
    }
    if (i == _CascadeCount)
    {
        data.strength = 0.0;
    }
    data.cascadeIndex = i;
    return data;
}

//采样ShadowAtlas，传入positionSTS（STS是Shadow Tile Space，即阴影贴图对应Tile像素空间下的片元坐标）
float SampleDirectionalShadowAtlas(float3 positionSTS)
{
    //使用特定宏来采样阴影贴图
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

float FilterDirectionalShadow(float3 positionSTS)
{
    #if defined(DIRECTIONAL_FILTER_SETUP)
        //out:每个采样结果的权重
        float weights[DIRECTIONAL_FILTER_SAMPLES];
        //out:每个采样的坐标
        float2 positions[DIRECTIONAL_FILTER_SAMPLES];
        //in：texelSizeX,texelSizeY,AtlasSizeX,AtlasSizeY
        float4 size = _ShadowAtlasSize.yyxx;
        //获取所有待采样点的坐标和权重
        DIRECTIONAL_FILTER_SETUP(size,positionSTS.xy,weights,positions);
        float shadow = 0;
        //采样并加权
        for(int i=0;i<DIRECTIONAL_FILTER_SAMPLES;i++)
        {
            shadow += weights[i] * SampleDirectionalShadowAtlas(float3(positions[i].xy,positionSTS.z));
        }
        return shadow;
    #else
    return SampleDirectionalShadowAtlas(positionSTS);
    #endif
}

//计算阴影衰减值，返回值[0,1]，0代表阴影衰减最大（片元完全在阴影中），1代表阴影衰减最少，片元完全被光照射。而[0,1]的中间值代表片元有一部分在阴影中
float GetDirectionalShadowAttenuation(
    DirectionalShadowData directional,
    ShadowData global,
    Surface surfaceWS)
{
    //忽略不开启阴影和阴影强度为0的光源
    if (directional.strength <= 0.0)
    {
        return 1.0;
    }
    float3 normalBias = surfaceWS.normal * (directional.normalBias * _CascadeData[global.cascadeIndex].y);
    //根据对应Tile阴影变换矩阵和片元的世界坐标计算Tile上的像素坐标STS
    //法线偏移法：采样的位置从surfaceWS.position偏移至surfaceWS.position + normalBias
    float3 positionSTS = mul(
        _DirectionalShadowMatrices[directional.tileIndex],
        float4(surfaceWS.position + normalBias, 1.0)).xyz;
    //采样Tile得到阴影强度值
    // float shadow = SampleDirectionalShadowAtlas(positionSTS);//硬阴影
    float shadow = FilterDirectionalShadow(positionSTS);

    
    if (global.cascadeBlend < 1.0) {
        normalBias = surfaceWS.normal *
            (directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
        positionSTS = mul(
            _DirectionalShadowMatrices[directional.tileIndex + 1],
            float4(surfaceWS.position + normalBias, 1.0)
        ).xyz;
        shadow = lerp(
            FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend
        );
    }
    
    //考虑光源的阴影强度，strength为0，依然没有阴影
    return lerp(1.0, shadow, directional.strength);
}


#endif
