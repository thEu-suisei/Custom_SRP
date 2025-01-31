using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    //摄像机渲染器实例，用于管理所有摄像机的渲染
    private CameraRenderer renderer = new CameraRenderer();

    private bool allowHDR;

    //批处理配置
    private bool useDynamicBatching, useGPUInstancing, useLightsPerObject;

    //Shadow Map配置
    private ShadowSettings shadowSettings;

    //Post Processing
    PostFXSettings postFXSettings;

    //LUT
    int colorLUTResolution;

    //构造函数，初始化管线的一些属性
    public CustomRenderPipeline(
        bool allowHDR,
        bool useDynamicBatching,
        bool useGPUInstancing,
        bool useSRPBatcher,
        bool useLightsPerObject,
        ShadowSettings shadowSettings,
        PostFXSettings postFXSettings,
        int colorLUTResolution
    )
    {
        this.allowHDR = allowHDR;
        this.postFXSettings = postFXSettings;
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.shadowSettings = shadowSettings;
        this.useLightsPerObject = useLightsPerObject;
        this.colorLUTResolution = colorLUTResolution;
        //配置SRP Batch
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        //设置光源颜色为线性空间
        GraphicsSettings.lightsUseLinearIntensity = true;
        InitializeForEditor();
    }

    //必须重写Render函数，渲染管线实例每帧执行Render函数
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        //按顺序渲染每个摄像机
        foreach (var camera in cameras)
        {
            renderer.Render(context, camera, allowHDR, useDynamicBatching, useGPUInstancing, useLightsPerObject,
                shadowSettings, postFXSettings, colorLUTResolution);
        }
    }
}