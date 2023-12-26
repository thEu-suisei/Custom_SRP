using UnityEngine;
using UnityEngine.Rendering;

/*
  Partial:It's a way to split a class—or struct—definition into multiple parts,
  stored in different files. The only purpose is to organize code.
  The typical use case is to keep automatically-generated code separate
  from manually-written code.
*/
public partial class CameraRenderer
{
    ScriptableRenderContext context;
    Camera camera;
    const string bufferName = "Render Camera";
    CommandBuffer buffer = new CommandBuffer { name = bufferName };
    CullingResults cullingResults;

    private static ShaderTagId
        unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
        litShaderTagId = new ShaderTagId("CustomLit");

    Lighting lighting = new Lighting();

    public void Render
    (
        ScriptableRenderContext context,
        Camera camera,
        bool useDynamicBatching,
        bool useGPUInstancing,
        ShadowSettings shadowSettings
    )
    {
        this.context = context;
        this.camera = camera;

        PrepareBuffer();
        PrepareForSceneWindow();
        if (!Cull(shadowSettings.maxDistance))
        {
            return;
        }

        Setup();
        lighting.Setup(context, cullingResults,shadowSettings);
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing);
        DrawUnsupportedShaders();
        DrawGizmos(); //Gizmos:for example light icon.
        lighting.Cleanup();//Draw完了，清一下shadow atlas
        Submit(); //The context delays the actual rendering until we submit it.
    }

    void Setup()
    {
        context.SetupCameraProperties(camera); //unity_MatrixVP
        CameraClearFlags flags = camera.clearFlags;
        buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, flags <= CameraClearFlags.Color,
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
    }

    void Submit()
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing)
    {
        //决定物体绘制顺序是正交排序还是基于深度排序的配置
        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        //决定摄像机支持的Shader Pass和绘制顺序等的配置
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
        {
            //启用动态批处理
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing
        };
        //增加对Lit.shader的绘制支持,index代表本次DrawRenderer中该pass的绘制优先级（0最先绘制）
        drawingSettings.SetShaderPassName(1, litShaderTagId);
        //决定过滤哪些Visible Objects的配置，包括支持的RenderQueue等
        var filteringSettings =
            new FilteringSettings(RenderQueueRange.opaque); // indicate which render queues are allowed.

        //渲染CullingResults内不透明的VisibleObjects
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

        //添加“绘制天空盒”指令，DrawSkybox为ScriptableRenderContext下已有函数，这里就体现了为什么说Unity已经帮我们封装好了很多我们要用到的函数，SPR的画笔~
        context.DrawSkybox(camera);

        //渲染透明物体
        //设置绘制顺序为从后往前
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        //注意值类型
        drawingSettings.sortingSettings = sortingSettings;
        //过滤出RenderQueue属于Transparent的物体
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;

        //绘制透明物体
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    bool Cull(float maxShadowDistance)
    {
        //Rendering those visible things
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            //实际shadowDistance取maxShadowDistance和camera.farClipPlane中较小值
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            cullingResults = context.Cull(ref p);
            return true;
        }

        return false;
    }
}