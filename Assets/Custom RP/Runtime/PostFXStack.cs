using UnityEditor.Rendering.LookDev;
using UnityEngine;
using UnityEngine.Rendering;

public partial class PostFXStack
{
    private const string bufferName = "Post FX";
    private CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };
    
    enum Pass
    {
        Copy
    }

    private ScriptableRenderContext context;
    private Camera camera;
    private PostFXSettings settings;

    private int fxSourceId = Shader.PropertyToID("_PostFXSource");

    public bool IsActive => settings != null;

    public void Setup(ScriptableRenderContext context, Camera camera, PostFXSettings settings)
    {
        this.context = context;
        this.camera = camera;
        this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
        ApplySceneViewState();
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
    {
        //准备需要复制的纹理
        buffer.SetGlobalTexture(fxSourceId,from);
        //复制的目标
        buffer.SetRenderTarget(to,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store);
        //与Blit不同，使用一个三角形来覆盖裁剪空间进行复制
        buffer.DrawProcedural(Matrix4x4.identity, settings.Material,(int)pass,MeshTopology.Triangles,3);
    }

    /// <summary>
    /// Render渲染堆栈
    /// </summary>
    /// <param name="sourceId"></param>
    public void Render(int sourceId)
    {
        //将目前所有渲染的结果  复制到  相机的帧缓冲区，完全复制因此不需要ClearRenderTarget
        //buffer.Blit(sourceId,BuiltinRenderTextureType.CameraTarget);
        Draw(sourceId,BuiltinRenderTextureType.CameraTarget,Pass.Copy);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}
