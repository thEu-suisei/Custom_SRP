using System;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class MeshBall : MonoBehaviour
{
    //使用int类型的PropertyId代替属性名称
    static int baseColorId = Shader.PropertyToID("_BaseColor");

    //GPU Instancing使用的Mesh
    [SerializeField] Mesh mesh = default;

    //GPU Instancing使用的Material
    [SerializeField] Material material = default;

    //我们可以new 1000个GameObject，但是我们也可以直接通过每实例数据去绘制GPU Instancing的物体
    //创建每实例数据
    private Matrix4x4[] matrices = new Matrix4x4[1023];
    private Vector4[] baseColors = new Vector4[1023];

    private MaterialPropertyBlock block;

    private void Awake()
    {
        for (int i = 0; i < matrices.Length; i++)
        {
            //在半径10米的球空间内随机实例小球的位置
            //TRS:Creates a translation, rotation and scaling matrix.
            matrices[i] = Matrix4x4.TRS(
                Random.insideUnitSphere * 10f,
                Quaternion.Euler(Random.value*360f,Random.value*360f,Random.value*360f),
                Vector3.one*Random.Range(0.5f,1.5f));
            
            baseColors[i] = new Vector4(
                Random.value,
                Random.value,
                Random.value, Random.Range(0.5f,1f));
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
        }

        //一帧绘制多个网格，并且没有创建不必要的游戏对象的开销（一次最多只能绘制1023个实例），材质必须支持GPU Instancing
        Graphics.DrawMeshInstanced(mesh, 0, material, matrices, 1023, block);
    }
}