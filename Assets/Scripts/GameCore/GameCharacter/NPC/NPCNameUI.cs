using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class NPCNameUI : MonoBehaviour
{
    [Header("Name UI")]
    [SerializeField] private GameObject nameUIPrefab;
    [SerializeField] private Transform nameUIRoot;

    [Header("Display")]
    [SerializeField] private float nameOffsetY = 1.5f;
    [SerializeField] private float uiScale = 0.01f;
    [SerializeField] private float showDistance = 20f;

    private GameObject nameUIInstance;
    private TextMeshProUGUI nameText;
    private CanvasGroup canvasGroup;
    private Transform playerTransform;
    private Camera mainCamera;
    private NPCDataComponent npcDataComponent;

    private void Awake()
    {
        npcDataComponent = GetComponent<NPCDataComponent>();
    }

    private void Start()
    {
        CacheRuntimeReferences();
        CreateNameUI();
        RefreshName();
    }

    private void LateUpdate()
    {
        if (nameUIInstance == null)
        {
            return;
        }

        CacheRuntimeReferences();

        if (playerTransform == null)
        {
            SetVisible(false);
            return;
        }

        bool shouldShow = Vector3.Distance(transform.position, playerTransform.position) <= showDistance;
        SetVisible(shouldShow);
    }

    private void OnValidate()
    {
        ApplyPlacement();
    }

    public void RefreshName()
    {
        if (nameText == null)
        {
            return;
        }

        string displayName = npcDataComponent != null ? npcDataComponent.NPCName : string.Empty;
        nameText.text = string.IsNullOrWhiteSpace(displayName) ? "NPC" : displayName;
    }

    private void CacheRuntimeReferences()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (playerTransform == null && GameMgr.Instance != null && GameMgr.Instance.Player != null)
        {
            playerTransform = GameMgr.Instance.Player.transform;
        }
    }

    private void CreateNameUI()
    {
        if (nameUIInstance != null)
        {
            return;
        }

        Transform parent = nameUIRoot != null ? nameUIRoot : transform;
        GameObject sourcePrefab = nameUIPrefab;

        if (sourcePrefab != null)
        {
            nameUIInstance = Instantiate(sourcePrefab, parent);
        }
        else
        {
            nameUIInstance = CreateFallbackCanvas();
            nameUIInstance.transform.SetParent(parent, false);
        }

        nameText = nameUIInstance.GetComponentInChildren<TextMeshProUGUI>(true);

        Canvas canvas = nameUIInstance.GetComponentInChildren<Canvas>(true);
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = mainCamera;

            if (canvas.GetComponent<UIBillboard>() == null)
            {
                canvas.gameObject.AddComponent<UIBillboard>();
            }
        }

        canvasGroup = nameUIInstance.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = nameUIInstance.AddComponent<CanvasGroup>();
        }

        ApplyPlacement();
        SetVisible(false);
    }

    private void ApplyPlacement()
    {
        if (nameUIInstance == null)
        {
            return;
        }

        Transform target = nameUIInstance.GetComponentInChildren<Canvas>(true)?.transform ?? nameUIInstance.transform;
        target.localPosition = new Vector3(0f, nameOffsetY, 0f);
        target.localScale = Vector3.one * uiScale;
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
        }
    }

    private GameObject CreateFallbackCanvas()
    {
        GameObject canvasGO = new GameObject("NPC_NameCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = mainCamera;

        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(200f, 40f);

        GameObject textGO = new GameObject("NPCName");
        textGO.transform.SetParent(canvasGO.transform, false);

        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = false;
        tmp.fontSize = 10f;
        tmp.color = Color.white;
        tmp.text = "NPC";

        return canvasGO;
    }
}
