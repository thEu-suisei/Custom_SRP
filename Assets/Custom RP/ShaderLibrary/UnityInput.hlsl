//UnityInput.hsls存储Shader中的一些常用的输入数据
#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

//uniform value
CBUFFER_START(UnityPerDraw)

    float4x4 unity_ObjectToWorld;
    float4x4 unity_WorldToObject;

    //在定义（UnityPerDraw）CBuffer时，因为Unity对一组相关数据都归到一个Feature中，即使我们没用到unity_LODFade，
    //我们也需要放到这个CBuffer中来构造一个完整的Feature,如果不加这个unity_LODFade，不能支持SRP Batcher
    float4 unity_LODFade;
    real4 unity_WorldTransformParams;

    //Lightmaps
    //光照贴图偏移
    float4 unity_LightmapST;
    //已被弃用，但如果不添加否则 SRP 批处理程序兼容性可能会中断
    float4 unity_DynamicLightmapST;
    //unity会将shadowmask烘焙到light probe中
    float4 unity_ProbesOcclusion;
    //用于解码反射探针，因为其可能是HDR或者LDR
    float4 unity_SpecCube0_HDR;

    //LightProbe
    //线性插值后得到的当前点的radiance SH系数，
    //有rgb三个通道，SHA*存储了0/1阶的4个系数，SHB*存储了2阶5个系数中的4个，SHC存储了r/g/b通道2阶系数的最后一个，最后一位没有用上。
    float4 unity_SHAr;
    float4 unity_SHAg;
    float4 unity_SHAb;
    float4 unity_SHBr;
    float4 unity_SHBg;
    float4 unity_SHBb;
    float4 unity_SHC;

    //LightProbeVolume
    float4 unity_ProbeVolumeParams; 
    float4x4 unity_ProbeVolumeWorldToObject;
    float4 unity_ProbeVolumeSizeInv;
    float4 unity_ProbeVolumeMin;

    //MetaPass
    //元通道可用于生成不同的数据。请求的内容通过bool4 unity_MetaFragmentControl标志向量进行传达。
    //如果下面分量为true则MetaFragment控制输出的信息有：
    //x：控制反照率（Albedo）信息的输出；y：控制自发光（Emission）信息的输出；z：通常与透明度相关；w：制是否启用双面渲染。
    bool4 unity_MetaFragmentControl;
    //unity_OneOverOutputBoost 通常是 HDR 输出增益（boost）的倒数，用来在着色器中反向缩放输出颜色值，确保最终输出符合所期望的亮度范围。
    float unity_OneOverOutputBoost;
    //unity_MaxOutputValue 用来限制或控制在 HDR 渲染模式下的最大输出颜色值。
    float unity_MaxOutputValue;

CBUFFER_END

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 unity_MatrixInvV;
float4x4 unity_prev_MatrixM;
float4x4 unity_prev_MatrixIM;
float4x4 glstate_matrix_projection;

float3 _WorldSpaceCameraPos;

#endif