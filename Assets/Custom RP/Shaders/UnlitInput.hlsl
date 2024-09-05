#ifndef CUSTOM_UNLIT_INPUT_INCLUDED
#define CUSTOM_UNLIT_INPUT_INCLUDED
//Meta Pass


//在Shader的全局变量区定义纹理的句柄和其采样器，通过名字来匹配
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

//为了使用GPU Instancing，每实例数据要构建成数组,使用UNITY_INSTANCING_BUFFER_START(END)来包裹每实例数据
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    //纹理坐标的偏移和缩放可以是每实例数据
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
    //_BaseColor在数组中的定义格式
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    //透明度测试阈值
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)


float2 TransformBaseUV(float2 baseUV)
{
    float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
    return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase(float2 baseUV)
{
    //获取采样纹理颜色
    float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
    //通过UNITY_ACCESS_INSTANCED_PROP获取每实例数据
    float4 color = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    return map * color;
}

float GetCutoff(float2 baseUV)
{
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff);
}

float GetMetallic(float2 baseUV)
{
    return 0.0;
}

float GetSmoothness(float2 baseUV)
{
    return 0.0;
}

float3 GetEmission(float2 baseUV)
{
    return GetBase(baseUV).rgb;
}

#endif
