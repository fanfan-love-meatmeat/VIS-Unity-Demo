using UnityEngine;
using UnityEngine.Rendering;

// Display-only override for observer/Scene views; restore materials before sensor rendering.
[ExecuteAlways, RequireComponent(typeof(Camera))]
public class ObserverCropTransparency : MonoBehaviour
{
    public Transform cropRows;
    [Range(0f, 1f), Tooltip("Crop-row opacity in the Game observer and Scene views: 0 is transparent; 1 is opaque. RGB, depth and VIS statistics are unchanged.")]
    public float opacity = .3f;

    private Transform cachedRoot;
    private MeshRenderer[] renderers;
    private Material[] originals;
    private Material source, faded;
    private bool overridden;

    void OnPreCull() { ApplyPreview(); }

    public void ApplyPreview()
    {
        RestoreMaterials();
        if (cropRows == null || opacity >= 1f) return;
        if (cachedRoot != cropRows || renderers == null)
        {
            cachedRoot = cropRows;
            renderers = cropRows.GetComponentsInChildren<MeshRenderer>();
            originals = new Material[renderers.Length];
        }
        if (renderers.Length == 0 || renderers[0] == null) return;
        Material currentSource = renderers[0].sharedMaterial;
        if (currentSource == null) return;
        if (faded == null || source != currentSource)
        {
            ReleaseMaterial();
            source = currentSource;
            faded = new Material(source) { name = "CropRow_ObserverPreview", hideFlags = HideFlags.HideAndDontSave };
            faded.SetFloat("_Mode", 2); // Standard shader: Fade.
            faded.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            faded.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            faded.SetInt("_ZWrite", 0);
            faded.DisableKeyword("_ALPHATEST_ON");
            faded.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            faded.EnableKeyword("_ALPHABLEND_ON");
            faded.SetOverrideTag("RenderType", "Transparent");
            faded.renderQueue = (int)RenderQueue.Transparent;
        }
        Color color = source.color;
        color.a = Mathf.Clamp01(opacity);
        faded.color = color;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            originals[i] = renderers[i].sharedMaterial;
            renderers[i].sharedMaterial = faded;
        }
        overridden = true;
    }

    void OnPostRender() { RestoreMaterials(); }

    public void RestoreMaterials()
    {
        if (!overridden) return;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].sharedMaterial = originals[i];
        overridden = false;
    }

    private void ReleaseMaterial()
    {
        if (faded == null) return;
        if (Application.isPlaying) Destroy(faded);
        else DestroyImmediate(faded);
        faded = null;
    }

    void OnDisable()
    {
        RestoreMaterials();
        ReleaseMaterial();
    }
}
