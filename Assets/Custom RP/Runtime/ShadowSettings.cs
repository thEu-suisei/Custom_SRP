using UnityEngine;

//单纯用来存放阴影配置选项的容器
[System.Serializable]
public class ShadowSettings
{
    //maxDistance决定视野内多大范围会被渲染到阴影贴图上，距离主摄像机超过maxDistance的物体不会被渲染在阴影贴图上
    //其具体逻辑猜测如下：
    //1.根据maxDistance（或者摄像机远平面）得到一个BoundingBox，这个BoundingBox（也可能是个球型）容纳了所有要渲染阴影的物体
    //2.根据这个BoundingBox（也可能是个球型）和方向光源的方向，确定渲染阴影贴图用的正交摄像机的视锥体，渲染阴影贴图
    public bool enableShadow = true;
    [Min(0.001f)] public float maxDistance = 100f;

    //用于最远阴影渐变消失变换强度的系数
    [Range(0.001f, 1f)] public float distanceFade = 0.1f;

    //阴影贴图的所有尺寸，使用枚举防止出现其他数值，范围为256-8192。
    public enum TextureSize
    {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
        _4096 = 4096,
        _8192 = 8192
    }

    //PCF采样尺寸
    public enum FilterMode
    {
        PCF2x2,
        PCF3x3,
        PCF5x5,
        PCF7x7
    }

    //定义方向光源的阴影贴图配置
    [System.Serializable]
    public struct Directional
    {
        public TextureSize atlasSize;

        //PCF Filter尺寸
        public FilterMode filter;

        //阴影级联的数量
        [Range(1, 4)] public int cascadeCount;

        //每个级联的的比例
        [Range(0f, 1f)] public float cascadeRatio1, cascadeRatio2, cascadeRatio3;

        //使用级联方法调用函数前需要提前打包这些参数
        public Vector3 CascadeRatios => new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3);

        [Range(0.001f, 1f)] public float cascadeFade;

        //Cascade配置与PCF配置不同，定义在Directional内
        public enum CascadeBlendMode
        {
            Hard,
            //软阴影混合级联需要对两个级联图进行采样，并且再加上PCF的多次采样，效率会比较低
            Soft,
            //抖动过度根据概率的方式只需要采样一个级联图，后续可以用AA对棋盘状阴影走样进行处理。
            Dither
        }

        public CascadeBlendMode cascadeBlend;
    }

    //创建一个1024大小的Directional Shadow Map
    public Directional directional = new Directional()
    {
        atlasSize = TextureSize._1024,
        filter = FilterMode.PCF2x2,
        cascadeCount = 4,
        cascadeRatio1 = 0.1f,
        cascadeRatio2 = 0.25f,
        cascadeRatio3 = 0.5f,
        cascadeFade = 0.1f,
        cascadeBlend = Directional.CascadeBlendMode.Hard
    };
}