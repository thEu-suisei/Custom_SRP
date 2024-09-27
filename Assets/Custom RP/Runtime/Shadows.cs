using System;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Rendering;

//所有Shadow Map相关逻辑，其上级为Lighting类
public class Shadows
{
    private const string bufferName = "Shadows";

    //支持阴影的方向光源最大数（注意这里，我们可以有多个方向光源，但支持的阴影的最多只有4个）
    private const int maxShadowedDirectionalLightCount = 4, maxShadowedOtherLightCount = 16;
    private const int maxCascades = 4;

    //方向光源Shadow Atlas、阴影变化矩阵数组的标识
    private static int
        dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"), //TEXTURE
        dirShadowMatricesId =
            Shader.PropertyToID(
                "_DirectionalShadowMatrices"), //数量：16(最大方向光数*最大Cascade数)个。矩阵：Directional Light对应4个Cascade的W2C矩阵
        otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas"), //TEXTURE
        otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices"), //World2Clip矩阵
        otherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles"), //NormalBias
        cascadeCountId = Shader.PropertyToID("_CascadeCount"),
        cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
        cascadeDataId = Shader.PropertyToID("_CascadeData"),
        shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
        shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade"),
        shadowPancakingId = Shader.PropertyToID("_ShadowPancaking");

    private static Vector4[]
        cascadeCullingSpheres = new Vector4[maxCascades],
        cascadeData = new Vector4[maxCascades],
        otherShadowTiles = new Vector4[maxShadowedOtherLightCount]; //NormalBias

    //传递GPU ShadowAtlas的尺寸，x存储Direction.atlas大小，y存储x存储Direction.texel大小，z和w为OtherLight
    Vector4 atlasSizes;

    //将世界坐标转换到阴影贴图上的像素坐标的变换矩阵
    private static Matrix4x4[]
        dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades],
        otherShadowMatrices = new Matrix4x4[maxShadowedOtherLightCount];

    private CommandBuffer buffer = new CommandBuffer()
    {
        name = bufferName
    };

    private ScriptableRenderContext context;

    private CullingResults cullingResults;

    private ShadowSettings settings;

    //Directional Light Struct:
    struct ShadowedDirectionalLight
    {
        //当前光源的索引，猜测该索引为CullingResults中光源的索引(也是Lighting类下的光源索引，它们都是统一的，非常不错~）
        public int visibleLightIndex;

        //可配置偏移 Configurable Biases
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }

    //虽然我们目前最大光源数为1，但依然用数组存储，因为最大数量可配置嘛~
    private ShadowedDirectionalLight[] shadowedDirectionalLights =
        new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

    //Other Light Struct:
    struct ShadowedOtherLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float normalBias;
    }

    ShadowedOtherLight[] shadowedOtherLights =
        new ShadowedOtherLight[maxShadowedOtherLightCount];


    //当前已配置完毕的方向光源数
    private int shadowedDirLightCount, shadowedOtherLightCount;

    static string[] directionalFilterKeywords =
    {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    };

    static string[] otherFilterKeywords =
    {
        "_OTHER_PCF3",
        "_OTHER_PCF5",
        "_OTHER_PCF7",
    };

    static string[] cascadeBlendKeywords =
    {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };

    private static string[] shadowMaskKeywords =
    {
        "_SHADOW_MASK_ALWAYS",
        "_SHADOW_MASK_DISTANCE"
    };

    //用来追踪是否使用shadowMask
    private bool useShadowMask;

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults,
        ShadowSettings settings)
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;
        //每帧初始时ShadowedDirectionalLightCount为0，在配置每个光源时其+1
        shadowedDirLightCount = shadowedOtherLightCount = 0;
        useShadowMask = false;
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    /// <summary>
    /// 设置PCF/Cascade关键字
    /// EnableShaderKeyword(keyword):有点类似在shader中使用#define (keyword)
    /// 在shader中启用不同的变体：#pragma multi_compile (keyword1) (keyword2) ...
    /// </summary>
    void SetKeywords(string[] keywords, int enabledIndex)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (i == enabledIndex)
            {
                buffer.EnableShaderKeyword(keywords[i]);
            }
            else
            {
                buffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }

    //渲染阴影贴图
    public void Render()
    {
        // Debug.Log(settings.enableShadow);

        if (settings.enableShadow)
        {
            if (shadowedDirLightCount > 0)
            {
                RenderDirectionalShadows();
            }
            else
            {
                //如果因为某种原因不需要渲染阴影，我们也需要生成一张1x1大小的ShadowAtlas
                //因为WebGL 2.0下如果某个材质包含ShadowMap但在加载时丢失了ShadowMap会报错
                buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
            }

            if (shadowedOtherLightCount > 0)
            {
                RenderOtherShadows();
            }
            else
            {
                buffer.SetGlobalTexture(otherShadowAtlasId, dirShadowAtlasId);
            }
        }

        buffer.BeginSample(bufferName);
        SetKeywords(shadowMaskKeywords,
            useShadowMask ? (QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1) : -1);

        buffer.SetGlobalInt(
            cascadeCountId,
            shadowedDirLightCount > 0 ? settings.directional.cascadeCount : 0
        );
        //传递最大阴影距离，在超出范围的阴影自然消失，All Light需要
        //Tips：下行被注释SetGlobalFloat替换成SetGlobalVector，因为当将它们作为矢量的XY组件发送到GPU时，使用一个除以值，这样我们就可以避免在着色器中进行分割，因为乘法更快。
        //buffer.SetGlobalFloat(shadowDistanceId, settings.maxDistance);
        float f = 1f - settings.directional.cascadeFade;
        buffer.SetGlobalVector(
            shadowDistanceFadeId, new Vector4(
                1f / settings.maxDistance, 1f / settings.distanceFade,
                1f / (1f - f * f)
            )
        );
        buffer.SetGlobalVector(shadowAtlasSizeId, atlasSizes);

        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    /// <summary>
    /// 每帧执行，用于为light配置shadow altas（shadowMap）上预留一片空间来渲染阴影贴图，同时存储一些其他必要信息
    /// </summary>
    /// <param name="light">获取对象light的信息</param>
    /// <param name="visibleLightIndex">对象light的索引</param>
    /// <returns>传递给GPU存储到Light结构体：(阴影强度,cascade索引,法线偏移,使用shadowmask的通道索引(一张RBGA图有4通道可以存储4个光照的mask))，
    /// 即(light shadow strength, Cascade Index, light shadow normalBias, maskChannel)</returns>
    public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        //配置光源数不超过最大值
        //只配置开启阴影且阴影强度大于0的光源
        //忽略不需要渲染任何阴影的光源（通过cullingResults.GetShadowCasterBounds方法）
        if (shadowedDirLightCount < maxShadowedDirectionalLightCount && light.shadows != LightShadows.None &&
            light.shadowStrength > 0f
            //新增了烘焙阴影，所以不再跳过没有实时阴影的光源
            //&& cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
           )
        {
            float maskChannel = -1;
            //获取烘焙信息
            LightBakingOutput lightBaking = light.bakingOutput;
            //检查光照烘焙模式，并更改对应设置
            if (
                lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
            )
            {
                useShadowMask = true;
                maskChannel = lightBaking.occlusionMaskChannel;
            }

            //GetShadowCasterBounds返回bool:光源影响了场景中至少一个阴影投射对象
            if (!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
            {
                //使用烘焙阴影，但是阴影强度>0仍会使用，所以取负即可跳过实时阴影，在计算烘焙阴影时取绝对值又可使用
                return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
            }

            shadowedDirectionalLights[shadowedDirLightCount] = new ShadowedDirectionalLight()
            {
                visibleLightIndex = visibleLightIndex,
                slopeScaleBias = light.shadowBias,
                nearPlaneOffset = light.shadowNearPlane
            };
            return new Vector4(
                light.shadowStrength,
                settings.directional.cascadeCount * shadowedDirLightCount++,
                light.shadowNormalBias,
                maskChannel);
        }

        return new Vector4(0f, 0f, 0f, -1f);
    }


    /// <summary>
    /// 使用mixed&shadowsmask时，传送GPU前的数据准备，类似ReserveDirectionalShadows，不过只关心阴影遮罩模式，并只需配置阴影强度和遮罩通道
    /// </summary>
    /// <param name="light"></param>
    /// <param name="visibleLightIndex"></param>
    /// <returns>(OtherLight的阴影强度,OtherLight索引,0f,Shadowmap中使用mask的通道(RGBA))</returns>
    public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
    {
        //没有阴影则返回
        if (light.shadows == LightShadows.None || light.shadowStrength <= 0f)
        {
            return new Vector4(0f, 0f, 0f, -1f);
        }

        //-1在GPU中为禁用
        float maskChannel = -1f;
        LightBakingOutput lightBaking = light.bakingOutput;
        //如果light是mixed并且是shadowmask模式，那么烘焙阴影可以使用
        if (
            lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
            lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
        )
        {
            useShadowMask = true;
            maskChannel = lightBaking.occlusionMaskChannel;
        }

        //灯光数量超过最大值||没有阴影投射物体,则不适用实时光，x分量为负
        if (
            shadowedOtherLightCount >= maxShadowedOtherLightCount ||
            !cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)
        )
        {
            return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
        }

        shadowedOtherLights[shadowedOtherLightCount] = new ShadowedOtherLight
        {
            visibleLightIndex = visibleLightIndex,
            slopeScaleBias = light.shadowBias,
            normalBias = light.shadowNormalBias
        };

        return new Vector4(light.shadowStrength, shadowedOtherLightCount++, 0f, maskChannel);
    }

    /// <summary>
    /// 将准备好的DirectionalLights数据传输，并渲染其ShadowAtlas
    /// </summary>
    void RenderDirectionalShadows()
    {
        //Shadow Atlas阴影图集的尺寸
        int atlasSize = (int)settings.directional.atlasSize;
        atlasSizes.x = atlasSize;
        atlasSizes.y = 1f / atlasSize;
        //使用CommandBuffer.GetTemporaryRT来申请一张RT用于Shadow Atlas，注意我们每帧自己管理其释放
        //参数分别为：该RT的标识，RT的宽，RT的高，depthBuffer的位宽，过滤模式，RT格式
        //我们使用32bits的Float位宽，URP使用的是16bits
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize,
            32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        //RenderBufferLoadAction.DontCare意味着在将其设置为RenderTarget之后，我们不关心它的初始状态，不对其进行任何预处理
        //RenderBufferStoreAction.Store意味着完成这张RT上的所有渲染指令之后（要切换为下一个RenderTarget时），我们会将其存储到显存中为后续采样使用
        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        //清理ShadowAtlas的DepthBuffer（我们的ShadowAtlas也只有32bits的DepthBuffer）,
        //设置参数分别为：true表示清除DepthBuffer，false表示不清除ColorBuffer，Color.clear表示清除为完全透明的颜色(r=0,g=0,b=0,a=0)
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId, 1f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        //给ShadowAtlas分Tile，大于1个光源时分成4个Tile
        int tiles = shadowedDirLightCount * settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        //为每个配置好的方向光源配置其ShadowAtlas上的Tile
        for (int i = 0; i < shadowedDirLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }

        //Cascade：
        buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
        //传递所有阴影变换矩阵给GPU
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
        SetKeywords(cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1);

        //PCF：
        SetKeywords(directionalFilterKeywords, (int)settings.directional.filter - 1);
        //传递向量，x存储atlas大小，y存储texel大小
        // buffer.SetGlobalVector(
        //     shadowAtlasSizeId,
        //     new Vector4(atlasSize, 1f / atlasSize));

        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    /// <summary>
    /// 渲染单个光源的阴影贴图到ShadowAtlas上
    /// </summary>
    /// <param name="index">光源的索引</param>
    /// <param name="split">分块量（一个方向）</param>
    /// <param name="tileSize">该光源在ShadowAtlas上分配的Tile块大小</param>
    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        //获取当前要配置光源的信息
        ShadowedDirectionalLight light = shadowedDirectionalLights[index];
        //根据cullingResults和当前光源的索引来构造一个ShadowDrawingSettings
        var shadowSettings = new ShadowDrawingSettings(
            cullingResults,
            light.visibleLightIndex,
            BatchCullingProjectionType.Orthographic);
        //设置Shadow cascade参数
        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = settings.directional.CascadeRatios;
        //剔除偏差因子
        float cullingFactor = Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);
        float tileScale = 1f / split;

        for (int i = 0; i < cascadeCount; i++)
        {
            //使用Unity提供的接口来为方向光源计算出其渲染阴影贴图用的VP矩阵和splitData
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex,
                i,
                cascadeCount,
                ratios,
                tileSize,
                light.nearPlaneOffset,
                out Matrix4x4 viewMatrix,
                out Matrix4x4 projectionMatrix,
                out ShadowSplitData splitData);
            //剔除偏差 Culling Bias
            // splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            //splitData包括投射阴影物体应该如何被裁剪的信息，我们需要把它传递给shadowSettings
            shadowSettings.splitData = splitData;
            if (index == 0)
            {
                SetCascadeData(i, splitData.cullingSphere, tileSize);
            }

            int tileIndex = tileOffset + i;
            //设置当前要渲染的Tile区域
            //设置阴影变换矩阵(世界空间到光源裁剪空间）
            dirShadowMatrices[tileIndex] =
                ConvertToAtlasMatrix(
                    projectionMatrix * viewMatrix,
                    SetTileViewport(tileIndex, split, tileSize),
                    tileScale);
            //将当前VP矩阵设置为计算出的VP矩阵，准备渲染阴影贴图
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);

            //深度偏移法/斜率偏差
            // buffer.SetGlobalDepthBias(0f, 3f);

            //在渲染阴影贴图前设置depth Bias来消除阴影痤疮,传入bias和slopBias
            //这里的bias单位应该不是米
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();

            //使用context.DrawShadows来渲染阴影贴图，其需要传入一个shadowSettings
            context.DrawShadows(ref shadowSettings);

            //渲染完阴影贴图后将bias设置回0
            buffer.SetGlobalDepthBias(0f, 0f);
        }
    }


    /// <summary>
    /// 将准备好的OtherLights数据传输，并渲染其ShadowAtlas
    /// </summary>
    void RenderOtherShadows()
    {
        //Shadow Atlas阴影图集的尺寸
        int atlasSize = (int)settings.other.atlasSize;
        atlasSizes.z = atlasSize;
        atlasSizes.w = 1f / atlasSize;

        buffer.GetTemporaryRT(otherShadowAtlasId, atlasSize, atlasSize,
            32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);

        buffer.SetRenderTarget(otherShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);

        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId, 0f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = shadowedOtherLightCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        //为每个配置好的方向光源配置其ShadowAtlas上的Tile
        for (int i = 0; i < shadowedOtherLightCount; i++)
        {
            RenderSpotShadows(i, split, tileSize);
        }

        //传递所有阴影变换矩阵给GPU
        buffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices);
        buffer.SetGlobalVectorArray(otherShadowTilesId, otherShadowTiles);

        //PCF：
        SetKeywords(otherFilterKeywords, (int)settings.other.filter - 1);

        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    /// <summary>
    /// 渲染SpotLight阴影
    /// </summary>
    /// <param name="index">在OtherLights数组上的索引</param>
    /// <param name="split">ShadowAtlas的分块数量</param>
    /// <param name="tileSize">分块后light可以使用的分块大小</param>
    void RenderSpotShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(
            cullingResults, light.visibleLightIndex,
            BatchCullingProjectionType.Perspective
        );
        cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
            light.visibleLightIndex, out Matrix4x4 viewMatrix,
            out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
        );
        shadowSettings.splitData = splitData;

        //使用ProjMat的第一个元素作为2tanθ
        float texelSize = 2f / (tileSize * projectionMatrix.m00);
        float filterSize = texelSize * ((float)settings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        Vector2 offset = SetTileViewport(index, split, tileSize);
        float tileScale = 1f / split;
        SetOtherTileData(index, offset, tileScale , bias);
        otherShadowMatrices[index] = ConvertToAtlasMatrix(
            projectionMatrix * viewMatrix, offset, tileScale
        );
        buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
        ExecuteBuffer();
        context.DrawShadows(ref shadowSettings);
        buffer.SetGlobalDepthBias(0f, 0f);
    }

    /// <summary>
    /// 设置级联数据
    /// </summary>
    /// <param name="index">级联索引</param>
    /// <param name="cullingSphere">剔除球</param>
    /// <param name="tileSize">tile大小</param>
    void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
    {
        float texelSize = 2f * cullingSphere.w / tileSize;
        cullingSphere.w *= cullingSphere.w;
        cascadeCullingSpheres[index] = cullingSphere;
        cascadeData[index] = new Vector4(
            1f / cullingSphere.w,
            texelSize * 1.4142136f
        );
    }

    /// <summary>
    /// 设置当前要渲染的Tile区域
    /// </summary>
    /// <param name="index">Tile索引</param>
    /// <param name="split">Tile一个方向上的总数</param>
    /// <param name="tileSize">一个Tile的宽度（高度）</param>
    Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
        return offset;
    }

    /// <summary>
    /// 设置Tiles数据，xy为边界，z是边界缩放比例，w为bias
    /// </summary>
    /// <param name="index">OtherLight Index</param>
    /// <param name="offset">限定采样范围</param>
    /// <param name="scale">限定采样范围</param>
    /// <param name="bias">偏移</param>
    void SetOtherTileData(int index, Vector2 offset, float scale, float bias)
    {
        float border = atlasSizes.w * 0.5f;
        Vector4 data;
        data.x = offset.x * scale + border;
        data.y = offset.y * scale + border;
        data.z = scale - border - border;
        data.w = bias;
        otherShadowTiles[index] = data;
    }

    /// <summary>
    /// 计算光照的某一层级Cascade的WorldToClip矩阵?
    /// </summary>
    /// <param name="m"></param>
    /// <param name="offset"></param>
    /// <param name="split"></param>
    /// <returns>WorldToClip矩阵</returns>
    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, float scale)
    {
        //如果使用反向Z缓冲区，为Z取反
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }

        //光源裁剪空间坐标范围为[-1,1]，而纹理坐标和深度都是[0,1]，因此，我们将裁剪空间坐标转化到[0,1]内
        //然后将[0,1]下的x,y偏移到光源对应的Tile上
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);
        return m;
    }

//完成因ShadowAtlas所有工作后，释放ShadowAtlas RT
    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        if (shadowedOtherLightCount > 0)
        {
            buffer.ReleaseTemporaryRT(otherShadowAtlasId);
        }

        ExecuteBuffer();
    }
}