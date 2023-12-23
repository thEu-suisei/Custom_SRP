//Unlit.shader
//在Unity中所编写的 .shader文件，其实是在编写Shader类，它和通常意义上的
//Shader最大的区别在于一个Shader类之中可以定义多个通俗意义上的Shader（着
//色器程序），也就是多个SubShader，而这些SubShader其实才是我们通常所说的
//Shader。

//Shader后面的字符串用于创建Unity Eiditor中的material下拉条目
Shader "Custom RP/Unlit"
{
    //[可选：特性]变量名(Inspector上的文本,类型名) = 默认值
    //[optional: attribute] name("display text in Inspector", type name) = default value
    Properties
    {
        _BaseColor("Color",Color) = (1.0,1.0,1.0,1.0)
        
        //混合模式使用的值，其值应该是枚举值，但是这里使用float
        //特性用于在Editor下更方便编辑
        [Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Src Blend",Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Dst Blend",Float) = 0
        
        //深度写入模式
        [Enum(Off,0,On,1)] _ZWrite("Z Write",Float) = 1
        
    }

    //着色器程序
    //SubShader的组成是一到多个Pass（通道）
    SubShader
    {
        //Pass是Shader对象的基本元素，它包含设置GPU状态的指令，以及在GPU上运行的着色器程序。
        Pass
        {
            
            //设置混合模式
            //Opaque物体的混合模式为Src=One、Dst=Zero，即新颜色会完全覆盖旧颜色，
            //而Transparent物体的混合模式为Src=SrcAlhpa、Dst=OneMinusSrcAlpha
            Blend [_SrcBlend] [_DstBlend]

            //HLSLPROGRAM & ENDHLSL :it's possible put other non-HLSL code inside the Pass block
            HLSLPROGRAM
            //pragma:在许多编程语言中用于发出特殊的编译器指令。
            //identify vertex shader and fragment shader with name
            #pragma multi_compile_instancing
            #pragma vertex UnlitPassVertex
            #pragma fragment UnlitPassFragment
            #include "UnlitPass.hlsl"
            ENDHLSL
        }
    }
}