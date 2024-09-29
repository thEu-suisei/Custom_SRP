using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject
{
    [SerializeField] private Shader shader = default;

    //使用shader需要材质，按需创建而不是在项目中保存
    [System.NonSerialized] private Material material;

    public Material Material
    {
        get
        {
            if (material==null&&shader!=null)
            {
                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave;
            }

            return material;
        }
    }
}