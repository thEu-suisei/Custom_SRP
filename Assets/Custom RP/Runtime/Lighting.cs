using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

//用于把场景中的光源信息通过cpu传递给gpu
public class Lighting
{
    private const string bufferName = "Lighting";
    //最大方向光源数量
    private const int maxDirLightCount = 4;

    //获取CBUFFER中对应数据名称的Id，CBUFFER就可以看作Shader的全局变量吧
    // private static int dirLightColorId = Shader.PropertyToID("_DirectionalLightColor"),
    //     dirLightDirectionId = Shader.PropertyToID("_DirectionalLightDirection");
    //改为传递Vector4数组+当前传递光源数
    private static int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
        dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
        dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

    //dirLightShadowData = ( light.shadowStrength , settings.directional.cascadeCount * ShadowedDirectionalLightCount++ , light.shadowNormalBias , ...)
    //dirLightShadowData = ( light的阴影强度属性    ,                              cascade索引                             , light.shadowNormalBias , ...)
    private static Vector4[] 
        dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirections = new Vector4[maxDirLightCount],
        dirLightShadowData = new Vector4[maxDirLightCount];

    private CommandBuffer buffer = new CommandBuffer()
    {
        name = bufferName
    };

    //主要使用到CullingResults下的光源信息
    private CullingResults cullingResults;
    
    //渲染阴影贴图相关
    private Shadows shadows = new Shadows();

    //传入参数context用于注入CmmandBuffer指令，cullingResults用于获取当前有效的光源信息
    public void Setup(ScriptableRenderContext context, CullingResults cullingResults,
        ShadowSettings shadowSettings)
    {
        //存储到字段方便使用
        this.cullingResults = cullingResults;
        //对于传递光源数据到GPU的这一过程，我们可能用不到CommandBuffer下的指令（其实用到了buffer.SetGlobalVector），但我们依然使用它来用于Debug
        buffer.BeginSample(bufferName);
        //渲染阴影相关
        shadows.Setup(context, cullingResults, shadowSettings);
        //传递cullingResults下的有效光源给GPU
        SetupLights();
        //渲染阴影贴图
        shadows.Render();
        buffer.EndSample(bufferName);
        //再次提醒这里只是提交CommandBuffer到Context的指令队列中，只有等到context.Submit()才会真正依次执行指令
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    //配置Vector4数组中的单个属性
    //传进的visibleLight添加了ref关键字，防止copy整个VisibleLight结构体（该结构体空间很大）
    void SetupDirectionalLight(int index,ref VisibleLight visibleLight)
    {
        //VisibleLight.finalColor为光源颜色（实际是光源颜色*光源强度，但是默认不是线性颜色空间，需要将Graphics.lightsUseLinearIntensity设置为true）
        dirLightColors[index] = visibleLight.finalColor;
        //光源方向为光源localToWorldMatrix的第三列，这里也需要取反
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        //配置阴影管理类，让其配置好支持阴影的光源相关信息，并且返回当前光源的阴影数据
        dirLightShadowData[index] = shadows.ReserveDirectionalShadows(visibleLight.light, index);
    }

    void SetupLights()
    {
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
        //循环配置两个Vector数组
        int dirLightCount = 0;
        for (int i = 0; i < visibleLights.Length; i++)
        {
            VisibleLight visibleLight = visibleLights[i];
            
            //按光源类型配置
            if (visibleLight.lightType == LightType.Directional)
            {
                //设置数组中单个光源的属性
                SetupDirectionalLight(dirLightCount++, ref visibleLight);
                if (dirLightCount >= maxDirLightCount)
                {
                    //最大不超过4个方向光源
                    break;
                }
            }
        }
        
        //传递当前有效光源数、光源颜色Vector数组、光源方向Vector数组。
        buffer.SetGlobalInt(dirLightCountId, visibleLights.Length);
        buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
        buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
        //传递每个光源的阴影数据（阴影强度、阴影光源索引）
        buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
    }

    //完成光源的所有工作后释放其相关内存
    public void Cleanup()
    {
        //释放ShadowAtlas内存
        shadows.Cleanup();
    }
}
