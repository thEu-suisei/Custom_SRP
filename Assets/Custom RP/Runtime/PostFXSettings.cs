using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject
{
    [SerializeField] private Shader shader = default;

    //使用shader需要材质，按需创建而不是在项目中保存
    [System.NonSerialized] private Material material;

    [System.Serializable]
    public struct BloomSettings
    {
        [Range(0f, 16f)] public int maxIterations;

        [Min(1f)] public int downscaleLimit;
        
        //是否启用双立方上采样(Bicubic Upsampling)
        public bool bicubicUpsampling;
    
        [Min(0f)]
        public float threshold;

        [Range(0f, 1f)]
        public float thresholdKnee;
        
        [Min(0f)]
        public float intensity;
    }

    [SerializeField] BloomSettings bloom = default;
    

    public BloomSettings Bloom => bloom;

    public Material Material
    {
        get
        {
            if (material == null && shader != null)
            {
                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave;
            }

            return material;
        }
    }
}