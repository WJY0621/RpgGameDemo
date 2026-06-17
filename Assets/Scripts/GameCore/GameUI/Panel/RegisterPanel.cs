using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RegisterPanel : BasePanel
{
    private const float ShakeDuration = 0.28f;
    private const float ShakeAmplitude = 8f;
    private const float ShakeFrequency = 44f;

    private RectTransform accountFieldRect;
    private RectTransform passwordFieldRect;
    private RectTransform rePasswordFieldRect;
    private TMP_InputField tmpAccountField;
    private TMP_InputField tmpPasswordField;
    private TMP_InputField tmpRePasswordField;
    private TMP_Text tmpTipText;
    private InputField legacyAccountField;
    private InputField legacyPasswordField;
    private InputField legacyRePasswordField;
    private Text legacyTipText;
    private Button registerButton;
    private Button closeButton;
    private bool initialized;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    public override void Init()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        Transform accountTransform = FindChild("AccountField");
        Transform passwordTransform = FindChild("PasswordField");
        Transform rePasswordTransform = FindChild("RePasswordField");
        Transform tipTextTransform = FindChild("TipText");

        accountFieldRect = accountTransform != null ? accountTransform.GetComponent<RectTransform>() : null;
        passwordFieldRect = passwordTransform != null ? passwordTransform.GetComponent<RectTransform>() : null;
        rePasswordFieldRect = rePasswordTransform != null ? rePasswordTransform.GetComponent<RectTransform>() : null;
        tmpAccountField = accountTransform != null ? accountTransform.GetComponent<TMP_InputField>() : null;
        tmpPasswordField = passwordTransform != null ? passwordTransform.GetComponent<TMP_InputField>() : null;
        tmpRePasswordField = rePasswordTransform != null ? rePasswordTransform.GetComponent<TMP_InputField>() : null;
        tmpTipText = tipTextTransform != null ? tipTextTransform.GetComponent<TMP_Text>() : null;
        legacyAccountField = accountTransform != null ? accountTransform.GetComponent<InputField>() : null;
        legacyPasswordField = passwordTransform != null ? passwordTransform.GetComponent<InputField>() : null;
        legacyRePasswordField = rePasswordTransform != null ? rePasswordTransform.GetComponent<InputField>() : null;
        legacyTipText = tipTextTransform != null ? tipTextTransform.GetComponent<Text>() : null;

        registerButton = GetButton(FindChild("RegisterButton"));
        closeButton = GetButton(FindChild("CloseButton"));

        BindButton(registerButton, OnClickRegister);
        BindButton(closeButton, Close);
        SetupPasswordVisibleToggle(passwordTransform, tmpPasswordField, legacyPasswordField);
        SetupPasswordVisibleToggle(rePasswordTransform, tmpRePasswordField, legacyRePasswordField);
        SetTipText(string.Empty);
    }

    public override void Show()
    {
        base.Show();
        SetTipText(string.Empty);
    }

    private void OnClickRegister()
    {
        string account = GetInputText(tmpAccountField, legacyAccountField);
        string password = GetInputText(tmpPasswordField, legacyPasswordField);
        string rePassword = GetInputText(tmpRePasswordField, legacyRePasswordField);

        if (string.IsNullOrWhiteSpace(account))
        {
            ShowFieldError("请输入注册账号", accountFieldRect);
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowFieldError("请输入注册密码", passwordFieldRect);
            return;
        }

        if (string.IsNullOrWhiteSpace(rePassword))
        {
            ShowFieldError("请再次输入密码", rePasswordFieldRect);
            return;
        }

        if (password != rePassword)
        {
            ShowFieldError("两次输入的密码不一致", rePasswordFieldRect);
            return;
        }

        AccountProfile profile = GameMgr.Account?.Register(account, password);
        if (profile == null)
        {
            ShowFieldError(GameMgr.Account?.LastError ?? "注册失败", accountFieldRect);
            return;
        }

        SetTipText(string.Empty);
        GameMgr.Social?.RefreshFriends();
        GameMgr.Social?.RefreshIncomingFriendRequests();
        GameMgr.Message?.RegisterMessage($"注册成功，ID: {profile.playerId}", priority: MessagePriority.Medium);
        Close();
        GameMgr.UI?.HidePanel<LoginPanel>();
        TryEnterRoleSelectAsync().Forget();
    }

    private async UniTaskVoid TryEnterRoleSelectAsync()
    {
        if (GameMgr.UI != null && GameMgr.UI.IsShow<GameStartPanel>())
        {
            await GameMgr.UI.SwitchPanelAsync<GameStartPanel, ChooseRolePanel>();
        }
    }

    private void Close()
    {
        GameMgr.UI?.HidePanel<RegisterPanel>();
    }

    private void SetupPasswordVisibleToggle(Transform fieldTransform, TMP_InputField tmpInput, InputField legacyInput)
    {
        if (fieldTransform == null)
        {
            return;
        }

        Transform visibleTransform = fieldTransform.Find("VisibleImage");
        Transform invisibleTransform = fieldTransform.Find("UnVisibleImage");
        Button visibleButton = EnsureButton(visibleTransform);
        Button invisibleButton = EnsureButton(invisibleTransform);

        SetPasswordVisible(tmpInput, legacyInput, visibleTransform, invisibleTransform, false);
        BindButton(visibleButton, () => SetPasswordVisible(tmpInput, legacyInput, visibleTransform, invisibleTransform, false));
        BindButton(invisibleButton, () => SetPasswordVisible(tmpInput, legacyInput, visibleTransform, invisibleTransform, true));
    }

    private void SetPasswordVisible(
        TMP_InputField tmpInput,
        InputField legacyInput,
        Transform visibleTransform,
        Transform invisibleTransform,
        bool visible)
    {
        if (tmpInput != null)
        {
            tmpInput.contentType = visible ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
            tmpInput.ForceLabelUpdate();
        }

        if (legacyInput != null)
        {
            legacyInput.contentType = visible ? InputField.ContentType.Standard : InputField.ContentType.Password;
            legacyInput.ForceLabelUpdate();
        }

        if (visibleTransform != null)
        {
            visibleTransform.gameObject.SetActive(visible);
        }

        if (invisibleTransform != null)
        {
            invisibleTransform.gameObject.SetActive(!visible);
        }
    }

    private void ShowFieldError(string message, RectTransform fieldRect)
    {
        SetTipText(message);
        ShakeFieldAsync(fieldRect).Forget();
    }

    private void SetTipText(string content)
    {
        string safeContent = content ?? string.Empty;
        bool shouldShow = !string.IsNullOrWhiteSpace(safeContent);
        if (tmpTipText != null)
        {
            tmpTipText.text = safeContent;
            tmpTipText.gameObject.SetActive(shouldShow);
        }

        if (legacyTipText != null)
        {
            legacyTipText.text = safeContent;
            legacyTipText.gameObject.SetActive(shouldShow);
        }
    }

    private async UniTaskVoid ShakeFieldAsync(RectTransform target)
    {
        if (target == null)
        {
            return;
        }

        Vector2 originalPosition = target.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < ShakeDuration && target != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float offset = Mathf.Sin(elapsed * ShakeFrequency) * ShakeAmplitude * (1f - elapsed / ShakeDuration);
            target.anchoredPosition = originalPosition + new Vector2(offset, 0f);
            await UniTask.Yield();
        }

        if (target != null)
        {
            target.anchoredPosition = originalPosition;
        }
    }

    private Transform FindChild(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == childName)
            {
                return children[i];
            }
        }

        return null;
    }

    private static Button GetButton(Transform target)
    {
        return target != null ? target.GetComponent<Button>() : null;
    }

    private static Button EnsureButton(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        Button button = target.GetComponent<Button>();
        if (button == null)
        {
            button = target.gameObject.AddComponent<Button>();
        }

        Image image = target.GetComponent<Image>();
        if (image != null)
        {
            button.targetGraphic = image;
            image.raycastTarget = true;
        }

        return button;
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static string GetInputText(TMP_InputField tmpInput, InputField legacyInput)
    {
        if (tmpInput != null)
        {
            return tmpInput.text;
        }

        return legacyInput != null ? legacyInput.text : string.Empty;
    }
}
