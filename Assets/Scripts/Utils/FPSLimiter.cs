using UnityEngine;

public class FPSLimiter : MonoBehaviour
{
    private void Awake()
    {
        Application.targetFrameRate = 60;
    }
}