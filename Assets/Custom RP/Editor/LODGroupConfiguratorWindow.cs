using UnityEditor;
using UnityEngine;

public class LODGroupConfiguratorWindow : EditorWindow
{
    private LODFadeMode selectedFadeMode = LODFadeMode.None;
    private float fadeTransitionWidth = 1f;

    // 在菜单中添加自定义窗口
    [MenuItem("Tools/LODGroup Configurator")]
    public static void ShowWindow()
    {
        // 创建窗口
        GetWindow<LODGroupConfiguratorWindow>("LODGroup Configurator");
    }

    // 创建GUI
    private void OnGUI()
    {
        GUILayout.Label("Configure LODGroup Settings", EditorStyles.boldLabel);

        // 下拉菜单选择 Fade Mode
        selectedFadeMode = (LODFadeMode)EditorGUILayout.EnumPopup("Fade Mode", selectedFadeMode);

        // 输入框设置 Fade Transition Width
        fadeTransitionWidth = EditorGUILayout.Slider("Fade Transition Width", fadeTransitionWidth, 0f, 1f);

        // 应用按钮
        if (GUILayout.Button("Apply to All LODGroups"))
        {
            ConfigureAllLODGroups();
        }
    }

    private void ConfigureAllLODGroups()
    {
        // 查找场景中的所有 LODGroup
        LODGroup[] lodGroups = FindObjectsOfType<LODGroup>();

        foreach (var lodGroup in lodGroups)
        {
            // 开始记录LODGroup的修改
            Undo.RecordObject(lodGroup, "Configure LODGroup");

            // 设置 LODGroup 的 Fade Mode
            lodGroup.fadeMode = selectedFadeMode;

            // 获取所有 LOD 层级
            LOD[] lods = lodGroup.GetLODs();

            for (int i = 0; i < lods.Length; i++)
            {
                // 设置每个 LOD 层级的 Fade Transition Width
                lods[i].fadeTransitionWidth = fadeTransitionWidth;
            }

            // 重新应用修改后的 LOD 设置
            lodGroup.SetLODs(lods);

            // 标记为已修改
            EditorUtility.SetDirty(lodGroup);
        }

        // 打印日志
        Debug.Log($"Applied settings to {lodGroups.Length} LODGroups.");
    }
}