using UnityEngine;
using UnityEditor;

[CanEditMultipleObjects]
[CustomEditorForRenderPipeline(typeof(Light),typeof(CustomRenderPipelineAsset))]
public class CustomLightEditor : LightEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        //是否选择了聚光灯
        if (!settings.lightType.hasMultipleDifferentValues&&(LightType)settings.lightType.enumValueIndex==LightType.Spot)
        {
            //在inspector中提供SpotLight的inner angle可配置
            settings.DrawInnerAndOuterSpotAngle();
            settings.ApplyModifiedProperties();
        }
    }
}
