using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject
{
    [SerializeField] private Shader shader = default;

    //使用shader需要材质，按需创建而不是在项目中保存
    [System.NonSerialized] private Material material;

    //Bloom
    [System.Serializable]
    public struct BloomSettings
    {
        [Range(0f, 16f)] public int maxIterations;

        [Min(1f)] public int downscaleLimit;

        //是否启用双立方上采样(Bicubic Upsampling)
        public bool bicubicUpsampling;

        [Min(0f)] public float threshold;

        [Range(0f, 1f)] public float thresholdKnee;

        [Min(0f)] public float intensity;

        public bool fadeFireflies;

        //Additive：加法滤波
        //Scatter：散射用于模拟相机和眼球的内部折射效果
        public enum Mode
        {
            Additive,
            Scattering
        }

        public Mode mode;

        [Range(0.05f, 0.95f)] public float scatter;
    }

    [SerializeField] BloomSettings bloom = new BloomSettings { scatter = 0.7f };

    public BloomSettings Bloom => bloom;

    //ToneMapping
    [System.Serializable]
    public struct ToneMappingSettings
    {
        public enum Mode
        {
            None,
            ACES,
            Neutral,
            Reinhard
        }

        public Mode mode;
    }

    [SerializeField] private ToneMappingSettings toneMapping = default;

    public ToneMappingSettings ToneMapping => toneMapping;

    //ColorAdjustment
    [Serializable]
    public struct ColorAdjustmentsSettings
    {
        //后曝光
        [Tooltip("后期曝光")] public float postExposure;

        //对比度
        [Range(-100f, 100f), Tooltip("对比度")] public float contrast;

        //颜色滤镜，即一种没有 alpha 的 HDR 颜色
        [ColorUsage(false, true), Tooltip("颜色滤镜")]
        public Color colorFilter;

        //色相偏移
        [Range(-180f, 180f), Tooltip("色相偏移")] public float hueShift;

        //饱和度
        [Range(-100f, 100f), Tooltip("饱和度")] public float saturation;
    }

    [SerializeField] ColorAdjustmentsSettings colorAdjustments = new ColorAdjustmentsSettings
    {
        colorFilter = Color.white
    };

    public ColorAdjustmentsSettings ColorAdjustments => colorAdjustments;

    //WhiteBalance
    [Serializable]
    public struct WhiteBalanceSettings
    {
        [Range(-100f, 100f), Tooltip("温度")] public float temperature;

        [Range(-100f, 100f), Tooltip("色调")] public float tint;
    }

    [SerializeField] WhiteBalanceSettings whiteBalance = default;

    public WhiteBalanceSettings WhiteBalance => whiteBalance;

    //Split Toning
    [Serializable]
    public struct SplitToningSettings
    {
        [ColorUsage(false)] public Color shadows, highlights;

        [Range(-100f, 100f)] public float balance;
    }

    [SerializeField] SplitToningSettings splitToning = new SplitToningSettings
    {
        shadows = Color.gray,
        highlights = Color.gray
    };

    public SplitToningSettings SplitToning => splitToning;

    //ChannelMixer
    [Serializable]
    public struct ChannelMixerSettings
    {
        public Vector3 red, green, blue;
    }

    [SerializeField] private ChannelMixerSettings channelMixer = new ChannelMixerSettings
    {
        red = Vector3.right,
        green = Vector3.up,
        blue = Vector3.forward
    };

    public ChannelMixerSettings ChannelMixer => channelMixer;

    //Shadows Midtones Highlights
    [Serializable]
    public struct ShadowsMidtonesHighlightsSettings
    {
        [ColorUsage(false, true)] public Color shadows, midtones, highlights;

        [Range(0f, 2f)] public float shadowsStart, shadowsEnd, highlightsStart, highlightsEnd;
    }

    [SerializeField] private ShadowsMidtonesHighlightsSettings shadowsMidtonesHighlights =
        new ShadowsMidtonesHighlightsSettings
        {
            shadows = Color.white,
            midtones = Color.white,
            highlights = Color.white,
            shadowsEnd = 0.3f,
            highlightsStart = 0.55f,
            highlightsEnd = 1f
        };

    public ShadowsMidtonesHighlightsSettings ShadowsMidtonesHighlights => shadowsMidtonesHighlights;

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