//复制纹理的Pass
#ifndef CUSTOM_POST_FX_PASSES_INCLUDED
#define CUSTOM_POST_FX_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

TEXTURE2D(_PostFXSource);
TEXTURE2D(_PostFXSource2);
TEXTURE2D(_ColorGradingLUT);
SAMPLER(sampler_linear_clamp);

float4 _ProjectionParams;
float4 _PostFXSource_TexelSize;
float4 _BloomThreshold;
float _BloomIntensity;
bool _BloomBicubicUpsampling;
float4 _ColorAdjustments;
float4 _ColorFilter;
float4 _WhiteBalance;
float4 _SplitToningShadows, _SplitToningHighlights;
float4 _ChannelMixerRed, _ChannelMixerGreen, _ChannelMixerBlue;
float4 _SMHShadows, _SMHMidtones, _SMHHighlights, _SMHRange;
float4 _ColorGradingLUTParameters;
bool _ColorGradingLUTInLogC;

struct Varyings
{
    float4 positionCS:SV_POSITION;
    float2 screenUV:VAR_SCREEN_UV;
};

float4 GetSourceTexelSize()
{
    return _PostFXSource_TexelSize;
}

float4 GetSource(float2 screenUV)
{
    return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_linear_clamp, screenUV, 0);
}

float4 GetSource2(float2 screenUV)
{
    return SAMPLE_TEXTURE2D_LOD(_PostFXSource2, sampler_linear_clamp, screenUV, 0);
}

float4 GetSourceBicubic(float2 screenUV)
{
    return SampleTexture2DBicubic(
        TEXTURE2D_ARGS(_PostFXSource, sampler_linear_clamp), screenUV,
        _PostFXSource_TexelSize.zwxy, 1.0, 0.0
    );
}

//滤波阈值，根据RGB最大值判断亮度，将明亮的区域Bloom，暗的区域则不会
float3 ApplyBloomThreshold(float3 color)
{
    float brightness = Max3(color.r, color.g, color.b);
    float soft = brightness + _BloomThreshold.y;
    soft = clamp(soft, 0.0, _BloomThreshold.z);
    soft = soft * soft * _BloomThreshold.w;
    float contribution = max(soft, brightness - _BloomThreshold.x);
    contribution /= max(brightness, 0.00001);
    return color * contribution;
}

//Luminance()变体，根据是否处于ACES空间计算
float Luminance(float3 color, bool useACES)
{
    return useACES ? AcesLuminance(color) : Luminance(color);
}

//————————————————————Color Grading————————————————————
//颜色调整: 后期曝光
//后期曝光应用在所以其他后期之后，所有其他颜色分级之前
float3 ColorGradePostExposure(float3 color)
{
    return color * _ColorAdjustments.x;
}

//颜色调整：白平衡
float3 ColorGradeWhiteBalance(float3 color)
{
    color = LinearToLMS(color);
    color *= _WhiteBalance.rgb;
    return LMSToLinear(color);
}

//颜色调整：对比度
//颜色减去中间灰度值(ACEScc)后乘上对比度，然后再加回中间灰度值。
//但是这样的处理再Log(c)完成而非在线性空间完成获得更好的效果
float3 ColorGradingContrast(float3 color, bool useACES)
{
    color = useACES ? ACES_to_ACEScc(unity_to_ACES(color)) : LinearToLogC(color);
    color = (color - ACEScc_MIDGRAY) * _ColorAdjustments.y + ACEScc_MIDGRAY;
    return useACES ? ACES_to_ACEScg(ACEScc_to_ACES(color)) : LogCToLinear(color);
}

//颜色调整：颜色滤镜
float3 ColorGradeColorFilter(float3 color)
{
    return color * _ColorFilter.rgb;
}

//颜色调整：分离色调
//On the shader side we'll perform split-toning in approximate gamma space, raising the color to the inverse of 2.2
//beforehand and to 2.2 afterwards. This is done to match the split-toning of Adobe products. The adjustment is made
//after the color filter, after negative values have been eliminated.
float3 ColorGradeSplitToning(float3 color, bool useACES)
{
    color = PositivePow(color, 1.0 / 2.2);

    //我们通过在颜色和阴影色调之间进行柔光混合来应用色调，然后是高光色调。我们可以SoftLight为此使用两次该功能。
    //在混合之前，我们通过在中性 0.5 和它们本身之间进行插值来将色调限制在各自的区域。对于高光，我们根据饱和亮度加上平衡（再次饱和）来做到这一点。对于阴影，我们使用相反的方法。
    float t = saturate(Luminance(saturate(color), useACES) + _SplitToningShadows.w);
    float3 shadows = lerp(0.5, _SplitToningShadows.rgb, 1.0 - t);
    float3 highlights = lerp(0.5, _SplitToningHighlights.rgb, t);
    color = SoftLight(color, shadows);
    color = SoftLight(color, highlights);

    return PositivePow(color, 2.2);
}

//颜色调整：Channel Mixer
float3 ColorGradingChannelMixer(float3 color)
{
    return mul(float3x3(_ChannelMixerRed.rgb, _ChannelMixerGreen.rgb, _ChannelMixerBlue.rgb), color);
}

//颜色调整：Shadows Midtones Highlights
float3 ColorGradingShadowsMidtonesHighlights(float3 color, bool useACES)
{
    float luminance = Luminance(color, useACES);
    float shadowsWeight = 1.0 - smoothstep(_SMHRange.x, _SMHRange.y, luminance);
    float highlightsWeight = smoothstep(_SMHRange.z, _SMHRange.w, luminance);
    float midtonesWeight = 1.0 - shadowsWeight - highlightsWeight;
    return
        color * _SMHShadows.rgb * shadowsWeight +
        color * _SMHMidtones.rgb * midtonesWeight +
        color * _SMHHighlights.rgb * highlightsWeight;
}

//颜色调整：色相偏移
float3 ColorGradingHueShift(float3 color)
{
    color = RgbToHsv(color);
    float hue = color.x + _ColorAdjustments.z;
    color.x = RotateHue(hue, 0.0, 1.0);
    return HsvToRgb(color);
}

//颜色调整：饱和度
//色彩的纯度，和对比度计算方法类似，只是不用Log(c)，中间灰度换成亮度
float3 ColorGradingSaturation(float3 color, bool useACES)
{
    float luminance = Luminance(color, useACES);
    return (color - luminance) * _ColorAdjustments.w + luminance;
}

//颜色调整
float3 ColorGrade(float3 color, bool useACES = false)
{
    //由于精度限制，该映射会在值非常大时出错，所以提前限制。但在LUT的log中已经被限制到59以内，所以注释掉
    // color = min(color, 60.0);
    color = ColorGradePostExposure(color);
    color = ColorGradeWhiteBalance(color);
    color = ColorGradingContrast(color, useACES);
    color = ColorGradeColorFilter(color);
    color = max(color, 0.0); //对比度会让有的颜色变负
    color = ColorGradeSplitToning(color, useACES);
    color = ColorGradingChannelMixer(color);
    color = max(color, 0.0); //ChannelMixer会让有的颜色变负
    color = ColorGradingShadowsMidtonesHighlights(color, useACES);
    color = ColorGradingHueShift(color);
    color = ColorGradingSaturation(color, useACES);
    return max(useACES ? ACEScg_to_ACES(color) : color, 0.0); //饱和度会让有的颜色变负
}

//————————————————————LUT————————————————————
float3 ApplyColorGradingLUT(float3 color)
{
    //ApplyLut2D函数负责将2D LUT纹理传入，解释为3D纹理后输出颜色
    return ApplyLut2D(
        TEXTURE2D_ARGS(_ColorGradingLUT, sampler_linear_clamp),
        saturate(_ColorGradingLUTInLogC ? LinearToLogC(color) : color),
        _ColorGradingLUTParameters.xyz
        );
}

float3 GetColorGradedLUT(float2 uv, bool useACES = false)
{
    //GetLutStripValue()通过传入uv和LUT纹理宽高相关的参数，可以返回基于这个2d纹理尺寸存储的3d纹理数据，将第三个维度拆分，比如32^3，则会有32片32x32的区域
    float3 color = GetLutStripValue(uv, _ColorGradingLUTParameters);
    return ColorGrade(_ColorGradingLUTInLogC ? LogCToLinear(color) : color, useACES);
}

//————————————————————着色器————————————————————
//顶点着色器
Varyings DefaultPassVertex(uint vertexID: SV_VertexID)
{
    Varyings output;
    output.positionCS = float4(
        vertexID <= 1 ? -1.0 : 3.0,
        vertexID == 1 ? 3.0 : -1.0,
        0.0, 1.0
    );
    output.screenUV = float2(
        vertexID <= 1 ? 0.0 : 2.0,
        vertexID == 1 ? 2.0 : 0.0
    );
    if (_ProjectionParams.x < 0.0)
    {
        output.screenUV.y = 1.0 - output.screenUV.y;
    }
    return output;
}

float4 CopyPassFragment(Varyings input):SV_TARGET
{
    return GetSource(input.screenUV);
}

//预滤波片段着色器，即第一次的半分辨率滤波
float4 BloomPrefilterPassFragment(Varyings input) : SV_TARGET
{
    float3 color = ApplyBloomThreshold(GetSource(input.screenUV).rgb);
    return float4(color, 1.0);
}

//预滤波片段着色器，6×6 box filter解决萤火虫光晕
float4 BloomPrefilterFirefliesPassFragment(Varyings input):SV_TARGET
{
    float3 color = 0.0;
    float weightSum = 0.0;
    float2 offsets[] = {
        float2(0.0, 0.0),
        float2(-1.0, -1.0), float2(-1.0, 1.0), float2(1.0, -1.0), float2(1.0, 1.0)
    };
    for (int i = 0; i < 5; i++)
    {
        float3 c = GetSource(input.screenUV + offsets[i] * GetSourceTexelSize().xy * 2.0).rgb;
        c = ApplyBloomThreshold(c);
        //Luminace()将rgb转换为sRGB空间的亮度
        float w = 1.0 / (Luminance(c) + 1.0);
        color += c * w;
        weightSum += w;
    }
    color /= weightSum;
    return float4(color, 1.0);
}

//加法模糊(双立方上采样)
float4 BloomAddPassFragment(Varyings input) : SV_TARGET
{
    float3 lowRes;
    if (_BloomBicubicUpsampling)
    {
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(input.screenUV).rgb;
    }
    float3 highRes = GetSource2(input.screenUV).rgb;
    return float4(lowRes * _BloomIntensity + highRes, 1.0);
}

//Scatter Bloom
float4 BloomScatterPassFragment(Varyings input) : SV_TARGET
{
    float3 lowRes;
    if (_BloomBicubicUpsampling)
    {
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(input.screenUV).rgb;
    }
    float3 highRes = GetSource2(input.screenUV).rgb;
    return float4(lerp(highRes, lowRes, _BloomIntensity), 1.0);
}

float4 BloomScatterFinalPassFragment(Varyings input) : SV_TARGET
{
    float3 lowRes;
    if (_BloomBicubicUpsampling)
    {
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(input.screenUV).rgb;
    }
    float3 highRes = GetSource2(input.screenUV).rgb;
    lowRes += highRes - ApplyBloomThreshold(highRes);
    return float4(lerp(highRes, lowRes, _BloomIntensity), 1.0);
}

//水平高斯滤波
float4 BloomHorizontalPassFragment(Varyings input):SV_TARGET
{
    float3 color = 0.0;
    float offsets[] = {
        -4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
    };
    //权重来自帕斯卡三角形
    float weights[] = {
        0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703, 0.19459459, 0.12162162, 0.05405405, 0.01621622
    };
    for (int i = 0; i < 9; i++)
    {
        float offset = offsets[i] * 2.0 * GetSourceTexelSize().x;
        color += GetSource(input.screenUV + float2(offset, 0.0)).rgb * weights[i];
    }
    return float4(color, 1.0);
}

//与Horizontal不同的是通过使用双线性过滤在高斯采样点之间以适当的偏移量进行采样，从而稍微减少样本量。这样可以将 9 个样本减少到 5 个
//We cannot do this in BloomHorizontalPassFragment because we already use bilinear filtering for downsampling in that pass.
//Each of its nine samples averages 2×2 source pixels.
float4 BloomVerticalPassFragment(Varyings input) : SV_TARGET
{
    float3 color = 0.0;
    float offsets[] = {
        -3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
    };
    float weights[] = {
        0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
    };
    for (int i = 0; i < 5; i++)
    {
        float offset = offsets[i] * GetSourceTexelSize().y;
        color += GetSource(input.screenUV + float2(0.0, offset)).rgb * weights[i];
    }
    return float4(color, 1.0);
}

//ToneMapping None
float4 ColorGradingNonePassFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradedLUT(input.screenUV);
    return float4(color, 1.0);
}

//ToneMapping ACES
float4 ColorGradingACESPassFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradedLUT(input.screenUV, true);
    color = AcesTonemap(color);
    return float4(color, 1.0);
}

//ToneMapping Neutral
float4 ColorGradingNeutralPassFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradedLUT(input.screenUV);
    color = NeutralTonemap(color);
    return float4(color, 1.0);
}

//ToneMapping Reinhard
float4 ColorGradingReinhardPassFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradedLUT(input.screenUV);
    color /= color + 1.0;
    return float4(color, 1.0);
}

float4 FinalPassFragment(Varyings input) : SV_TARGET
{
    float4 color = GetSource(input.screenUV);
    color.rgb = ApplyColorGradingLUT(color.rgb);
    return color;
}
#endif
