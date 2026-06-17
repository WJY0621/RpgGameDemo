using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RoleInfoPanel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const string HoverSoundName = "UI_Click2";
    private const string ClickSoundName = "UI_Hover2";

    private Transform uiTimeText;
    private Transform uiNameText;
    private Transform uiHpText;
    private Transform uiGoldText;
    private Transform uiDeleteButton;
    private Transform uiSelectIcon;
    private Transform uiHighLightIcon;
    private Transform uiRoleIcon;

    private GameFile gameFile;
    private bool isSelected;
    private int iconLoadVersion;

    public Action<RoleInfoPanel> onSelectedChanged;
    public Action<RoleInfoPanel> onDeleted;

    private void Awake()
    {
        InitUI();
    }

    private void InitUI()
    {
        uiTimeText = transform.Find("TimeText");
        uiNameText = transform.Find("NameText");
        uiHpText = transform.Find("HpText");
        uiGoldText = transform.Find("GoldText");
        uiDeleteButton = transform.Find("DeleteButton");
        uiSelectIcon = transform.Find("SelectIcon");
        uiHighLightIcon = transform.Find("HighLightIcon");
        uiRoleIcon = transform.Find("RoleIcon");

        ConfigureRoleIconOverlay();
        SetSelectIconVisible(false);
        SetHighLightIconVisible(false);

        if (uiDeleteButton != null)
        {
            AddClickEvent(uiDeleteButton.gameObject, OnDeleteButtonClick);
        }

        AddClickEvent(gameObject, OnPanelClicked);
    }

    private static void AddClickEvent(GameObject target, Action callback)
    {
        if (target == null)
        {
            return;
        }

        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = target.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerClick
        };
        entry.callback.AddListener(_ => callback?.Invoke());
        trigger.triggers.Add(entry);
    }

    private void OnPanelClicked()
    {
        PlayClickSound();
        onSelectedChanged?.Invoke(this);
    }

    public async void SetData(GameFile gameFile, string roleName, string createTime)
    {
        this.gameFile = gameFile;
        int loadVersion = ++iconLoadVersion;
        ClearRoleIcon();

        SetTMPText(uiNameText, roleName);
        SetTMPText(uiTimeText, createTime);

        int maxHp = gameFile != null && gameFile.playerData != null ? gameFile.playerData.GetMaxHP() : 0;
        int currentGold = gameFile != null ? gameFile.gold : 0;

        SetTMPText(uiHpText, maxHp.ToString());
        SetTMPText(uiGoldText, currentGold.ToString());

        await LoadRoleIcon(loadVersion, gameFile);
    }

    private async Task LoadRoleIcon(int loadVersion, GameFile sourceFile)
    {
        if (gameFile == null || string.IsNullOrEmpty(gameFile.roleModelName) || uiRoleIcon == null)
        {
            return;
        }

        string modelName = gameFile.roleModelName;
        bool isMale = modelName.Contains("_Man_");
        int roleID = 1;

        string[] parts = modelName.Split('_');
        if (parts.Length >= 3 && int.TryParse(parts[parts.Length - 1], out int id))
        {
            roleID = id;
        }

        string sexPrefix = isMale ? "Man" : "Women";
        string iconName = $"Role_{sexPrefix}_Icon_{roleID:D2}";

        Sprite iconSprite = await GameMgr.IconAtlas.GetRoleIcon(iconName);
        if (loadVersion != iconLoadVersion || gameFile != sourceFile || this == null || uiRoleIcon == null)
        {
            return;
        }

        if (iconSprite == null)
        {
            return;
        }

        Image roleIconImage = uiRoleIcon.GetComponent<Image>();
        if (roleIconImage != null)
        {
            ApplyRoleIconSprite(roleIconImage, iconSprite);
        }
    }

    private void ClearRoleIcon()
    {
        if (uiRoleIcon == null)
        {
            return;
        }

        Image roleIconImage = uiRoleIcon.GetComponent<Image>();
        if (roleIconImage == null)
        {
            return;
        }

        roleIconImage.sprite = null;
        roleIconImage.overrideSprite = null;
        roleIconImage.enabled = false;
        roleIconImage.SetAllDirty();
    }

    private void ConfigureRoleIconOverlay()
    {
        if (uiRoleIcon == null)
        {
            return;
        }

        Image[] images = uiRoleIcon.GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            image.raycastTarget = false;
            image.canvasRenderer.cullTransparentMesh = false;
            image.SetAllDirty();
        }
    }

    private static void ApplyRoleIconSprite(Image image, Sprite sprite)
    {
        image.sprite = sprite;
        image.overrideSprite = sprite;
        image.color = Color.white;
        image.material = null;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.canvasRenderer.cullTransparentMesh = false;
        image.enabled = sprite != null;
        image.SetAllDirty();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        SetSelectIconVisible(selected);
        SetHighLightIconVisible(false);
    }

    public bool IsSelected => isSelected;

    public GameFile GetGameFile() => gameFile;

    private void OnDestroy()
    {
        iconLoadVersion++;
        onSelectedChanged = null;
        onDeleted = null;
    }

    private void SetSelectIconVisible(bool visible)
    {
        if (uiSelectIcon != null)
        {
            uiSelectIcon.gameObject.SetActive(visible);
        }
    }

    private void SetHighLightIconVisible(bool visible)
    {
        if (uiHighLightIcon == null)
        {
            return;
        }

        uiHighLightIcon.gameObject.SetActive(!isSelected && visible);
    }

    private async void OnDeleteButtonClick()
    {
        PlayClickSound();
        await GameMgr.UI.ShowPanel<TipPanel>();
        TipPanel tipPanel = GameMgr.UI.GetPanelWithoutLoad<TipPanel>();
        if (tipPanel == null)
        {
            return;
        }

        await tipPanel.ShowTip("是否要删除此存档？", () =>
        {
            if (gameFile != null && GameMgr.File.gameFileData != null)
            {
                GameMgr.File.gameFileData.gameFiles.Remove(gameFile);
                GameMgr.File.SaveGameFile();
            }

            onDeleted?.Invoke(this);
            Destroy(gameObject);
        }, () => { });
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        GameMgr.Audio?.PlayUIEffect(HoverSoundName);
        SetHighLightIconVisible(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHighLightIconVisible(false);
    }

    private static void SetTMPText(Transform target, string content)
    {
        if (target == null)
        {
            return;
        }

        TMP_Text textComp = target.GetComponent<TMP_Text>();
        if (textComp != null)
        {
            textComp.text = content;
        }
    }

    private static void PlayClickSound()
    {
        GameMgr.Audio?.PlayUIEffect(ClickSoundName);
    }
}
