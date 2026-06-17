# Coding Rules

本文件总结当前项目的编码和协作约定。它描述“现有项目倾向”，不是新的强制大重构计划。

## General Style

- C# 使用 4 空格缩进，见 `.editorconfig`。
- 大括号独立成行。
- 优先使用现有命名和目录结构，不要为单个小功能引入全新架构。
- 修改范围保持小而清晰；避免顺手重排、格式化整个文件或批量改注释编码。
- 当前源码中部分类/注释含中文，后续可以写中文注释，但只在复杂逻辑需要解释时添加。

## Unity Patterns

- 全局入口是 `GameMgr : MonoSingleton<GameMgr>`，不要轻易新增另一个全局根对象。
- 非 MonoBehaviour 的服务类通常由 `GameMgr.Awake` 创建并通过静态属性暴露。
- 生命周期相关逻辑放在 Unity 组件里；纯数据/业务状态放在普通 C# 类里。
- 场景对象、Prefab、ScriptableObject 的移动/重命名要保护 `.meta` 和 GUID 引用。
- 涉及场景、Prefab、Animator、Addressables 的改动，不能只依赖 `dotnet build` 判断成功。

## Async And Loading

- 异步流程优先使用 UniTask，跟随 `GameMgr.Start`、`LoadingMgr`、`UIMgr` 的写法。
- Addressables 资源加载优先走 `GameMgr.AssetLoader` 或已有 Manager，不要散落重复缓存逻辑。
- 场景切换优先走 `GameMgr.Scene.LoadSceneAsync`、`PrepareSceneAsync`、`ActivatePreparedSceneAsync`。
- Loading 流程中不要绕过 `SceneControllerBase` 注册机制。

## UI Rules

- 新面板脚本继承 `BasePanel` 并实现 `Init()`。
- 面板显示优先使用 `await GameMgr.UI.ShowPanel<TPanel>()`。
- 面板 Prefab 通常要求 Addressables key 与脚本类名一致，例如 `PackagePanel`。
- 不要在多个地方手动 Instantiate 同一个 UI 面板，避免绕过 `UIMgr` 缓存。
- 如果面板需要阻塞式切换或黑屏过渡，优先使用 `UIMgr.SwitchPanelAsync`、`ShowPanelWithBlackAsync` 或已有淡入淡出接口。

## Data And Save Rules

- 背包数据由 `PackageMgr` 管，保存快照是 `InventorySaveData`。
- 任务数据由 `TaskManager` 管，保存快照是 `TaskSystemSaveData`。
- 统一存档入口是 `GameMgr.File.SaveGameFile()`。
- Editor 下存档位于 `Assets/Save/gameSaveData.sav`。不要在普通功能任务里清空或改写真实存档。
- 新增物品/怪物/配方数据时，优先跟随现有 JSON、Excel 转换工具或 ScriptableObject 数据资产。

## Player And Combat Rules

- 玩家输入统一从 `GameMgr.input.Data` 进入 `PlayerStateDriver`。
- 玩家移动、跳跃、飞行、攻击状态优先接入 `PlayerContext` 和 HSM 状态，而不是在外部组件里直接抢 CharacterController。
- 远端联机玩家必须关闭本地输入，使用 `PlayerStateDriver.SetLocalControlEnabled(false)`。
- 伤害相关逻辑优先使用 `IDamageable`、`AttackDamageInfo`、`PlayerHealth`、`MonsterHealth` 等现有接口。
- 装备属性变化后，需要通过 `GameMgr.Equipment.ApplyEquipmentStatsToPlayerData` 或已有事件路径刷新玩家状态。

## Inventory, Equipment, Buff

- 物品实例有 `uid`，装备槽位通过 `InventoryItem.location` 和 `locationKey` 表达。
- 添加/移除物品后通过 `PackageMgr` 触发保存和 `OnInventoryChanged`。
- 饰品能力和 Buff 已由 `EquipmentMgr` 同步，改装备系统时要检查该路径。
- Buff 应通过 `GameMgr.Buff.Apply/Remove/HasBuff` 操作，目标对象必须有 `BuffComponent`。
- 消耗品即时回血/回蓝和 Buff 触发在 `PackageMgr.UseConsumable` 中处理。

## Task And Dialogue

- 任务接取、追踪、完成和保存都通过 `TaskManager`。
- 任务目标推进已有 `NotifyNpcDialogue`、`NotifyMonsterKilled` 等入口；新增目标类型时优先扩展任务 Runtime，而不是 UI 直接改状态。
- 对话和任务互相影响时，要同时检查 `NPCInteraction`、`DialogueMgr`、`TaskManager` 和相关 UI。
- 对话图/任务图编辑器位于 Editor 目录，运行时代码不要依赖 `UnityEditor`。

## Build System

- 建造模式入口是 `GameMgr.Build` 和 `BuildManager.Tick()`。
- 配方优先来自 Addressables key `BuildRecipeList`，缺失时会创建运行时默认配方。
- 放置校验使用 `BuildPlacementValidator`，预览使用 `BuildPreview`。
- 建造消耗材料通过 `GameMgr.Package.RemoveAvailableItems`，拆除返还通过 `GameMgr.Package.AddItem`。
- 改建造前先阅读 `Assets/Scripts/GameCore/GameBuild/BUILD_SYSTEM_README.md`。

## Networking

- 当前联机是第一阶段，不要默认所有 gameplay 系统都已同步。
- `NetworkMgr` 运行时创建/配置 `NetworkManager` 和 `UnityTransport`。
- Player Prefab 必须有 `NetworkObject` 和 `NetworkPlayer`。
- 本地 Host/Client 测试流程见 `Docs/MultiplayerStageOne.md`。
- 新增网络同步时先明确所有权、服务器权威、离线兼容和存档影响。

## AI Collaboration Rules

- 每个任务前先读 `AGENTS.md`、`Docs/AI_CONTEXT.md`、`Docs/FEATURE_MAP.md`、`Docs/CODING_RULES.md`。
- 每个任务后更新 `Docs/CHANGELOG_AI.md`。
- 如果改变启动流程、模块边界、数据格式或跨系统依赖，也更新对应上下文文档。
- 发现不确定信息时写“待确认”，不要把猜测写成事实。
