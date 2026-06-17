using UnityEngine;
using UnityEngine.UI;

public class MonsterHPUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MonsterHealth targetHealth;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Transform followTarget;

    [Header("Display")]
    [SerializeField] private float offsetY = 2f;
    [SerializeField] private bool useRendererBounds = true;
    [SerializeField] private float extraHeadOffset = 0.2f;
    [SerializeField] private float uiScale = 0.01f;
    [SerializeField] private bool hideWhenDead = true;
    [SerializeField] private bool alignSliderToCanvasCenter = true;
    [SerializeField] private bool hiddenOnStart = true;
    [SerializeField] private float visibleDurationAfterHit = 2f;
    [SerializeField] private float fadeDuration = 0.35f;

    private Camera mainCamera;
    private Renderer[] cachedRenderers;
    private CanvasGroup canvasGroup;
    private float visibleTimer;
    private bool isFading;

    private void Awake()
    {
        if (hpSlider == null)
        {
            hpSlider = GetComponentInChildren<Slider>(true);
        }

        if (targetHealth == null)
        {
            targetHealth = GetComponentInParent<MonsterHealth>();
        }

        if (targetCanvas == null)
        {
            targetCanvas = GetComponentInChildren<Canvas>(true);
        }

        if (targetCanvas == null)
        {
            targetCanvas = GetComponentInParent<Canvas>(true);
        }

        ResolveFollowTarget();

        if (targetHealth != null)
        {
            cachedRenderers = targetHealth.GetComponentsInChildren<Renderer>(true);
        }

        mainCamera = Camera.main;
        SetupCanvas();
        AlignSliderRect();

        if (hpSlider != null)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = 1f;
        }

        RefreshUI();
        UpdateWorldPosition();

        if (hiddenOnStart)
        {
            SetVisible(false, immediate: true);
        }
    }

    private void LateUpdate()
    {
        UpdateWorldPosition();
        UpdateFadeState();
    }

    private void OnEnable()
    {
        BindTarget(targetHealth);
        RefreshUI();
    }

    private void OnDisable()
    {
        UnbindTarget(targetHealth);
    }

    public void SetTarget(MonsterHealth health)
    {
        if (targetHealth == health)
        {
            RefreshUI();
            return;
        }

        UnbindTarget(targetHealth);
        targetHealth = health;
        BindTarget(targetHealth);
        ResolveFollowTarget();
        cachedRenderers = targetHealth != null ? targetHealth.GetComponentsInChildren<Renderer>(true) : null;
        RefreshUI();
        UpdateWorldPosition();
    }

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
        UpdateWorldPosition();
    }

    private void ResolveFollowTarget()
    {
        if (followTarget != null)
        {
            return;
        }

        if (targetHealth == null)
        {
            return;
        }

        Transform hpPoint = FindChildRecursive(targetHealth.transform, "HPPoint");
        if (hpPoint != null)
        {
            followTarget = hpPoint;
            useRendererBounds = false;
            return;
        }

        followTarget = targetHealth.transform;
    }

    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent == null)
        {
            return null;
        }

        foreach (Transform child in parent)
        {
            if (child.name == targetName)
            {
                return child;
            }

            Transform result = FindChildRecursive(child, targetName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private void SetupCanvas()
    {
        if (targetCanvas == null)
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        targetCanvas.renderMode = RenderMode.WorldSpace;
        targetCanvas.worldCamera = mainCamera;
        targetCanvas.transform.localScale = Vector3.one * uiScale;
        targetCanvas.transform.localRotation = Quaternion.identity;

        RectTransform rectTransform = targetCanvas.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        if (targetCanvas.GetComponent<UIBillboard>() == null)
        {
            targetCanvas.gameObject.AddComponent<UIBillboard>();
        }

        canvasGroup = targetCanvas.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = targetCanvas.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void AlignSliderRect()
    {
        if (!alignSliderToCanvasCenter || hpSlider == null || targetCanvas == null)
        {
            return;
        }

        RectTransform sliderRect = hpSlider.GetComponent<RectTransform>();
        RectTransform canvasRect = targetCanvas.GetComponent<RectTransform>();
        if (sliderRect == null || canvasRect == null)
        {
            return;
        }

        sliderRect.SetParent(canvasRect, false);
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = Vector2.zero;
        sliderRect.localRotation = Quaternion.identity;
        sliderRect.localScale = Vector3.one;
    }

    private void UpdateWorldPosition()
    {
        if (targetCanvas == null || followTarget == null)
        {
            return;
        }

        targetCanvas.transform.position = GetHeadPosition();
    }

    private Vector3 GetHeadPosition()
    {
        if (!useRendererBounds || cachedRenderers == null || cachedRenderers.Length == 0)
        {
            return followTarget.position + Vector3.up * offsetY;
        }

        Bounds bounds = cachedRenderers[0].bounds;
        for (int i = 1; i < cachedRenderers.Length; i++)
        {
            bounds.Encapsulate(cachedRenderers[i].bounds);
        }

        Vector3 headPosition = bounds.center;
        headPosition.y = bounds.max.y + extraHeadOffset;
        return headPosition;
    }

    private void RefreshUI()
    {
        if (hpSlider == null)
        {
            return;
        }

        if (targetHealth == null)
        {
            hpSlider.SetValueWithoutNotify(0f);
            return;
        }

        hpSlider.SetValueWithoutNotify(targetHealth.NormalizedHP);

        if (hideWhenDead && targetHealth.IsDead)
        {
            SetVisible(false, immediate: true);
        }
    }

    private void BindTarget(MonsterHealth health)
    {
        if (health == null)
        {
            return;
        }

        health.OnHealthChanged -= HandleHealthChanged;
        health.OnHealthChanged += HandleHealthChanged;
        health.OnDamaged -= HandleDamaged;
        health.OnDamaged += HandleDamaged;
        health.OnDied -= HandleDied;
        health.OnDied += HandleDied;
    }

    private void UnbindTarget(MonsterHealth health)
    {
        if (health == null)
        {
            return;
        }

        health.OnHealthChanged -= HandleHealthChanged;
        health.OnDamaged -= HandleDamaged;
        health.OnDied -= HandleDied;
    }

    private void HandleHealthChanged(MonsterHealth health)
    {
        RefreshUI();
    }

    private void HandleDamaged(MonsterHealth health, AttackDamageInfo damageInfo, int actualDamage)
    {
        RefreshUI();
        SetVisible(true, immediate: true);
        visibleTimer = visibleDurationAfterHit;
        isFading = false;
    }

    private void HandleDied(MonsterHealth health)
    {
        if (hideWhenDead)
        {
            SetVisible(false, immediate: true);
        }
    }

    private void UpdateFadeState()
    {
        if (canvasGroup == null || targetHealth == null || targetHealth.IsDead)
        {
            return;
        }

        if (visibleTimer > 0f)
        {
            visibleTimer -= Time.deltaTime;
            if (visibleTimer <= 0f)
            {
                isFading = true;
            }
            return;
        }

        if (!isFading)
        {
            return;
        }

        if (fadeDuration <= 0f)
        {
            SetVisible(false, immediate: true);
            isFading = false;
            return;
        }

        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 0f, Time.deltaTime / fadeDuration);
        if (canvasGroup.alpha <= 0.001f)
        {
            SetVisible(false, immediate: true);
            isFading = false;
        }
    }

    private void SetVisible(bool visible, bool immediate)
    {
        if (targetCanvas == null || canvasGroup == null)
        {
            return;
        }

        targetCanvas.gameObject.SetActive(true);
        canvasGroup.alpha = visible ? 1f : 0f;

        if (!visible && immediate)
        {
            visibleTimer = 0f;
        }
    }
}
