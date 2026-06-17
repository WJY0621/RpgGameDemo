# AI Context

本文件是后续 AI/Codex 窗口接手 WorkDemo 前的核心上下文入口。它描述项目当前结构和模块关系，不替代源码阅读；涉及具体实现时仍应打开对应脚本确认。

## Project Goal

WorkDemo 是一个 Unity 3D ARPG/生存建造向 Demo。当前功能覆盖角色移动/战斗、角色模型选择、背包装备、消耗品、Buff、任务、NPC 对话、怪物/Boss、采集掉落、建造、商店、UI 面板、音频/VFX、场景加载和第一阶段本地联机。

## Runtime Entry Flow

1. Unity 启动 `GameInitializeScene`。
2. 场景中存在或创建 `GameMgr`，`GameMgr : MonoSingleton<GameMgr>` 在 `Awake` 初始化大多数全局服务。
3. `GameMgr.Start` 异步加载 `GameSoundDataSO`、初始化音频、VFX、`PlayerInitialData`，然后显示 `LogoPanel`。
4. `GameMgr` 调用 `Scene.PrepareSceneAsync("GameStartScene")` 预加载开始场景，但不立即激活。
5. `InitializeSceneController` 监听任意按键，隐藏 Logo 后调用 `GameMgr.Scene.ActivatePreparedSceneAsync()`。
6. `StartSceneController` 进入开始菜单，显示 `GameStartPanel`。
7. 进入 `GameScene` 时，`GameSceneController` 显示 `PlayerMainPanel`，确保单一 Player，应用存档位置，并刷新相机。

## Core Service Pattern

`Assets/Scripts/Manager/GameMgr.cs` 是全局服务定位点。常用访问方式：

- `GameMgr.AssetLoader`
- `GameMgr.Scene`
- `GameMgr.UI`
- `GameMgr.input`
- `GameMgr.Audio`
- `GameMgr.File`
- `GameMgr.Package`
- `GameMgr.Equipment`
- `GameMgr.TaskMgr`
- `GameMgr.Build`
- `GameMgr.Buff`
- `GameMgr.VFX`
- `GameMgr.Network`

新增全局服务时应谨慎。除非确实需要跨模块生命周期管理，否则优先保持模块内聚。

## Scene System

- `SceneMgr` 保存 `SceneControllerBase` 注册表，并委托 `LoadingMgr` 执行 Addressables 场景加载。
- `SceneControllerBase` 通过 `[SceneController(sceneName = "...", isGameScene = ...)]` 自动注册到 `GameMgr.Scene`。
- `LoadingMgr` 支持 Prepare/Activate 分离、LoadingPanel、黑屏淡入淡出、场景 Controller 等待、失败回退和资源清理。
- `GameMgr` 注册 `"SceneChanged"` 事件，用于场景 BGM 切换。

关键文件：

- `Assets/Scripts/Manager/SceneMgr.cs`
- `Assets/Scripts/Manager/LoadingMgr.cs`
- `Assets/Scripts/GameCore/GameScene/SceneControllerBase.cs`
- `Assets/Scripts/GameCore/GameScene/InitializeSceneController.cs`
- `Assets/Scripts/GameCore/GameScene/StartSceneController.cs`
- `Assets/Scripts/GameCore/GameScene/GameSceneController.cs`

## Player And Character

- 运行时玩家核心是 `PlayerStateDriver`，内部使用 HSM 状态机。
- `PlayerStateDriver` 读取 `GameMgr.input.Data`，写入 `PlayerContext`，驱动移动、旋转、跳跃/飞行、攻击输入、Animator 参数和 CharacterController。
- `SetLocalControlEnabled(false)` 用于联机远端玩家，禁用本地输入。
- 玩家启动时会确保挂载 `PlayerHealth`、`PlayerDeathController`、`BuffComponent`、武器模型、翅膀、拖尾、攻击特效、音频、武器模式等组件。
- 装备属性通过 `GameMgr.Equipment` 写入 `PlayerData`，并刷新 `PlayerHealth`。
- 死亡/复活：`PlayerHealth.OnDied`（HP<=0）触发 `PlayerDeathController`，它用 `SetLocalControlEnabled(false)` 冻结控制、播放 `Dead` 动画、释放鼠标并显示 `PlayerDeadPanel`（同时销毁其它面板）。点击 `ContinueButton` 调用 `PlayerDeathController.ReviveAsync`：传送到重生点、回满血、播放 `Idle`、恢复控制并重新显示 `PlayerMainPanel`。重生点解析顺序为：`GameFile` 的床重生点 -> `PlayerInitialDataSO.initialPosition`。

关键目录：

- `Assets/Scripts/GameCore/HSM`
- `Assets/Scripts/GameCore/GameCharacter/Player`
- `Assets/Scripts/GameCore/GameCharacter/Role`
- `Assets/Scripts/GameCore/GameCharacter/Combat`

## UI System

- UI Manager 是 `UIMgr`，通过 Addressables 按面板类名加载 Prefab，例如 `ShowPanel<PackagePanel>()` 加载 key 为 `PackagePanel` 的资源。
- 面板基类是 `BasePanel`，负责 CanvasGroup 淡入淡出，子类必须实现 `Init()`。
- `UIMgr` 缓存已加载面板，支持 `ShowPanel`、`HidePanel`、`SwitchPanelAsync`、黑屏淡入淡出和销毁除某个面板外的所有面板。
- `MainCanvas` 提供面板挂载父节点和黑屏淡入淡出。
- Red-dot reminders use `GameMgr.RedDot` and `RedDotView`. `RedDotMgr` stores counts by `RedDotType`; UI decides whether to show a pure dot or a numeric dot. `RedDotType.Friend` is an aggregate of friend requests plus chat unread count.

关键目录：

- `Assets/Scripts/GameCore/GameUI/Panel`
- `Assets/Scripts/GameCore/GameUI/Item`
- `Assets/Scripts/GameCore/GameRedDot`
- `Assets/Scripts/GameCore/GameUI/MainPanelUI`
- `Assets/Resources_moved/Prefabs`，包含大量 UI Prefab。

## Data And Persistence

- `FileMgr` 在 Editor 下读写 `Assets/Save/gameSaveData.sav`，打包后使用 `Application.persistentDataPath/Save`。
- `GameFileData` 包含存档列表和当前存档名。
- `GameFile` 保存玩家数据、金币、背包、任务、最后场景和各场景位置。
- `GameFile` 另有独立的床重生点字段（`hasRespawnPoint`/`respawnScene`/`respawnPosition`/`respawnRotation`，由 `BedInteraction` 写入）。它与 `playerSceneLocations` 故意分离：`FileMgr.UpdateFileData` 每次存档都会用玩家当前位置覆盖场景位置，因此那份列表不能当作稳定重生点。无床时死亡复活回退到初始点。
- Account-aware saves: `GameFile.ownerPlayerId` stores the account `playerId` that created a role. `FileMgr.GetCurrentAccountGameFiles()` filters the role list for the current logged-in account, and `EnsureCurrentGameFileForCurrentAccount()` updates the selected save when the account changes. Older saves without an owner are hidden once an account is logged in.
- Game time is persisted per role save: `GameFile.hasSavedGameTime`, `savedTimeInHours`, and `savedDay` are written from `TimeMgr` during `FileMgr.SaveGameFile()` and restored through `TimeMgr.SetDateTime` when the save is applied. New role saves are initialized to day 1 at 08:00 through `TimeMgr.DefaultStartTimeInHours`; older saves without the flag keep using TimeMgr defaults until they are saved again.
- `PackageMgr` 构建 `InventorySaveData`，`TaskManager` 构建 `TaskSystemSaveData`，统一由 `FileMgr.SaveGameFile()` 写回。
- `ItemJsonDatabase`、`RecipeJsonDatabase`、`MonsterJsonDatabase` 等使用 JSON 配置，路径集中在 `Assets/GameData`。

关键目录：

- `Assets/Scripts/GameCore/GameFile`
- `Assets/Scripts/Manager/FileMgr.cs`
- `Assets/Scripts/Manager/PackageMgr.cs`
- `Assets/Resources/GameData/ItemData`
- `Assets/GameData/RecipeData`
- `Assets/GameData/MonsterData`
- `Assets/Save`

## Gameplay Systems

- 背包/装备：`PackageMgr` 维护物品实例、金币、装备槽位，`EquipmentMgr` 负责装备属性、手持槽、饰品 Buff 和能力同步。
- 任务：`TaskManager` 维护已接任务、完成任务、追踪任务、任务目标推进和奖励发放。
- 对话：`DialogueMgr`、`GameDialogue` 和 `NPC` 模块负责对话图、NPC 规则和对话 UI。当前编辑器代码已移动到 `GameDialogue/Editor`。
- 建造：`BuildManager` 处理建造菜单、配方、预览、放置校验、材料扣除和拆除返还。
- Buff：`BuffMgr` 从 `Resources/Buff/BuffDatabase` 加载数据库，目标对象需挂 `BuffComponent`。
- 怪物/Boss：怪物和 Boss 各自有 FSM、血量、攻击、掉落、池化和生成区。
- Boss aggro: `BossAggroController` keeps a lightweight threat table for Boss target selection. Actual damage adds threat, threat decays over time, dead/out-of-range targets are removed, and `BossController` uses the best threat target during battle.
- 采集：`GameHarvest` 包含可采集资源、掉落、矿石池和刷新区域。
- 音频/VFX：`AudioMgr`、`GameSoundDataSO`、`VFXMgr`、武器攻击效果数据库共同处理声音与特效。
- 联机：`NetworkMgr` 使用 Netcode for GameObjects，当前是本地 Host/Client 第一阶段验证。

## Current Multiplayer Scope

详见 `Docs/MultiplayerStageOne.md`。当前联机目标是：

- 两个本地客户端进入 `GameScene`。
- 一个 Host，一个 Client。
- 每个玩家只控制自己的角色。
- 远端玩家通过网络同步位置显示。

不应假设账号、好友、Relay、Lobby、怪物、背包、任务、建造同步已经完成。

## Current Account And Social Scope

- `GameMgr.Account` uses `AccountMgr` and `IAccountService`; `GameMgr.Awake` constructs it with `HttpAccountService` by default.
- `MockAccountService` is still available for local-only fallback tests, but the normal demo path is the WorkDemoServer account backend.
- `IAccountService.LastError` carries user-facing account errors such as account missing or wrong password; LoginPanel uses it for inline TipText feedback.
- `IAccountService.SaveProfile` persists changes to the current account profile. `AccountMgr.SetDisplayName(roleName)` updates `CurrentProfile.displayName` and saves it.
- Registered accounts are stored by WorkDemoServer. Unity keeps the returned account profile and session token in `PlayerPrefs` for startup validation.
- Runtime HTTP/WebSocket requests use `HttpSessionContext` for the current process session token, and `PlayerPrefs` is only the persistence source for startup validation. This prevents two local client processes from overwriting each other's live session token while testing friend requests, chat, and online invites on one computer.
- `AccountProfile.accountName` is the login account; `AccountProfile.displayName` is intentionally separate and should be driven later by the role/character naming flow.
- `CreateRoleNamePanel` and `ChooseRolePanel` now synchronize the selected/created role name into `AccountProfile.displayName`.
- `MockSocialService` keeps a local PlayerPrefs cache from `playerId` to display name. `SocialMgr.SyncCurrentAccountProfileName()` updates it from the current account profile; mock search, request, and friend display use the cached name when available. This is local mock behavior only, so cross-client display names should eventually come from backend social/profile endpoints.
- `GameMgr.Social` now uses `HttpSocialService` by default. It reuses the same WorkDemoServer base URL and session token PlayerPrefs keys as `HttpAccountService`, and calls the backend social endpoints for player search, friend requests, accept/refuse, friend list, and delete friend.
- `MockSocialService` remains available as a local fallback implementation, but it is no longer the default runtime social service.
- `FriendPanel` refreshes `GameMgr.Social` when shown and runs a lightweight active-panel friend refresh loop. With `HttpSocialService`, this polls the backend so accepted friendships and online-state changes can appear without relogging while the panel is open.
- `GameMgr.Chat` now uses `HttpChatService` by default. It calls WorkDemoServer chat endpoints for private friend message send/history/read/unread summary, and updates `RedDotType.Chat` from the server unread count.
- `ChatPanel` opens a friend conversation, polls while visible, loads server-side message history, and marks incoming messages from that friend as read. `MockChatService` remains a local fallback only.
- `GameMgr.Realtime` uses `RealtimeMgr` and connects to WorkDemoServer `ws://.../ws?token=...` after login. It receives `chat.message` and `online.invite` events, then forwards them to `ChatMgr` and `SocialMgr`.
- `RealtimeMgr` also receives `social.refresh`; `GameMgr` forwards it to `SocialMgr.HandleRealtimeSocialRefresh()`, which refreshes incoming friend requests and friends. This keeps AddFriendPanel/ApplyListPanel/FriendPanel in sync without relogging.
- `SocialMgr.SendOnlineRequestAsync` now posts to `POST /api/online/invites` through `HttpOnlineInviteService` instead of local loopback. Incoming invite popups are opened only when an `online.invite` WebSocket event is received.
- Online invite replies use `POST /api/online/invites/{inviteId}/result`. Refuse sends a negative result back to the requester. Accept follows role-specific network startup: `InviteToMyWorld` starts Host on the requester before sending, then the receiver starts Client after accepting; `RequestToJoinWorld` starts Host on the receiver, then the requester receives `online.invite-result` and starts Client.
- `GameStartPanel` opens `LoginPanel` before role selection if no account is logged in.
- `LoginPanel` and `RegisterPanel` should have Addressables keys matching their class names.

## WorkDemoServer Backend Scope

- `WorkDemoServer` is a standalone ASP.NET Core backend project under the Unity project root.
- It currently runs at `http://127.0.0.1:5188` and provides the first real account backend slice.
- Implemented account endpoints: `POST /api/account/register`, `POST /api/account/login`, `GET /api/account/me`, `GET /api/account/player/{playerId}`, and `PATCH /api/account/display-name`.
- Register/login return an `AccountAuthResponse` containing `profile` and `sessionToken`; the token is accepted through `X-Session-Token` or `Authorization: Bearer <token>`.
- The current account store is local JSON behind `IAccountRepository`, not SQLite yet. This keeps the server buildable without external NuGet packages and leaves a clear replacement point for SQLite.
- Server-generated `playerId` values are sequential 6-digit numbers starting at `100001`. Registration scans existing account records and uses the maximum numeric `playerId` plus one, so preserved old JSON account data affects the next generated ID.
- Implemented social endpoints: `GET /api/social/search/{playerId}`, `POST /api/social/friend-requests`, `GET /api/social/friend-requests`, `POST /api/social/friend-requests/{requestId}/accept`, `POST /api/social/friend-requests/{requestId}/refuse`, `GET /api/social/friends`, and `DELETE /api/social/friends/{friendPlayerId}`.
- Implemented chat endpoints: `GET /api/chat/friends/{friendPlayerId}/messages`, `POST /api/chat/friends/{friendPlayerId}/messages`, `POST /api/chat/friends/{friendPlayerId}/read`, and `GET /api/chat/unread`.
- Implemented realtime endpoint: `GET /ws?token=<sessionToken>` upgraded to WebSocket. It pushes `chat.message` and `online.invite` events to connected players.
- Social endpoints broadcast `social.refresh` after friend request send/accept/refuse and friend deletion so online clients can refresh their social cache immediately.
- Implemented online invite endpoints: `POST /api/online/invites` and `POST /api/online/invites/{inviteId}/result`, validating account session, friendship, target WebSocket online state, invite type, address, and port. The server keeps pending invites in memory and pushes `online.invite-result` back to the requester.
- Social and private chat data are stored in local JSON behind `ISocialRepository` at `Data/social.json` by default. Friend list responses include `isOnline`, currently derived from active WebSocket connections. Chat unread counts are based on unread messages where the current player is the receiver.
- Development admin inspection endpoints exist at `GET /api/admin/accounts` and `GET /admin/accounts`. They are read-only, local-development oriented, and return account summaries without password hashes.
- Unity now has `HttpAccountService` beside `MockAccountService`, and `GameMgr.Awake` constructs `AccountMgr` with `new HttpAccountService()` by default.
- `HttpAccountService` uses `http://127.0.0.1:5188` unless `PlayerPrefs` key `WorkDemo.HttpAccount.BaseUrl` overrides it.
- Login/Register calls are synchronous in the first backend slice and save the returned account profile plus session token into `PlayerPrefs`.
- Startup auto-login is now validated against the backend: `HttpAccountService.LoadSavedProfile()` calls `GET /api/account/me` with the saved session token. If validation fails, it clears the local profile/token and the start flow should show LoginPanel again.
- `WorkDemoServer` currently stores session tokens in memory, so restarting the server invalidates existing Unity saved tokens. This is acceptable for the current demo slice but should become persistent/expiring token storage later.
- Account sessions are single-active-session per `playerId`. Login/register creates a new token, invalidates old tokens for the same account, sends a `session.kicked` realtime event to old WebSocket connections, and Unity clears the old local account state before showing `LoginPanel`.
- `AccountMgr.SetDisplayName` still calls `IAccountService.SaveProfile`; with `HttpAccountService`, this updates local profile data and attempts `PATCH /api/account/display-name` when a session token exists.
- Unity now has `HttpSocialService` beside `MockSocialService`, and `GameMgr.Awake` constructs `SocialMgr` with `new HttpSocialService()` by default.
- `HttpSocialService` calls `GET /api/social/search/{playerId}`, `POST /api/social/friend-requests`, `GET /api/social/friend-requests`, `POST /api/social/friend-requests/{requestId}/accept`, `POST /api/social/friend-requests/{requestId}/refuse`, `GET /api/social/friends`, and `DELETE /api/social/friends/{friendPlayerId}`.
- `HttpSocialService` parses backend list responses through a Unity `JsonUtility` wrapper because `JsonUtility` cannot parse top-level JSON arrays directly.
- Unity now has `HttpChatService` beside `MockChatService`, and `GameMgr.Awake` constructs `ChatMgr` with `new HttpChatService()` by default.
- `HttpChatService` calls `GET /api/chat/friends/{friendPlayerId}/messages?markRead=true`, `POST /api/chat/friends/{friendPlayerId}/messages`, `POST /api/chat/friends/{friendPlayerId}/read`, and `GET /api/chat/unread`.
- Unity now has `HttpOnlineInviteService` and `RealtimeMgr`. `HttpOnlineInviteService` calls `POST /api/online/invites`; `RealtimeMgr` uses `ClientWebSocket` and dispatches realtime chat/invite events on the Unity main thread.
- `RealtimeMgr` reconnects automatically when the WebSocket closes or connection fails, as long as the current runtime session token has not changed. This keeps friend request refresh, chat push, and online invite push alive after a temporary server restart or socket drop.
- Unity now has a Relay/Lobby bridge for friend online invites behind the `WORKDEMO_USE_UGS_RELAY` scripting define. `WorkDemoRelayLobbyService` initializes Unity Services, signs in anonymously, creates or joins Relay allocations, creates a private Lobby for host sessions, keeps host Lobbies alive with heartbeat pings, and configures `UnityTransport` with Relay server data when Unity Gaming Services packages are installed.
- `WorkDemoRelayLobbyService` switches Unity Authentication to a deterministic profile based on the current WorkDemo `playerId` before anonymous sign-in, so local multi-client testing does not accidentally reuse the same cached UGS anonymous player across different WorkDemo accounts. Lobby "already member" responses during invite acceptance are recoverable; the client continues with the Relay join code.
- Online invite payloads now carry optional `relayJoinCode` and `lobbyId` fields through `HttpOnlineInviteService`, `RealtimeMgr`, and WorkDemoServer. When Relay is available, accepted invites join by Relay code; otherwise the same flow falls back to the existing direct address/port connection.
- WorkDemoServer online invite contracts and WebSocket payloads pass Relay/Lobby fields through but do not own Relay allocation lifecycle. Allocation, Lobby creation, and Lobby cleanup remain Unity-client responsibilities.

## Known Risks And Open Questions

- 当前 Git 工作区有大量未提交改动。开始任何开发前先看 `git status --short`，不要覆盖非本任务改动。
- 部分源码中文注释在 PowerShell 输出中显示为乱码，可能是编码或终端显示问题。不要批量改编码，除非用户明确要求。
- Addressables key 与 Prefab/ScriptableObject 名称关系需要在 Unity Editor 中确认，纯文本扫描不能完全保证资源引用有效。
- `Assets/Resources_moved` 目录名说明资源可能经历过迁移，移动或重命名前要确认引用。
- `FORMAT_README.md` 在当前终端显示为乱码，内容似乎是格式化说明，暂不作为可靠协作入口。
