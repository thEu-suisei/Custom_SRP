Shader "Custom RP/Lit"
{
    Properties
    {
        //[可选：特性]变量名(Inspector上的文本,类型名) = 默认值
        //[optional: attribute] name("display text in Inspector", type name) = default value

        //"white"为默认纯白贴图，{}在很久之前用于纹理的设置
        _BaseMap("Texture", 2D) = "white"{}
        _BaseColor("Color",Color) = (0.5,0.5,0.5,1.0)
        //透明度测试阈值
        _Cutoff("Alpha Cutoff",Range(0.0,1.0)) = 0.5
        //Clip的Shader关键字，启用该Toggle会将_Clipping关键字添加到该材质的活动关键字列表中，而禁用该Toggle会将其删除
        [Toggle(_CLIPPING)] _Clipping("Alpha Clipping",Float) = 0
        //Shadow模式属性
        [KeywordEnum(On,Clip,Dither,Off)]_Shadows("Shadows",Float)=0
        //PBR模型下Metallic Workflow的两个物体表面参数
        //金属度
        _Metallic("Metallic",Range(0,1)) = 0
        //光滑度
        _Smoothness("Smoothness",Range(0,1)) = 0.5
        //菲涅尔调节
        _Fresnel ("Fresnel", Range(0, 1)) = 1
        
        //Emission自发光
        [NoScaleOffset] _EmissionMap("Emission", 2D) = "white" {}
		[HDR] _EmissionColor("Emission", Color) = (0.0, 0.0, 0.0, 0.0)
        
        //Premultiply Alpha的关键字
        [Toggle(_PREMULTIPLY_ALPHA)]_PremulAlpha("Premultiply Alpha",Float) = 0

        //混合模式使用的值，其值应该是枚举值，但是这里使用float
        //特性用于在Editor下更方便编辑
        [Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Src Blend",Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Dst Blend",Float) = 0
        //深度写入模式
        [Enum(Off,0,On,1)] _ZWrite("Z Write",Float) = 1
        //接受阴影，选择让表面是否接受阴影
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1
        
        //烘焙透明物体，需要用到硬编码属性
        [HideInInspector] _MainTex("Texture for Lightmap", 2D) = "white" {}
		[HideInInspector] _Color("Color for Lightmap", Color) = (0.5, 0.5, 0.5, 1.0)
    }

    SubShader
    {
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "LitInput.hlsl"
        ENDHLSL
        Pass
        {
            //设置Pass Tags，最关键的Tag为"LightMode"
            Tags
            {
                "LightMode" = "CustomLit"
            }
            //设置混合模式
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            //不生成OpenGL ES 2.0等图形API的着色器变体，其不支持可变次数的循环与线性颜色空间
            #pragma target 3.5
            //告诉Unity启用_CLIPPING关键字时编译不同版本的Shader
            #pragma shader_feature _CLIPPING
            //定义diffuse项是否使用Premultiplied alpha的关键字
            #pragma shader_feature _PREMULTIPLY_ALPHA
            //接受阴影
            #pragma shader_feature _RECEIVE_SHADOWS
            //多编译，会根据生成Shader的不同关键字变体
            #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            //CascadeMode多编译
            #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            //ShadowMask
            #pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
            //光照贴图
            #pragma multi_compile _ LIGHTMAP_ON
            //LOD Cross-Fade
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            //这一指令会让Unity生成两个该Shader的变体，一个支持GPU Instancing，另一个不支持。
            #pragma multi_compile_instancing
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #include "LitPass.hlsl"
            ENDHLSL
        }

        //渲染阴影的Pass
        Pass
        {
            //阴影Pass的LightMode为ShadowCaster
            Tags
            {
                "LightMode" = "ShadowCaster"
            }
            //因为只需要写入深度，关闭对颜色通道的写入
            ColorMask 0

            HLSLPROGRAM
            //支持的最低平台
            #pragma target 3.5
            //支持Alpha Test的裁剪
            #pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
            //LOD Cross-Fade
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            //定义diffuse项是否使用Premultiplied alpha的关键字
            #pragma multi_compile_instancing
            #pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment
            //阴影相关方法写在ShadowCasterPass.hlsl
            #include "ShadowCasterPass.hlsl"
            ENDHLSL
        }

        //Meta Pass 用于烘焙而非运行时的pass
        Pass 
        {
            Name "Meta"
            Tags
            {
                "LightMode" = "Meta"
            }

            //Cull Off/Front/Cull Back，不剔除/正面剔除/背面剔除
			Cull Off

			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex MetaPassVertex
			#pragma fragment MetaPassFragment
			#include "MetaPass.hlsl"
			ENDHLSL
		}
    }

    //告诉Unity编辑器使用CustomShaderGUI类的一个实例来为使用Lit.shader的材质绘制Inspector窗口
    CustomEditor "CustomShaderGUI"
}