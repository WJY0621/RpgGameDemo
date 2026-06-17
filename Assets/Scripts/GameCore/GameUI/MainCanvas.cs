using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class MainCanvas : MonoBehaviour
{
    private bool startFade;
    [SerializeField] private bool coverScreenOnAwake = true;
    public CanvasGroup BlackPanel;
    public float fadeTime = 2f;
    public Transform panelsParent;
    public static MainCanvas instance;
    private const float DefaultTransitionFadeTime = 0.18f;
    private const string RuntimeTransitionPanelName = "RuntimeTransitionBlackPanel";
    private CanvasGroup transitionBlackPanel;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            GameObject.DontDestroyOnLoad(gameObject);
            if (coverScreenOnAwake)
            {
                ShowBlackScreenImmediate();
            }
            return;
        }
        GameObject.Destroy(gameObject);
    }
    private void Start()
    {
        
    }

    private void Update()
    {
        CanvasGroup fadePanel = transitionBlackPanel != null ? transitionBlackPanel : BlackPanel;
        if (startFade && fadePanel != null && fadePanel.alpha > 0)
        {
            fadePanel.alpha -= Time.unscaledDeltaTime / fadeTime;
            if (fadePanel.alpha <= 0)
            {
                fadePanel.alpha = 0;
                fadePanel.blocksRaycasts = false;
                startFade = false;
            }
        }
    }

    public void StartFade()
    {
        startFade = true;
    }

    public void PrepareFade()
    {
        CanvasGroup panel = EnsureTransitionBlackPanel();
        startFade = false;
        panel.alpha = 1;
        panel.blocksRaycasts = true;
    }

    public void ShowBlackScreenImmediate()
    {
        CanvasGroup panel = EnsureTransitionBlackPanel();
        startFade = false;
        panel.alpha = 1;
        panel.blocksRaycasts = true;
    }

    public void HideBlackScreenImmediate()
    {
        CanvasGroup panel = EnsureTransitionBlackPanel();
        startFade = false;
        panel.alpha = 0;
        panel.blocksRaycasts = false;
    }

    public async UniTask FadeToBlackAsync(float duration = DefaultTransitionFadeTime)
    {
        CanvasGroup panel = EnsureTransitionBlackPanel();
        startFade = false;
        panel.blocksRaycasts = true;
        await FadeBlackPanelAsync(panel, 1f, duration);
    }

    public async UniTask FadeFromBlackAsync(float duration = DefaultTransitionFadeTime)
    {
        CanvasGroup panel = EnsureTransitionBlackPanel();
        startFade = false;
        await FadeBlackPanelAsync(panel, 0f, duration);
        panel.blocksRaycasts = false;
    }

    private async UniTask FadeBlackPanelAsync(CanvasGroup panel, float targetAlpha, float duration)
    {
        float startAlpha = panel.alpha;
        float safeDuration = Mathf.Max(0.01f, duration);
        float timer = 0f;

        while (timer < safeDuration)
        {
            timer += Time.unscaledDeltaTime;
            panel.alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / safeDuration);
            await UniTask.Yield();
        }

        panel.alpha = targetAlpha;
    }

    private CanvasGroup EnsureTransitionBlackPanel()
    {
        if (transitionBlackPanel == null)
        {
            Transform existing = transform.Find(RuntimeTransitionPanelName);
            if (existing != null)
            {
                transitionBlackPanel = existing.GetComponent<CanvasGroup>();
            }
        }

        if (transitionBlackPanel == null)
        {
            GameObject panelObject = new GameObject(RuntimeTransitionPanelName, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panelObject.transform.SetParent(transform, false);
            transitionBlackPanel = panelObject.GetComponent<CanvasGroup>();
        }

        GameObject gameObject = transitionBlackPanel.gameObject;
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;

        Image image = gameObject.GetComponent<Image>();
        if (image == null)
        {
            image = gameObject.AddComponent<Image>();
        }

        image.color = Color.black;
        image.raycastTarget = true;

        transitionBlackPanel.transform.SetAsLastSibling();
        return transitionBlackPanel;
    }
}
