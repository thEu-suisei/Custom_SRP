using UnityEngine;
using UnityEngine.UI;

//camera的component，显示文字信息
public class TextUI : MonoBehaviour
{
    public Font customFont; // 用于存储自定义字体文件的引用

    void Start()
    {
        // 创建一个新的 GameObject 用于承载 Canvas
        GameObject canvasGO = new GameObject("UICanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; // 设置渲染模式为屏幕空间摄像机
        canvas.worldCamera = Camera.main; // 将 Canvas 渲染到指定的摄像机上

        // 添加 CanvasScaler 组件，确保 UI 在不同分辨率下保持一致的比例
        canvasGO.AddComponent<CanvasScaler>();
        // 添加 GraphicRaycaster 组件，用于处理 UI 事件的射线检测
        canvasGO.AddComponent<GraphicRaycaster>();

        // 创建一个文本对象
        GameObject textGO = new GameObject("UIText");
        textGO.transform.SetParent(canvasGO.transform); // 设置文本对象的父对象为 Canvas
        Text textComponent = textGO.AddComponent<Text>(); // 添加 Text 组件

        // 设置文本属性
        textComponent.text = "Hello, World!";
        textComponent.font = customFont; // 使用自定义字体
        textComponent.fontSize = 24;
        textComponent.color = Color.white;

        // 设置文本对象的 RectTransform 属性
        RectTransform rectTransform = textGO.GetComponent<RectTransform>();
        rectTransform.localPosition = new Vector3(0, 0, 100); // 设置文本位置为屏幕中心，离摄像机100个单位
    }
}