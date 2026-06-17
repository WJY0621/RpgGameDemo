using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Floating damage number. It can play on a screen-space overlay canvas so 3D
/// monster meshes cannot hide it with depth testing.
/// </summary>
[DisallowMultipleComponent]
public class DamageNumber : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Phase 1 - Pop")]
    [Min(0.01f)] [SerializeField] private float popDuration = 0.10f;
    [SerializeField] private float popHeight = 0.4f;
    [SerializeField] private float popOvershoot = 1.3f;
    [SerializeField] private float startScale = 0.5f;

    [Header("Phase 2 - Hold")]
    [Min(0f)] [SerializeField] private float holdDuration = 0.5f;

    [Header("Phase 3 - Fade")]
    [Min(0.01f)] [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private float fadeDriftHeight = 0.1f;

    [Header("Overlay")]
    [SerializeField] private float overlayPixelsPerWorldUnit = 100f;

    [Header("Scatter")]
    [SerializeField] private float horizontalScatter = 0.12f;

    private RectTransform rectTransform;
    private Canvas overlayCanvas;
    private Canvas ownCanvas;
    private Camera mainCamera;
    private Coroutine playRoutine;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    public void PrepareForOverlay(Canvas canvas)
    {
        ResolveReferences();
        overlayCanvas = canvas;

        if (overlayCanvas == null)
            return;

        if (rectTransform != null)
        {
            rectTransform.SetParent(overlayCanvas.transform, false);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
        }

        if (ownCanvas != null && ownCanvas != overlayCanvas)
        {
            ownCanvas.enabled = true;
            ownCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ownCanvas.worldCamera = null;
            ownCanvas.overrideSorting = true;
            ownCanvas.sortingOrder = short.MaxValue;
        }

        UIBillboard billboard = GetComponent<UIBillboard>();
        if (billboard != null)
            billboard.enabled = false;

        NormalizeChildRects(transform);
        SetRaycastTargets(false);
    }

    public void Play(int damage, Vector3 worldPos)
    {
        ResolveReferences();

        if (damageText != null)
            damageText.text = damage.ToString();

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        if (!TryPlaceOnOverlay(worldPos))
            FinishPlayback();
        else
            playRoutine = StartCoroutine(PlayOverlayRoutine());
    }

    private void ResolveReferences()
    {
        if (damageText == null)
            damageText = GetComponentInChildren<TMP_Text>(true);

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        if (ownCanvas == null)
            ownCanvas = GetComponent<Canvas>();

        if (damageText != null)
            damageText.raycastTarget = false;
    }

    private void NormalizeChildRects(Transform root)
    {
        RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i] != rectTransform)
                rects[i].localScale = Vector3.one;
        }
    }

    private void SetRaycastTargets(bool raycastTarget)
    {
        CanvasRenderer[] renderers = GetComponentsInChildren<CanvasRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].cullTransparentMesh = false;
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null)
                texts[i].raycastTarget = raycastTarget;
        }
    }

    private bool TryPlaceOnOverlay(Vector3 worldPos)
    {
        if (overlayCanvas == null || rectTransform == null)
            return false;

        mainCamera = ResolveWorldCamera();
        if (mainCamera == null)
            return false;

        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
        if (screenPos.z <= 0f)
            return false;

        float scatterPixels = horizontalScatter * overlayPixelsPerWorldUnit;
        screenPos += new Vector3(Random.Range(-scatterPixels, scatterPixels), Random.Range(-scatterPixels * 0.35f, scatterPixels * 0.35f), 0f);

        rectTransform.position = screenPos;
        rectTransform.localScale = Vector3.one * startScale;
        rectTransform.localRotation = Quaternion.identity;
        return true;
    }

    private Camera ResolveWorldCamera()
    {
        if (IsUsableWorldCamera(mainCamera))
            return mainCamera;

        Camera taggedMainCamera = Camera.main;
        if (IsUsableWorldCamera(taggedMainCamera))
            return taggedMainCamera;

        Camera bestCamera = null;
        float bestDepth = float.NegativeInfinity;
        int uiLayer = LayerMask.NameToLayer("UI");
        int uiOnlyMask = uiLayer >= 0 ? 1 << uiLayer : 0;

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (!IsUsableWorldCamera(camera))
                continue;

            if (uiOnlyMask != 0 && camera.cullingMask == uiOnlyMask)
                continue;

            if (camera.depth >= bestDepth)
            {
                bestDepth = camera.depth;
                bestCamera = camera;
            }
        }

        return bestCamera;
    }

    private bool IsUsableWorldCamera(Camera camera)
    {
        return camera != null &&
               camera.isActiveAndEnabled &&
               camera.gameObject.activeInHierarchy &&
               camera.targetTexture == null;
    }

    private IEnumerator PlayOverlayRoutine()
    {
        Vector3 startPos = rectTransform.position;
        Vector3 holdPos = startPos + Vector3.up * (popHeight * overlayPixelsPerWorldUnit);

        yield return PlayScaleAndMove(
            popDuration,
            t => rectTransform.position = Vector3.LerpUnclamped(startPos, holdPos, EaseOutQuad(t))
        );

        rectTransform.position = holdPos;
        rectTransform.localScale = Vector3.one;

        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);

        Vector3 endPos = holdPos + Vector3.up * (fadeDriftHeight * overlayPixelsPerWorldUnit);
        yield return PlayFade(
            fadeDuration,
            t => rectTransform.position = Vector3.Lerp(holdPos, endPos, t)
        );

        FinishPlayback();
    }

    private IEnumerator PlayScaleAndMove(float duration, System.Action<float> move)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            move?.Invoke(t);

            float s = t < 0.6f
                ? Mathf.Lerp(startScale, popOvershoot, t / 0.6f)
                : Mathf.Lerp(popOvershoot, 1f, (t - 0.6f) / 0.4f);
            transform.localScale = new Vector3(s, s, s);

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator PlayFade(float duration, System.Action<float> move)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            move?.Invoke(t);
            if (canvasGroup != null)
                canvasGroup.alpha = 1f - t;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    private void FinishPlayback()
    {
        playRoutine = null;
        DamageNumberSpawner.Return(this);
    }
}
