using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField] private bool
        allowHDR = true,
        useDynamicBatching = true, 
        useGPUInstancing = true, 
        useSRPBatcher = true,
        useLightsPerObject = true;
    
    [SerializeField] private ShadowSettings shadows = default;

    [SerializeField] private PostFXSettings postFXSettings = default;
    
    public enum ColorLUTResolution { _16 = 16, _32 = 32, _64 = 64 }

    [SerializeField]
    ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;
    
    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(allowHDR,useDynamicBatching, useGPUInstancing, useSRPBatcher,useLightsPerObject, shadows,postFXSettings, (int)colorLUTResolution);
    }
}
