using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerMainEquipPreviewController
{
    private readonly Transform root;

    private Transform equipContent;
    private Transform equipItem1;
    private Transform equipItem2;
    private Transform equipItem3;
    private Transform equipItem4;
    private Transform nowEquipContent;
    private Transform nowEquipBG;
    private Transform nowEquipName;
    private Transform nowEquipIcon;

    private TMP_Text equipItem1NameText;
    private TMP_Text equipItem2NameText;
    private TMP_Text equipItem3NameText;
    private TMP_Text equipItem4NameText;
    private TMP_Text nowEquipNameText;
    private Image nowEquipBGImage;
    private Image nowEquipIconImage;
    private int currentNowEquipIndex = -1;

    // ── 选中动画 ──────────────────────────────────────────────────
    private readonly RectTransform[] itemRects     = new RectTransform[4];
    private readonly Vector2[]       origSizes     = new Vector2[4];
    private readonly Vector2[]       origPositions = new Vector2[4];

    private int       animIndex = -1;
    private float     animTimer;
    private AnimPhase animPhase = AnimPhase.Idle;

    // 展开动画时长 / 保持时长 / 复原动画时长（单位：秒）
    private const float ExpandDuration  = 0.12f;
    private const float HoldDuration    = 0.20f;
    private const float RestoreDuration = 0.15f;

    private enum AnimPhase { Idle, Expanding, Holding, Restoring }
    // ─────────────────────────────────────────────────────────────

    public PlayerMainEquipPreviewController(Transform panelRoot)
    {
        root = panelRoot;
    }

    public void Init()
    {
        equipContent = root.Find("EquipContent");
        equipItem1 = FindEquipPreviewItem("Equpitem1", "Equipitem1", "EquipItem1");
        equipItem2 = FindEquipPreviewItem("Equpitem2", "Equipitem2", "EquipItem2");
        equipItem3 = FindEquipPreviewItem("Equpitem3", "Equipitem3", "EquipItem3");
        equipItem4 = FindEquipPreviewItem("Equpitem4", "Equipitem4", "EquipItem4");
        nowEquipContent = root.Find("NowEquipContent");
        nowEquipBG   = nowEquipContent != null ? nowEquipContent.Find("BG") : root.Find("NowEquipContent/BG");
        nowEquipName = nowEquipContent != null ? nowEquipContent.Find("EquipName") : root.Find("NowEquipContent/EquipName");
        nowEquipIcon = nowEquipContent != null ? nowEquipContent.Find("EquipIcon") : root.Find("NowEquipContent/EquipIcon");

        equipItem1NameText = GetEquipNameText(equipItem1);
        equipItem2NameText = GetEquipNameText(equipItem2);
        equipItem3NameText = GetEquipNameText(equipItem3);
        equipItem4NameText = GetEquipNameText(equipItem4);
        nowEquipNameText  = nowEquipName != null ? nowEquipName.GetComponent<TMP_Text>() : null;
        nowEquipBGImage   = nowEquipBG   != null ? nowEquipBG.GetComponent<Image>()      : null;
        nowEquipIconImage = nowEquipIcon != null ? nowEquipIcon.GetComponent<Image>()    : null;

        CacheItemRects();
        Refresh();
    }

    public void Tick()
    {
        HandleKeyInput();
        UpdateNowEquipVisibility();
        UpdateAnimation();
    }

    public void Refresh()
    {
        RefreshEquipPreviewItem(equipItem1, equipItem1NameText, "WeaponContent1");
        RefreshEquipPreviewItem(equipItem2, equipItem2NameText, "WeaponContent2");
        RefreshEquipPreviewItem(equipItem3, equipItem3NameText, "ToolContent1");
        RefreshEquipPreviewItem(equipItem4, equipItem4NameText, "ToolContent2");
        RefreshNowEquipPreview();
    }

    // ── 私有：键盘输入 ────────────────────────────────────────────

    private void HandleKeyInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetNowEquipByIndex(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) SetNowEquipByIndex(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) SetNowEquipByIndex(2);
        else if (Input.GetKeyDown(KeyCode.Alpha4)) SetNowEquipByIndex(3);
    }

    // ── 私有：装备预览刷新 ────────────────────────────────────────

    private void RefreshEquipPreviewItem(Transform itemRoot, TMP_Text nameText, string slotKey)
    {
        if (itemRoot == null)
        {
            return;
        }

        Item equippedItem = GameMgr.Equipment != null ? GameMgr.Equipment.GetEquippedItem(slotKey) : null;
        bool hasEquippedItem = equippedItem != null;

        itemRoot.gameObject.SetActive(hasEquippedItem);

        if (!hasEquippedItem)
        {
            if (nameText != null)
            {
                nameText.text = string.Empty;
            }
            return;
        }

        if (nameText != null)
        {
            nameText.text  = equippedItem.name;
            nameText.color = GetQualityColor(equippedItem.quality);
        }
    }

    private void RefreshNowEquipPreview()
    {
        Item[] equippedItems = GetNowEquipCandidates();
        if (!HasAnyNowEquipCandidate(equippedItems))
        {
            currentNowEquipIndex = -1;
            ApplyNowEquipDisplay(null);
            return;
        }

        string activeSlotKey = GameMgr.Equipment != null ? GameMgr.Equipment.GetActiveHandSlotKey() : string.Empty;
        int activeIndex = GetIndexBySlotKey(activeSlotKey);
        if (activeIndex >= 0 && activeIndex < equippedItems.Length && equippedItems[activeIndex] != null)
        {
            currentNowEquipIndex = activeIndex;
        }

        if (currentNowEquipIndex < 0 || currentNowEquipIndex >= equippedItems.Length || equippedItems[currentNowEquipIndex] == null)
        {
            currentNowEquipIndex = GetFirstAvailableEquipIndex(equippedItems);
        }

        ApplyCurrentHandSlot();
        ApplyNowEquipDisplay(equippedItems[currentNowEquipIndex]);
        UpdateNowEquipVisibility();
    }

    private void SetNowEquipByIndex(int index)
    {
        Item[] equippedItems = GetNowEquipCandidates();
        if (index < 0 || index >= equippedItems.Length || equippedItems[index] == null)
        {
            return;
        }

        currentNowEquipIndex = index;
        ApplyCurrentHandSlot();
        ApplyNowEquipDisplay(equippedItems[index]);
        StartSelectAnimation(index);
        EquipCurrentSlotIfPlayerIsUnarmed();
        UpdateNowEquipVisibility();
    }

    private Item[] GetNowEquipCandidates()
    {
        return new[]
        {
            GameMgr.Equipment != null ? GameMgr.Equipment.GetEquippedHandItem("WeaponContent1") : null,
            GameMgr.Equipment != null ? GameMgr.Equipment.GetEquippedHandItem("WeaponContent2") : null,
            GameMgr.Equipment != null ? GameMgr.Equipment.GetEquippedHandItem("ToolContent1")   : null,
            GameMgr.Equipment != null ? GameMgr.Equipment.GetEquippedHandItem("ToolContent2")   : null
        };
    }

    private void ApplyCurrentHandSlot()
    {
        if (GameMgr.Equipment == null || currentNowEquipIndex < 0)
        {
            return;
        }

        string slotKey = GetSlotKeyByIndex(currentNowEquipIndex);
        if (!string.IsNullOrEmpty(slotKey))
        {
            GameMgr.Equipment.SetActiveHandSlot(slotKey);
        }
    }

    private async void ApplyNowEquipDisplay(Item weaponItem)
    {
        bool hasItem = weaponItem != null;

        if (nowEquipNameText != null)
        {
            nowEquipNameText.gameObject.SetActive(hasItem);
            nowEquipNameText.text = hasItem ? weaponItem.name : string.Empty;
        }

        if (nowEquipIconImage != null)
        {
            nowEquipIconImage.gameObject.SetActive(hasItem);
            if (!hasItem)
            {
                nowEquipIconImage.sprite = null;
            }
            else
            {
                Sprite icon = await GameMgr.IconAtlas.GetItemIcon(weaponItem);
                if (weaponItem != null && nowEquipIconImage != null)
                {
                    nowEquipIconImage.sprite = icon;
                    nowEquipIconImage.gameObject.SetActive(icon != null);
                }
            }
        }

        if (nowEquipBGImage != null)
        {
            Color bgColor = hasItem ? GetQualityColor(weaponItem.quality) : Color.white;
            bgColor.a = hasItem ? 0.3f : 0.1f;
            nowEquipBGImage.color = bgColor;
        }

        UpdateNowEquipVisibility();
    }

    private void UpdateNowEquipVisibility()
    {
        if (nowEquipContent == null)
        {
            return;
        }

        Item activeItem = GameMgr.Equipment != null ? GameMgr.Equipment.GetActiveHandItem() : null;
        bool shouldShow = activeItem != null && IsPlayerWeaponArmed();
        if (nowEquipContent.gameObject.activeSelf != shouldShow)
        {
            nowEquipContent.gameObject.SetActive(shouldShow);
        }
    }

    private void EquipCurrentSlotIfPlayerIsUnarmed()
    {
        PlayerWeaponModeController weaponModeController = GetPlayerWeaponModeController();
        if (weaponModeController == null || weaponModeController.IsArmed || weaponModeController.IsTransitioning)
        {
            return;
        }

        weaponModeController.EquipCurrentWeapon();
    }

    private static bool IsPlayerWeaponArmed()
    {
        PlayerWeaponModeController weaponModeController = GetPlayerWeaponModeController();
        return weaponModeController == null || weaponModeController.IsArmed;
    }

    private static PlayerWeaponModeController GetPlayerWeaponModeController()
    {
        return GameMgr.Instance != null && GameMgr.Instance.Player != null
            ? GameMgr.Instance.Player.GetComponent<PlayerWeaponModeController>()
            : null;
    }

    // ── 私有：选中动画 ────────────────────────────────────────────

    private void CacheItemRects()
    {
        Transform[] items = { equipItem1, equipItem2, equipItem3, equipItem4 };
        for (int i = 0; i < 4; i++)
        {
            if (items[i] == null)
            {
                continue;
            }

            RectTransform rt = items[i].GetComponent<RectTransform>();
            itemRects[i]     = rt;
            origSizes[i]     = rt != null ? rt.sizeDelta        : Vector2.zero;
            origPositions[i] = rt != null ? rt.anchoredPosition : Vector2.zero;
        }
    }

    private void StartSelectAnimation(int index)
    {
        // 如果有另一个 Item 正在动画，立即还原它
        if (animPhase != AnimPhase.Idle && animIndex >= 0 && animIndex != index)
        {
            InstantRestore(animIndex);
        }

        animIndex = index;
        animTimer = 0f;
        animPhase = AnimPhase.Expanding;
    }

    private void UpdateAnimation()
    {
        if (animPhase == AnimPhase.Idle || animIndex < 0 || itemRects[animIndex] == null)
        {
            return;
        }

        animTimer += Time.unscaledDeltaTime;

        switch (animPhase)
        {
            case AnimPhase.Expanding:
            {
                float t = Mathf.Clamp01(animTimer / ExpandDuration);
                ApplyAnimLerp(animIndex, EaseInOut(t));
                if (animTimer >= ExpandDuration)
                {
                    ApplyAnimLerp(animIndex, 1f);
                    animPhase = AnimPhase.Holding;
                    animTimer = 0f;
                }
                break;
            }

            case AnimPhase.Holding:
            {
                if (animTimer >= HoldDuration)
                {
                    animPhase = AnimPhase.Restoring;
                    animTimer = 0f;
                }
                break;
            }

            case AnimPhase.Restoring:
            {
                float t = Mathf.Clamp01(animTimer / RestoreDuration);
                ApplyAnimLerp(animIndex, EaseInOut(1f - t));
                if (animTimer >= RestoreDuration)
                {
                    InstantRestore(animIndex);
                    animPhase = AnimPhase.Idle;
                    animIndex = -1;
                }
                break;
            }
        }
    }

    /// <summary>
    /// t=0 → 原始状态；t=1 → 选中放大+偏移状态
    /// </summary>
    private void ApplyAnimLerp(int index, float t)
    {
        RectTransform rt = itemRects[index];
        if (rt == null)
        {
            return;
        }

        Vector2 targetSize = origSizes[index] * 1.1f;
        Vector2 targetPos  = new Vector2(origPositions[index].x - 5f, origPositions[index].y);

        rt.sizeDelta        = Vector2.Lerp(origSizes[index],     targetSize, t);
        rt.anchoredPosition = Vector2.Lerp(origPositions[index], targetPos,  t);
    }

    private void InstantRestore(int index)
    {
        if (index < 0 || index >= itemRects.Length || itemRects[index] == null)
        {
            return;
        }

        itemRects[index].sizeDelta        = origSizes[index];
        itemRects[index].anchoredPosition = origPositions[index];
    }

    /// <summary>平滑三次缓入缓出 (smoothstep)</summary>
    private static float EaseInOut(float t)
    {
        return t * t * (3f - 2f * t);
    }

    // ── 私有：通用工具 ────────────────────────────────────────────

    private Transform FindEquipPreviewItem(params string[] candidateNames)
    {
        if (equipContent == null || candidateNames == null)
        {
            return null;
        }

        for (int i = 0; i < candidateNames.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(candidateNames[i]))
            {
                continue;
            }

            Transform found = equipContent.Find(candidateNames[i]);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static TMP_Text GetEquipNameText(Transform itemRoot)
    {
        if (itemRoot == null)
        {
            return null;
        }

        Transform nameTransform = itemRoot.Find("EquipName");
        return nameTransform != null ? nameTransform.GetComponent<TMP_Text>() : null;
    }

    private static bool HasAnyNowEquipCandidate(Item[] equippedItems)
    {
        for (int i = 0; i < equippedItems.Length; i++)
        {
            if (equippedItems[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetFirstAvailableEquipIndex(Item[] equippedItems)
    {
        for (int i = 0; i < equippedItems.Length; i++)
        {
            if (equippedItems[i] != null)
            {
                return i;
            }
        }

        return -1;
    }

    private static string GetSlotKeyByIndex(int index)
    {
        return index switch
        {
            0 => "WeaponContent1",
            1 => "WeaponContent2",
            2 => "ToolContent1",
            3 => "ToolContent2",
            _ => string.Empty
        };
    }

    private static int GetIndexBySlotKey(string slotKey)
    {
        return slotKey switch
        {
            "WeaponContent1" => 0,
            "WeaponContent2" => 1,
            "ToolContent1" => 2,
            "ToolContent2" => 3,
            _ => -1
        };
    }

    private static Color GetQualityColor(ItemQuality quality)
    {
        return quality switch
        {
            ItemQuality.Common    => Color.white,
            ItemQuality.Advanced  => new Color(0.20f, 0.80f, 0.30f, 1f),
            ItemQuality.Rare      => new Color(0.25f, 0.55f, 1f,    1f),
            ItemQuality.Epic      => new Color(0.70f, 0.35f, 0.95f, 1f),
            ItemQuality.Legendary => new Color(1f,    0.58f, 0.15f, 1f),
            _                     => Color.white
        };
    }
}
