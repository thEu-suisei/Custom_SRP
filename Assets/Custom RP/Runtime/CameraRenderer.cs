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

    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");

    public void Render(ScriptableRenderContext context, Camera camera)
    {
        this.context = context;
        this.camera = camera;

        PrepareBuffer();
        PrepareForSceneWindow();
        if (!Cull())
        {
            return;
        }

        Setup();
        DrawVisibleGeometry();
        DrawUnsupportedShaders();
        DrawGizmos();//Gizmos:for example light icon.
        Submit();//The context delays the actual rendering until we submit it.
    }

    void Setup()
    {
        context.SetupCameraProperties(camera);//unity_MatrixVP
        CameraClearFlags flags = camera.clearFlags;
        buffer.ClearRenderTarget(flags<=CameraClearFlags.Depth,flags<=CameraClearFlags.Color,flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
    }

    void Submit()
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();;
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void DrawVisibleGeometry()
    {
        var sortingSettings = new SortingSettings(camera){criteria = SortingCriteria.CommonOpaque};
        var drawingSettings = new DrawingSettings(unlitShaderTagId,sortingSettings);//indicate which kind of shader passes are allowed
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);// indicate which render queues are allowed.

        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
            
        context.DrawSkybox(camera);

        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    bool Cull()
    {
        //Rendering those visible things
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }
}