//PerObjectMaterialProperties.cs
//该脚本作用：使不同物体使用同一材质可以设置不同的参数

using UnityEngine;

//不允许同一物体挂多个该组件
[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour {
	
	//获取名为"_BaseColor"的Shader属性（全局）
	static int baseColorId = Shader.PropertyToID("_BaseColor");
	static int cutoffId = Shader.PropertyToID("_Cutoff");
	static int metallicId = Shader.PropertyToID("_MetallicId");
	static int smoothnessId = Shader.PropertyToID("_smoothnessId");

	//每个物体自己的颜色
	[SerializeField] Color baseColor = Color.white;
	
	//每个实例有自己的cutoff值
	[SerializeField, Range(0f, 1f)]
	float alphaCutoff = 0.5f, metallic = 0f, smoothness = 0.5f;

	//MaterialPropertyBlock用于给每个物体设置材质属性，将其设置为静态，所有物体使用同一个block
	private static MaterialPropertyBlock block;

	//每当设置脚本的属性时都会调用 OnValidate（Editor下）
	private void OnValidate()
	{
		if (block == null)
		{
			block = new MaterialPropertyBlock();
			block.SetFloat(cutoffId, alphaCutoff);
		}

		//设置block中的baseColor属性(通过baseCalorId索引)为baseColor
		block.SetColor(baseColorId, baseColor);
		block.SetFloat(cutoffId, alphaCutoff);
		block.SetFloat(metallicId,metallic);
		block.SetFloat(smoothnessId,smoothness);
		//将物体的Renderer中的颜色设置为block中的颜色
		GetComponent<Renderer>().SetPropertyBlock(block);
	}

	//Runtime时也执行
	private void Awake()
	{
		OnValidate();
	}
}