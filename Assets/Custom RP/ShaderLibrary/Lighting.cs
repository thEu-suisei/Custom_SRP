using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

//用于把场景中的光源信息通过cpu传递给gpu
public class Lighting
{
    const string bufferName = "Lighting";

    //最大方向光源数量
    const int maxDirLightCount = 4;

    //获取CBUFFER中对应数据名称的Id，CBUFFER就可以看作Shader的全局变量吧
    static int
        // dirLightColorId = Shader.PropertyToID("_DirectionalLightColor"),
        // dirLightDirectionId = Shader.PropertyToID("_DirectionalLightDirection");
        //改为传递Vector4数组+当前传递光源数
        dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
        dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections");
    
    static Vector4[]
        dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirections = new Vector4[maxDirLightCount];

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    CullingResults cullingResults;

    private Shadows shadows = new Shadows();

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults,ShadowSettings shadowSettings)
    {
        this.cullingResults = cullingResults;

        //对于传递光源数据到GPU的这一过程，我们可能用不到CommandBuffer下的指令
        //（其实用到了buffer.SetGlobalVector），但我们依然使用它来用于Debug
        buffer.BeginSample(bufferName);
        
        shadows.Setup(context,cullingResults,shadowSettings);
        SetupLights();
        shadows.Render();
        
        buffer.EndSample(bufferName);

        //这里只是提交CommandBuffer到Context的指令队列中，只有等到context.Submit()才会真正依次执行指令
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void SetupLights()
    {
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
        //循环配置两个Vector数组
        int dirLightCount = 0;
        for (int i = 0; i < visibleLights.Length; i++)
        {
            VisibleLight visibleLight = visibleLights[i];

            //这一步只设置Directional Light
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
            
            //可能之后有其他light
        }
        
        //传递当前有效光源数、光源颜色Vector数组、光源方向Vector数组。
        buffer.SetGlobalInt(dirLightCountId, visibleLights.Length);
        buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
        buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
    }

    //配置Vector4数组中的单个属性
    //传进的visibleLight添加了ref关键字，防止copy整个VisibleLight结构体（该结构体空间很大）
    void SetupDirectionalLight(int index,ref VisibleLight visibleLight)
    {
        // //通过RenderSettings.sun获取场景中默认的最主要的一个方向光，我们可以在
        // //Window/Rendering/Lighting Settings中显式配置它。
        // Light light = RenderSettings.sun;
        // //使用CommandBuffer.SetGlobalVector将光源信息传递给GPU
        // //该方法传递的永远是Vector4，即使我们传递的是Vector3，在传递过程中也会隐式
        // //转换成Vector4,然后在Shader读取时自动屏蔽掉最后一个分量
        // buffer.SetGlobalVector(dirLightColorId, light.color.linear * light.intensity);
        // //注意光源方向要取反再传递过去
        // buffer.SetGlobalVector(dirLightDirectionId, -light.transform.forward);

        //VisibleLight.finalColor为光源颜色（实际是光源颜色*光源强度，但是默认不是线性颜色空间，需要将Graphics.lightsUseLinearIntensity设置为true）
        dirLightColors[index] = visibleLight.finalColor;
        //光源方向为光源localToWorldMatrix的第三列，这里也需要取反
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        //保留阴影
        shadows.ReserveDirectionalShadows(visibleLight.light,index);
    }

    public void Cleanup()
    {
        shadows.Cleanup();
    }
}