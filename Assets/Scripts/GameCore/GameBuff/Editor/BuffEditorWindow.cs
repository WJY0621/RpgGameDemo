#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UIElements;

public class BuffEditorWindow : EditorWindow
{
    private const string DefaultDatabaseAssetPath = "Assets/Resources/Buff/BuffDatabase.asset";
    private const string DatabasePathPrefKey = "BuffEditor.DatabasePath";
    private const string BuffAtlasName = "BuffAtlas";

    private static readonly Dictionary<BuffBehavior, string> BehaviorLabels = new Dictionary<BuffBehavior, string>
    {
        { BuffBehavior.BoostATK,         "提升攻击力" },
        { BuffBehavior.BoostDEF,         "提升防御力" },
        { BuffBehavior.BoostATKPercent,  "提升攻击力百分比" },
        { BuffBehavior.BoostDEFPercent,  "提升防御力百分比" },
        { BuffBehavior.BoostHP,          "提升生命上限" },
        { BuffBehavior.DecreaseATK,      "降低攻击力" },
        { BuffBehavior.DecreaseDEF,      "降低防御力" },
        { BuffBehavior.BoostMoveSpeed,   "提升移动速度" },
        { BuffBehavior.BoostAttackSpeed, "提升攻击速度" },
        { BuffBehavior.DamageOverTime,   "持续伤害 (DoT)" },
        { BuffBehavior.HealOverTime,     "持续治疗 (HoT)" }
    };

    private BuffDatabaseSO database;
    private SerializedObject databaseSO;
    private SerializedProperty buffsProperty;

    private ListView buffListView;
    private VisualElement detailPane;
    private Label statusLabel;
    private ToolbarSearchField searchField;
    private string currentSearch = string.Empty;
    private readonly List<int> filteredIndices = new List<int>();

    private List<string> cachedAtlasSpriteNames;

    [MenuItem("Tools/Game Buff/Buff Editor")]
    public static void Open()
    {
        BuffEditorWindow window = GetWindow<BuffEditorWindow>("Buff Editor");
        window.minSize = new Vector2(720, 480);
    }

    private void OnEnable()
    {
        TryRestoreDatabase();
        BuildUI();
    }

    private void OnDisable()
    {
        SaveIfDirty();
    }

    private void TryRestoreDatabase()
    {
        string savedPath = EditorPrefs.GetString(DatabasePathPrefKey, DefaultDatabaseAssetPath);
        BuffDatabaseSO loaded = AssetDatabase.LoadAssetAtPath<BuffDatabaseSO>(savedPath);
        if (loaded == null && savedPath != DefaultDatabaseAssetPath)
        {
            loaded = AssetDatabase.LoadAssetAtPath<BuffDatabaseSO>(DefaultDatabaseAssetPath);
        }
        if (loaded != null)
        {
            database = loaded;
            EditorPrefs.SetString(DatabasePathPrefKey, AssetDatabase.GetAssetPath(database));
            RebuildSerializedObject();
        }
    }

    private void RebuildSerializedObject()
    {
        if (database == null)
        {
            databaseSO = null;
            buffsProperty = null;
            return;
        }
        databaseSO = new SerializedObject(database);
        buffsProperty = databaseSO.FindProperty("buffs");
    }

    private void BuildUI()
    {
        rootVisualElement.Clear();
        rootVisualElement.style.flexDirection = FlexDirection.Column;
        BuildToolbar();
        BuildContent();
        UpdateStatus();
    }

    private void BuildToolbar()
    {
        Toolbar toolbar = new Toolbar();

        toolbar.Add(new ToolbarButton(SaveDatabase) { text = "Save" });
        toolbar.Add(new ToolbarButton(ReloadDatabase) { text = "Reload" });
        toolbar.Add(MakeSpacer(8));
        toolbar.Add(new ToolbarButton(AddBuff) { text = "+ Add" });
        toolbar.Add(new ToolbarButton(DuplicateSelectedBuff) { text = "Copy" });
        toolbar.Add(new ToolbarButton(DeleteSelectedBuff) { text = "- Delete" });
        toolbar.Add(MakeSpacer(8));

        ObjectField dbField = new ObjectField("Database")
        {
            objectType = typeof(BuffDatabaseSO),
            value = database
        };
        dbField.style.flexGrow = 1;
        dbField.style.minWidth = 240;
        dbField.RegisterValueChangedCallback(evt => SetDatabase(evt.newValue as BuffDatabaseSO));
        toolbar.Add(dbField);

        toolbar.Add(new ToolbarButton(CreateNewDatabase) { text = "Create New" });
        toolbar.Add(new ToolbarButton(RefreshAtlasCache) { text = "Refresh Icons" });

        rootVisualElement.Add(toolbar);
    }

    private static VisualElement MakeSpacer(float width)
    {
        return new VisualElement { style = { width = width } };
    }

    private void BuildContent()
    {
        TwoPaneSplitView split = new TwoPaneSplitView(0, 260, TwoPaneSplitViewOrientation.Horizontal);
        split.style.flexGrow = 1;

        VisualElement leftPane = new VisualElement { style = { flexDirection = FlexDirection.Column } };

        searchField = new ToolbarSearchField();
        searchField.style.marginLeft = 4;
        searchField.style.marginRight = 4;
        searchField.style.marginTop = 4;
        searchField.style.maxWidth = 240;
        searchField.style.alignSelf = Align.FlexStart;
        searchField.RegisterValueChangedCallback(evt =>
        {
            currentSearch = evt.newValue ?? string.Empty;
            RefreshList();
        });
        leftPane.Add(searchField);

        buffListView = new ListView
        {
            fixedItemHeight = 22,
            selectionType = SelectionType.Single,
            showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly
        };
        buffListView.style.flexGrow = 1;
        buffListView.style.marginTop = 4;
        buffListView.makeItem = () =>
        {
            Label label = new Label();
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.marginLeft = 8;
            return label;
        };
        buffListView.bindItem = (element, idx) =>
        {
            BuffData buff = GetFilteredBuff(idx);
            ((Label)element).text = buff != null ? $"#{buff.buffID}  {buff.buffName}" : "(null)";
        };
        buffListView.selectionChanged += _ => RefreshDetail();
        leftPane.Add(buffListView);

        statusLabel = new Label
        {
            style = {
                marginLeft = 8,
                marginTop = 4,
                marginBottom = 4,
                color = new StyleColor(new Color(0.65f, 0.65f, 0.65f))
            }
        };
        leftPane.Add(statusLabel);

        ScrollView right = new ScrollView { style = { flexGrow = 1 } };
        detailPane = new VisualElement
        {
            style = { paddingLeft = 12, paddingRight = 12, paddingTop = 8, paddingBottom = 12 }
        };
        right.Add(detailPane);

        split.Add(leftPane);
        split.Add(right);
        rootVisualElement.Add(split);

        RefreshList();
    }

    private void RefreshList()
    {
        filteredIndices.Clear();
        if (database != null && database.buffs != null)
        {
            string search = currentSearch?.Trim().ToLowerInvariant() ?? string.Empty;
            for (int i = 0; i < database.buffs.Count; i++)
            {
                BuffData b = database.buffs[i];
                if (b == null) continue;

                if (search.Length == 0 ||
                    (b.buffName ?? string.Empty).ToLowerInvariant().Contains(search) ||
                    b.buffID.ToString().Contains(search))
                {
                    filteredIndices.Add(i);
                }
            }
        }

        if (buffListView != null)
        {
            buffListView.itemsSource = filteredIndices;
            buffListView.RefreshItems();
        }
        UpdateStatus();
        RefreshDetail();
    }

    private BuffData GetFilteredBuff(int filteredIdx)
    {
        if (database == null || filteredIdx < 0 || filteredIdx >= filteredIndices.Count) return null;
        int realIdx = filteredIndices[filteredIdx];
        if (realIdx < 0 || realIdx >= database.buffs.Count) return null;
        return database.buffs[realIdx];
    }

    private int GetSelectedRealIndex()
    {
        if (buffListView == null) return -1;
        int idx = buffListView.selectedIndex;
        if (idx < 0 || idx >= filteredIndices.Count) return -1;
        return filteredIndices[idx];
    }

    private void RefreshDetail()
    {
        if (detailPane == null) return;
        detailPane.Clear();

        if (database == null)
        {
            detailPane.Add(new HelpBox("没有 BuffDatabase。点击工具栏 Create New 新建，或在 Project 里拖一个到 Database 字段。",
                HelpBoxMessageType.Info));
            return;
        }

        int realIdx = GetSelectedRealIndex();
        if (realIdx < 0)
        {
            detailPane.Add(new HelpBox("从左侧选择一个 Buff，或点击 + Add 新增。", HelpBoxMessageType.Info));
            return;
        }

        databaseSO.Update();
        SerializedProperty element = buffsProperty.GetArrayElementAtIndex(realIdx);
        BuffData buff = database.buffs[realIdx];

        detailPane.Add(MakeSection("基本信息"));
        AddPropertyField(detailPane, element, "buffID", "Buff ID");
        AddPropertyField(detailPane, element, "buffName", "名称");
        AddIconPicker(detailPane, element.FindPropertyRelative("iconName"));
        AddPropertyField(detailPane, element, "description", "描述");

        detailPane.Add(MakeSection("行为"));
        AddBehaviorDropdown(detailPane, element.FindPropertyRelative("behavior"));
        AddValueField(detailPane, element.FindPropertyRelative("value"), buff.behavior);
        AddPropertyField(detailPane, element, "duration", "持续秒数 (≤0 永久)");
        if (buff.IsPeriodic)
        {
            AddPropertyField(detailPane, element, "tickInterval", "Tick 间隔 (秒)");
        }

        detailPane.Add(MakeSection("预览"));
        Label preview = new Label
        {
            text = BuildPreview(buff),
            style = {
                whiteSpace = WhiteSpace.Normal,
                color = new StyleColor(new Color(0.7f, 0.85f, 1f)),
                marginTop = 2,
                marginBottom = 8
            }
        };
        detailPane.Add(preview);

        detailPane.Bind(databaseSO);
    }

    private void AddBehaviorDropdown(VisualElement parent, SerializedProperty behaviorProp)
    {
        if (behaviorProp == null) return;

        List<string> labels = new List<string>();
        foreach (var pair in BehaviorLabels)
        {
            labels.Add(pair.Value);
        }

        BuffBehavior currentBehavior = (BuffBehavior)behaviorProp.enumValueIndex;
        string currentLabel = BehaviorLabels.TryGetValue(currentBehavior, out string lab) ? lab : labels[0];

        DropdownField dropdown = new DropdownField("行为类型", labels, currentLabel);
        dropdown.RegisterValueChangedCallback(evt =>
        {
            int idx = labels.IndexOf(evt.newValue);
            if (idx < 0) return;
            BuffBehavior chosen = GetBehaviorFromLabelIndex(idx);
            behaviorProp.enumValueIndex = (int)chosen;
            behaviorProp.serializedObject.ApplyModifiedProperties();
            RefreshDetail();
        });
        parent.Add(dropdown);
    }

    private static BuffBehavior GetBehaviorFromLabelIndex(int idx)
    {
        int i = 0;
        foreach (var pair in BehaviorLabels)
        {
            if (i == idx) return pair.Key;
            i++;
        }
        return BuffBehavior.BoostATK;
    }

    private void AddValueField(VisualElement parent, SerializedProperty valueProp, BuffBehavior behavior)
    {
        if (valueProp == null) return;

        string label = GetValueLabel(behavior);
        bool isInt = IsIntBehavior(behavior);

        if (isInt)
        {
            int rounded = Mathf.RoundToInt(valueProp.floatValue);
            if (!Mathf.Approximately(valueProp.floatValue, rounded))
            {
                // 底层 float 之前可能被写入过小数，进编辑器时一次性规整
                valueProp.floatValue = rounded;
                valueProp.serializedObject.ApplyModifiedProperties();
            }

            IntegerField field = new IntegerField(label) { value = rounded };
            field.RegisterValueChangedCallback(evt =>
            {
                valueProp.floatValue = evt.newValue;
                valueProp.serializedObject.ApplyModifiedProperties();
                MarkDirtyAndRefreshPreview();
            });
            parent.Add(field);
        }
        else
        {
            FloatField field = new FloatField(label) { value = valueProp.floatValue };
            field.RegisterValueChangedCallback(evt =>
            {
                valueProp.floatValue = evt.newValue;
                valueProp.serializedObject.ApplyModifiedProperties();
                MarkDirtyAndRefreshPreview();
            });
            parent.Add(field);
        }
    }

    private void MarkDirtyAndRefreshPreview()
    {
        if (database != null) EditorUtility.SetDirty(database);
    }

    private static string GetValueLabel(BuffBehavior behavior)
    {
        switch (behavior)
        {
            case BuffBehavior.BoostATK: return "攻击力 +";
            case BuffBehavior.BoostDEF: return "防御力 +";
            case BuffBehavior.BoostATKPercent: return "攻击力 (%)";
            case BuffBehavior.BoostDEFPercent: return "防御力 (%)";
            case BuffBehavior.BoostHP:  return "生命上限 +";
            case BuffBehavior.DecreaseATK: return "攻击力 - (填正数)";
            case BuffBehavior.DecreaseDEF: return "防御力 - (填正数)";
            case BuffBehavior.BoostMoveSpeed:   return "移动速度 (%)";
            case BuffBehavior.BoostAttackSpeed: return "攻击速度 (%)";
            case BuffBehavior.DamageOverTime: return "每 Tick 伤害";
            case BuffBehavior.HealOverTime:   return "每 Tick 回血";
        }
        return "数值";
    }

    private static bool IsIntBehavior(BuffBehavior behavior) => BuffFormatter.IsIntBehavior(behavior);

    private void AddIconPicker(VisualElement parent, SerializedProperty iconNameProp)
    {
        if (iconNameProp == null) return;

        List<string> spriteNames = GetAtlasSpriteNames();

        if (spriteNames.Count == 0)
        {
            HelpBox warn = new HelpBox(
                $"未找到 SpriteAtlas '{BuffAtlasName}'。先在 Project 里建一个名为 BuffAtlas 的 SpriteAtlas，把 buff 图片打进去后点工具栏 Refresh Icons。",
                HelpBoxMessageType.Warning);
            parent.Add(warn);

            TextField fallback = new TextField("图标资源名 (手动填)") { value = iconNameProp.stringValue };
            fallback.RegisterValueChangedCallback(evt =>
            {
                iconNameProp.stringValue = evt.newValue;
                iconNameProp.serializedObject.ApplyModifiedProperties();
                MarkDirtyAndRefreshPreview();
            });
            parent.Add(fallback);
            return;
        }

        // 把空选项加到第一个，便于"清空图标"
        List<string> choices = new List<string>(spriteNames.Count + 1);
        choices.Add("(无)");
        choices.AddRange(spriteNames);

        string currentValue = iconNameProp.stringValue;
        string display = string.IsNullOrEmpty(currentValue) ? "(无)" :
            (choices.Contains(currentValue) ? currentValue : currentValue + "  (图集中未找到)");
        if (!choices.Contains(display))
        {
            choices.Insert(1, display);
        }

        DropdownField dropdown = new DropdownField("图标 (BuffAtlas)", choices, display);
        dropdown.style.maxWidth = 360;
        dropdown.RegisterValueChangedCallback(evt =>
        {
            string val = evt.newValue == "(无)" ? string.Empty : evt.newValue.Replace("  (图集中未找到)", string.Empty);
            iconNameProp.stringValue = val;
            iconNameProp.serializedObject.ApplyModifiedProperties();
            MarkDirtyAndRefreshPreview();
        });
        parent.Add(dropdown);
    }

    private List<string> GetAtlasSpriteNames()
    {
        if (cachedAtlasSpriteNames != null)
        {
            return cachedAtlasSpriteNames;
        }

        cachedAtlasSpriteNames = new List<string>();

        string[] guids = AssetDatabase.FindAssets($"t:SpriteAtlas {BuffAtlasName}");
        SpriteAtlas atlas = null;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (Path.GetFileNameWithoutExtension(path) == BuffAtlasName)
            {
                atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                break;
            }
        }

        if (atlas == null)
        {
            return cachedAtlasSpriteNames;
        }

        int count = atlas.spriteCount;
        Sprite[] sprites = new Sprite[count];
        atlas.GetSprites(sprites);
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite s = sprites[i];
            if (s == null) continue;
            string name = s.name.Replace("(Clone)", string.Empty);
            if (!cachedAtlasSpriteNames.Contains(name))
            {
                cachedAtlasSpriteNames.Add(name);
            }
        }
        cachedAtlasSpriteNames.Sort();
        return cachedAtlasSpriteNames;
    }

    private void RefreshAtlasCache()
    {
        cachedAtlasSpriteNames = null;
        RefreshDetail();
        Debug.Log("[BuffEditor] Refreshed BuffAtlas icon cache.");
    }

    private static void AddPropertyField(VisualElement parent, SerializedProperty parentProp, string relativePath, string label)
    {
        SerializedProperty prop = parentProp.FindPropertyRelative(relativePath);
        if (prop == null) return;
        PropertyField field = new PropertyField(prop, label);
        parent.Add(field);
    }

    private static Label MakeSection(string text)
    {
        return new Label(text)
        {
            style = {
                marginTop = 10,
                marginBottom = 4,
                unityFontStyleAndWeight = FontStyle.Bold,
                color = new StyleColor(new Color(0.85f, 0.85f, 0.85f))
            }
        };
    }

    private static string BuildPreview(BuffData b) => BuffFormatter.GetSummary(b, includeRawSeconds: true);

    private void UpdateStatus()
    {
        if (statusLabel == null) return;
        if (database == null)
        {
            statusLabel.text = "(未选择 Database)";
            return;
        }
        int total = database.buffs?.Count ?? 0;
        int shown = filteredIndices.Count;
        statusLabel.text = total == shown ? $"共 {total} 个 Buff" : $"显示 {shown} / 共 {total}";
    }

    private void AddBuff()
    {
        if (!EnsureDatabase()) return;

        Undo.RecordObject(database, "Add Buff");
        BuffData newBuff = new BuffData
        {
            buffID = GetNextBuffID(),
            buffName = "New Buff",
            behavior = BuffBehavior.BoostATK,
            duration = 10f,
            value = 0
        };
        database.buffs.Add(newBuff);
        EditorUtility.SetDirty(database);
        RebuildSerializedObject();
        RefreshList();
        SelectByRealIndex(database.buffs.Count - 1);
    }

    private int GetNextBuffID()
    {
        int max = 1000;
        if (database?.buffs != null)
        {
            for (int i = 0; i < database.buffs.Count; i++)
            {
                BuffData b = database.buffs[i];
                if (b != null && b.buffID > max) max = b.buffID;
            }
        }
        return max + 1;
    }

    private void DuplicateSelectedBuff()
    {
        if (!EnsureDatabase()) return;
        int realIdx = GetSelectedRealIndex();
        if (realIdx < 0) return;
        BuffData src = database.buffs[realIdx];
        if (src == null) return;

        Undo.RecordObject(database, "Duplicate Buff");
        BuffData copy = JsonUtility.FromJson<BuffData>(JsonUtility.ToJson(src));
        copy.buffID = GetNextBuffID();
        copy.buffName = src.buffName + " (Copy)";
        database.buffs.Add(copy);
        EditorUtility.SetDirty(database);
        RebuildSerializedObject();
        RefreshList();
        SelectByRealIndex(database.buffs.Count - 1);
    }

    private void DeleteSelectedBuff()
    {
        if (!EnsureDatabase()) return;
        int realIdx = GetSelectedRealIndex();
        if (realIdx < 0) return;

        BuffData buff = database.buffs[realIdx];
        if (!EditorUtility.DisplayDialog("删除 Buff",
                $"确定删除 #{buff.buffID} {buff.buffName} 吗？", "删除", "取消"))
        {
            return;
        }

        Undo.RecordObject(database, "Delete Buff");
        database.buffs.RemoveAt(realIdx);
        EditorUtility.SetDirty(database);
        RebuildSerializedObject();
        RefreshList();
    }

    private void SelectByRealIndex(int realIdx)
    {
        for (int i = 0; i < filteredIndices.Count; i++)
        {
            if (filteredIndices[i] == realIdx)
            {
                buffListView.selectedIndex = i;
                return;
            }
        }
    }

    private void SaveDatabase()
    {
        if (database == null) return;
        databaseSO?.ApplyModifiedProperties();
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Debug.Log("[BuffEditor] Saved BuffDatabase.");
    }

    private void SaveIfDirty()
    {
        if (database != null && EditorUtility.IsDirty(database))
        {
            AssetDatabase.SaveAssets();
        }
    }

    private void ReloadDatabase()
    {
        if (database == null) return;
        AssetDatabase.Refresh();
        databaseSO?.Update();
        cachedAtlasSpriteNames = null;
        RefreshList();
    }

    private void SetDatabase(BuffDatabaseSO db)
    {
        database = db;
        if (db != null)
        {
            EditorPrefs.SetString(DatabasePathPrefKey, AssetDatabase.GetAssetPath(db));
            RebuildSerializedObject();
        }
        else
        {
            databaseSO = null;
            buffsProperty = null;
        }
        RefreshList();
    }

    private bool EnsureDatabase()
    {
        if (database == null)
        {
            EditorUtility.DisplayDialog("没有数据库", "请先选择或创建一个 BuffDatabase。", "OK");
            return false;
        }
        return true;
    }

    private void CreateNewDatabase()
    {
        string dir = Path.GetDirectoryName(DefaultDatabaseAssetPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(dir))
        {
            CreateFoldersRecursive(dir);
        }

        string path = AssetDatabase.GenerateUniqueAssetPath(DefaultDatabaseAssetPath);
        BuffDatabaseSO newDB = ScriptableObject.CreateInstance<BuffDatabaseSO>();
        AssetDatabase.CreateAsset(newDB, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        SetDatabase(newDB);
        BuildUI();
        Debug.Log($"[BuffEditor] Created new database at {path}");
    }

    private static void CreateFoldersRecursive(string folderPath)
    {
        folderPath = folderPath.Replace('\\', '/');
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string parent = current;
            current = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(current))
            {
                AssetDatabase.CreateFolder(parent, parts[i]);
            }
        }
    }
}
#endif
