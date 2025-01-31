using System;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;


public class MeshBall : MonoBehaviour
{
    //和之前一样，使用int类型的PropertyId代替属性名称
    private static int baseColorId = Shader.PropertyToID("_BaseColor"),
        metallicId = Shader.PropertyToID("_Metallic"),
        smoothnessId = Shader.PropertyToID("_Smoothness");

    //GPU Instancing使用的Mesh
    [SerializeField] private Mesh mesh = default;
    //GPU Instancing使用的Material
    [SerializeField] private Material material = default;
    
    [SerializeField]
    LightProbeProxyVolume lightProbeVolume = null;

    //我们可以new 1000个GameObject，但是我们也可以直接通过每实例数据去绘制GPU Instancing的物体
    //创建每实例数据
    private Matrix4x4[] matrices = new Matrix4x4[1023];
    private Vector4[] baseColors = new Vector4[1023];

    private float[] metallic = new float[1023],
        smoothness = new float[1023];

    private MaterialPropertyBlock block;

    private void Awake()
    {
        
        for (int i = 0; i < matrices.Length; i++)
        {
            //在半径10米的球空间内随机实例小球的位置
            matrices[i] = Matrix4x4.TRS(Random.insideUnitSphere * 10f,
                Quaternion.Euler(Random.value * 360f, Random.value * 360f, Random.value * 360f),
                Vector3.one * Random.Range(0.5f, 1.5f));
            baseColors[i] = new Vector4(Random.value, Random.value, Random.value, Random.Range(0.5f,1f));
            metallic[i] = Random.value < 0.25f ? 1f : 0f;
            smoothness[i] = Random.Range(0.05f, 0.95f);
        }
    }

    private void Update()
    {
        //由于没有创建GameObject，需要每帧绘制
        if (block == null)
        {
            block = new MaterialPropertyBlock();
            //设置向量属性数组
            block.SetVectorArray(baseColorId, baseColors);
            block.SetFloatArray(metallicId, metallic);
            block.SetFloatArray(smoothnessId, smoothness);
            
            //对于每个实例，计算一个球谐函数并传递给GPU的成本比较大，我们可以使用一个LPPV，
            //让所有实例都共用这一个LPPV。这有两个好处，一是优化了性能，二是在实例位置移动时，不需要重新计算Probes。
            if (!lightProbeVolume)
            {
                var positions = new Vector3[1023];
                for (int i = 0; i < matrices.Length; i++)
                {
                    positions[i] = matrices[i].GetColumn(3);
                }
                var lightProbes = new SphericalHarmonicsL2[1023];
                var occlusionProbes = new Vector4[1023];
                LightProbes.CalculateInterpolatedLightAndOcclusionProbes(
                    positions, lightProbes, occlusionProbes
                );
                block.CopySHCoefficientArraysFrom(lightProbes);
                block.CopyProbeOcclusionArrayFrom(occlusionProbes);
            }

        }

        //一帧绘制多个网格，并且没有创建不必要的游戏对象的开销（一次最多只能绘制1023个实例），材质必须支持GPU Instancing
        Graphics.DrawMeshInstanced(
            mesh,
            0, 
            material, 
            matrices, 
            1023,
            block,
            ShadowCastingMode.On,
            true,
            0,
            null,
            lightProbeVolume ? LightProbeUsage.UseProxyVolume : LightProbeUsage.CustomProvided,
            lightProbeVolume
            );
    }
}
