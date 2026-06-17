# Feature Map

本文件帮助后续 AI/Codex 窗口快速定位功能模块。路径均为项目相对路径。

## Global Managers

| 功能 | 关键路径 | 说明 |
| --- | --- | --- |
| 总入口 | `Assets/Scripts/Manager/GameMgr.cs` | 初始化全局服务，保存玩家引用和 PlayerData，启动开始场景流程。 |
| 资源加载 | `Assets/Scripts/Manager/AssetLoader.cs` | Addressables 资源/Prefab/场景加载和缓存。 |
| 场景 | `Assets/Scripts/Manager/SceneMgr.cs`, `Assets/Scripts/Manager/LoadingMgr.cs` | 场景 Controller 注册、异步加载、预加载、激活、回退。 |
| UI | `Assets/Scripts/Manager/UIMgr.cs` | 面板加载、缓存、显示/隐藏、黑屏过渡。 |
| 输入 | `Assets/Scripts/Manager/InputMgr.cs`, `Assets/Scripts/GameCore/GameInput` | Input System action map 和输入状态。 |
| 存档 | `Assets/Scripts/Manager/FileMgr.cs`, `Assets/Scripts/GameCore/GameFile` | 存档读写、玩家/背包/任务数据保存。 |
| Account-scoped role saves | `Assets/Scripts/Manager/FileMgr.cs`, `Assets/Scripts/GameCore/GameFile/GameFile.cs`, `Assets/Scripts/GameCore/GameUI/Panel/ChooseRolePanel.cs` | Role saves carry `ownerPlayerId`; role selection filters saves to the current logged-in account. |
| Game time save | `Assets/Scripts/Manager/TimeMgr.cs`, `Assets/Scripts/Manager/FileMgr.cs`, `Assets/Scripts/GameCore/GameFile/GameFile.cs` | Saves `TimeMgr.CurrentTime` and `CurrentDay` into the current role file, restores them when a role is loaded or selected, and initializes new role saves to day 1 at 08:00. |
| 事件 | `Assets/Scripts/Manager/EventMgr.cs`, `Assets/Scripts/GameCore/GameEvent` | 字符串事件注册和广播。 |
| 音频 | `Assets/Scripts/Manager/AudioMgr.cs`, `Assets/Scripts/GameCore/GameSound` | BGM、环境音、UI、游戏音效。 |
| 消息 | `Assets/Scripts/Manager/MessageMgr.cs`, `Assets/Scripts/GameCore/GameMessage` | 游戏内消息队列和提示。 |

## Player And Combat

| 功能 | 关键路径 | 说明 |
| --- | --- | --- |
| 玩家状态机 | `Assets/Scripts/GameCore/HSM` | 玩家移动、地面/空中/攻击/飞行等状态。 |
| 玩家驱动 | `Assets/Scripts/GameCore/HSM/PlayerStateDriver.cs` | 读取输入，驱动 `PlayerContext`、CharacterController 和 Animator。 |
| 玩家数据 | `Assets/Scripts/GameCore/GameCharacter/Player/PlayerData.cs`, `PlayerInitialDataSO.cs` | 属性、初始数据、运行时属性加成。 |
| Player starting inventory | `Assets/Scripts/GameCore/GameCharacter/Player/PlayerInitialDataSO.cs`, `Assets/Scripts/Manager/FileMgr.cs` | `PlayerInitialDataSO.initialInventoryItems` configures item id/count rows. `FileMgr.CreateNewGame` applies them only when creating a new role save. |
| 玩家生命 | `Assets/Scripts/GameCore/GameCharacter/Player/PlayerHealth.cs` | 玩家生命值和受击/恢复。 |
| 玩家死亡/复活 | `Assets/Scripts/GameCore/GameCharacter/Player/PlayerDeathController.cs`, `Assets/Scripts/GameCore/GameUI/Panel/PlayerDeadPanel.cs`, `Assets/Scripts/GameCore/GameFile/GameFile.cs`, `Assets/Scripts/GameCore/GameBuild/BedInteraction.cs` | HP<=0 时冻结控制、播放 `Dead` 动画、显示 `PlayerDeadPanel`；点击 `ContinueButton` 复活到重生点（床 -> 初始点）。床通过 `GameFile.SetRespawnPoint` 写入独立重生点。 |
| 玩家攻击 | `Assets/Scripts/GameCore/GameCharacter/Player/PlayerAttackController.cs` | 玩家攻击逻辑。 |
| 角色模型 | `Assets/Scripts/GameCore/GameCharacter/Role` | 角色列表、模型切换、武器/翅膀/拖尾挂载。 |
| 通用伤害 | `Assets/Scripts/GameCore/GameCharacter/Combat` | `IDamageable`、`AttackDamageInfo` 等战斗接口。 |
| 武器特效 | `Assets/Scripts/GameCore/GameCombat` | 武器攻击效果 SO、数据库、运行时控制器和编辑器窗口。 |

## Inventory, Equipment, Shop, Craft

| 功能 | 关键路径 | 说明 |
| --- | --- | --- |
| 背包 | `Assets/Scripts/Manager/PackageMgr.cs` | 物品实例、金币、装备槽、保存数据。 |
| 装备属性 | `Assets/Scripts/Manager/EquipmentMgr.cs` | 装备属性、饰品 Buff、手持槽位、玩家能力同步。 |
| 物品配置 | `Assets/Scripts/GameCore/GameItem`, `Assets/Resources/GameData/ItemData` | 物品类、运行时 JSON 数据库和 Excel 转 JSON 工具。 |
| 合成 | `Assets/Scripts/Manager/CraftMgr.cs`, `Assets/GameData/RecipeData` | 配方数据和合成逻辑。 |
| 商店 | `Assets/Scripts/Manager/ShopMgr.cs`, `Assets/Scripts/GameCore/GameShop` | 商店数据和商店 UI。 |
| 相关 UI | `Assets/Scripts/GameCore/GameUI/Panel/PackagePanel.cs`, `EquipPanel.cs`, `RecipePanel.cs`, `ShopPanel.cs` | 背包、装备、合成、商店面板。 |

## Tasks And Dialogue

| 功能 | 关键路径 | 说明 |
| --- | --- | --- |
| 任务运行时 | `Assets/Scripts/GameCore/GameTask/TaskManager.cs` | 接取、追踪、目标推进、完成、奖励和保存。 |
| 任务数据 | `Assets/Scripts/GameCore/GameTask`, `Assets/GameData/TaskData/TaskDataSO.asset` | 任务、步骤、目标、奖励数据。 |
| 任务编辑器 | `Assets/Scripts/GameCore/GameTask/Editor` | 任务图编辑器。 |
| 对话运行时 | `Assets/Scripts/Manager/DialogueMgr.cs`, `Assets/Scripts/GameCore/GameDialogue` | 对话数据、对话图、选择和事件。 |
| 对话编辑器 | `Assets/Scripts/GameCore/GameDialogue/Editor` | 对话图编辑器、节点、Inspector、IO。 |
| NPC | `Assets/Scripts/Manager/NPCMgr.cs`, `Assets/Scripts/GameCore/GameCharacter/NPC` | NPC 数据、交互、名称 UI、对话规则。 |
| 相关 UI | `Assets/Scripts/GameCore/GameUI/Panel/GameDialoguePanel.cs`, `TaskPanel.cs` | 对话面板和任务面板。 |

## World, Monsters, Build, Harvest

| 功能 | 关键路径 | 说明 |
| --- | --- | --- |
| 建造系统 | `Assets/Scripts/GameCore/GameBuild` | 建造配方、预览、放置校验、可建造物、门/床/工作台交互。 |
| 建造 Manager | `Assets/Scripts/GameCore/GameBuild/BuildManager.cs` | 建造模式、拆除模式、材料扣除和 UI 切换。 |
| 建造文档 | `Assets/Scripts/GameCore/GameBuild/BUILD_SYSTEM_README.md` | 建造系统局部说明。 |
| 采集系统 | `Assets/Scripts/GameCore/GameHarvest` | 可采集资源、掉落、资源数据库、矿石池和刷新区。 |
| 怪物系统 | `Assets/Scripts/GameCore/GameCharacter/Monster` | 怪物数据、控制器、FSM、血量、攻击、掉落、池化、生成。 |
| Boss 系统 | `Assets/Scripts/GameCore/GameCharacter/Boss` | Boss FSM 和技能阶段。 |
| 怪物数据 | `Assets/GameData/MonsterData` | 怪物 JSON、池配置、生成区 SO。 |
| Boss 数据 | `Assets/GameData/BossSkillData` | Boss 技能和配置 SO。 |

| Boss aggro | `Assets/Scripts/GameCore/GameCharacter/Boss/BossAggroController.cs`, `BossController.cs` | Lightweight threat table for Boss target selection. Damage adds threat, threat decays over time, dead/out-of-range targets are removed, and `BossController` uses the best threat target during combat. |

## UI And Presentation

| 功能 | 关键路径 | 说明 |
| --- | --- | --- |
| 面板基类 | `Assets/Scripts/GameCore/GameUI/Panel/BasePanel.cs` | CanvasGroup 淡入淡出和 `Init()` 约定。 |
| 主 Canvas | `Assets/Scripts/GameCore/GameUI/MainCanvas.cs` | 面板父节点和黑屏过渡。 |
| 主 HUD | `Assets/Scripts/GameCore/GameUI/Panel/PlayerMainPanel.cs`, `GameUI/MainPanelUI` | 生命/飞行/任务/装备预览/Buff 显示。 |
| 交互 UI | `Assets/Scripts/GameCore/GameUI/Interact` | 可交互提示。 |
| 伤害数字 | `Assets/Scripts/GameCore/GameUI/DamageNumber` | 伤害飘字。 |
| Red-dot reminders | `Assets/Scripts/GameCore/GameRedDot`, `Assets/Scripts/GameCore/GameUI/Item/RedDotView.cs` | Shared notification count system. Views can show pure dots or numeric dots; friend entry currently aggregates friend requests and chat unread count. |
| UI Prefab | `Assets/Resources_moved/Prefabs` | 多数面板 Prefab，通常 Addressables key 与类名相同。 |
| 图集 | `Assets/Resources_moved/Image/UI/Atlas` | 物品、Buff、建造、角色等图集。 |

## Audio And VFX

| 功能 | 关键路径 | 说明 |
| --- | --- | --- |
| 音频 Manager | `Assets/Scripts/Manager/AudioMgr.cs` | BGM、环境、UI、游戏音效播放。 |
| 音频数据 | `Assets/Scripts/GameCore/GameSound`, `Assets/GameData/SoundData` | 音效分组和声音配置。 |
| 音频资源 | `Assets/Resources_moved/Suond` | 项目音频资源，目录名为 `Suond`。 |
| VFX Manager | `Assets/Scripts/Manager/VFXMgr.cs` | VFX 加载/播放/池化入口。 |
| VFX 数据 | `Assets/Scripts/GameCore/GameVFX`, `Assets/GameData/VFXData` | VFX 配置、武器拖尾、投射物运行时数据。 |
| VFX 资源 | `Assets/Resources_moved/UseVFX` | 战斗、Boss、任务区域等特效 Prefab。 |

## Account, Social, Chat

| Feature | Key paths | Notes |
| --- | --- | --- |
| Mock account login | `Assets/Scripts/GameCore/GameAccount`, `Assets/Scripts/GameCore/GameUI/Panel/LoginPanel.cs`, `RegisterPanel.cs` | Local account/password registration and login through `AccountMgr`; registered demo accounts generate a 6-digit numeric player ID. |
| Server-backed friends | `Assets/Scripts/GameCore/GameSocial/HttpSocialService.cs`, `SocialMgr.cs`, `FriendPanel.cs`, `AddFriendPanel.cs`, `ApplyListPanel.cs` | Unity friend search/request/list/delete flow now calls WorkDemoServer social endpoints through `HttpSocialService`; `MockSocialService` remains as a local fallback implementation. |
| Mock online invites | `Assets/Scripts/GameCore/GameSocial/SocialMgr.cs`, `OnlinePanel.cs`, `OnlineRequestPanel.cs` | Online world invite/request UI still uses local mock delivery through `SocialMgr.ReceiveOnlineRequest` before a backend push/WebSocket channel exists. |
| Server-backed friend chat | `Assets/Scripts/GameCore/GameChat/HttpChatService.cs`, `ChatMgr.cs`, `ChatPanel.cs`, `ChatMessageItem.cs` | Private friend chat now uses WorkDemoServer endpoints for send/history/read/unread count; ChatPanel polls while open and `RedDotType.Chat` drives the main friend red dot. `MockChatService` remains as local fallback only. |
| Realtime social channel | `Assets/Scripts/GameCore/GameSocial/RealtimeMgr.cs`, `WorkDemoServer/Services/RealtimeConnectionHub.cs` | Unity connects to `/ws?token=<sessionToken>` after login. The server pushes `chat.message`, `online.invite`, `online.invite-result`, and `social.refresh` events to online players through WebSocket, and Unity dispatches them back into `ChatMgr`/`SocialMgr`. `RealtimeMgr` retries automatically after socket drops while the same runtime session token is active. |
| Server-backed online invites | `Assets/Scripts/GameCore/GameSocial/HttpOnlineInviteService.cs`, `InviteData.cs`, `InviteResultData.cs`, `SocialMgr.cs`, `OnlinePanel.cs`, `OnlineRequestPanel.cs`, `WorkDemoServer/Services/OnlineInviteService.cs` | Online invite/request buttons now call `POST /api/online/invites`; the server validates friendship and target WebSocket online state, then pushes `online.invite` to the target client. Accept/refuse calls `/api/online/invites/{inviteId}/result`, and accepted requests automatically start the correct Host/Client side. Invite payloads can carry Relay `relayJoinCode` and `lobbyId` when the UGS Relay/Lobby bridge is enabled. |
| Account backend | `WorkDemoServer`, `Assets/Scripts/GameCore/GameAccount/HttpAccountService.cs`, `Assets/Scripts/GameCore/GameAccount/HttpSessionContext.cs` | ASP.NET Core account server with register/login/profile/display-name endpoints, sequential 6-digit player ID generation from `100001`, password hashing, session tokens, JSON-backed storage behind `IAccountRepository`, and Unity-side HTTP account integration. Unity keeps the active token in `HttpSessionContext` per process and persists it to `PlayerPrefs` only for startup validation. Server sessions are single-active-session per player: new login/register invalidates old tokens and pushes `session.kicked` to old realtime clients. |
| Friend backend | `WorkDemoServer/Services/SocialService.cs`, `WorkDemoServer/Services/JsonSocialRepository.cs`, `WorkDemoServer/Contracts/Social*.cs`, `Assets/Scripts/GameCore/GameSocial/HttpSocialService.cs` | ASP.NET Core social endpoints plus Unity HTTP client for player search, friend requests, accept/refuse, friend list, delete friend, JSON-backed social storage, and session-based online state. |
| Chat backend | `WorkDemoServer/Services/ChatService.cs`, `WorkDemoServer/Contracts/Chat*.cs`, `WorkDemoServer/Models/ChatMessageRecord.cs`, `Assets/Scripts/GameCore/GameChat/HttpChatService.cs` | ASP.NET Core private friend chat endpoints plus Unity HTTP client for latest messages, message send, mark-read, and unread summary. Chat data is currently persisted in `Data/social.json` through `ISocialRepository`. |
| Backend admin page | `WorkDemoServer/Services/AdminService.cs`, `WorkDemoServer/Contracts/AdminResponses.cs`, `WorkDemoServer/Program.cs` | Read-only development account inspection at `/admin/accounts` and `/api/admin/accounts`; shows safe account summaries without password hashes. |

## Networking

| 功能 | 关键路径 | 说明 |
| --- | --- | --- |
| 联机 Manager | `Assets/Scripts/GameCore/GameNetwork/NetworkMgr.cs` | Runtime 创建 NetworkManager，启动 Host/Client，注册 Player Prefab。 |
| 玩家同步 | `Assets/Scripts/GameCore/GameNetwork/NetworkPlayer.cs` | 本地控制和远端显示分离。 |
| 调试 UI | `Assets/Scripts/GameCore/GameNetwork/NetworkDebugHud.cs` | F9 显示本地 Host/Client 测试面板。 |
| Prefab 工具 | `Assets/Scripts/GameCore/GameNetwork/Editor/MultiplayerPrefabSetup.cs` | 配置 Player Prefab 的 NetworkObject/NetworkPlayer。 |
| 阶段文档 | `Docs/MultiplayerStageOne.md` | 当前联机目标、测试流程和下一阶段。 |

Networking note: the optional Relay/Lobby invite bridge lives in `Assets/Scripts/GameCore/GameNetwork/WorkDemoRelayLobbyService.cs`, `RelayLobbySessionInfo.cs`, and `NetworkMgr.cs`. It is compiled behind `WORKDEMO_USE_UGS_RELAY`; hosts create a Relay allocation plus private Lobby, invite receivers join by Relay code, and direct IP/port remains the fallback before Unity Gaming Services packages and the define are enabled.

## When Starting A Task

- 改 UI：先看 `UIMgr`、`BasePanel`、目标 Panel、对应 Prefab 和 Addressables key。
- 改场景切换：先看 `SceneMgr`、`LoadingMgr`、对应 `SceneController`。
- 改玩家动作：先看 `PlayerStateDriver`、`PlayerContext`、`HSM/States`、Animator 参数。
- 改背包装备：先看 `PackageMgr`、`EquipmentMgr`、`GameItem`、对应 UI。
- 改任务对话：先看 `TaskManager`、`GameTask`、`DialogueMgr`、`GameDialogue`、`NPCInteraction`。
- 改建造：先读 `GameBuild/BUILD_SYSTEM_README.md`，再看 `BuildManager` 和 `BuildPlacementValidator`。
- 改联机：先读 `Docs/MultiplayerStageOne.md`，再看 `NetworkMgr`、`NetworkPlayer`、`PlayerStateDriver.SetLocalControlEnabled`。
