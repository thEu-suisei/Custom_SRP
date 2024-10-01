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

        public bool fadeFireflies;
        
        //Additive：加法滤波
        //Scatter：散射用于模拟相机和眼球的内部折射效果
        public enum Mode {Additive,Scattering}

        public Mode mode;
        
        [Range(0.05f, 0.95f)]
        public float scatter;
    }

    [SerializeField] BloomSettings bloom = new BloomSettings{scatter = 0.7f};
    

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