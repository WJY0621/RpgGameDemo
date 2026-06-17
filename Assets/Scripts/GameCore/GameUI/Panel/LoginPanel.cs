using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginPanel : BasePanel
{
    private const float ShakeDuration = 0.28f;
    private const float ShakeAmplitude = 8f;
    private const float ShakeFrequency = 44f;

    private RectTransform accountFieldRect;
    private RectTransform passwordFieldRect;
    private TMP_InputField tmpAccountField;
    private TMP_InputField tmpPasswordField;
    private TMP_Text tmpTipText;
    private InputField legacyAccountField;
    private InputField legacyPasswordField;
    private Text legacyTipText;
    private Button loginButton;
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
        Transform tipTextTransform = FindChild("TipText");

        accountFieldRect = accountTransform != null ? accountTransform.GetComponent<RectTransform>() : null;
        passwordFieldRect = passwordTransform != null ? passwordTransform.GetComponent<RectTransform>() : null;
        tmpAccountField = accountTransform != null ? accountTransform.GetComponent<TMP_InputField>() : null;
        tmpPasswordField = passwordTransform != null ? passwordTransform.GetComponent<TMP_InputField>() : null;
        tmpTipText = tipTextTransform != null ? tipTextTransform.GetComponent<TMP_Text>() : null;
        legacyAccountField = accountTransform != null ? accountTransform.GetComponent<InputField>() : null;
        legacyPasswordField = passwordTransform != null ? passwordTransform.GetComponent<InputField>() : null;
        legacyTipText = tipTextTransform != null ? tipTextTransform.GetComponent<Text>() : null;

        loginButton = GetButton(FindChild("LoginButton"));
        registerButton = GetButton(FindChild("RegisterButton"));
        closeButton = GetButton(FindChild("CloseButton"));

        BindButton(loginButton, OnClickLogin);
        BindButton(registerButton, OnClickRegister);
        BindButton(closeButton, Close);
        SetupPasswordVisibleToggle(passwordTransform, tmpPasswordField, legacyPasswordField);
        SetTipText(string.Empty);
    }

    public override void Show()
    {
        base.Show();
        SetTipText(string.Empty);
    }

    private void OnClickLogin()
    {
        string account = GetInputText(tmpAccountField, legacyAccountField);
        string password = GetInputText(tmpPasswordField, legacyPasswordField);
        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(password))
        {
            SetTipText("请输入账号和密码");
            ShakeFieldAsync(string.IsNullOrWhiteSpace(account) ? accountFieldRect : passwordFieldRect).Forget();
            return;
        }

        AccountProfile profile = GameMgr.Account?.Login(account, password);
        if (profile == null)
        {
            string error = GameMgr.Account?.LastError;
            if (string.IsNullOrWhiteSpace(error))
            {
                error = "登录失败";
            }

            SetTipText(error);
            RectTransform target = error == "账户不存在" ? accountFieldRect : passwordFieldRect;
            ShakeFieldAsync(target).Forget();
            return;
        }

        SetTipText(string.Empty);
        GameMgr.Social?.RefreshFriends();
        GameMgr.Social?.RefreshIncomingFriendRequests();
        GameMgr.Message?.RegisterMessage($"登录成功，ID: {profile.playerId}", priority: MessagePriority.Medium);
        Close();
        TryEnterRoleSelectAsync().Forget();
    }

    private void OnClickRegister()
    {
        OpenRegisterPanelAsync().Forget();
    }

    private async UniTaskVoid OpenRegisterPanelAsync()
    {
        if (GameMgr.UI == null)
        {
            return;
        }

        await GameMgr.UI.ShowPanel<RegisterPanel>();
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
        GameMgr.UI?.HidePanel<LoginPanel>();
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
