using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;


public class CustomShaderGUI : ShaderGUI
{
    //存储当前折叠标签状态
    private bool showPresets;

    //Unity 材质编辑器
    MaterialEditor editor;

    //当前选中的material是数组形式，因为我们可以同时多选多个使用同一Shader的材质进行编辑。
    Object[] materials;

    //材质可编辑属性
    MaterialProperty[] properties;

    enum ShadowMode
    {
        On,
        Clip,
        Dither,
        Off
    }

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        EditorGUI.BeginChangeCheck();
        //首先绘制材质Inspector下原本所有的GUI，例如材质的Properties等
        base.OnGUI(materialEditor, properties);
        //将editor、material、properties存储到字段中
        editor = materialEditor;
        materials = materialEditor.targets;
        this.properties = properties;

        BakeEmission();

        //增加一行空行
        EditorGUILayout.Space();
        //设置折叠标签
        showPresets = EditorGUILayout.Foldout(showPresets, "Presets", true);
        if (showPresets)
        {
            //绘制各个渲染模式预设值的按钮
            OpaquePreset();
            ClipPreset();
            FadePreset();
            TransparentPreset();
        }

        if (EditorGUI.EndChangeCheck())
        {
            SetShadowCasterPass();
            CopyLightMappingProperties(); //烘焙透明物体，硬编码属性拷贝
        }
    }

    //@这里的透明材质烘焙没有成功，Catlike:5.6
    void CopyLightMappingProperties()
    {
        //Unity会自己对每个材质判断其属于Opaque、Clip、Transparency，对于Clip和Transparency的材质，
        //Unity会自动捕捉材质中名为"_MainTex"的纹理与名为"_Color"的颜色，并让两者相乘得到alpha值，
        //再捕捉材质中名为"_Cutoff"的float值来进行Clip操作。

        //要实现透明材质的烘培，只需要维护好材质的_MainTex、_Color、_Cutoff三个属性
        MaterialProperty mainTex = FindProperty("_MainTex", properties, false);
        MaterialProperty baseMap = FindProperty("_BaseMap", properties, false);
        if (mainTex != null && baseMap != null)
        {
            mainTex.textureValue = baseMap.textureValue;
            mainTex.textureScaleAndOffset = baseMap.textureScaleAndOffset;
        }

        MaterialProperty color = FindProperty("_Color", properties, false);
        MaterialProperty baseColor =
            FindProperty("_BaseColor", properties, false);
        if (color != null && baseColor != null)
        {
            color.colorValue = baseColor.colorValue;
        }
    }

    void BakeEmission()
    {
        //Unity不会自动烘焙Emission，需要手动启用
        EditorGUI.BeginChangeCheck();
        //显示Global Illumination下拉菜单，只会控制unity_MetaFragmentControl.y
        editor.LightmapEmissionProperty();
        if (EditorGUI.EndChangeCheck())
        {
            foreach (Material m in editor.targets)
            {
                m.globalIlluminationFlags &=
                    ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }
    }

    /// <summary>
    /// 设置float类型的材质属性
    /// </summary>
    /// <param name="name">Property名字</param>
    /// <param name="value">要设定的值</param>
    bool SetProperty(string name, float value)
    {
        MaterialProperty property = FindProperty(name, properties, false);
        if (property != null)
        {
            property.floatValue = value;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 对选中的所有材质设置shader关键字
    /// </summary>
    /// <param name="keyword">关键字名称</param>
    /// <param name="enabled">是否开启</param>
    void SetKeyword(string keyword, bool enabled)
    {
        if (enabled)
        {
            foreach (Material m in materials)
            {
                m.EnableKeyword(keyword);
            }
        }
        else
        {
            foreach (Material m in materials)
            {
                m.DisableKeyword(keyword);
            }
        }
    }

    /// <summary>
    /// 因为我们之前在Lit.shader中使用Toggle标签的属性来切换关键字，因此在通过代码开关关键字时也要对Toggle操作以同步
    /// </summary>
    /// <param name="name">关键字对应Toggle名字</param>
    /// <param name="keyword">关键字名字</param>
    /// <param name="value">是否开关</param>
    void SetProperty(string name, string keyword, bool value)
    {
        if (SetProperty(name, value ? 1f : 0f))
        {
            SetKeyword(keyword, value);
        }
    }

    /// <summary>
    /// 该函数判断当前材质是否包含该属性
    /// </summary>
    /// <param name="name">属性名称</param>
    /// <returns></returns>
    bool HasProperty(string name) => FindProperty(name, properties, false) != null;

    private bool HasPremultiplyAlpha => HasProperty("_PremulAlpha");

    private bool Clipping
    {
        set => SetProperty("_Clipping", "_CLIPPING", value);
    }

    private bool PremultiplyAlpha
    {
        set => SetProperty("_PremulAlpha", "_PREMULTIPLY_ALPHA", value);
    }

    private BlendMode SrcBlend
    {
        set => SetProperty("_SrcBlend", (float)value);
    }

    private BlendMode DstBlend
    {
        set => SetProperty("_DstBlend", (float)value);
    }

    private bool ZWrite
    {
        set => SetProperty("_ZWrite", value ? 1f : 0f);
    }

    RenderQueue RenderQueue
    {
        set
        {
            foreach (Material m in materials)
            {
                m.renderQueue = (int)value;
            }
        }
    }

    /// <summary>
    /// 在设置预设值前，为Button注册撤回操作
    /// </summary>
    /// <param name="name">Button要设置的渲染模式</param>
    /// <returns></returns>
    bool PresetButton(string name)
    {
        if (GUILayout.Button(name))
        {
            editor.RegisterPropertyChangeUndo(name);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 设置为Opaque材质预设值
    /// </summary>
    void OpaquePreset()
    {
        if (PresetButton("Opaque"))
        {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.Geometry;
        }
    }

    /// <summary>
    /// 设置为Alpha Clip材质预设值
    /// </summary>
    void ClipPreset()
    {
        if (PresetButton("Clip"))
        {
            Clipping = true;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.SrcAlpha;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = true;
            RenderQueue = RenderQueue.AlphaTest;
        }
    }

    /// <summary>
    /// 设置为Fade(Alpha Blend,高光不完全保留)材质预设值
    /// </summary>
    void FadePreset()
    {
        if (PresetButton("Fade"))
        {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.SrcAlpha;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }
    }

    /// <summary>
    /// 设置为Transparent(开启Premultiply Alpha)材质预设值
    /// </summary>
    void TransparentPreset()
    {
        if (HasPremultiplyAlpha && PresetButton("Transparent"))
        {
            Clipping = false;
            PremultiplyAlpha = true;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }
    }

    ShadowMode Shadows
    {
        set
        {
            if (SetProperty("_Shadows", (float)value))
            {
                SetKeyword("_SHADOWS_CLIP", value == ShadowMode.Clip);
                SetKeyword("_SHADOWS_DITHER", value == ShadowMode.Dither);
            }
        }
    }

    void SetShadowCasterPass()
    {
        MaterialProperty shadows = FindProperty("_Shadows", properties, false);
        //如果没有模式或混合模式，则中止
        if (shadows == null || shadows.hasMixedValue)
        {
            return;
        }

        //否则，启用或禁用ShadowCaster
        bool enabled = shadows.floatValue < (float)ShadowMode.Off;
        foreach (Material m in materials)
        {
            m.SetShaderPassEnabled("ShadowCaster", enabled);
        }
    }
}