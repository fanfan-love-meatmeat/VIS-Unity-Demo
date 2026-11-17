// DepthDisplay.cs
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class DepthDisplay : MonoBehaviour
{
    public Material depthMaterial;

    void Start()
    {
        GetComponent<Camera>().depthTextureMode |= DepthTextureMode.Depth;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (depthMaterial != null)
        {
            Graphics.Blit(src, dest, depthMaterial);
        }
        else
        {
            Graphics.Blit(src, dest);
        }
    }
}
