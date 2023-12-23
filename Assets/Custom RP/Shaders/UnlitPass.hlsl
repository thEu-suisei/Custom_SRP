#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

//使用Core RP Library的CBUFFER宏指令包裹材质属性，让Shader支持SRP Batcher，同时在不支持SRP Batcher的平台自动关闭它。
//CBUFFER_START后要加一个参数，参数表示该C buffer的名字(Unity内置了一些名字，如UnityPerMaterial，UnityPerDraw。
// CBUFFER_START(UnityPerMaterial)
// float4 _BaseColor;
// CBUFFER_END

//为了使用GPU Instancing，每实例数据要构建成数组,使用UNITY_INSTANCING_BUFFER_START(END)来包裹每实例数据
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
//_BaseColor在数组中的定义格式
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

//使用结构体定义顶点着色器的输入，一个是为了代码更整洁，一个是为了支持GPU Instancing（获取object的index）
struct Attributes
{
    float3 positionOS : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

//为了在片元着色器中获取实例ID，给顶点着色器的输出（即片元着色器的输入）也定义一个结构体
//命名为Varings是因为它包含的数据可以在同一三角形的片段之间变化
struct Varyings
{
    float4 positionCS: SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

//顶点着色器
//语义，POSITION，SV_POSITION顶点的位置信息
Varyings UnlitPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input,output);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionCS = TransformWorldToObject(positionWS);
    return output;
}

//片元着色器
//语义SV_TARGET这个像素的颜色值
float4 UnlitPassFragment (Varyings input) : SV_TARGET {
    UNITY_SETUP_INSTANCE_ID(input);
	return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,_BaseColor);
}

#endif
