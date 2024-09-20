using UnityEngine;

public class LightController : MonoBehaviour
{
    private Light lightComp;

    private void Start()
    {
        lightComp = GetComponentInParent<Light>();
        lightComp.intensity = 0f;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            ChangeIntensity(0.1f);
            Debug.Log(lightComp.intensity);
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            ChangeIntensity(-0.1f);
            Debug.Log(lightComp.intensity);
        }

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            ChangeTransformY(-0.5f);
            Debug.Log(lightComp.intensity);
        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            ChangeTransformY(0.5f);
            Debug.Log(lightComp.intensity);
        }
    }

    private void ChangeIntensity(float changesValue)
    {
        float temp = lightComp.intensity + changesValue;
        if (temp > 1)
        {
            lightComp.intensity = 1.0f;
        }
        else if (temp < 0)
        {
            lightComp.intensity = 0.0f;
        }
        else
        {
            lightComp.intensity = temp;
        }
    }

    private void ChangeTransformY(float ChangeAngle)
    {
        float temp = (ChangeAngle + lightComp.transform.localEulerAngles.y) % 360;
        
        lightComp.transform.localEulerAngles = new Vector3 (lightComp.transform.localEulerAngles.x,temp,lightComp.transform.localEulerAngles.z);
    }
}