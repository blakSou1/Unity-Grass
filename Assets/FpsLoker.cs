using UnityEngine;

public class FpsLoker : MonoBehaviour
{
    private void Start()
    {
        Application.targetFrameRate = 60;
    }
}