using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public class CustomRenderPipelineCamera : MonoBehaviour
{
    [SerializeField] CameraSettings settings = default;

    //??意思是如果settings!=null，则等于...
    public CameraSettings Settings => settings ?? (settings = new CameraSettings());
}