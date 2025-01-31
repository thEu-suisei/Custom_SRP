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

#if defined(_OTHER_PCF3)
    #define OTHER_FILTER_SAMPLES 4
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_OTHER_PCF5)
    #define OTHER_FILTER_SAMPLES 9
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_OTHER_PCF7)
    #define OTHER_FILTER_SAMPLES 16
    #define OTHER_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_SHADOWED_OTHER_LIGHT_COUNT 16
#define MAX_CASCADE_COUNT 4

//接收CPU端传来的ShadowAtlas
//使用TEXTURE2D_SHADOW来明确我们接收的是阴影贴图
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
TEXTURE2D_SHADOW(_OtherShadowAtlas);
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
    float4x4 _OtherShadowMatrices[MAX_SHADOWED_OTHER_LIGHT_COUNT];
    float4 _OtherShadowTiles[MAX_SHADOWED_OTHER_LIGHT_COUNT]; //xyz：确定采样边界,w:法线偏移
    float4 _ShadowAtlasSize;
    float4 _ShadowDistanceFade;
CBUFFER_END

//每个方向光源的的阴影信息（包括不支持阴影的光源，不支持，其阴影强度就是0）
struct DirectionalShadowData
{
    float strength;
    int tileIndex;
    float normalBias;
    int shadowMaskChannel;
};

//ShadowMask Data
//shader需要知道是否正在使用shadowmask，如果使用则需要知道烘焙的阴影是是什么
struct ShadowMask
{
    //ShadowMask Mode = shadowmask
    bool always;
    //ShadowMask Mode = distance shadowmask
    bool distance;
    float4 shadows;
};

struct ShadowData
{
    int cascadeIndex;
    //用于不同级联插值，表示渐变强度
    float cascadeBlend;
    //默认设置为1，如果我们结束了最后一个级联，设置为零。
    float strength;
    ShadowMask shadowMask;
};

struct OtherShadowData
{
    float strength;
    int tileIndex;
    bool isPoint;
    int shadowMaskChannel;
    float3 lightPositionWS;
    float3 lightDirectionWS;
    float3 spotDirectionWS;
};

static const float3 pointShadowPlanes[6] =
{
    float3(-1.0, 0.0, 0.0),
    float3(1.0, 0.0, 0.0),
    float3(0.0, -1.0, 0.0),
    float3(0.0, 1.0, 0.0),
    float3(0.0, 0.0, -1.0),
    float3(0.0, 0.0, 1.0)
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
    data.shadowMask.always = false;
    data.shadowMask.distance = false;
    data.shadowMask.shadows = 1.0;
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
            if (i == _CascadeCount - 1)
            {
                data.strength *= fade;
            }
            else
            {
                data.cascadeBlend = fade;
            }
            break;
        }
    }

    if (i == _CascadeCount && _CascadeCount > 0)
    {
        data.strength = 0.0;
    }
    #if defined(_CASCADE_BLEND_DITHER)
    //在混合区间，与随机噪声比较，概率性会变成下一级级联的阴影
    else if(data.cascadeBlend<surfaceWS.dither)
    {
        i+=1;
    }
    #endif
    #if !defined(_CASCADE_BLEND_SOFT)
    data.cascadeBlend = 1.0;
    #endif

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

float SampleOtherShadowAtlas(float3 positionSTS, float3 bounds)
{
    positionSTS.xy = clamp(positionSTS.xy, bounds.xy, bounds.xy + bounds.z);
    return SAMPLE_TEXTURE2D_SHADOW(_OtherShadowAtlas, SHADOW_SAMPLER, positionSTS);
}


float FilterOtherShadow(float3 positionSTS, float3 bounds)
{
    #if defined(OTHER_FILTER_SETUP)
        real weights[OTHER_FILTER_SAMPLES];
        real2 positions[OTHER_FILTER_SAMPLES];
        float4 size = _ShadowAtlasSize.wwzz;
        OTHER_FILTER_SETUP(size, positionSTS.xy, weights, positions);
        float shadow = 0;
        for (int i = 0; i < OTHER_FILTER_SAMPLES; i++)
        {
            shadow += weights[i] * SampleOtherShadowAtlas(float3(positions[i].xy, positionSTS.z),bounds);
        }
        return shadow;
    #else
    return SampleOtherShadowAtlas(positionSTS, bounds);
    #endif
}

//GetShadowAttenuation()→
//计算阴影衰减值，返回值[0,1]，0代表阴影衰减最大（片元完全在阴影中），1代表阴影衰减最少，片元完全被光照射。而[0,1]的中间值代表片元有一部分在阴影中
float GetCascadedShadow(
    DirectionalShadowData directional,
    ShadowData global,
    Surface surfaceWS)
{
    float3 normalBias = surfaceWS.interpolatedNormal * (directional.normalBias * _CascadeData[global.cascadeIndex].y);
    //根据对应Tile阴影变换矩阵和片元的世界坐标计算Tile上的像素坐标STS
    //法线偏移法：采样的位置从surfaceWS.position偏移至surfaceWS.position + normalBias
    float3 positionSTS = mul(
        _DirectionalShadowMatrices[directional.tileIndex],
        float4(surfaceWS.position + normalBias, 1.0)).xyz;
    //采样Tile得到阴影强度值
    // float shadow = SampleDirectionalShadowAtlas(positionSTS);//硬阴影
    float shadow = FilterDirectionalShadow(positionSTS);


    if (global.cascadeBlend < 1.0)
    {
        normalBias = surfaceWS.interpolatedNormal *
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
    return shadow;
}

//GetShadowAttenuation()→
float GetBakedShadow(ShadowMask mask, int channel)
{
    float shadow = 1.0;
    if (mask.always || mask.distance)
    {
        if (channel >= 0)
        {
            shadow = mask.shadows[channel];
        }
    }
    return shadow;
}

//用于超出最大阴影距离时渲染烘焙阴影
float GetBakedShadow(ShadowMask mask, int channel, float strength)
{
    if (mask.always || mask.distance)
    {
        return lerp(1.0, GetBakedShadow(mask, channel), strength);
    }
    return 1.0;
}

//GetShadowAttenuation()→
//将烘焙阴影和实时阴影混合

float MixBakedAndRealtimeShadows(ShadowData global, float shadow, int shadowMaskChannel, float strength)
{
    float baked = GetBakedShadow(global.shadowMask, shadowMaskChannel);
    if (global.shadowMask.always)
    {
        shadow = lerp(1.0, shadow, global.strength);
        shadow = min(baked, shadow);
        return lerp(1.0, shadow, strength);
    }
    if (global.shadowMask.distance)
    {
        shadow = lerp(baked, shadow, global.strength);
        return lerp(1.0, shadow, strength);
    }
    return lerp(1.0, shadow, strength * global.strength);
}

//计算阴影衰减值
//返回值[0,1]，0代表阴影衰减最大（片元完全在阴影中），1代表阴影衰减最少，片元完全被光照射。而[0,1]的中间值代表片元有一部分在阴影中
float GetDirectionalShadowAttenuation(
    DirectionalShadowData directional,
    ShadowData global,
    Surface surfaceWS)
{
    #if !defined(_RECEIVE_SHADOWS)
    return 1.0;
    #endif
    float shadow;
    //如果阴影强度为0，则仅使用烘焙阴影
    if (directional.strength * global.strength <= 0.0)
    {
        shadow = GetBakedShadow(global.shadowMask, directional.shadowMaskChannel, abs(directional.strength));
    }
    else
    {
        shadow = GetCascadedShadow(directional, global, surfaceWS);
        shadow = MixBakedAndRealtimeShadows(global, shadow, directional.shadowMaskChannel, directional.strength);
    }

    //考虑光源的阴影强度，strength为0，依然没有阴影
    return shadow;
}

//实时阴影，采样Shadowmap
float GetOtherShadow(OtherShadowData other, ShadowData global, Surface surfaceWS)
{
    float tileIndex = other.tileIndex;
    float3 lightPlane = other.spotDirectionWS;
    if (other.isPoint)
    {
        //CubeMapFaceID会根据入射光方向的 +X、−X、+Y、−Y、+Z、−Z，返回一个0~5的faceID，作为采样图块索引偏移
        float faceOffset = CubeMapFaceID(-other.lightDirectionWS);
        tileIndex += faceOffset;
        lightPlane = pointShadowPlanes[faceOffset];
    }
    float4 tileData = _OtherShadowTiles[tileIndex];
    float3 surfaceToLight = other.lightPositionWS - surfaceWS.position;
    float distanceToLightPlane = dot(surfaceToLight, lightPlane);
    float3 normalBias = surfaceWS.interpolatedNormal * (distanceToLightPlane * tileData.w);
    float4 positionSTS = mul(
        _OtherShadowMatrices[tileIndex],
        float4(surfaceWS.position + normalBias, 1.0)
    );
    return FilterOtherShadow(positionSTS.xyz / positionSTS.w, tileData.xyz);
}

float GetOtherShadowAttenuation(
    OtherShadowData other, ShadowData global, Surface surfaceWS
)
{
    #if !defined(_RECEIVE_SHADOWS)
    return 1.0;
    #endif

    float shadow;
    if (other.strength * global.strength <= 0.0)
    {
        shadow = GetBakedShadow(
            global.shadowMask, other.shadowMaskChannel, abs(other.strength)
        );
    }
    else
    {
        shadow = GetOtherShadow(other, global, surfaceWS);
        shadow = MixBakedAndRealtimeShadows(
            global, shadow, other.shadowMaskChannel, other.strength
        );
    }
    return shadow;
}


#endif
