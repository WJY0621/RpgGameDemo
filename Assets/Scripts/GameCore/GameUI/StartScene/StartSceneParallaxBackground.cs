using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StartSceneParallaxBackground : MonoBehaviour
{
    [SerializeField] private RectTransform referenceArea;
    [SerializeField] private List<ParallaxLayer> layers = new List<ParallaxLayer>();
    [SerializeField] private float globalParallaxStrength = 1f;
    [SerializeField] private float globalFollowSpeedMultiplier = 1f;
    [SerializeField] private RectTransform cursorLight;
    [SerializeField] private float cursorLightFollowSpeed = 8f;
    [SerializeField] private float cursorLightMaxOffset = 260f;
    [SerializeField] private bool hideCursorLightWhenMouseLeaves = true;

    private readonly Dictionary<RectTransform, Vector2> basePositions = new Dictionary<RectTransform, Vector2>();
    private readonly Dictionary<RectTransform, Vector2> baseSizeDeltas = new Dictionary<RectTransform, Vector2>();
    private Vector2 smoothedPointer;
    private bool hasPointer;

    private RectTransform ReferenceRect => referenceArea != null ? referenceArea : transform as RectTransform;

    private void Awake()
    {
        CacheBasePositions();
        if (cursorLight != null)
        {
            cursorLight.gameObject.SetActive(!hideCursorLightWhenMouseLeaves);
        }
    }

    private void OnEnable()
    {
        CacheBasePositions();
    }

    private void LateUpdate()
    {
        RectTransform rect = ReferenceRect;
        if (rect == null)
        {
            return;
        }

        bool pointerInside = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rect,
            Input.mousePosition,
            null,
            out Vector2 localPointer);

        Vector2 normalizedPointer = NormalizePointer(rect.rect, localPointer);
        if (!hasPointer)
        {
            smoothedPointer = normalizedPointer;
            hasPointer = true;
        }
        else
        {
            smoothedPointer = Vector2.Lerp(smoothedPointer, normalizedPointer, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 6f));
        }

        ApplyLayerOffsets(smoothedPointer);
        ApplyCursorLight(pointerInside, localPointer);
    }

    private void OnValidate()
    {
        cursorLightFollowSpeed = Mathf.Max(0.01f, cursorLightFollowSpeed);
        cursorLightMaxOffset = Mathf.Max(0f, cursorLightMaxOffset);
        globalParallaxStrength = Mathf.Max(0f, globalParallaxStrength);
        globalFollowSpeedMultiplier = Mathf.Max(0.01f, globalFollowSpeedMultiplier);
    }

    private void CacheBasePositions()
    {
        basePositions.Clear();
        baseSizeDeltas.Clear();
        for (int i = 0; i < layers.Count; i++)
        {
            ParallaxLayer layer = layers[i];
            RectTransform target = layer != null ? layer.target : null;
            if (target != null && !basePositions.ContainsKey(target))
            {
                basePositions.Add(target, target.anchoredPosition);
                baseSizeDeltas.Add(target, target.sizeDelta);
                ApplyOverscan(target, layer);
            }
        }

        if (cursorLight != null && !basePositions.ContainsKey(cursorLight))
        {
            basePositions.Add(cursorLight, cursorLight.anchoredPosition);
            baseSizeDeltas.Add(cursorLight, cursorLight.sizeDelta);
        }
    }

    private void ApplyLayerOffsets(Vector2 pointer)
    {
        for (int i = 0; i < layers.Count; i++)
        {
            ParallaxLayer layer = layers[i];
            RectTransform target = layer != null ? layer.target : null;
            if (target == null)
            {
                continue;
            }

            if (!basePositions.TryGetValue(target, out Vector2 basePosition))
            {
                basePosition = target.anchoredPosition;
                basePositions[target] = basePosition;
                baseSizeDeltas[target] = target.sizeDelta;
                ApplyOverscan(target, layer);
            }

            Vector2 desired = basePosition + new Vector2(
                pointer.x * layer.maxOffset.x * globalParallaxStrength,
                pointer.y * layer.maxOffset.y * globalParallaxStrength);

            target.anchoredPosition = Vector2.Lerp(
                target.anchoredPosition,
                desired,
                1f - Mathf.Exp(-Time.unscaledDeltaTime * Mathf.Max(0.01f, layer.followSpeed * globalFollowSpeedMultiplier)));
        }
    }

    private void ApplyCursorLight(bool pointerInside, Vector2 localPointer)
    {
        if (cursorLight == null)
        {
            return;
        }

        bool shouldShow = pointerInside || !hideCursorLightWhenMouseLeaves;
        if (cursorLight.gameObject.activeSelf != shouldShow)
        {
            cursorLight.gameObject.SetActive(shouldShow);
        }

        if (!shouldShow)
        {
            return;
        }

        Vector2 clampedPointer = Vector2.ClampMagnitude(localPointer, cursorLightMaxOffset);
        cursorLight.anchoredPosition = Vector2.Lerp(
            cursorLight.anchoredPosition,
            clampedPointer,
            1f - Mathf.Exp(-Time.unscaledDeltaTime * cursorLightFollowSpeed));

        Image image = cursorLight.GetComponent<Image>();
        if (image != null)
        {
            Color color = image.color;
            color.a = Mathf.Lerp(color.a, pointerInside ? 1f : 0f, 1f - Mathf.Exp(-Time.unscaledDeltaTime * cursorLightFollowSpeed));
            image.color = color;
        }
    }

    private static Vector2 NormalizePointer(Rect rect, Vector2 localPointer)
    {
        float x = rect.width > 0f ? Mathf.Clamp(localPointer.x / (rect.width * 0.5f), -1f, 1f) : 0f;
        float y = rect.height > 0f ? Mathf.Clamp(localPointer.y / (rect.height * 0.5f), -1f, 1f) : 0f;
        return new Vector2(x, y);
    }

    private void ApplyOverscan(RectTransform target, ParallaxLayer layer)
    {
        if (target == null || layer == null || !layer.autoOverscan)
        {
            return;
        }

        if (!baseSizeDeltas.TryGetValue(target, out Vector2 baseSizeDelta))
        {
            baseSizeDelta = target.sizeDelta;
            baseSizeDeltas[target] = baseSizeDelta;
        }

        Vector2 movementPadding = new Vector2(
            Mathf.Abs(layer.maxOffset.x) * globalParallaxStrength * 2f * layer.autoOverscanMultiplier,
            Mathf.Abs(layer.maxOffset.y) * globalParallaxStrength * 2f * layer.autoOverscanMultiplier);
        target.sizeDelta = baseSizeDelta + movementPadding + layer.overscanPadding;
    }
}

[Serializable]
public class ParallaxLayer
{
    public RectTransform target;
    public Vector2 maxOffset = new Vector2(12f, 6f);
    public float followSpeed = 5f;
    public bool autoOverscan = true;
    [Range(0f, 1f)] public float autoOverscanMultiplier = 1f;
    public Vector2 overscanPadding = new Vector2(96f, 54f);
}
