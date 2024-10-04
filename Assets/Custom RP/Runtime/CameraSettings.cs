using System;
using UnityEngine.Rendering;

//挂载到Camera上
[Serializable]
public class CameraSettings
{
    public bool overridePostFX = false;

    public PostFXSettings postFXSettings = default;
    
    //Multiple Camera Blend Mode
    [Serializable]
    public struct FinalBlendMode
    {
        public BlendMode source, destination;
    }

    public FinalBlendMode finalBlendMode = new FinalBlendMode
    {
        source = BlendMode.One,
        destination = BlendMode.Zero
    };
}