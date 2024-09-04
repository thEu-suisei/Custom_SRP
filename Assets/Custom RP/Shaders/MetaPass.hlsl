#ifndef CUSTOM_META_PASS_INCLUDED
#define CUSTOM_META_PASS_INCLUDED

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"

struct Attributes {
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    float2 lightMapUV : TEXCOORD1;
};

struct Varyings {
    float4 positionCS : SV_POSITION;
    float2 baseUV : VAR_BASE_UV;
};

Varyings MetaPassVertex (Attributes input) {
    Varyings output;
    //由于我们需要将片元光照信息烘培到光照贴图中，因此我们需要知道物体表面片元在光照贴图中的坐标。
    //这里并不是objectSpace，而是lightMapUV
    input.positionOS.xy = input.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
    //我们只需要用顶点属性来作为数值的传输，但是如果不明确z属性OpenGL无法工作
    input.positionOS.z > 0.0 ? FLT_MIN : 0.0;
    //?:为什么要换成CS，不是直接用就行了吗
    output.positionCS = TransformWorldToHClip(input.positionOS);
    output.baseUV = TransformBaseUV(input.baseUV);
    return output;
}

//疑惑：为什么return float4(1,0,0,1)不会得到红色的lightmap
float4 MetaPassFragment (Varyings input) : SV_TARGET {
    float4 base = GetBase(input.baseUV);
    Surface surface;
    ZERO_INITIALIZE(Surface, surface);
    surface.color = base.rgb;
    surface.metallic = GetMetallic(input.baseUV);
    surface.smoothness = GetSmoothness(input.baseUV);
    BRDF brdf = GetBRDF(surface);
    float4 meta = 0.0;
    //如果设置了x，则请求漫反射率
    if(unity_MetaFragmentControl.x)
    {
        meta = float4(brdf.diffuse,1.0);
        //Unity 的元通道还通过添加按粗糙度缩放的一半镜面反射率来稍微增强结果。这背后的想法是，高度镜面但粗糙的材料也会传递一些间接光。
        meta.rgb += brdf.specular * brdf.roughness*0.5;
        //unity_OneOverOutputBoost然后，通过将结果提升为通过方法提供的幂来修改结果PositivePow，然后将其限制为unity_MaxOutputValue。
        meta.rgb = min(PositivePow(meta.rgb,unity_OneOverOutputBoost),unity_MaxOutputValue);
    }
    return meta;
}
 
#endif