using System.Collections.Generic;
using UnityEngine;

public class BuildPreview
{
    private readonly List<Renderer> renderers = new List<Renderer>();
    private readonly List<BuildSnapPoint> anchors = new List<BuildSnapPoint>();
    private Material validMaterial;
    private Material invalidMaterial;
    private GameObject previewObject;
    private bool? lastValidState;
    private bool? lastVisibleState;

    public Transform Transform => previewObject != null ? previewObject.transform : null;
    public bool HasPreview => previewObject != null;

    public IReadOnlyList<Collider> Colliders { get; private set; } = new List<Collider>();
    public IReadOnlyList<BuildSnapPoint> Anchors => anchors;

    public void Create(BuildRecipeSO recipe, Material valid, Material invalid)
    {
        Dispose();

        validMaterial = valid != null ? valid : CreatePreviewMaterial(new Color(0.2f, 0.55f, 1f, 0.28f));
        invalidMaterial = invalid != null ? invalid : CreatePreviewMaterial(new Color(1f, 0.18f, 0.12f, 0.32f));

        GameObject source = recipe != null && recipe.previewPrefab != null ? recipe.previewPrefab : recipe?.buildPrefab;
        previewObject = source != null ? Object.Instantiate(source) : BuildFallbackVisual(recipe);
        previewObject.name = recipe != null ? $"{recipe.displayName}_Preview" : "BuildPreview";
        previewObject.SetActive(true);

        if (recipe != null && source == null)
        {
            previewObject.transform.localScale = recipe.visualScale;
        }

        Collider[] colliders = previewObject.GetComponentsInChildren<Collider>();
        Colliders = colliders;
        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }

        anchors.Clear();
        foreach (BuildSnapPoint snapPoint in previewObject.GetComponentsInChildren<BuildSnapPoint>(true))
        {
            if (snapPoint != null && snapPoint.IsAnchor)
            {
                anchors.Add(snapPoint);
            }

            snapPoint.enabled = false;
        }

        foreach (Rigidbody rigidbody in previewObject.GetComponentsInChildren<Rigidbody>())
        {
            rigidbody.isKinematic = true;
            rigidbody.detectCollisions = false;
        }

        renderers.Clear();
        renderers.AddRange(previewObject.GetComponentsInChildren<Renderer>(true));
        lastValidState = null;
        lastVisibleState = null;
        SetValid(true);
    }

    public void SetPose(Vector3 position, Quaternion rotation)
    {
        if (previewObject == null)
        {
            return;
        }

        previewObject.transform.SetPositionAndRotation(position, rotation);
    }

    public void SetVisible(bool visible)
    {
        if (previewObject == null)
        {
            return;
        }

        if (lastVisibleState.HasValue && lastVisibleState.Value == visible)
        {
            return;
        }

        previewObject.SetActive(visible);
        lastVisibleState = visible;
    }

    public void SetValid(bool valid)
    {
        if (lastValidState.HasValue && lastValidState.Value == valid)
        {
            return;
        }

        Material material = valid ? validMaterial : invalidMaterial;
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].sharedMaterial = material;
            }
        }

        lastValidState = valid;
    }

    public void Dispose()
    {
        if (previewObject != null)
        {
            Object.Destroy(previewObject);
            previewObject = null;
        }

        renderers.Clear();
        anchors.Clear();
        Colliders = System.Array.Empty<Collider>();
        lastValidState = null;
        lastVisibleState = null;
    }

    internal static Material CreatePreviewMaterial(Color color)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader);
        SetMaterialColor(material, color);
        ConfigureTransparentMaterial(material);
        return material;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 3f);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasProperty("_AlphaClip"))
        {
            material.SetFloat("_AlphaClip", 0f);
        }

        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
    }

    private static GameObject BuildFallbackVisual(BuildRecipeSO recipe)
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(root.GetComponent<Collider>());
        root.transform.localScale = recipe != null ? recipe.visualScale : Vector3.one;
        return root;
    }
}
