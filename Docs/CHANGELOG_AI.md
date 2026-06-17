# AI Changelog

## 2026-06-08 - Bed Respawn Scene Fix
- Change/task: Fixed player revival falling back to the initial position after placing and interacting with a bed.
- Changed files: `Assets/Scripts/GameCore/GameBuild/BedInteraction.cs`, `Assets/Scripts/GameCore/GameBuild/BUILD_SYSTEM_README.md`, `Docs/CHANGELOG_AI.md`.
- Root cause: Runtime build objects are parented under the persistent `PlacedBuildObjects` root, which moves them into Unity's internal `DontDestroyOnLoad` scene. Bed registration saved that internal scene name, while revival queried the active gameplay scene such as `GameScene`, so the saved bed point never matched.
- Behavior change: Beds under `DontDestroyOnLoad` now register against the active gameplay scene. Interacting with a bed also refreshes and saves the latest bed respawn point, covering cases where the save was unavailable during the bed's initial `Start`.
- Affected modules: build system, bed interaction, player revival save data.
- Verification: `dotnet build WorkDemo.sln` passed with 63 existing warnings and 0 errors. Unity Play Mode should verify placing or interacting with a bed, dying in `GameScene`, and reviving at the bed instead of the initial position.

## 2026-06-06 - GameMgr Structure Cleanup
- Change/task: Reorganized `GameMgr` so its initialization and startup orchestration are easier to read without changing runtime behavior.
- Changed files: `Assets/Scripts/Manager/GameMgr.cs`, `Assets/Scripts/Manager/EquipmentMgr.cs`, `Assets/Scripts/Manager/EquipmentMgr.cs.meta`, `Assembly-CSharp.csproj`, `Docs/FEATURE_MAP.md`, `Docs/CHANGELOG_AI.md`.
- Behavior change: No intended gameplay behavior change. `Awake` now delegates to grouped core/gameplay/online/runtime initialization methods, startup audio/player/start-scene work is split into named async methods, realtime lambdas are named handlers, and `EquipmentMgr` now lives in its own file.
- Affected modules: Global service initialization, startup flow, equipment service source location.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings only. Unity Play Mode should still verify boot through Logo, start menu, role selection, and GameScene entry.

## 2026-06-04 - Settings Panel 960 Resolution Toggle
- Change/task: Updated the settings panel's second resolution toggle from `1600Toggle` to `960Toggle`.
- Changed files: `Assets/Scripts/GameCore/GameUI/Panel/GameSettingPanel.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Selecting `960Toggle` now applies 960x540 resolution and switches the screen mode to windowed so the actual window can resize. Windowed resolution changes are applied once immediately and checked again on the next frame, with debug logs reporting the requested and actual screen size. Resolution toggle lookup now tolerates accidental leading/trailing spaces in child object names, fixing the prefab object currently named `960Toggle `.
- Affected modules: settings UI, video resolution settings.
- Verification: `dotnet build WorkDemo.sln` passed with existing warnings and 0 errors. Unity Play Mode should verify `PanelBK/PictureContent/ToggleGroup/960Toggle` is bound and changes the window size correctly.

## 2026-06-04 - New Role Starts At 08:00
- Change/task: Made newly created roles start their first GameScene entry at 08:00.
- Changed files: `Assets/Scripts/Manager/TimeMgr.cs`, `Assets/Scripts/Manager/FileMgr.cs`, `Assets/Scripts/GameCore/GameFile/GameFile.cs`, `Docs/AI_CONTEXT.md`, `Docs/FEATURE_MAP.md`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `TimeMgr` now exposes default start constants for day 1 at 08:00. `FileMgr.CreateNewGame` writes that time directly into the new role save and resets any active `TimeMgr` to the same value, so the first save/load path does not depend on scene-serialized TimeMgr values.
- Affected modules: role creation, save data, game time system.
- Verification: `dotnet build WorkDemo.sln` passed with 63 existing warnings and 0 errors. Unity Play Mode should verify creating a new role enters GameScene at day 1, 08:00, and later saves still restore their saved time.

## 2026-06-03 - Game Time Save And Faster Wake
- Change/task: Sped up leaving bed sleep and persisted game time in save files.
- Changed files: `Assets/Scripts/GameCore/GameBuild/BedInteraction.cs`, `Assets/Scripts/GameCore/GameFile/GameFile.cs`, `Assets/Scripts/Manager/FileMgr.cs`, `Assets/Scripts/Manager/TimeMgr.cs`, `Docs/AI_CONTEXT.md`, `Docs/FEATURE_MAP.md`, `Docs/CHANGELOG_AI.md`.
- Data format change: `GameFile` now stores `hasSavedGameTime`, `savedTimeInHours`, and `savedDay`. Older saves without `hasSavedGameTime` keep using `TimeMgr` defaults until they are saved again.
- Behavior change: Waking from a bed now immediately crossfades to the configured wake state, defaulting to `Idle`, with a very short blend so the player leaves the `Sleep` animation faster. `FileMgr.SaveGameFile()` now writes `TimeMgr.CurrentTime` and `CurrentDay`; loading or selecting a role restores those values through `TimeMgr.SetDateTime`. Creating a new role resets `TimeMgr` to its configured start day/time before the first save.
- Affected modules: bed interaction, player sleep animation, save data, time system.
- Verification: `dotnet build WorkDemo.sln` passed with 62 existing warnings and 0 errors. Unity Play Mode should verify waking from bed exits the sleep pose quickly, saving at a later time/day and re-entering restores that time/day, and creating a new role still starts at the configured initial time.

## 2026-06-03 - Bed Sleep Pose Offset
- Change/task: Adjusted the player pose and state handling used when interacting with a bed to sleep.
- Changed files: `Assets/Scripts/GameCore/GameBuild/BedInteraction.cs`, `Assets/Scripts/GameCore/GameBuild/BUILD_SYSTEM_README.md`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Bed sleep teleport now applies a default local position offset of `(0, 0.2, 0)` and a default local rotation offset of `(0, 180, 0)`, so the player is placed slightly above the sleep target and turned around before playing the `Sleep` animation. While sleeping, player control is disabled, bed interaction prompts are hidden, repeated bed interaction is blocked, and movement input exits sleep, restores player control, and restores the original `TimeMgr` speed. Bed interaction remains suppressed until the player leaves the sleep spot.
- Affected modules: build system, bed interaction, player sleep pose, time system.
- Verification: `dotnet build WorkDemo.sln` passed with 62 existing warnings and 0 errors. Unity Play Mode should verify interacting with a bed places the sleeping player at the expected height/facing direction, hides the bed interaction prompt while sleeping or still on the sleep spot, prevents repeated bed interaction, and exits sleep when movement is pressed.

## 2026-06-03 - Bed Auto Respawn And Sleep Interaction
- Change/task: Changed bed build/interaction behavior for respawn and sleeping.
- Changed files: `Assets/Scripts/GameCore/GameBuild/BedInteraction.cs`, `Assets/Scripts/GameCore/GameBuild/BuildManager.cs`, `Assets/Scripts/GameCore/GameFile/GameFile.cs`, `Assets/Scripts/GameCore/GameBuild/BUILD_SYSTEM_README.md`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Beds now register themselves as respawn points after placement instead of requiring interaction. The newest placed bed becomes the active respawn point; demolishing it unregisters it and falls back to the previous active bed, or clears the respawn point when no beds remain. Bed interaction now teleports the player to the bed/sleep target, plays the `Sleep` animation, heals if enabled, and sets `TimeMgr` speed to 10x the original sleep baseline.
- Affected modules: build system, bed interaction, save-file respawn data, time system.
- Verification: `dotnet build WorkDemo.sln` passed with 62 existing warnings and 0 errors. Unity Play Mode should verify placing beds updates respawn immediately, demolishing the newest bed falls back to the previous bed, and interacting with a bed moves the player, plays `Sleep`, and accelerates game time.

## 2026-06-03 - Boss Player Death Idle Animation Fix
- Change/task: Stopped Boss from repeating attack animation after the player dies.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossController.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Boss player-death handling now clears the aggro table, stops active skills and attack windows, ends movement/combat, moves the Boss into non-combat idle, and immediately crossfades to `IdleNormal` through the new `playerDeathIdleStateName` fallback. The delayed return-home recovery also preserves the same idle animation after teleporting home and restoring HP.
- Affected modules: Boss AI, Boss animation state flow, player death combat reset.
- Verification: `dotnet build WorkDemo.sln` passed with 62 existing warnings and 0 errors. Unity Play Mode should verify that after the player dies, Boss stops the current attack animation and plays `IdleNormal` instead of replaying the death-hit attack.

## 2026-06-03 - Multiplayer Boss Aggro Table
- Change/task: Added a lightweight Boss threat table for multiplayer target selection.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossAggroController.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossController.cs`, `Assembly-CSharp.csproj`, `Docs/CHANGELOG_AI.md`, `Docs/FEATURE_MAP.md`.
- Behavior change: Bosses now have a `BossAggroController` that records player threat, adds threat from actual damage, decays threat over time, applies small near-target and current-target stickiness bonuses, removes invalid/dead/out-of-range targets, and lets `BossController` pick the best target instead of always chasing the last attacker. Boss death/end-battle clears the threat table, and player death can fall through to the next valid aggro target before resetting the fight.
- Affected modules: Boss AI, multiplayer Boss target selection, Boss damage response.
- Verification: `dotnet build WorkDemo.sln` passed with 62 existing warnings and 0 errors. Unity Play Mode should verify in Host/Client Boss fights that the Boss can switch targets based on which player deals more damage, and that it does not keep targeting a dead or disconnected player.

## 2026-06-03 - Player Buff Duplicate After Death Fix
- Change/task: Fixed player buffs appearing duplicated after death/revive.
- Changed files: `Assets/Scripts/GameCore/GameBuff/Runtime/BuffComponent.cs`, `Assets/Scripts/GameCore/GameUI/Panel/PlayerMainPanel.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Refreshing an already-active buff no longer fires `OnBuffAdded` or the global added broadcast, so listeners do not treat duration refresh as a new buff. `PlayerMainPanel` initialization is now idempotent and no longer calls `Init` from `Awake`, preventing the HUD buff controller, button listeners, and existing buff icons from being initialized twice when the panel is loaded through `UIMgr`.
- Affected modules: player buff runtime, main HUD buff display, player death/revive UI flow.
- Verification: `dotnet build WorkDemo.sln` passed with 62 existing warnings and 0 errors. Unity Play Mode should verify that dying and reviving with active buffs does not create duplicate buff icons or duplicate buff effects.

## 2026-06-03 - Boss Return Home After Player Death
- Change/task: Made Boss reset after killing/losing the player.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossController.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossSummonArena.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Boss now records a home transform. Scene-placed Bosses use their initial transform; Bosses prepared by `BossSummonArena` update home after being placed at the summon spawn. When the tracked player dies, Boss clears combat, waits 3 seconds, teleports back home, resets phase/skill runtime and hurt thresholds, returns to Inactive, and restores HP to full.
- Affected modules: Boss AI, Boss summon arena, player death combat reset.
- Verification: `dotnet build WorkDemo.sln` passed with 62 existing warnings and 0 errors. Unity Play Mode should verify player death causes the Boss to stop combat, wait 3 seconds, return to the summon position, and show full HP.

## 2026-06-03 - Clear Monster And Boss Aggro On Player Death
- Change/task: Made monsters and Boss forget the player when the player dies.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Monster/MonsterController.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossController.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `MonsterController` and `BossController` now subscribe to the current `PlayerHealth.OnDied` event, clear their current player target on death, stop movement/combat, and reject dead players in `SetTarget`, `CachePlayerTarget`, and `HasValidTarget`. Small monsters return after losing the dead player; Boss ends the active battle without treating the dead player as a valid target.
- Affected modules: monster AI, Boss AI, player death combat flow.
- Verification: `dotnet build WorkDemo.sln` passed with 63 existing warnings and 0 errors. Unity Play Mode should verify that dying near a small monster or Boss makes it stop chasing/attacking and that it does not immediately re-acquire the dead player.

## 2026-06-02 - Dialogue Choice Click Sound
- Change/task: Added UI sound feedback when clicking dialogue choices.
- Changed files: `Assets/Scripts/GameCore/GameUI/Panel/GameDialoguePanel.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Dialogue choice buttons now play the UI sound `UI_Hover3` through `GameMgr.Audio.PlayUIEffect` before hiding choices and advancing the dialogue.
- Affected modules: dialogue UI, UI audio feedback.
- Verification: `dotnet build WorkDemo.sln` passed with 62 existing field warnings and 0 errors. Unity Play Mode should verify clicking any dialogue option plays `UI_Hover3`.

## 2026-06-02 - Chest Open Sound
- Change/task: Added chest opening sound playback.
- Changed files: `Assets/Scripts/GameCore/GameItem/ChestInteraction.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `ChestInteraction` now exposes `Open Sound Group`, `Open Sound Name`, and `Open Sound Volume`, defaulting to `Game` / `OpenChest` / `1`. When a chest opens successfully, it plays the configured sound at the chest position before the open animation/effect and reward delay.
- Affected modules: chest interaction feedback, audio playback.
- Verification: `dotnet build WorkDemo.sln` passed with 62 existing field warnings and 0 errors. Unity Play Mode should verify opening `ChestNew` plays the `OpenChest` sound when that sound is configured in the audio data.

## 2026-06-02 - Shop Insufficient Gold Tip
- Change/task: Updated ShopPanel purchase failure messaging for insufficient gold.
- Changed files: `Assets/Scripts/GameCore/GameUI/Panel/ShopPanel.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: When buying from `ShopPanel` fails because the player does not have enough gold, `TipPanel` now shows `金币不足，无法购买`. Stock failures still show `库存不足`, and other invalid buy cases show `无法购买该物品`.
- Affected modules: shop UI purchase confirmation and failure tips.
- Verification: `dotnet build WorkDemo.sln` passed with 62 existing field warnings and 0 errors. Unity Play Mode should verify attempting to buy an item above the player's current gold shows the exact insufficient-gold message.

## 2026-06-02 - Shop Remaining Gold Text
- Change/task: Added current player gold display to ShopPanel.
- Changed files: `Assets/Scripts/GameCore/GameUI/Panel/ShopPanel.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `ShopPanel` now binds `ComInfoContent/RemainGoldText` and displays the player's current gold as `剩余xxxx金`. The value refreshes when the shop opens, when shop state changes after a purchase, and when `PackageMgr.OnInventoryChanged` fires while the panel is open.
- Affected modules: shop UI, package gold display.
- Verification: `dotnet build WorkDemo.sln` passed with 62 existing field warnings and 0 errors. Unity Play Mode should verify `RemainGoldText` shows the current player gold and updates immediately after buying an item.

## 2026-06-02 - Chest Destroy After Open
- Change/task: Added delayed chest removal after opening.
- Changed files: `Assets/Scripts/GameCore/GameItem/ChestInteraction.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `ChestInteraction` now exposes `Destroy After Open` and `Destroy Delay After Open` under a Disappear section. After a chest opens and grants rewards, it destroys its GameObject after the configured delay; disabling `Destroy After Open` keeps the opened chest in the scene.
- Affected modules: chest interaction lifecycle.
- Verification: `dotnet build WorkDemo.sln` passed with 62 existing field warnings and 0 errors. Unity Play Mode should verify opening `ChestNew` grants rewards, waits the configured delay, then removes the chest.

## 2026-06-02 - Chest Gold Reward
- Change/task: Added configurable gold rewards to chest interaction rewards.
- Changed files: `Assets/Scripts/GameCore/GameItem/ChestInteraction.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `ChestInteraction` now exposes `Reward Gold` under the Rewards section. Opening a chest grants the configured gold amount through `PackageMgr.AddGold` before granting item rewards; item reward behavior is unchanged.
- Affected modules: chest rewards, package gold balance.
- Verification: `dotnet build WorkDemo.sln` passed with 62 existing field warnings and 0 errors. Unity Play Mode should verify setting `Reward Gold` on `ChestNew` increases the player's gold when the chest opens.

## 2026-06-02 - Boss Death Chest Addressables Priority
- Change/task: Switched Boss death chest spawning to prefer the Addressables prefab address.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossController.cs`, `Assets/GameData/BossSkillData/BossConfig.asset`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `BossController` now loads `deathChestPrefabAddress` first through `GameMgr.AssetLoader.LoadPrefab`; the direct `deathChestPrefab` reference is only a fallback. The current Boss config clears the direct prefab reference and keeps `deathChestPrefabAddress: ChestNew`, so the dropped chest can come from Addressables once `ChestNew` is saved with that address.
- Affected modules: Boss death reward chest spawning, Addressables prefab loading.
- Verification: `dotnet build WorkDemo.sln` passed with 62 existing field warnings and 0 errors. Unity Play Mode should verify killing the Boss creates one Addressables-loaded `ChestNew` chest.

## 2026-06-02 - Boss Death Chest Drop
- Change/task: Spawn the configured reward chest when a Boss dies.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossConfigSO.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossController.cs`, `Assets/GameData/BossSkillData/BossConfig.asset`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `BossConfigSO` now exposes death chest settings. When `BossHealth.OnDied` fires, `BossController` spawns the configured chest once at the Boss position, probes downward to place it on the ground, and only does this in single-player/local authority or on the server during a network session.
- Affected modules: Boss death flow, reward chest spawning.
- Verification: `dotnet build WorkDemo.sln` passed with 62 existing field warnings and 0 errors. Unity Play Mode should verify killing the Boss creates one `ChestNew` that can be interacted with and plays `ChestOpen` when opened.

## 2026-06-02 - Chest Open Animation Playback
- Change/task: Added legacy animation playback when interacting with a reward chest.
- Changed files: `Assets/Scripts/GameCore/GameItem/ChestInteraction.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `ChestInteraction` now exposes `Chest Open Animation` and `Chest Open Animation Name` (default `ChestOpen`). When the chest opens successfully, it plays the configured animation, or automatically finds an `Animation` component in child objects and plays `ChestOpen`; if that named clip is missing, it falls back to the component's default clip.
- Affected modules: chest interaction, chest open reward feedback.
- Verification: `dotnet build WorkDemo.sln` passed with 63 existing field warnings and 0 errors. Unity Play Mode should verify interacting with `ChestNew` plays the child `Object001` `ChestOpen` animation before/while rewards are granted.

## 2026-06-02 - Package Right Click Equip All Equipment
- Change/task: Extended package-panel right-click equipping to non-weapon equipment while the equip panel is closed.
- Changed files: `Assets/Scripts/GameCore/GameUI/Panel/PackagePanel.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `PackagePanel.TryQuickEquipInventoryItem` now maps equipment slot types to the actual equip slot keys (`HeadContent`, `ChestContent`, `LegContent`, `WeaponContent1/2`, `ToolContent1/2`, `AccessContent1/2/3`). With only the package panel open, right-clicking armor, tools, accessories, or weapons equips to the first empty compatible slot; if all compatible non-weapon slots are occupied it replaces the first compatible slot. Full weapon slots still use `EquipTipPanel` so the player chooses which weapon to replace.
- Affected modules: package item right-click handling, equipment slot assignment.
- Verification: `dotnet build WorkDemo.sln` passed with 63 existing field warnings and 0 errors. Unity Play Mode should verify right-clicking head/chest/leg/tool/accessory items equips them without opening `EquipPanel`.

## 2026-06-01 - Package Right Click Weapon Equip Without Equip Panel
- Change/task: Allowed right-click weapon equipping directly from the package panel without requiring the equip panel to be open.
- Changed files: `Assets/Scripts/GameCore/GameUI/Item/PackageItemUI.cs`, `Assets/Scripts/GameCore/GameUI/Panel/PackagePanel.cs`, `Assets/Scripts/GameCore/GameUI/Panel/EquipTipPanel.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Right-clicking a weapon in `PackagePanel` now equips it into the first empty weapon slot even when `EquipPanel` is closed. If both weapon slots are occupied, `EquipTipPanel` opens so the player can choose which weapon slot to replace; the replacement now works even without an active `EquipPanel`. Open package/equip panels refresh after the operation.
- Affected modules: package item right-click handling, weapon equip flow, weapon replacement tip flow.
- Verification: `dotnet build WorkDemo.sln` passed with 62 existing `ItemJsonDatabase`/`BuildManager`/`ConfigHotUpdater` field warnings and 0 errors. Unity Play Mode should verify right-clicking a weapon with only `PackagePanel` open equips into an empty weapon slot, and opens `EquipTipPanel` when both slots are filled.

## 2026-05-31 - ServerData Sync Script (Resource Hot-Update Tooling)
- Change/task: Added a one-click script to sync Addressables remote output to the WorkDemoServer hosting folder, completing the resource hot-update workflow.
- Changed files: `sync-serverdata.ps1` (new), `sync-serverdata.bat` (new), `Docs/CHANGELOG_AI.md`.
- Behavior change: After an Addressables "Build" or "Update a Previous Build", running `sync-serverdata.bat` parses the catalog for http-referenced (remote) bundles, resets `WorkDemoServer/AddressablesContent/<BuildTarget>`, copies the catalog + hash + remote bundles, and prunes orphan bundles from ServerData. ASCII-only output for Windows PowerShell 5.1 compatibility. Currently only `ItemIcon` is a Remote group (Weapon/Monster reverted to Local), so the server hosts catalog + the itemicon bundle (~675 KB).
- Affected modules: resource hot-update deployment workflow.
- Verification: Ran the script; server hosts catalog_0.1.json + itemicon bundle, matching ServerData. `http://127.0.0.1:5188/addressables/StandaloneWindows64/catalog_0.1.json` returns 200.

## 2026-05-31 - NPC Dialogue Rule Config Resources Placement
- Change/task: Moved the global NPC dialogue rule config into a Resources path so runtime rule loading works directly.
- Changed files: `Assets/Resources/GameData/NPCDialogueRuleConfig.asset`, `Assets/Resources/GameData/NPCDialogueRuleConfig.asset.meta`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `Resources.Load<NPCDialogueRuleConfigSO>("NPCDialogueRuleConfig")` can now find the configured NPC dialogue rule table without relying on the Editor-only fallback. The moved `.meta` keeps the original asset GUID.
- Affected modules: NPC dialogue rule loading, task-stage dialogue group selection.
- Verification: Confirmed the asset and `.meta` exist under `Assets/Resources/GameData/` and no longer exist under `Assets/GameData/NPCData/`.

## 2026-05-31 - NPC Dialogue Rule Config Editor Fallback
- Change/task: Fixed NPC dialogue group resolution falling back to numeric/default group names when the configured rule asset is outside a `Resources` folder.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/NPC/NPCDataComponent.cs`, `Assets/Scripts/GameCore/GameCharacter/NPC/NPCDialogueRuleConfigSO.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: NPC dialogue rules still prefer `Resources.Load("NPCDialogueRuleConfig")`, but in Editor Play Mode they now fall back to finding `NPCDialogueRuleConfigSO` assets through `AssetDatabase`, then loaded assets. NPC rule-set ID matching now trims both IDs before comparison, so configured dialogue group names like `t1s3` are used instead of falling back to an NPC default group such as `3`.
- Affected modules: NPC dialogue rule loading, NPC task-stage dialogue group selection.
- Verification: `dotnet build WorkDemo.sln` passed with 62 existing `ItemJsonDatabase`/`BuildManager`/`ConfigHotUpdater` field warnings and 0 errors. Unity Play Mode should verify NPC 2 step 3 requests `t1s3`, not `3`.

## 2026-05-31 - CreateRolePanel Avatar Version-Race Fix
- Change/task: Fixed role avatars staying blank in CreateRolePanel due to a version-counter race that dropped valid async icon loads.
- Changed files: `Assets/Scripts/GameCore/GameUI/Panel/CreateRolePanel.cs`, `Assets/Scripts/Manager/IconAtlasMgr.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `RefreshRoleList` bumped `roleListLoadVersion` on every call (and `OnDisable` also bumped it); each icon's async `LoadRoleIcon` only called `SetVisible(true)` if `listVersion == roleListLoadVersion` at completion, so any extra refresh/disable during loading left finished loads hidden → blank cards. Removed the `roleListLoadVersion` counter; icon loads are now gated only on `icon == null` (a refreshed list Destroys old icons, so stale loads drop via Unity fake-null), correct because icons are destroyed+recreated, never reused. Also added negative caching + in-flight dedup to `IconAtlasMgr` so an unresolvable icon no longer re-spawns async Addressables work on every UI refresh (was causing a hover-driven freeze). `roleSelectionVersion` (model/background load on click) unchanged.
- Affected modules: CreateRolePanel avatar list, IconAtlasMgr sprite loading.
- Verification: `dotnet build WorkDemo.sln` 0 errors. Requires `Use Asset Database` play mode and reimported role atlas/sprites.

## 2026-05-31 - Task Driven NPC Dialogue Group Refresh
- Change/task: Added a task-stage-change notification path so configured NPC dialogue groups refresh when task progress changes.
- Changed files: `Assets/Scripts/GameCore/GameTask/TaskManager.cs`, `Assets/Scripts/Manager/DialogueMgr.cs`, `Assets/Scripts/Manager/NPCMgr.cs`, `Assets/Scripts/GameCore/GameCharacter/NPC/NPCDataComponent.cs`, `Assets/Scripts/Manager/GameMgr.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `TaskManager` now emits `OnTaskStepChanged` when a task is accepted into its first step or advances to another step. `DialogueMgr` subscribes during `GameMgr` initialization and refreshes registered NPCs that have `NPCDialogueRuleConfig` entries. Each NPC stores a current dialogue group, updates it from task rules on refresh, and still refreshes once more before starting dialogue as a missed-event safeguard.
- Affected modules: Task runtime progression, NPC dialogue rule selection, NPC registration, dialogue manager initialization.
- Verification: `dotnet build WorkDemo.sln` passed with 62 existing `ItemJsonDatabase`/`BuildManager`/`ConfigHotUpdater` field warnings and 0 errors. Unity Play Mode should verify NPCs configured in `NPCDialogueRuleConfig` switch from task 1 step 1 group `t1s1` to the next configured group immediately after the task step advances.

## 2026-05-31 - IconAtlasMgr Negative Caching (Fix Hover Freeze)
- Change/task: Fixed a hard freeze when rapidly hovering role/item icons whose sprite can't currently be resolved (exposed after a Library reimport left the RoleAtlas unpacked). Complements the earlier "Role Avatar Loading Stabilization" (which prevents stale UI writes but did not stop the async flood).
- Changed files: `Assets/Scripts/Manager/IconAtlasMgr.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `IconAtlasMgr` had no negative caching, so an unresolved icon re-ran async Addressables work on every UI refresh — `GetDirectSprite` called `Addressables.LoadResourceLocationsAsync` every time, and a not-found atlas sprite was never remembered. Rapid hover re-queries icons, spawning an unbounded flood of async operations → freeze. Added negative caches (`noDirectSprite`, `missingAtlasSprites`), in-flight dedup of atlas loads (`atlasLoadTasks` + `UniTask.Preserve`), and warn-once logging. An unresolved icon now resolves to null once and returns instantly thereafter — no async flood, no freeze (icon stays blank until the source atlas is reimported).
- Affected modules: icon/sprite loading for all UI (items, roles, buffs, package).
- Verification: `dotnet build WorkDemo.sln` 0 errors. The underlying RoleAtlas still needs reimport to make role avatars actually display.

## 2026-05-31 - Role Avatar Loading Stabilization
- Change/task: Fixed role avatar loading instability in role creation/selection UI.
- Changed files: `Assets/Scripts/Manager/IconAtlasMgr.cs`, `Assets/Scripts/GameCore/GameUI/Item/RoleChooseIcon.cs`, `Assets/Scripts/GameCore/GameUI/Panel/CreateRolePanel.cs`, `Assets/Scripts/GameCore/GameUI/Panel/RoleInfoPanel.cs`, `Assets/Scripts/GameCore/GameCharacter/Role/RoleModelController.cs`, `Assets/AddressableAssetsData/AssetGroups/RoleImage.asset`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Role avatars now prefer directly addressable Sprite assets and only fall back to `RoleAtlas` if a direct sprite address is unavailable. Existing role avatar images are registered in the `RoleImage` Addressables group, UI image assignment forces simple aspect-preserving sprite display, prefab default avatar sprites are cleared before async loading, stale async icon/background/model loads are ignored by version checks, save-role avatar rows reject stale async icon writes and force UI image/material/vertex refresh, and CreateRolePanel no longer preloads every role model while opening the avatar list.
- Affected modules: CreateRolePanel avatar list, ChooseRolePanel save-role avatar rows, social default `PlayerIcon1` avatar path.
- Verification: `dotnet build WorkDemo.sln` passed with existing warnings. Unity Play Mode should still confirm CreateRolePanel and ChooseRolePanel avatar rendering after rebuilding Addressables content/catalogs if using packed/remote Addressables mode.

## 2026-05-30 - Resource Hot-Update Runtime + Server Hosting (Step 2)
- Change/task: Added the runtime + server pieces for Addressables remote resource hot-update (new item icons/models deliverable without rebuilding). The Addressables Editor config + content build remain manual.
- Changed files: `Assets/Scripts/GameCore/GameHotUpdate/ResourceHotUpdater.cs` (new), `Assets/Scripts/Manager/GameMgr.cs`, `Assembly-CSharp.csproj`; server: `WorkDemoServer/Program.cs` (static hosting), `WorkDemoServer/AddressablesContent/README.txt` (new hosting folder); `Docs/CHANGELOG_AI.md`.
- Behavior change: `ResourceHotUpdater.CheckAndUpdateCatalogsAsync()` (called best-effort from `GameMgr.Start` after the config hot-update) runs `Addressables.InitializeAsync` → `CheckForCatalogUpdates` → `UpdateCatalogs`; with no remote catalog configured/hosted it is a silent no-op. WorkDemoServer now serves `AddressablesContent/` at `/addressables` (ServeUnknownFileTypes for .bundle/.hash/.json), acting as the CDN. Intended RemoteLoadPath: `http://127.0.0.1:5188/addressables/[BuildTarget]`.
- Affected modules: startup flow, Addressables runtime, WorkDemoServer hosting.
- Verification: client `dotnet build WorkDemo.sln` 0 errors; server compiles 0 errors. End-to-end needs the manual Editor steps: set Profile RemoteLoadPath, enable Build Remote Catalog, mark ItemIcon/WeaponModel/MonsterModel groups Remote, build Addressables, copy `ServerData/*` into `AddressablesContent/`, rebuild the client once. Later content updates use "Update a Previous Build".

## 2026-05-30 - Config Hot-Update Loop (Step 1)
- Change/task: Added a config hot-update closed loop so item/recipe/monster JSON can be updated from the server without rebuilding the client.
- Changed files: `Assets/Scripts/GameCore/GameHotUpdate/HotUpdatePaths.cs` (new), `Assets/Scripts/GameCore/GameHotUpdate/ConfigHotUpdater.cs` (new), `Assets/Scripts/GameCore/GameItem/ItemJsonDatabase.cs`, `Assets/Scripts/GameCore/GameItem/RecipeJsonDatabase.cs`, `Assets/Scripts/GameCore/GameCharacter/Monster/MonsterJsonDatabase.cs`, `Assets/Scripts/Manager/GameMgr.cs`, `Assembly-CSharp.csproj`; server: `WorkDemoServer/Services/HotUpdateService.cs` (new), `WorkDemoServer/Contracts/HotUpdateResponses.cs` (new), `WorkDemoServer/Program.cs`, `WorkDemoServer/HotUpdateContent/GameData/**` (seeded JSON); `Docs/CHANGELOG_AI.md`.
- Behavior change: New load priority in the JSON databases is `persistentDataPath/HotUpdate/<relative>` (downloaded) → editor filesystem paths → `Resources` (baked baseline). `ConfigHotUpdater.CheckAndApplyAsync()` (called best-effort from `GameMgr.Start` after VFX init) fetches `GET /api/hotupdate/manifest`, compares MD5 against the locally saved manifest, downloads changed files via `GET /api/hotupdate/file?path=...` into the hot-update dir, then `GameMgr` reloads the three databases. Server unreachable = silent no-op (game uses baked baseline). Server side serves files + MD5 from `WorkDemoServer/HotUpdateContent/` with path-traversal protection.
- Affected modules: Item/Recipe/Monster config loading, startup flow, WorkDemoServer.
- Verification: client `dotnet build WorkDemo.sln` 0 errors; server compiles 0 errors (the running server only locks its bin output during rebuild). End-to-end requires running the server + a player build; editing a JSON under `HotUpdateContent` should change the data in-client on next launch without a rebuild.
- Note: this is data/config hot-update only (reusing existing C# mechanics). New item BEHAVIOR/logic still needs Lua/HybridCLR. Asset (icon/model) hot-update via Addressables remote is the planned step 2. For server publish (not `dotnet run`), `HotUpdateContent` must be copied to the publish output.

## 2026-05-30 - Monster Data JSON Into Resources
- Change/task: Fixed monster AI being inert in builds (monsters stood still, never chased/attacked) because `MonsterData.json` did not load in the packaged player.
- Changed files: moved `Assets/GameData/MonsterData/MonsterData.json` (+ .meta) to `Assets/Resources/GameData/MonsterData/MonsterData.json`; `Assets/Scripts/GameCore/GameCharacter/Monster/Editor/Tools/MonsterExcelToJsonTool.cs`; `Docs/CHANGELOG_AI.md`.
- Behavior change: `MonsterJsonDatabase.LoadJsonText()` only resolves `Application.dataPath`/`Directory.GetCurrentDirectory` paths in the Editor; in a build it relies on the `Resources.Load("GameData/MonsterData/MonsterData")` fallback. The JSON was under `Assets/GameData` (not a Resources folder), so the build loaded no monster data, `MonsterController.runtime` stayed null, and `CanDetectPlayer()`/`CanEnterAttackState()` always returned false. Moving the JSON into `Assets/Resources/...` (mirroring the earlier item-JSON fix) lets it load in both Editor and build. `MonsterExcelToJsonTool.OutputRelativePath` now writes to the Resources location so re-exports stay in sync.
- Affected modules: Monster data loading, monster FSM detection/attack, monster Excel export tool.
- Verification: `dotnet build WorkDemo.sln` passed with 60 pre-existing warnings and 0 errors. Unity must reimport the moved JSON; a packaged build should then show monsters detecting/chasing/attacking. NOTE: a second, separate build error remains — `Failed to create agent because there is no valid NavMesh` — which blocks movement and must be fixed by baking the GameScene NavMesh and ensuring it ships with the (Addressable) scene.

## 2026-05-30 - Recipe Data JSON Into Resources
- Change/task: Fixed crafting recipes not loading in builds (same packaging bug as monster data).
- Changed files: moved `Assets/GameData/RecipeData/RecipeData.json` (+ .meta) to `Assets/Resources/GameData/RecipeData/RecipeData.json`; `Assets/Scripts/GameCore/GameItem/RecipeJsonDatabase.cs`; `Assets/Scripts/GameCore/GameItem/Editor/Tools/RecipeExcelToJsonTool.cs`; `Docs/CHANGELOG_AI.md`.
- Behavior change: `RecipeJsonDatabase.LoadJsonText()` previously only resolved Editor-only filesystem paths and had NO `Resources.Load` fallback, so builds loaded zero recipes. Added a `Resources.Load<TextAsset>("GameData/RecipeData/RecipeData")` fallback and moved the JSON into `Assets/Resources/...`. `RecipeExcelToJsonTool.OutputRelativePath` now writes to the Resources location.
- Affected modules: Recipe data loading, crafting, recipe Excel export tool.
- Verification: `dotnet build WorkDemo.sln` passed with 60 pre-existing warnings and 0 errors. Unity must reimport the moved JSON; a packaged build should then load crafting recipes.

## 2026-05-30 - Settings Screen Mode Toggles
- Change/task: Replaced the broken full-screen/windowed `TMP_Dropdown` in the settings panel with two single-select Toggles, matching the resolution ToggleGroup pattern.
- Changed files: `Assets/Scripts/GameCore/GameUI/Panel/GameSettingPanel.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `GameSettingPanel` no longer reads `ScreenDropdown`. It now looks for `PanelBK/PictureContent/FullScreenToggle` and `WindowedToggle`, binds them so the selected one applies the screen mode through `Screen.SetResolution` and persists `FullScreenPrefKey`. Reason: the TMP_Dropdown was correctly wired but its popup list was unusable at runtime (options not visible / felt unresponsive); two toggles avoid the dropdown popup entirely and mirror the existing resolution selector.
- Affected modules: Settings panel video options.
- Verification: `dotnet build WorkDemo.sln` passed with 60 pre-existing warnings and 0 errors. Unity Play Mode should verify the two toggles exist under PictureContent in the prefab, share one ToggleGroup with Allow Switch Off disabled, and switch the game between full-screen and windowed.

## 2026-05-30 - Player Death And Respawn
- Change/task: Added a player death state with a Dead animation, a death panel, and revive at a bed/initial respawn point.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Player/PlayerDeathController.cs` (new), `Assets/Scripts/GameCore/GameUI/Panel/PlayerDeadPanel.cs` (new), `Assets/Scripts/GameCore/HSM/PlayerStateDriver.cs`, `Assets/Scripts/GameCore/GameFile/GameFile.cs`, `Assets/Scripts/GameCore/GameBuild/BedInteraction.cs`, `Assembly-CSharp.csproj`, `Docs/CHANGELOG_AI.md`, `Docs/AI_CONTEXT.md`, `Docs/FEATURE_MAP.md`.
- Behavior change: When `PlayerHealth.OnDied` fires (HP<=0), `PlayerDeathController` disables local control via `PlayerStateDriver.SetLocalControlEnabled(false)`, plays the `Dead` animator state, frees the cursor + switches to the UI action map, then shows `PlayerDeadPanel` and destroys all other panels. Clicking `ContinueButton` calls `PlayerDeathController.ReviveAsync`, which teleports the player to the respawn point, full-heals via `PlayerHealth.SetCurrentHP(MaxHP)`, replays `Idle`, re-enables control, relocks the cursor + restores the player action map, and re-shows `PlayerMainPanel`. The death controller is auto-added to the local gameplay player by `PlayerStateDriver.Start`, mirroring `PlayerHealth`.
- Data format change: `GameFile` gained a dedicated respawn point (`hasRespawnPoint`, `respawnScene`, `respawnPosition`, `respawnRotation`) with `SetRespawnPoint`/`TryGetRespawnPoint`. The bed (`BedInteraction`) now writes this respawn point. It is intentionally separate from `playerSceneLocations`, because `FileMgr.UpdateFileData` overwrites the scene location with the player's current position on every save, so that list cannot serve as a stable respawn point. With no bed built, respawn falls back to `PlayerInitialDataSO.initialPosition/initialRotation`. Older saves without the new fields default to `hasRespawnPoint=false` (initial point).
- Affected modules: Player health/death, HSM driver component setup, UI panel flow, save data (`GameFile`), build bed interaction.
- Verification: `dotnet build WorkDemo.sln` passed with 60 pre-existing warnings and 0 errors. Unity Play Mode should verify the player Animator has a `Dead` state, the `PlayerDeadPanel` prefab has an Addressables key matching its class name with a `ContinueButton` child, and that revive returns to the bed when built and to the initial point otherwise.

## 2026-05-29 - Runtime Item JSON Resources
- Change/task: Moved runtime item JSON files into Resources and added a packaged-player fallback loader.
- Changed files: `.gitignore`, `Assets/Resources/GameData/ItemData/*.json`, `Assets/Scripts/GameCore/GameItem/ItemJsonDatabase.cs`, `Assets/Scripts/GameCore/GameItem/Editor/Tools/*ExcelToJsonTool.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Item config can load in packaged builds through `Resources.Load<TextAsset>`, so task item rewards and debug item grants no longer fail because item ids have no config.
- Affected modules: Item database, PackageMgr item grants, task item rewards, item Excel export tools.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. Unity packaged build should verify item rewards and grant-all behavior.

## 2026-05-29 - Player Initial Inventory Config
- Change/task: Added configurable starting inventory items to the player initial data SO.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Player/PlayerInitialDataSO.cs`, `Assets/Scripts/Manager/FileMgr.cs`, `Docs/CHANGELOG_AI.md`, `Docs/FEATURE_MAP.md`.
- Behavior change: `PlayerInitialDataSO` now exposes `initialInventoryItems`, each with an item id and count. When `FileMgr.CreateNewGame` creates a new role save, it validates item ids through `ItemJsonDatabase`, writes valid entries into `InventorySaveData`, stacks non-weapon items by id, and creates one inventory instance per weapon count.
- Affected modules: Player initial data, new-role save creation, inventory save data.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. Unity Play Mode should verify a newly created role receives the configured starting items in the correct inventory tabs.

## 2026-05-29 - Crafting Station Recipe Filters
- Change/task: Changed crafting station prompts and filtered RecipePanel entries by station type.
- Changed files: `Assets/Scripts/GameCore/GameBuild/CraftingStationInteraction.cs`, `Assets/Scripts/GameCore/GameUI/Panel/RecipePanel.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Equipment crafting stations now show `合成装备` and only list recipes whose target item type is `Weapon`; consumable crafting stations now show `合成消耗品` and only list recipes whose target item type is `Consumable`.
- Affected modules: Build interaction prompts, RecipePanel recipe listing, crafting stations.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. Unity Play Mode should verify both station prefabs have the expected `BuildInteractionType` and show the correct filtered recipe lists.

## 2026-05-29 - Save Before Quit
- Change/task: Made quit buttons save the current game file before exiting the application.
- Changed files: `Assets/Scripts/GameCore/GameUI/Panel/GameStartPanel.cs`, `Assets/Scripts/GameCore/GameUI/Panel/GameSettingPanel.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `GameStartPanel/EndButton` now saves the current active save file before quitting when a current save exists. `GameSettingPanel/GameEndButton` and the quit confirmation flow now share a guarded save-before-quit path and only quit after `GameMgr.File.SaveGameFile()` succeeds. If saving fails, the game stays open and shows the existing save-failed message.
- Affected modules: Start menu quit flow, in-game settings quit flow, save-file persistence.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors.

## 2026-05-29 - Build Place And Demolish Wood Sound
- Change/task: Played the `WoodHit2` sound when build objects are placed or demolished.
- Changed files: `Assets/Scripts/GameCore/GameBuild/BuildManager.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Successful placement and successful demolish now call `GameMgr.Audio.PlayAt("Game", "WoodHit2", position)` at the build object's world position.
- Affected modules: Build placement, build demolish mode, game sound playback.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. Unity Play Mode should verify the `Game/WoodHit2` sound entry exists and is audible.

## 2026-05-29 - Boss Battle BGM Switch
- Change/task: Switch BGM during Boss battles and restore the previous scene/music afterward.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossController.cs`, `Assets/Scripts/Manager/AudioMgr.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `BossController` now plays `BGM_BOSSBattle` when Boss battle starts and restores the previously playing BGM, or the current scene BGM if no previous BGM was recorded, when battle ends. A shared active Boss battle counter prevents music from restoring early if multiple Boss battle states overlap.
- Affected modules: Boss battle lifecycle, BGM playback, audio manager state access.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. Unity Play Mode should verify entering a Boss fight fades to `BGM_BOSSBattle`, ending/defeating the Boss restores the previous or scene BGM, and overlapping Boss battle states do not restore early.

## 2026-05-29 - TaskPanel Close Button Binding
- Change/task: Made `TaskPanel` close itself from the `CloseButton`.
- Changed files: `Assets/Scripts/GameCore/GameUI/Panel/TaskPanel.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `TaskPanel` now resolves the close button from the `CloseButton` transform or its children and calls `GameMgr.UI.HidePanel<TaskPanel>()` when clicked.
- Affected modules: Task UI panel.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. Unity Play Mode should verify the prefab's `CloseButton` receives clicks.

## 2026-05-29 - Build Demolish Target Highlight
- Change/task: Added a clear red transparent highlight for the currently aimed build object while in demolish mode.
- Changed files: `Assets/Scripts/GameCore/GameBuild/BuildManager.cs`, `Assets/Scripts/GameCore/GameBuild/BuildPreview.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Demolish mode now updates the current raycast target every frame, temporarily replaces that build object's renderer materials with a red transparent material, restores the original materials when the target changes or the mode exits, and destroys the highlighted target on left click.
- Affected modules: Build demolish mode, build preview material helper.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. Unity Play Mode should verify the selected demolish target is visually obvious and material restoration works when moving the cursor away or exiting demolish mode.

## 2026-05-27 - Dialogue Choice Cleanup And Link Diagnostics
- Change/task: Fixed dialogue options persisting across consecutive multiple-choice nodes and added stronger diagnostics for dialogue start/group/link failures.
- Changed files: `Assets/Scripts/GameCore/GameUI/Panel/GameDialoguePanel.cs`, `Assets/Scripts/Manager/DialogueMgr.cs`, `Assets/Scripts/GameCore/GameDialogue/Editor/Utilities/DLIOUtility.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Dialogue choices are now cleared whenever the panel switches nodes and immediately disabled when a choice is clicked, preventing stale UI choices from sending invalid choice indexes and stopping the dialogue. Runtime warnings now include the dialogue container, requested group, available groups, fallback target, current node, and missing choice link text. Dialogue graph export now warns when a choice has no valid connected target node.
- Affected modules: Dialogue UI, runtime dialogue traversal, dialogue graph export diagnostics.
- Verification: `dotnet build WorkDemo.sln` passed with 61 existing `gltfExporter`/`ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. Unity Play Mode should verify NPC 2 task dialogue `t1s1` clears old options when moving from node 1 to node 2 and either reaches the `CompleteDialogueTask` event node or logs the exact missing group/link.

## 2026-05-27 - Dialogue Group Matching Diagnostics
- Change/task: Hardened dialogue group lookup and improved dialogue task completion diagnostics.
- Changed files: `Assets/Scripts/Manager/DialogueMgr.cs`, `Assets/Scripts/GameCore/GameTask/TaskManager.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Dialogue group name lookup now trims whitespace and ignores casing before falling back to the first available start node. `CompleteDialogueTask` warnings now report when the task is not accepted/already completed or what the current step is when the requested dialogue step does not match.
- Affected modules: NPC dialogue group selection, dialogue task completion debugging.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors.

## 2026-05-27 - Boss Quarter HP Hurt Reactions
- Change/task: Added Boss hurt animation reactions when HP drops by quarter thresholds.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossConfigSO.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossController.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Boss configs now expose `Enter Hurt State On Quarter HP Loss` and `Hurt HP Loss Step`, defaulting to one hurt reaction per 25% max HP lost. `BossController` tracks the next normalized HP threshold, queues crossed thresholds, and enters `BossHurtState` to play the configured `HurtStateName` animation once per crossed quarter, while avoiding Dead/PhaseChange/Hurt interruptions.
- Affected modules: Boss damage response, Boss hurt animation playback, Boss config tuning.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. Unity Play Mode should verify the Boss plays its configured hurt animation after crossing 75%, 50%, and 25% HP, and does not interrupt death or phase-change states.

## 2026-05-27 - Explicit Dialogue Task Completion Node
- Change/task: Added a dialogue graph event type for explicitly completing dialogue-task steps.
- Changed files: `Assets/Scripts/GameCore/GameDialogue/Enumerations/DialogueEventType.cs`, `Assets/Scripts/GameCore/GameDialogue/ScriptableObjects/DLSO.cs`, `Assets/Scripts/GameCore/GameDialogue/Data/Save/DLNodeSaveData.cs`, `Assets/Scripts/GameCore/GameDialogue/Editor/Elements/DLNode.cs`, `Assets/Scripts/GameCore/GameDialogue/Editor/Elements/DLEventNode.cs`, `Assets/Scripts/GameCore/GameDialogue/Editor/Utilities/DLIOUtility.cs`, `Assets/Scripts/Manager/DialogueMgr.cs`, `Assets/Scripts/GameCore/GameTask/TaskManager.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Dialogue event nodes now include `CompleteDialogueTask`, using Task ID plus Task Step ID in the visual dialogue editor. When runtime reaches this event node, `TaskManager.CompleteDialogueTask(taskID, stepID)` completes only the matching current dialogue step and then runs the normal task update/completion/save path. Closing a dialogue no longer auto-advances dialogue objectives by NPC id, so task completion timing is controlled by the explicit event node.
- Affected modules: Dialogue graph event nodes, NPC dialogue task completion, task runtime progress/save.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. Unity Editor should verify the new `CompleteDialogueTask` option appears on dialogue Event nodes, shows Task Step ID, and completes only the intended accepted dialogue task step.

## 2026-05-27 - Task Objective Display Names
- Change/task: Replaced raw task target IDs in task objective prompts with readable NPC, monster, and item names.
- Changed files: `Assets/Scripts/GameCore/GameTask/TaskDisplayNameResolver.cs`, `Assets/Scripts/GameCore/GameTask/TaskObjectiveRuntime.cs`, `Assets/Scripts/GameCore/GameTask/TaskStepRuntime.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Runtime task prompts now resolve dialogue objectives through registered NPC data, kill objectives through `MonsterJsonDatabase`, and collect objectives through item config data. Multi-objective steps use the step name as a heading and show each resolved objective underneath, avoiding old generated descriptions like `请和2对话`.
- Affected modules: Main HUD tracked-task text, TaskPanel target text, task runtime objective display.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. Unity Play Mode should verify NPC id 2 displays as the configured NPC name after the NPC has registered in the scene.

## 2026-05-27 - Multiplayer Boss Death And Resummon Cleanup
- Change/task: Fixed summoned Boss lifecycle issues after Boss death in multiplayer.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossSummonArena.cs`, `Assets/Scripts/GameCore/GameNetwork/NetworkBoss.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Client display Bosses now hide shortly after receiving a Host dead-state snapshot so defeated Bosses disappear from invited players' view. `BossSummonArena` now listens for the active Boss death, resets summon state, and cleans up dead Boss instances before the next summon so using another Boss summon item can create a fresh Boss instead of reusing the dead one.
- Affected modules: Boss summon item flow, Host-authoritative Boss death sync, client Boss display cleanup, repeated Boss summon testing.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. Unity Play Mode should verify Boss disappears for invited players after death and a second summon item can spawn a fresh Boss.

## 2026-05-27 - Multiplayer Boss Projectile Visual Sync
- Change/task: Added client-side visual synchronization for Host Boss barrage/projectile attacks.
- Changed files: `Assets/Scripts/GameCore/GameNetwork/NetworkBoss.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossSkillRuntimePlayer.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossProjectileRuntimeController.cs`, `Docs/MultiplayerStageOne.md`, `Docs/CHANGELOG_AI.md`.
- Behavior change: When the Host Boss spawns an authored barrage projectile, it now broadcasts a `WorkDemo.BossProjectileVisual` custom message containing the projectile VFX key, pose, speed, lifetime, and offset data. Clients create a visual-only projectile runner, attach the matching looping VFX key to it, and move it locally without damage/collision authority. Boss projectile damage remains Host-authoritative through the existing Boss damage RPC path.
- Affected modules: Multiplayer Boss barrage visuals, NetworkBoss custom messaging, Boss projectile runtime pooling.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. Unity Play Mode should verify invited players can see Boss projectile/barrage effects such as `BOSS_VFX1/2/3`.

## 2026-05-27 - Multiplayer Boss Combat Feedback Fix
- Change/task: Fixed several missing feedback/authority paths in multiplayer Boss fights.
- Changed files: `Assets/Scripts/GameCore/GameNetwork/NetworkBoss.cs`, `Assets/Scripts/GameCore/GameNetwork/NetworkPlayer.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossHealth.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossAttackController.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossSkillRuntimePlayer.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossProjectileRuntimeController.cs`, `Docs/MultiplayerStageOne.md`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Boss state snapshots now include the Host Animator base-layer state so clients can see Boss attack animations instead of only position/HP changes. The Host keeps remote players' `CharacterController` enabled for server-side Boss hit detection, and Boss fallback attacks, authored skill hitboxes, contact damage, and Boss projectiles now route hits on remote `NetworkPlayer` objects through the owner-targeted Boss damage RPC instead of damaging the Host-side replica. Client-submitted Boss damage now returns the actual server damage number to the attacking client so invited players see their own damage popup.
- Affected modules: Host-authoritative Boss combat, remote player Boss damage, Boss animation display sync, client Boss damage numbers.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. Unity Play Mode should verify invited players can see Boss attack animations, take damage from Boss attacks, and see damage numbers after hitting the Boss.

## 2026-05-27 - Multiplayer Boss Summon Replication
- Change/task: Connected Boss summon item flow to the current Host-authoritative multiplayer bridge.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossController.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossSummonArena.cs`, `Assets/Scripts/GameCore/GameNetwork/NetworkBoss.cs`, `Assets/Scripts/GameCore/GameNetwork/NetworkMgr.cs`, `Assets/Scripts/Manager/PackageMgr.cs`, `Docs/MultiplayerStageOne.md`, `Docs/CHANGELOG_AI.md`.
- Behavior change: When the Host uses a Boss summon consumable, the normal `BossSummonArena` flow now broadcasts a `WorkDemo.BossSummon` custom message with the Arena id, Boss id, pose, HP, and battle-active state. Clients register Boss network handlers as soon as Host/Client/Relay starts, receive the summon before any local Boss exists, prepare a display-only Boss from the matching local Arena prefab, and then keep it updated through the existing `WorkDemo.BossState` snapshots.
- Affected modules: Boss summon consumables, BossSummonArena, NetworkBoss custom messaging, Host-authoritative Boss bridge.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing field warnings and 0 errors. Unity Play Mode should verify Host can summon a Boss during multiplayer, clients see the same Boss appear, and client Boss objects remain display-only while Host keeps authority.

## 2026-05-27 - Multiplayer Boss Summon Safety Guard
- Change/task: Added safety guards for using Boss summon consumables during an active multiplayer session.
- Changed files: `Assets/Scripts/GameCore/GameNetwork/NetworkBoss.cs`, `Assets/Scripts/Manager/PackageMgr.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Host/server instances now ignore incoming lightweight Boss state snapshot messages, preventing the authoritative Host Boss from being treated as a client display replica and repeatedly disabling/restoring Boss AI, skill runtime, and NavMesh authority. During multiplayer, non-host clients can no longer directly consume Boss summon items because dynamic Boss network spawning has not been implemented yet.
- Affected modules: Boss summon consumables, Host-authoritative Boss bridge, multiplayer Boss state sync.
- Verification: `dotnet build WorkDemo.sln` passed with 61 existing field warnings and 0 errors. Unity Play Mode should verify the Host can use a Boss summon item without freezing; full summoned Boss visibility on clients still needs a dedicated network-spawn/summon replication step.

## 2026-05-27 - Remote Attack Layer Reset
- Change/task: Fixed remote players staying visually stuck in a weapon attack after one networked attack playback.
- Changed files: `Assets/Scripts/GameCore/GameCombat/PlayerWeaponAttackEffectController.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Remote visual-only weapon attack playback now resets the Animator `UpperBody` attack layer back to `Empty` when the attack clip/effect duration ends. This matches the local attack flow where the weapon attack controller clears `weaponAttackPlaying` and the attack layer instead of leaving `Atk`/`Atk2` weighted on the remote replica.
- Affected modules: Remote player attack animation cleanup, multiplayer weapon attack presentation.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. Unity Play Mode should verify a remote player returns to idle/move after one attack instead of looping the attack pose.

## 2026-05-27 - Remote Weapon Animator Controller Sync
- Change/task: Fixed remote multiplayer players not switching to the armed Animator Controller before playing weapon attacks.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Player/PlayerWeaponModeController.cs`, `Assets/Scripts/GameCore/GameNetwork/NetworkPlayer.cs`, `Assets/Scripts/GameCore/GameCombat/PlayerWeaponAttackEffectController.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Remote network replicas now apply a visual-only weapon mode from network state, switching between `PlayerAnimator` and `PlayerNormal` instead of relying on the disabled local `PlayerWeaponModeController`/local equipment flow. Weapon attack RPCs also carry the owner's armed state so the remote player switches to the armed controller before playing attack visuals. Remote attack playback now tries the active controller's `UpperBody` attack states first and only falls back to timeline playback if the state is missing.
- Affected modules: Remote player weapon mode, remote attack animation, remote weapon VFX/audio playback, Animator Controller selection.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. Unity Play Mode should verify that the remote player switches to the armed controller and plays `Atk`/`Atk2` when attacking with a weapon.

## 2026-05-27 - Relay Invalid Allocation Recovery
- Change/task: Added recovery around Unity Relay allocation failures during friend-invite world joins.
- Changed files: `Assets/Scripts/GameCore/GameNetwork/NetworkMgr.cs`, `Assets/Scripts/GameCore/GameNetwork/WorkDemoRelayLobbyService.cs`, `WorkDemoServer/Services/OnlineInviteService.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `NetworkMgr` now listens for Netcode `OnTransportFailure`, reports that the Relay allocation expired/invalidated, cleans up Relay/Lobby state, and restores the player back to single-player instead of leaving the join flow in a bad state. Relay/Lobby session creation and joining now clear any previous session first, and `WorkDemoServer` expires pending online invites after 120 seconds so old invites cannot keep reusing stale Relay join data.
- Affected modules: Unity Relay/Lobby friend invite flow, invite accept loading path, client disconnect/restore behavior, backend online invite lifetime.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. Unity Play Mode should verify accepting a fresh invite succeeds and accepting an old invite asks for a new invite instead of repeatedly logging `allocation ID not found`.

## 2026-05-27 - Remote Attack Timeline Playback
- Change/task: Fixed remote network attack playback trying to use Animator Controller state names that may not exist on the remote player.
- Changed files: `Assets/Scripts/GameCore/GameCombat/PlayerWeaponAttackEffectController.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Remote visual-only weapon attacks now force the existing PlayableGraph timeline path from `WeaponAttackEffectSO.animationEvents` instead of calling `Animator.Play("Atk"/"Atk2")` on the `UpperBody` layer. This avoids `Animator.GotoState: State could not be found` warnings on remote replicas and lets remote attack animation/VFX/audio play from the effect asset clips without requiring matching Animator states.
- Affected modules: Remote player weapon attack animation, remote VFX/audio timing, Animator warning cleanup.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors.

## 2026-05-27 - Network Attack Visual Lookup And PlayerInput Pairing Fix
- Change/task: Fixed network attack visual lookup using only weapon model keys and stopped network players from enabling extra `PlayerInput` components.
- Changed files: `Assets/Scripts/GameCore/GameNetwork/NetworkPlayer.cs`, `Assets/Scripts/GameCore/GameCombat/PlayerWeaponAttackEffectController.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Weapon attack visual RPCs now send the owner's current `WeaponAttackEffectSO` asset key, effect weapon name, and weapon model name so remote clients can resolve attack VFX/audio from the same effect database instead of relying only on keys like `WeaponModel_08`. Network player instances now keep prefab `PlayerInput` components disabled and continue using the existing global `GameMgr.input` flow, preventing `Cannot find matching control scheme for PlayerRole` warnings from duplicate device pairing.
- Affected modules: Network player input ownership, remote weapon attack animation/VFX playback, weapon attack effect lookup.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors.

## 2026-05-27 - Weapon Attack Effect Addressable Key Guard
- Change/task: Prevented missing weapon attack effect Addressable keys from throwing red `InvalidKeyException` logs during multiplayer attack visual sync.
- Changed files: `Assets/Scripts/GameCore/GameCombat/PlayerWeaponAttackEffectController.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Weapon attack effect fallback loading now checks `Addressables.LoadResourceLocationsAsync` before calling `AssetLoader.LoadAsset<WeaponAttackEffectSO>`. Missing keys such as `WeaponAttackEffect_WeaponModel_08` now quietly resolve to no effect instead of logging `No Location found for Key=...` errors.
- Affected modules: Local weapon attack effect fallback loading, remote network attack visual playback, Addressables error noise.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors.

## 2026-05-27 - Boss VFX Missing Script Cleanup
- Change/task: Removed missing-script references from Boss VFX prefabs that were flooding the Console during runtime instantiation.
- Changed files: `Assets/Resources_moved/UseVFX/BOSS_VFX1.prefab`, `Assets/Resources_moved/UseVFX/BOSS_VFX2.prefab`, `Assets/Resources_moved/UseVFX/BOSS_VFX3.prefab`, `Docs/CHANGELOG_AI.md`.
- Behavior change: The three Boss VFX prefabs no longer reference the deleted script GUID `12ba759cd568c0e47a7018ab70867a0d`, so spawning these prefabs should stop producing repeated `The referenced script on this Behaviour ... is missing!` warnings. Particle and renderer components remain intact.
- Affected modules: Boss VFX resources, runtime VFX instantiation warnings.
- Verification: Text scan confirmed the deleted script GUID is no longer present in `BOSS_VFX1/2/3.prefab`. Unity Play Mode should verify the Console warning is gone when the Boss VFX plays.

## 2026-05-27 - Network Weapon Attack Visual RPC
- Change/task: Added first-pass network synchronization for player weapon attack VFX/audio playback.
- Changed files: `Assets/Scripts/GameCore/GameNetwork/NetworkPlayer.cs`, `Assets/Scripts/GameCore/GameCombat/PlayerWeaponAttackEffectController.cs`, `Assets/Scripts/GameCore/GameVFX/WeaponProjectileRuntimeController.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: When the local owner starts a weapon attack effect, `NetworkPlayer` now sends a ServerRpc/ClientRpc visual event so other clients play the matching remote player's weapon attack effect from the synced weapon model key. `PlayerWeaponAttackEffectController` now supports a remote visual-only mode that plays VFX/audio/projectile visuals without reading local equipment and without opening hitboxes or applying damage. Projectile runtime also has a visual-only initialization path so remote projectiles can move visually without damaging monsters.
- Affected modules: Player weapon attack VFX/audio, remote player combat presentation, projectile visual playback, Netcode player RPC flow.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors.

## 2026-05-27 - Network Player Weapon Visual Sync
- Change/task: Added lightweight network synchronization for player weapon visuals so remote player replicas no longer read the local client's `GameMgr.Equipment`.
- Changed files: `Assets/Scripts/GameCore/GameNetwork/NetworkPlayer.cs`, `Assets/Scripts/GameCore/GameCharacter/Role/PlayerWeaponModelController.cs`, `Assets/Scripts/GameCore/GameCharacter/Role/PlayerWeaponTrailController.cs`, `Assets/Scripts/GameCore/GameCharacter/Player/PlayerWeaponModeController.cs`, `Assets/Scripts/GameCore/GameCombat/PlayerWeaponAttackEffectController.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `NetworkPlayer` now publishes the owner's current weapon model key, armed/back state, artifact flag, and tool flag through Netcode variables. Remote replicas apply these values through `PlayerWeaponModelController` network visual mode instead of querying local equipment. Remote-only weapon mode, attack effect, and trail controllers self-disable when added after network spawn, preventing delayed components from binding to local equipment state.
- Affected modules: Host/client player weapon display, remote player visual isolation, weapon trail/effect local-only ownership.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors.

## 2026-05-27 - Online Invite Accept Loading Feedback
- Change/task: Added loading feedback while an invited player accepts and joins a friend's world.
- Changed files: `Assets/Scripts/GameCore/GameUI/Panel/OnlineRequestPanel.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Clicking `AgreeButton` on `OnlineRequestPanel` now disables the agree/refuse buttons, shows the existing `LoadingPanel` with the tip `正在加入好友世界...`, keeps it visible while `SocialMgr.AcceptOnlineRequestAsync` runs the invite response plus direct/Relay client connection, then hides it after success or failure. This makes the join wait feel like an intentional loading step instead of a frozen UI.
- Affected modules: Online invite popup, friend-world join UX, LoadingPanel reuse.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors.

## 2026-05-27 - Invite Client Connect Wait And Network PlayerInput Defaults
- Change/task: Hardened friend invite acceptance so the invited client waits for a real Netcode connection and avoids early `PlayerInput` control-scheme pairing on network player prefabs.
- Changed files: `Assets/Scripts/GameCore/GameNetwork/NetworkMgr.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `NetworkMgr` now disables `PlayerInput` on the registered network player prefab before Netcode spawns it; `NetworkPlayer` still re-enables input only for the local owner after ownership is known. Client and Relay-client starts now capture the local pose, wait up to 15 seconds for `IsConnectedClient`, and restore the local single-player player with a preserved `LastError` if the connection fails or times out.
- Affected modules: Friend online invite acceptance, direct/Relay client startup, network player prefab input defaults, failed client connection recovery.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors.

## 2026-05-27 - Player Boss Hit Reaction
- Change/task: Made Boss damage play the player's Hit animation and use a smaller diagonal knockback.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Player/PlayerHealth.cs`, `Assets/Scripts/GameCore/HSM/PlayerStateDriver.cs`, `Assets/Scripts/GameCore/HSM/PlayerContext.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Boss-sourced player damage now triggers `PlayerStateDriver.ApplyHitReaction`, crossfading the base Animator to the configured `Hit` state while briefly suppressing normal locomotion state updates so the hit animation is not immediately overwritten. The Boss hit knockback defaults were reduced to a smaller horizontal/vertical impulse.
- Affected modules: Player damage response, Boss hit knockback, player Animator state playback.
- Verification: `dotnet build WorkDemo.sln` passed with existing `ItemJsonDatabase`/`BuildManager` warnings and 0 errors. Unity Play Mode should verify Boss hits crossfade the player Animator to `Hit`, briefly preserve the animation, and apply a smaller diagonal knockback.

## 2026-05-27 - Boss Manual Body Collider Flow
- Change/task: Stopped runtime generation of Boss body solid colliders and switched contact-damage collision back to authored/manual colliders.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossBodyContactCapsuleSet.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossSkillRuntimePlayer.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `BossBodyContactCapsuleSet` no longer rebuilds generated solid colliders during play; any old generated solid collider children are disabled on startup. During Boss contact-damage windows, `BossSkillRuntimePlayer` now re-enables the Boss authored non-trigger colliders instead of enabling generated body colliders, so body blocking should come from colliders manually placed on the Boss body parts.
- Affected modules: Boss manual body colliders, Boss burrow movement collision suppression, Boss contact-damage collision mode.
- Verification: `dotnet build WorkDemo.sln` passed with existing `ItemJsonDatabase`/`BuildManager` warnings and 0 errors. Unity Play Mode should verify manually placed Boss body colliders follow the animated body parts, are disabled while the burrow movement is hidden, and return during the contact-damage window.

## 2026-05-27 - Boss Damage Layer Fix
- Change/task: Fixed player attacks failing to damage the runtime Boss when Boss child colliders were not on the Monster layer.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossController.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossBodyContactCapsuleSet.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `BossController` now forces its whole runtime hierarchy onto the `Monster` layer by default, matching the player weapon hit layer mask. Generated solid body colliders from `BossBodyContactCapsuleSet` now inherit the Boss layer as well, so contact-body colliders remain hittable/blocking under the same layer setup.
- Affected modules: Boss runtime spawning, Boss child colliders, generated Boss body solid colliders, player weapon damage against Boss.
- Verification: `dotnet build WorkDemo.sln` passed with existing `ItemJsonDatabase`/`BuildManager` warnings and 0 errors. Unity Play Mode should verify player melee/projectile attacks reduce Boss HP when the Boss is visible and not in the intentional underground collision-suppressed part of a movement skill.

## 2026-05-27 - Boss Burrow Collision And Knockback Flow
- Change/task: Fixed Boss burrow/teleport skill collision flow and added Boss hit knockback on players.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossSkillSO.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossSkillRuntimePlayer.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossBodyContactCapsuleSet.cs`, `Assets/Scripts/GameCore/GameCharacter/Player/PlayerHealth.cs`, `Assets/Scripts/GameCore/HSM/PlayerStateDriver.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Boss movement events now have `disableCollisionUntilContactDamage`, enabled by default. When the Movement trigger fires, Boss authored solid colliders and generated body colliders are disabled before teleporting to the locked target-foot position, so the hidden underground Boss will not push the player out of the map. When a contact-damage window starts, authored solid colliders stay disabled and generated body colliders are enabled; after contact damage ends or the skill stops, normal authored colliders are restored. Player damage from Boss sources now applies a diagonal upward knockback using the hit direction.
- Affected modules: Boss burrow/teleport skills, Boss body contact damage collision, player damage response.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings and 0 errors. Unity Play Mode should verify the Boss remains non-blocking during underground warning, emerges from the locked warning position, contact damage still applies, and Boss attacks knock the player up/back.

## 2026-05-26 - Boss Lifebar Main UI
- Change/task: Connected the new `PlayerMainPanel/BossContent` UI to Boss battle state and health.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossController.cs`, `Assets/Scripts/GameCore/GameUI/Panel/PlayerMainPanel.cs`, `Assets/Scripts/GameCore/GameUI/MainPanelUI/PlayerMainBossLifeBarController.cs`, `Assembly-CSharp.csproj`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `BossController` now raises global battle start/end notifications and exposes `DisplayName` from `BossConfigSO.bossName`. `PlayerMainPanel` hides `BossContent` by default, shows it when a Boss battle starts, writes `BossName`, and refreshes `BossLifebar` from `BossHealth.NormalizedHP`.
- Affected modules: Boss battle UI, PlayerMainPanel runtime UI controllers, Boss lifecycle events.
- Verification: `dotnet build WorkDemo.sln` passed with existing `ItemJsonDatabase`/`BuildManager` warnings and 0 errors. Unity Play Mode should verify `BossContent` is hidden outside battle, appears after the Boss fight starts, updates on damage, and hides when the Boss battle ends.

## 2026-05-26 - Boss Intro Timeline Animation Binding
- Change/task: Bound the Boss intro Timeline animation track to the runtime-spawned Boss instance.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossSummonArena.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Before the Boss intro Timeline plays, `BossSummonArena` now binds the configured Animation Track index, defaulting to the first Animation Track, to the active Boss Animator. This keeps Timeline animation working after the Boss is instantiated under `BossPos` at runtime.
- Affected modules: Boss summon arena intro Timeline playback.
- Verification: `dotnet build WorkDemo.sln` passed with existing `ItemJsonDatabase`/`BuildManager` warnings and 0 errors. Unity Play Mode should verify the Boss model plays the intro Timeline animation while the existing camera/audio tracks continue working.

## 2026-05-26 - Boss Audio Clip Fallback
- Change/task: Fixed Boss skill audio clips not playing when their names are not registered in `GameSoundDataSO`.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossSkillRuntimePlayer.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Boss skill audio events still prefer registered sound library entries when `soundName` resolves, but now fall back to the directly assigned `AudioClip` if the sound name is not found. This matches the weapon effect runtime behavior and makes dragged audio clips audible in Boss skills without extra sound library registration.
- Affected modules: Boss skill runtime audio playback.
- Verification: `dotnet build WorkDemo.sln` passed with existing `ItemJsonDatabase`/`BuildManager` warnings and 0 errors. Unity Play Mode should verify Boss skill audio clips play at the Boss position.

## 2026-05-26 - Boss Movement Foot Target And Warning Fix
- Change/task: Fixed Boss movement trigger destination sampling and target warning activation.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossSkillRuntimePlayer.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/Editor/BossSkillEditorWindow.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Movement triggers now resolve player destinations from the target's foot position instead of raw `Target.position`, and ground projection skips colliders belonging to the Boss or target so the teleport point is not pushed off by character colliders. Target warning preview/runtime activation now also starts when a warning prefab or VFX key is configured, even if the old checkbox was missed.
- Affected modules: Boss burrow/teleport skills, movement warning VFX/prefab playback, Boss skill editor movement preview.
- Verification: `dotnet build WorkDemo.sln` passed with existing `ItemJsonDatabase`/`BuildManager` warnings and 0 errors. Unity Play Mode should verify the burrow skill locks the player's feet at Movement trigger time and shows the configured red warning effect for `warningDuration`.

## 2026-05-26 - Boss Projectile Visual Physics Disabled
- Change/task: Disabled physics participation on spawned Boss projectile visual prefabs.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossProjectileRuntimeController.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Boss projectile visual prefabs now have child `Collider`s disabled and child `Rigidbody`s forced kinematic/non-colliding when spawned from the projectile visual pool. Projectile movement and damage remain code-driven through `BossProjectileRuntimeController`, preventing multi-shot visuals with colliders from bumping, rotating, or drifting off course.
- Affected modules: Boss barrage projectile visuals and projectile pooling.
- Verification: `dotnet build WorkDemo.sln` passed with existing `ItemJsonDatabase`/`BuildManager` warnings and 0 errors. Unity Play Mode should verify existing projectile prefabs that previously relied on visual colliders for non-damage behavior.

## 2026-05-26 - Boss Projectile Visual Rotation Stabilizer
- Change/task: Added an optional visual rotation stabilizer for Boss barrage projectile prefabs.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossSkillSO.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossSkillRuntimePlayer.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossProjectileRuntimeController.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/Editor/BossSkillEditorWindow.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Boss barrage clips now expose `Stabilize Visual Rotation`, enabled by default. When enabled, the projectile visual records child local rotations on spawn and restores them in `LateUpdate`, preventing prefab-side animation/scripts from spinning the projectile away from its launch-facing direction.
- Affected modules: Boss barrage projectile visuals and Boss skill editor barrage inspector.
- Verification: `dotnet build WorkDemo.sln` passed with existing `ItemJsonDatabase`/`BuildManager` warnings and 0 errors. Unity Play Mode should verify projectile prefabs that intentionally rely on rotating child transforms with this option disabled.

## 2026-05-26 - Boss Movement Target Warning
- Change/task: Added target warning VFX support to Boss movement triggers for burrow/teleport attacks.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossSkillSO.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossSkillRuntimePlayer.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/Editor/BossSkillEditorWindow.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `BossMovementEvent` can now show a warning at the movement destination after teleporting. The warning can use a VFX key or prefab, starts at `triggerTime`, locks the destination at movement trigger time, and is cleaned up after `warningDuration`.
- Affected modules: Boss skill movement events, Boss skill runtime playback, Boss skill editor scene preview.
- Verification: `dotnet build WorkDemo.sln` passed with existing `ItemJsonDatabase`/`BuildManager` warnings and 0 errors. Unity Play Mode should verify the warning prefab/VFX orientation, scale, and timing.

## 2026-05-26 - Boss Solid Collider Inset
- Change/task: Made generated Boss solid colliders slightly smaller than contact damage capsules.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossBodyContactCapsuleSet.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `BossBodyContactCapsuleSet` now exposes `Solid Collider Radius Inset` under `Solid Collision`; generated non-trigger `CapsuleCollider` radius uses contact capsule radius minus this inset, defaulting to a small 0.05m shrink so damage capsules can cover the blocking volume.
- Affected modules: Boss generated body collision, Boss contact damage authoring.
- Verification: `dotnet build WorkDemo.sln` passed with existing `ItemJsonDatabase`/`BuildManager` warnings and 0 errors. Unity Play Mode should tune the inset against the actual Boss scale.

## 2026-05-26 - Boss Skill Movement Trigger
- Change/task: Added a real movement trigger to Boss skills for burrow/teleport-style displacement attacks.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossSkillSO.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossSkillRuntimePlayer.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossController.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/Editor/BossSkillEditorWindow.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Boss skills can now add a Movement track/trigger point. At runtime the trigger teleports the Boss to either the target position captured at skill start, the target's current position, or the Boss current position plus offset, with optional ground projection and facing after move.
- Affected modules: Boss skill data, Boss skill runtime playback, Boss skill editor timeline, Boss controller movement/teleport handling.
- Verification: `dotnet build WorkDemo.sln` passed with existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. Unity Play Mode should verify the exact burrow timing, ground layer mask, and configured Boss skill asset.

## 2026-05-26 - Boss Body Solid Colliders
- Change/task: Added optional solid collider generation from Boss body contact capsules so the player cannot walk through large Boss bodies.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossBodyContactCapsuleSet.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `BossBodyContactCapsuleSet` now has a `Solid Collision` section. When `Build Solid Colliders` is enabled, it rebuilds non-trigger `CapsuleCollider` children under `[BossSolidBodyColliders]` from the configured body contact capsules at runtime; a context menu also allows manual rebuilds in the editor.
- Affected modules: Boss physical collision, Boss contact capsule authoring, player `CharacterController` blocking against Boss bodies.
- Verification: `dotnet build WorkDemo.sln` passed with existing `ItemJsonDatabase`/`BuildManager` warnings and 0 errors. Unity Play Mode should verify the generated collider fit and Physics layer collision matrix.

## 2026-05-25 - Chest Interaction Rewards
- Change/task: Converted ARPG chest VFX usage from automatic looping to player-triggered interaction with configurable rewards.
- Changed files: `Assets/Scripts/GameCore/GameItem/ChestInteraction.cs`, `Assets/Scripts/GameCore/GameUI/Interact/InteractUI.cs`, `Assets/Scripts/GameCore/GameCharacter/NPC/NPCInteraction.cs`, `Assembly-CSharp.csproj`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Chests can now show an "open chest" interaction prompt when the player is close, play the configured open VFX once when pressing `F`, and grant configured item rewards by `itemId` and count through `PackageMgr`. The chest component disables `ARPGFXLoopScript` and `ARPGFXCycler` on the same object so the old demo looping effect no longer starts automatically.
- Affected modules: Chest interaction, interact prompt UI, inventory reward grant, ARPGFX chest effect usage.
- Verification: `dotnet build WorkDemo.sln` passed with existing `ItemJsonDatabase`/`BuildManager` warnings and 0 errors. Unity Play Mode should verify the chest prefab component bindings and one-shot open timing.

## 2026-05-25 - Boss Summon Parent And Stationary Lock
- Change/task: Made summoned Boss prefabs attach under the configured `BossPos` and added a runtime movement lock for stationary Boss battles.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossSummonArena.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossController.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `BossSummonArena` now parents the spawned Boss instance under `Boss Spawn Point`, resets both the placement root and BossController transform local position/rotation to zero, and locks normal AI/NavMesh movement by default. Movement lock disables `NavMeshAgent` so stationary Boss prefabs are not moved back by agent synchronization. Boss skill runtime movement and animation-driven skill behavior remain available.
- Affected modules: Boss summon arena, Boss AI movement control, Boss prefab hierarchy placement.
- Verification: `dotnet build WorkDemo.sln` passed with existing `ItemJsonDatabase`/`BuildManager` warnings and 0 errors. Unity Play Mode should confirm the spawned Boss appears as a child of `BossPos` and only moves through configured skills.

## 2026-05-25 - Settings Panel Function Buttons
- Change/task: Connected the settings panel close, save, get-all-items, return-to-start, and end-game buttons.
- Changed files: `Assets/Scripts/GameCore/GameUI/Panel/GameSettingPanel.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `CloseButton` hides the settings panel, `SaveButton` saves the current game, `GetItemButton` grants every configured item using the existing all-items debug logic, `BackStartButton` saves then loads `GameStartScene` and shows `GameStartPanel`, and `GameEndButton` saves before stopping Play Mode or quitting the build.
- Affected modules: Settings UI, save flow, inventory debug item grant, scene return flow.
- Verification: `dotnet build WorkDemo.sln` passed with existing 60 warnings and 0 errors. Unity Play Mode should verify the new prefab button hierarchy and runtime scene transition.

## 2026-05-25 - Settings Panel Display Options
- Change/task: Connected the settings panel `ScreenDropdown` and resolution toggles to runtime display settings.
- Changed files: `Assets/Scripts/GameCore/GameUI/Panel/GameSettingPanel.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Selecting the fullscreen option switches to `FullScreenMode.FullScreenWindow`, and selecting the window option switches to `FullScreenMode.Windowed`. `1920Toggle`, `1600Toggle`, and `1280Toggle` are mutually exclusive and apply 1920x1080, 1600x900, and 1280x720 respectively. Choices are saved to `PlayerPrefs` and restored when the settings panel initializes.
- Affected modules: Settings UI, display mode, resolution settings.
- Verification: `dotnet build WorkDemo.sln` passed with existing 60 warnings and 0 errors. Unity Play Mode should verify the Dropdown/Toggle bindings and actual window/fullscreen/resolution transitions.

## 2026-05-25 - Settings Panel Audio Sliders
- Change/task: Connected the settings panel volume sliders to runtime audio control.
- Changed files: `Assets/Scripts/Manager/AudioMgr.cs`, `Assets/Scripts/GameCore/GameUI/Panel/GameSettingPanel.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `MainVoiceSlider` controls master volume, `BGMSlider` controls BGM/ambient music, and `SoundSlider` controls UI/game sound effects played through `AudioMgr`. Values are saved to `PlayerPrefs` and restored on startup.
- Affected modules: Settings UI, BGM playback, UI/game effect playback.
- Verification: `dotnet build WorkDemo.sln` passed with existing `ItemJsonDatabase`/`BuildManager` warnings and 0 errors. Unity Play Mode should verify the three Slider bindings on the edited prefab.

## 2026-05-25 - Suppress Multiplayer Input And Animator Warnings
- Change/task: Reduced noisy multiplayer warnings for duplicated player input pairing and Animator calls made before a valid Animator Controller is available.
- Changed files: `Assets/Scripts/GameCore/GameNetwork/NetworkPlayer.cs`, `Assets/Scripts/GameCore/HSM/PlayerStateDriver.cs`, `Assets/Scripts/GameCore/HSM/States/Idle.cs`, `Assets/Scripts/GameCore/HSM/States/Walking.cs`, `Assets/Scripts/GameCore/HSM/States/Run.cs`, `Assets/Scripts/GameCore/HSM/States/JumpState.cs`, `Assets/Scripts/GameCore/HSM/States/FallState.cs`, `Assets/Scripts/GameCore/HSM/States/LandingState.cs`, `Assets/Scripts/GameCore/HSM/States/HoverFallState.cs`, `Assets/Scripts/GameCore/HSM/States/FlyingState.cs`, `Assets/Scripts/GameCore/HSM/States/Atk.cs`, `Assets/Scripts/GameCore/HSM/States/PlayerRoot.cs`, `Assets/Scripts/GameCore/GameCharacter/Player/PlayerWeaponModeController.cs`, `Assets/Scripts/GameCore/GameCombat/PlayerWeaponAttackEffectController.cs`, `Assets/Scripts/GameCore/GameCharacter/Role/RoleModelController.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Network player instances now temporarily disable child `PlayerInput` components while Netcode ownership is unresolved, then re-enable them only for the local owner. Player animation code now prefers Animator components that actually have a `RuntimeAnimatorController`, and state/attack/role idle code skips Animator reads or playback until a valid controller is present.
- Affected modules: Multiplayer player spawn/input ownership, player HSM animation playback, weapon mode animation, weapon attack effect animation event polling, role model random idle.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors.

## 2026-05-25 - GameStart Logout Message Anchor
- Change/task: Moved the "已退出当前账号" message toast to the new `MessagePosition` anchor.
- Changed files: `Assets/Scripts/GameCore/GameMessage/GameMessage.cs`, `Assets/Scripts/Manager/MessageMgr.cs`, `Assets/Scripts/GameCore/GameUI/Panel/GameStartPanel.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `MessageMgr` now supports an optional target parent for a message. `GameStartPanel` uses `MessagePosition` only for the logout completion message, while the logout confirmation `TipPanel` keeps its normal placement.
- Affected modules: Game start menu logout completion message UI, message toast parenting.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors.

## 2026-05-25 - Relay Lobby Already Member Join Fix
- Change/task: Fixed Relay invite acceptance failing with `player is already a member of the lobby`.
- Changed files: `Assets/Scripts/GameCore/GameNetwork/WorkDemoRelayLobbyService.cs`, `Docs/CHANGELOG_AI.md`, `Docs/AI_CONTEXT.md`.
- Behavior change: Unity Gaming Services Authentication now uses a deterministic profile based on the current WorkDemo `playerId` before anonymous sign-in, preventing different WorkDemo accounts in local multi-client testing from sharing the same cached UGS anonymous player. Relay clients also treat Lobby "already member" responses as recoverable and continue to configure Relay from the invite join code.
- Affected modules: Relay/Lobby invite join, UGS Authentication profile selection.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. `dotnet build WorkDemoServer/WorkDemoServer.csproj -o Temp/ServerBuildCheck` passed with 0 warnings and 0 errors.

## 2026-05-25 - Single Account Session Enforcement
- Change/task: Prevented the same backend account from staying online in multiple Unity clients at the same time.
- Changed files: `WorkDemoServer/Services/SessionService.cs`, `WorkDemoServer/Services/RealtimeConnectionHub.cs`, `WorkDemoServer/Program.cs`, `Assets/Scripts/GameCore/GameSocial/RealtimeMgr.cs`, `Assets/Scripts/Manager/GameMgr.cs`, `Docs/CHANGELOG_AI.md`, `Docs/AI_CONTEXT.md`, `Docs/FEATURE_MAP.md`.
- Behavior change: Creating a login/register session now invalidates old tokens for the same `playerId`. The server sends a `session.kicked` WebSocket event to old realtime connections and closes them. Unity handles that event by disconnecting any active network session, clearing the saved account/session token, and showing `LoginPanel`.
- Affected modules: Account login/register, session validation, realtime WebSocket lifecycle, Unity account state.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. `dotnet build WorkDemoServer/WorkDemoServer.csproj -o Temp/ServerBuildCheck` passed with 0 warnings and 0 errors. Direct build to `WorkDemoServer/bin` was blocked because the currently running `WorkDemoServer` process had the output DLL/EXE locked.

## 2026-05-25 - Relay Lobby Invite Bridge
- Change/task: Added the first Unity Relay/Lobby integration bridge for friend online invites while keeping local IP direct connect as a fallback.
- Changed files: `Assets/Scripts/GameCore/GameNetwork/RelayLobbySessionInfo.cs`, `Assets/Scripts/GameCore/GameNetwork/WorkDemoRelayLobbyService.cs`, `Assets/Scripts/GameCore/GameNetwork/NetworkMgr.cs`, `Assets/Scripts/GameCore/GameSocial/SocialMgr.cs`, `Assets/Scripts/GameCore/GameSocial/InviteData.cs`, `Assets/Scripts/GameCore/GameSocial/InviteResultData.cs`, `Assets/Scripts/GameCore/GameSocial/HttpOnlineInviteService.cs`, `Assets/Scripts/GameCore/GameSocial/RealtimeMgr.cs`, `WorkDemoServer/Contracts/OnlineInviteRequests.cs`, `WorkDemoServer/Contracts/OnlineInviteResponses.cs`, `WorkDemoServer/Services/OnlineInviteService.cs`, `WorkDemoServer/Services/RealtimeConnectionHub.cs`, `Assembly-CSharp.csproj`, `Docs/CHANGELOG_AI.md`, `Docs/MultiplayerStageOne.md`.
- Behavior change: Online invite payloads now carry `relayJoinCode` and `lobbyId` in addition to the previous direct IP/port fields. `NetworkMgr` has Relay Host/Client entry points and cleans up Lobby/Relay state on disconnect. `SocialMgr` now prefers Relay when `WorkDemoRelayLobbyService.IsAvailable` is true, so inviting a friend creates a Relay allocation/Lobby, keeps the host Lobby alive with heartbeat pings, and sends the Relay join code through the existing WorkDemoServer invite push; accepting joins via Relay. Without the Unity Gaming Services packages and scripting define, the project compiles and continues to fall back to the existing local IP direct-connect flow.
- Affected modules: Friend online invite flow, NetworkMgr session startup, WorkDemoServer invite DTOs/realtime push, Relay/Lobby setup path.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors. `dotnet build WorkDemoServer/WorkDemoServer.csproj` passed with 0 warnings and 0 errors.
- Setup note: Install `com.unity.services.core`, `com.unity.services.authentication`, `com.unity.services.relay`, and `com.unity.services.lobby`, link the project to Unity Dashboard, enable Authentication/Relay/Lobby services, then add the scripting define `WORKDEMO_USE_UGS_RELAY`.

## 2026-05-25 - Host Authoritative Multiplayer Boss Bridge
- Change/task: Added a first multiplayer Boss bridge so linked players can damage the Host-authoritative Boss and receive Boss damage on their owning clients.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Boss/BossHealth.cs`, `Assets/Scripts/GameCore/GameCharacter/Boss/BossAttackController.cs`, `Assets/Scripts/GameCore/GameNetwork/NetworkBoss.cs`, `Assets/Scripts/GameCore/GameNetwork/NetworkPlayer.cs`, `Assembly-CSharp.csproj`, `Docs/CHANGELOG_AI.md`, `Docs/MultiplayerStageOne.md`.
- Behavior change: `BossHealth` now auto-attaches `NetworkBoss`. In a multiplayer session, Client-side Boss hits are redirected to the owning `NetworkPlayer`, submitted to the Host by `ServerRpc`, and applied to the Host Boss health. The Host sends lightweight Boss position/rotation/HP/dead-state snapshots to clients through Netcode custom messages. Client Boss instances switch into display mode by disabling local Boss AI/attack/phase/runtime/NavMesh authority while preserving health and presentation. When the Host Boss attack overlaps a remote `NetworkPlayer`, damage is sent to that player's owning client so their local `PlayerHealth` takes damage and then syncs HP back through the existing player vitals path.
- Affected modules: Boss damage flow, Boss HP/position state sync, Boss-to-player damage, NetworkPlayer RPC bridge, multiplayer Boss authority rules.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors.
- Follow-up note: This is a first playable Boss bridge, not final Boss replication. Boss animation/skill VFX/event sync still needs a dedicated pass after two-client Play Mode testing confirms damage and HP sync.

## 2026-05-25 - Event Driven Multiplayer Player HP Sync
- Change/task: Made local `PlayerHealth.OnHealthChanged` immediately drive multiplayer HP synchronization for the owning `NetworkPlayer`.
- Changed files: `Assets/Scripts/GameCore/GameNetwork/NetworkPlayer.cs`, `Docs/CHANGELOG_AI.md`, `Docs/MultiplayerStageOne.md`.
- Behavior change: Owner `NetworkPlayer` now binds its local `PlayerHealth` when local gameplay state initializes. HP changes from damage, healing, initialization, or stat refresh immediately update server-owned HP network variables; Host owners write directly, while Client owners submit a dedicated vitals `ServerRpc`. Remote `FriendLifebar` can now react to HP changes without waiting for the movement sync interval.
- Affected modules: Player HP network sync, remote friend lifebar refresh, Host/Client player state replication.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors.

## 2026-05-25 - Multiplayer Friend Lifebar HUD
- Change/task: Connected the new `PlayerMainPanel/FriendLifebar` UI to remote multiplayer player state.
- Changed files: `Assets/Scripts/GameCore/GameNetwork/NetworkPlayer.cs`, `Assets/Scripts/GameCore/GameUI/Panel/PlayerMainPanel.cs`, `Assets/Scripts/GameCore/GameUI/MainPanelUI/PlayerMainFriendLifeBarController.cs`, `Assembly-CSharp.csproj`, `Docs/CHANGELOG_AI.md`, `Docs/MultiplayerStageOne.md`.
- Behavior change: `NetworkPlayer` now syncs owner display name, current HP, and max HP through server-owned network variables alongside movement/animation state. `PlayerMainPanel` drives a new friend lifebar controller that hides `FriendLifebar` outside active multiplayer or when no remote player exists, then shows the first remote player with default `PlayerIcon1`, remote display name, and HP percentage.
- Affected modules: Multiplayer player state replication, PlayerMainPanel HUD, remote friend HP/name display.
- Verification: `dotnet build WorkDemo.sln` passed with 60 existing `ItemJsonDatabase`/`BuildManager` field warnings and 0 errors.

## 2026-05-25 - Multiplayer Disconnect Button And Single-Player Restore
- Change/task: Added the PlayerMainPanel `BrokenLineButton` as the runtime disconnect entry and fixed the Host/Client disconnect flow so players return to local single-player control after leaving a multiplayer session.
- Changed files: `Assets/Scripts/GameCore/GameNetwork/NetworkMgr.cs`, `Assets/Scripts/GameCore/GameUI/Panel/PlayerMainPanel.cs`, `Docs/CHANGELOG_AI.md`, `Docs/MultiplayerStageOne.md`.
- Behavior change: `BrokenLineButton` is hidden outside multiplayer and shown while the runtime `NetworkManager` is listening. Clicking it stops the active Host/Client session, destroys network player replicas in `GameScene`, respawns the local single-player `Player` prefab, restores the last local/network pose when possible, re-enables player input, and rebinds scene cameras. If the Host stops the session, connected Clients now handle the disconnect callback by returning to their own single-player mode instead of staying in the Host world with disappearing characters. If a Client disconnects, its network player disappears from the Host world and the Client restores local single-player control.
- Affected modules: Player main HUD, multiplayer session lifecycle, Host/Client disconnect handling, local player/camera restore after network shutdown.
- Verification: `dotnet build WorkDemo.sln` passed with 0 warnings and 0 errors.

## 2026-05-25 - Game Start Account Display And Continue Flow
- Change/task: Updated `GameStartPanel` to show the logged-in account, move the logout confirmation prompt upward, and let Continue enter the current account's current/last save directly.
- Changed files: `Assets/Scripts/GameCore/GameUI/Panel/GameStartPanel.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `AccountText` now displays `当前账号：xxxxxxxxx` using the account name when available, falling back to player ID or `未登录`. `ContinueButton` now checks login, selects the current account's current save through `FileMgr`, applies save data, syncs the account display name from the character name, restores `PlayerModelManager.CurrentRoleModelName`, and loads the saved last scene with `GameScene` fallback. The logout confirmation `TipPanel` is positioned higher when opened from the start panel.
- Affected modules: Start menu UI, account display, save continuation flow, logout confirmation prompt positioning.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings and 0 errors.

## 2026-05-25 - Fix Client Remote Animation Stuck In Fall On Host
- Change/task: Fixed Host view showing the Client player stuck in the falling animation while Client view showed Host animation normally.
- Changed files: `Assets/Scripts/GameCore/GameNetwork/NetworkPlayer.cs`, `Assets/Scripts/GameCore/HSM/PlayerStateDriver.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Client-owned animation state submitted through `SubmitOwnerStateServerRpc` is no longer overwritten by the server-side remote replica's local `PlayerStateDriver.ctx`, which often has `grounded=false` because remote replicas do not read local input/control. Remote animation playback and Animator parameter writes now skip inactive Animators or Animators without a live controller.
- Affected modules: Host/Client remote player animation state replication, Animator warning suppression.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings and 0 errors.

## 2026-05-25 - Stabilize Multiplayer Remote Animation And HP Init
- Change/task: Improved the first-stage Host/Client player sync after reports that remote players could freeze on one animation and entering multiplayer could clear the local HP bar.
- Changed files: `Assets/Scripts/GameCore/GameNetwork/NetworkPlayer.cs`, `Assets/Scripts/GameCore/GameCharacter/Player/PlayerHealth.cs`, `Assets/Scripts/GameCore/GameUI/MainPanelUI/PlayerMainLifeBarController.cs`, `Docs/CHANGELOG_AI.md`, `Docs/MultiplayerStageOne.md`.
- Behavior change: Remote network player replicas now disable local-only gameplay/input components such as `PlayerInput`, Buff, weapon mode, attack controller, and attack effect controller, while still keeping visual/model/Animator display alive. Remote animation state re-applies when the Animator or Animator Controller changes and follows the local state names directly. Local owner spawn now forces HP initialization from player data before equipment/stat refresh can write runtime HP back. The main HP bar falls back to `playerData` instead of showing empty when a `PlayerHealth` component is not initialized yet.
- Affected modules: Host/Client remote player animation display, local-only input/gameplay isolation, player HP initialization, main HUD HP display.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings and 0 errors.
- Follow-up note: This is still a first-stage lightweight animation sync, not final full Animator replication. A later portfolio-quality step should replace/extend it with authoritative state parameters, weapon mode sync, attack trigger sync, and possibly `NetworkAnimator`/custom animation events.

## 2026-05-25 - Harden Local Player Damage Path
- Change/task: Fixed a regression where single-player monster attacks could fail to reduce the player's HP after multiplayer/Buff ownership isolation changes.
- Changed files: `Assets/Scripts/GameCore/GameCharacter/Player/PlayerHealth.cs`, `Assets/Scripts/GameCore/GameCharacter/Monster/MonsterAttackController.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `PlayerHealth` now lazily initializes itself from `GameMgr.Instance.playerData` before runtime HP changes if Start-order or prefab state leaves it uninitialized. Local HP changes still write back to player data, while non-owned network player replicas are ignored. Monster attack hit resolution now falls back through player components and children so hits on nested/player-root colliders can still find `PlayerHealth`.
- Affected modules: Monster melee damage, local player HP/runtime data, single-player and Host-owned player damage handling.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings and 0 errors.

## 2026-05-25 - Isolate Remote Player Buff Runtime
- Change/task: Fixed Buff gameplay data becoming ineffective after multiplayer changes allowed remote player instances to run local gameplay initialization.
- Changed files: `Assets/Scripts/GameCore/HSM/PlayerStateDriver.cs`, `Assets/Scripts/GameCore/GameCharacter/Player/PlayerHealth.cs`, `Assets/Scripts/GameCore/GameNetwork/NetworkPlayer.cs`, `Docs/CHANGELOG_AI.md`, `Docs/MultiplayerStageOne.md`.
- Behavior change: Only the local gameplay player initializes `PlayerHealth`, `BuffComponent`, initial buffs, and equipment-derived stat refreshes. Remote network players still keep visual/model/Animator initialization alive, but their Buff components are disabled and `PlayerHealth` no longer reads/writes `GameMgr.Instance.playerData`.
- Affected modules: Player Buff runtime, player health data sync, equipment stat refresh, Host/Client remote player display.
- Verification: `dotnet build WorkDemo.sln` passed with 0 warnings and 0 errors.
- Follow-up note: Unity Play Mode or two-client testing should verify that consuming/equipping Buff sources still changes the local player's stats, and that remote players no longer clear or overwrite local `playerData.buffBonus`.

## 2026-05-25 - Remote Player Animation State Sync
- Change/task: Fixed remote multiplayer characters sliding in a default pose and logging `Animator is not playing an AnimatorController`.
- Changed files: `Assets/Scripts/GameCore/GameNetwork/NetworkPlayer.cs`, `Docs/CHANGELOG_AI.md`, `Docs/MultiplayerStageOne.md`.
- Behavior change: Remote `PlayerStateDriver` components now remain enabled so model/Animator setup still runs, while `SetLocalControlEnabled(false)` prevents remote instances from reading local input. `NetworkPlayer` syncs a lightweight remote animation state for Idle, Walk, Run, Jump, Fall, Fly, and Attack, and only plays those states after a valid Animator Controller is available.
- Affected modules: Host/Client remote player display, Animator initialization, remote locomotion/airborne/attack animation playback.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings only.
- Follow-up note: Unity Play Mode or two-client testing should verify that both sides see remote movement and jumping animations instead of default-pose sliding.

## 2026-05-25 - Friend Chat Red Dots
- Change/task: Added per-friend chat unread red dots on top of the existing main FriendButton chat aggregate red dot.
- Changed files: `Assets/Scripts/GameCore/GameChat/ChatUnreadSummaryData.cs`, `Assets/Scripts/GameCore/GameChat/IChatService.cs`, `Assets/Scripts/GameCore/GameChat/ChatMgr.cs`, `Assets/Scripts/GameCore/GameChat/HttpChatService.cs`, `Assets/Scripts/GameCore/GameChat/MockChatService.cs`, `Assets/Scripts/GameCore/GameUI/Item/FriendItemUI.cs`, `Assembly-CSharp.csproj`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `ChatMgr` now stores unread counts by friend from the server unread summary. `FriendItemUI` shows a red dot with count on each friend's `ChatButton` when that friend has unread messages; opening the conversation marks messages read and clears the item red dot on refresh.
- Affected modules: Chat unread summary, FriendPanel friend list, ChatPanel read flow, PlayerMainPanel FriendButton aggregate red dot.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings only.

## 2026-05-24 - Fix Multiplayer Local Camera Ownership
- Change/task: Fixed Host/Client camera and input target confusion where a remote player could become the local main-view player.
- Changed files: `Assets/Scripts/GameCore/HSM/PlayerStateDriver.cs`, `Assets/Scripts/GameCore/GameNetwork/NetworkPlayer.cs`, `Assets/Scripts/GameCore/GameCamera/ThirdPersonCameraComtrol.cs`, `Assets/Scripts/GameCore/GameCamera/CameraControl.cs`, `Docs/CHANGELOG_AI.md`, `Docs/MultiplayerStageOne.md`.
- Behavior change: `PlayerStateDriver` only registers itself as `GameMgr.Instance.Player` when it is not network-spawned or when its `NetworkObject` is owned by the local client. `NetworkPlayer` now disables/deprioritizes player-attached cameras, Camera components, and AudioListeners on remote instances, and rebonds scene camera controllers after the local owner is assigned. Scene camera scripts now refresh their cached target when the current local player changes.
- Affected modules: Host/Client player spawning, local input ownership, Cinemachine target selection, main camera follow/rotation behavior.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings only.
- Follow-up note: Unity Play Mode or two-client build testing should verify that Host controls Host's own character/camera, Client controls Client's own character/camera, and remote characters never steal the active view.

## 2026-05-24 - Per-Client Runtime Session Token And Realtime Reconnect
- Change/task: Fixed friend requests not appearing on the target client until relogging when testing multiple local clients.
- Changed files: `Assets/Scripts/GameCore/GameAccount/HttpSessionContext.cs`, `Assets/Scripts/GameCore/GameAccount/HttpAccountService.cs`, `Assets/Scripts/GameCore/GameSocial/HttpSocialService.cs`, `Assets/Scripts/GameCore/GameChat/HttpChatService.cs`, `Assets/Scripts/GameCore/GameSocial/HttpOnlineInviteService.cs`, `Assets/Scripts/GameCore/GameSocial/RealtimeMgr.cs`, `Assembly-CSharp.csproj`, `Docs/CHANGELOG_AI.md`, `Docs/AI_CONTEXT.md`, `Docs/FEATURE_MAP.md`.
- Behavior change: Unity now keeps the active server session token in per-process memory through `HttpSessionContext`, while `PlayerPrefs` remains only the saved startup source. HTTP social/chat/online-invite calls and WebSocket connections use the runtime token, so two clients running on one computer no longer overwrite each other's live login state through shared `PlayerPrefs`. `RealtimeMgr` also retries WebSocket connection after disconnects, so friend request refresh push can recover without relogging.
- Affected modules: Login/register session flow, HttpSocialService friend request refresh, HttpChatService, HttpOnlineInviteService, RealtimeMgr social refresh/chat/invite push.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings only.
- Follow-up note: Existing running clients need to be restarted or re-enter Play Mode to pick up the new runtime-token behavior.

## 2026-05-24 - Harden Realtime WebSocket Disconnect Handling
- Change/task: Prevent expected WebSocket disconnect/send races from surfacing as unhandled Kestrel application exceptions.
- Changed files: `WorkDemoServer/Services/RealtimeConnectionHub.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Realtime connections now wrap each socket with a per-connection send lock, so chat, friend refresh, and online invite pushes do not call `SendAsync` concurrently on the same WebSocket. Disconnect, request-cancel, disposed socket, and close-race exceptions are cleaned up as normal offline events instead of escaping to Kestrel.
- Affected modules: WorkDemoServer realtime WebSocket channel, friend request refresh push, private chat push, online invite/result push, friend online-state detection.
- Verification: Normal backend build was blocked by the currently running `WorkDemoServer (10252)` process locking `bin/Debug/net9.0/WorkDemoServer.dll`; `dotnet build WorkDemoServer/WorkDemoServer.csproj /p:UseAppHost=false -o Temp/RealtimeSafeBuild` passed with 0 warnings/errors.
- Follow-up note: Restart the running WorkDemoServer process before testing this fix. If Kestrel still logs an unhandled exception after restart, copy the stack trace lines after the `Kestrel[13]` message because this one-line wrapper does not include the actual source method.

## 2026-05-24 - Preserve RedDot Layout On Runtime Bind
- Change/task: Fixed prefab-authored red dots moving to a default runtime position when PlayerMainPanel binds `RedDotView`.
- Changed files: `Assets/Scripts/GameCore/GameUI/Item/RedDotView.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `RedDotView.OnEnable()` no longer creates or mutates the visual before `Configure(...)` runs. This prevents the AddComponent lifecycle from changing an existing `FriendButton/RedDot` RectTransform before `preserveLayout` is applied.
- Affected modules: PlayerMainPanel FriendButton red dot and any UI that binds an existing prefab `RedDot` child at runtime.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings only.

## 2026-05-24 - Realtime Social Refresh For Friend Requests
- Change/task: Fixed the issue where a target player could not see a new friend request until relogging.
- Changed files: `WorkDemoServer/Program.cs`, `WorkDemoServer/Services/RealtimeConnectionHub.cs`, `Assets/Scripts/GameCore/GameSocial/RealtimeMgr.cs`, `Assets/Scripts/GameCore/GameSocial/SocialMgr.cs`, `Assets/Scripts/Manager/GameMgr.cs`, `Docs/CHANGELOG_AI.md`, `Docs/AI_CONTEXT.md`, `Docs/FEATURE_MAP.md`.
- Behavior change: WorkDemoServer now broadcasts `social.refresh` through WebSocket after friend request send/accept/refuse and friend deletion. Unity handles this realtime event by refreshing incoming friend requests and the friend list, which updates ApplyListPanel and red-dot counts without relogging.
- Affected modules: AddFriendPanel send flow, ApplyListPanel request display, FriendPanel friend list, friend request red dot, WorkDemoServer social endpoints.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings only. Normal backend build was blocked by a running `WorkDemoServer.exe`; `dotnet build WorkDemoServer/WorkDemoServer.csproj /p:UseAppHost=false -o Temp/SocialRefreshBuild` passed with 0 warnings/errors.
- Follow-up note: Restart the running WorkDemoServer process before testing this fix, because the currently running server still has the old realtime behavior.

## 2026-05-24 - Online Invite Result And Auto Join Flow
- Change/task: Added accept/refuse results for online invites and automated the correct Host/Client startup path after an invite is accepted.
- Changed files: `WorkDemoServer/Program.cs`, `WorkDemoServer/Contracts/OnlineInviteRequests.cs`, `WorkDemoServer/Contracts/OnlineInviteResponses.cs`, `WorkDemoServer/Services/OnlineInviteService.cs`, `WorkDemoServer/Services/RealtimeConnectionHub.cs`, `Assets/Scripts/GameCore/GameSocial/InviteResultData.cs`, `Assets/Scripts/GameCore/GameSocial/HttpOnlineInviteService.cs`, `Assets/Scripts/GameCore/GameSocial/RealtimeMgr.cs`, `Assets/Scripts/GameCore/GameSocial/SocialMgr.cs`, `Assets/Scripts/GameCore/GameUI/Panel/OnlinePanel.cs`, `Assets/Scripts/Manager/GameMgr.cs`, `Assembly-CSharp.csproj`, `WorkDemoServer/README.md`, `Docs/CHANGELOG_AI.md`, `Docs/AI_CONTEXT.md`, `Docs/FEATURE_MAP.md`, `Docs/MultiplayerFriendInvitePlan.md`.
- Behavior change: WorkDemoServer now keeps pending online invites in memory and supports `POST /api/online/invites/{inviteId}/result`. Accept/refuse results are pushed to the requester through `online.invite-result`. `InviteToMyWorld` starts Host on the requester before sending the invite; the receiver starts Client after accepting. `RequestToJoinWorld` starts Host on the receiver after accepting; the requester then receives the accept result and starts Client.
- Affected modules: OnlinePanel, OnlineRequestPanel, SocialMgr invite flow, RealtimeMgr event dispatch, WorkDemoServer online invite flow.
- Verification: `dotnet build WorkDemoServer/WorkDemoServer.csproj` passed with 0 warnings/errors; `dotnet build WorkDemo.sln` passed with existing project warnings only.
- Follow-up note: Pending invites are in-memory and disappear on server restart. The next networking milestone is Relay/Lobby or another NAT traversal layer so accepted invites work beyond local/LAN IP assumptions.

## 2026-05-24 - WebSocket Realtime Invites And Chat Push
- Change/task: Added a lightweight WorkDemoServer WebSocket realtime channel and moved online world invites from local mock loopback to server-backed delivery.
- Changed files: `WorkDemoServer/Program.cs`, `WorkDemoServer/Contracts/OnlineInviteRequests.cs`, `WorkDemoServer/Contracts/OnlineInviteResponses.cs`, `WorkDemoServer/Services/RealtimeConnectionHub.cs`, `WorkDemoServer/Services/OnlineInviteService.cs`, `WorkDemoServer/Services/OnlineInviteResult.cs`, `WorkDemoServer/Services/SocialService.cs`, `Assets/Scripts/GameCore/GameSocial/RealtimeMgr.cs`, `Assets/Scripts/GameCore/GameSocial/HttpOnlineInviteService.cs`, `Assets/Scripts/GameCore/GameSocial/SocialMgr.cs`, `Assets/Scripts/GameCore/GameChat/ChatMgr.cs`, `Assets/Scripts/Manager/GameMgr.cs`, `Assembly-CSharp.csproj`, `WorkDemoServer/README.md`, `Docs/CHANGELOG_AI.md`, `Docs/AI_CONTEXT.md`, `Docs/FEATURE_MAP.md`, `Docs/MultiplayerFriendInvitePlan.md`.
- Behavior change: Unity connects to `ws://127.0.0.1:5188/ws?token=<sessionToken>` after login. WorkDemoServer pushes `chat.message` events after private chat sends and `online.invite` events after `POST /api/online/invites`. Online invite buttons now validate through the server and only show the receiver popup on the target client. Friend online state now uses active WebSocket connections.
- Affected modules: Chat realtime refresh, FriendPanel online state, OnlinePanel invite/request buttons, OnlineRequestPanel popup delivery, WorkDemoServer realtime connection management.
- Verification: `dotnet build WorkDemoServer/WorkDemoServer.csproj` passed with 0 warnings/errors; `dotnet build WorkDemo.sln` passed with existing project warnings only.
- Follow-up note: The transport for invite delivery is now real, but the actual world connection still uses local IP/port assumptions. The next portfolio step is Relay/Lobby or another NAT traversal layer plus accept/refuse acknowledgement back to the requester.

## 2026-05-24 - Server-backed Friend Chat
- Change/task: Moved friend private chat from local mock storage to WorkDemoServer-backed HTTP chat.
- Changed files: `WorkDemoServer/Program.cs`, `WorkDemoServer/Models/ChatMessageRecord.cs`, `WorkDemoServer/Models/SocialStoreData.cs`, `WorkDemoServer/Contracts/ChatRequests.cs`, `WorkDemoServer/Contracts/ChatResponses.cs`, `WorkDemoServer/Services/ChatService.cs`, `WorkDemoServer/Services/ChatResult.cs`, `WorkDemoServer/Services/ISocialRepository.cs`, `WorkDemoServer/Services/JsonSocialRepository.cs`, `Assets/Scripts/GameCore/GameChat/IChatService.cs`, `Assets/Scripts/GameCore/GameChat/HttpChatService.cs`, `Assets/Scripts/GameCore/GameChat/MockChatService.cs`, `Assets/Scripts/GameCore/GameChat/ChatMgr.cs`, `Assets/Scripts/GameCore/GameUI/Panel/ChatPanel.cs`, `Assets/Scripts/Manager/GameMgr.cs`, `Assembly-CSharp.csproj`, `WorkDemoServer/README.md`, `Docs/CHANGELOG_AI.md`, `Docs/AI_CONTEXT.md`, `Docs/FEATURE_MAP.md`.
- Behavior change: Friend chat messages are now sent to `POST /api/chat/friends/{friendPlayerId}/messages`, history is loaded from `GET /api/chat/friends/{friendPlayerId}/messages?markRead=true`, unread summaries come from `GET /api/chat/unread`, and opening a conversation marks that friend's incoming messages as read. `RedDotType.Chat` now follows the backend unread count.
- Affected modules: ChatPanel, ChatMgr, main friend red dot, WorkDemoServer social JSON store.
- Verification: `dotnet build WorkDemoServer/WorkDemoServer.csproj` passed with 0 warnings/errors; `dotnet build WorkDemo.sln` passed with existing project warnings only.
- Follow-up note: This is still HTTP polling. The next stronger portfolio step is adding SignalR/WebSocket push for live message delivery and online invite delivery.

## 2026-05-24 - Unity HttpSocialService Integration
- Change/task: Added Unity-side `HttpSocialService` and switched `GameMgr.Social` from local mock friends to WorkDemoServer social endpoints.
- Changed files: `Assets/Scripts/GameCore/GameSocial/HttpSocialService.cs`, `Assets/Scripts/Manager/GameMgr.cs`, `Assets/Scripts/GameCore/GameSocial/SocialDebugHud.cs`, `Assembly-CSharp.csproj`, `Docs/CHANGELOG_AI.md`, `Docs/AI_CONTEXT.md`, `Docs/FEATURE_MAP.md`, `Docs/MultiplayerFriendInvitePlan.md`, `WorkDemoServer/README.md`.
- Behavior change: Friend search, friend request send/list/accept/refuse, friend list loading, and friend deletion now use WorkDemoServer with the saved `X-Session-Token`. `MockSocialService` remains as a local fallback implementation only.
- Affected modules: `SocialMgr`, FriendPanel, AddFriendPanel, ApplyListPanel, WorkDemoServer-backed social flow.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings only; `dotnet build WorkDemoServer/WorkDemoServer.csproj` passed with 0 warnings and 0 errors.
- Follow-up note: Online invites and private chat are still local/mock-side features. The next production-style step is adding server-backed chat history/unread counts and invite signaling.

## 2026-05-24 - Default Social Avatar Icon
- Change/task: Unified social/player UI avatar images to use `PlayerIcon1` as the default icon.
- Changed files: `Assets/Scripts/GameCore/GameUI/Item/SocialAvatarIcon.cs`, `Assets/Scripts/GameCore/GameUI/Item/FriendItemUI.cs`, `Assets/Scripts/GameCore/GameUI/Item/AddFriendItemUI.cs`, `Assets/Scripts/GameCore/GameUI/Item/ApplyItemUI.cs`, `Assets/Scripts/GameCore/GameUI/Item/ChatMessageItem.cs`, `Assets/Scripts/GameCore/GameUI/Panel/FriendPanel.cs`, `Assets/Scripts/GameCore/GameUI/Panel/ChatPanel.cs`, `Assets/Scripts/GameCore/GameUI/Panel/OnlineRequestPanel.cs`, `Assembly-CSharp.csproj`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Friend list entries, add-friend search results, friend request entries, FriendPanel player info, ChatPanel icons, chat message left/right icons, and OnlineRequestPanel requester icon now apply the shared default avatar sprite `PlayerIcon1`. The helper loads it from `RoleAtlas` first and falls back to `Resources`.
- Affected modules: Social UI avatar display and chat avatar display.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings only.

## 2026-05-23 - Preserve Existing RedDot Layouts
- Change/task: Updated the shared red-dot view so any prefab-authored `RedDot` child keeps its existing layout.
- Changed files: `Assets/Scripts/GameCore/GameUI/Item/RedDotView.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `RedDotView.Configure(type, showCount)` now preserves the position, size, anchors, and color of an existing `RedDot` child by default. Default layout/color sizing is only applied to red dots that the code creates at runtime.
- Affected modules: All UI buttons and panels that already contain a `RedDot` child, including PlayerMainPanel and FriendPanel entries.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings only.

## 2026-05-23 - Preserve FriendButton RedDot Prefab Position
- Change/task: Changed PlayerMainPanel friend red-dot binding to use the prefab-authored `FriendButton/RedDot` child instead of creating or repositioning a runtime dot.
- Changed files: `Assets/Scripts/GameCore/GameUI/Panel/PlayerMainPanel.cs`, `Assets/Scripts/GameCore/GameUI/Item/RedDotView.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: `PlayerMainPanel` now binds a friend red dot only when the `RedDot` child already exists under `FriendButton`, and configures `RedDotView` to preserve the existing prefab layout. `RedDotView` still supports auto-created/default-position dots for other UI surfaces that rely on that behavior.
- Affected modules: PlayerMainPanel friend button red-dot display, shared RedDotView configuration.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings only.

## 2026-05-23 - Mock Friend Acceptance Live Refresh
- Change/task: Fixed the mock friend acceptance flow so the requester side can see the new friend without relogging.
- Changed files: `Assets/Scripts/GameCore/GameSocial/MockSocialService.cs`, `Assets/Scripts/GameCore/GameSocial/SocialMgr.cs`, `Assets/Scripts/GameCore/GameUI/Panel/FriendPanel.cs`, `Docs/CHANGELOG_AI.md`, `Docs/AI_CONTEXT.md`.
- Behavior change: `FriendPanel` now refreshes the underlying social friend cache when shown and runs a lightweight active-panel refresh loop. `SocialMgr.RefreshFriends(false)` only raises `OnFriendsChanged` when the loaded friend list differs from the current cache, avoiding unnecessary UI rebuilds during polling. `MockSocialService.LoadFriends` also resolves cached display names when reading saved friend entries, so old `Player_<id>` placeholders can be corrected on load.
- Affected modules: FriendPanel live friend-list display, mock friend acceptance flow, mock display-name cleanup.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings only.
- Follow-up note: This is still a local mock synchronization workaround. The production-style solution remains switching Unity social operations to the server-backed `HttpSocialService` so both clients poll/push state from the same backend.

## 2026-05-23 - Mock Social Display Name Cache
- Change/task: Improved mock social display-name resolution so friend search and friend requests avoid `Player_<id>` placeholders when a real account display name is known locally.
- Changed files: `Assets/Scripts/GameCore/GameSocial/MockSocialService.cs`, `Assets/Scripts/GameCore/GameSocial/SocialMgr.cs`, `Assets/Scripts/Manager/GameMgr.cs`, `Docs/CHANGELOG_AI.md`, `Docs/AI_CONTEXT.md`.
- Behavior change: `MockSocialService` now caches known `playerId -> displayName` values in PlayerPrefs. `SocialMgr` syncs the current account display name after login/register/role-name changes. Search results, incoming requests, and accepted friend entries prefer the cached real display name and fall back to the player ID instead of `Player_<id>`.
- Affected modules: AddFriendPanel search result display, ApplyListPanel request display, FriendPanel friend display in mock mode.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings only.
- Follow-up note: Because this is still mock local storage, each Unity client only knows names it has cached locally. The fully correct cross-client solution is switching friend search/requests to the server-backed `HttpSocialService`, where the backend returns real display names.

## 2026-05-23 - Fix Mock Friend Request Direction
- Change/task: Fixed the mock friend request flow so requests are stored on the target player's incoming request list instead of the sender's list.
- Changed files: `Assets/Scripts/GameCore/GameSocial/MockSocialService.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: When account A sends a friend request to account B, A no longer receives B's request locally. B sees the incoming request, and accepting it adds the friendship on both sides in mock storage.
- Affected modules: AddFriendPanel, ApplyListPanel, FriendPanel, mock social flow before Unity switches to `HttpSocialService`.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings only.
- Follow-up note: Any incorrect requests already written by the old mock logic are still in local PlayerPrefs until refused from ApplyListPanel or cleared with a local mock data reset.

## 2026-05-23 - WorkDemoServer Account Admin Page
- Change/task: Added a development-only backend account manager for inspecting local accounts.
- Changed files: `WorkDemoServer/Program.cs`, `WorkDemoServer/Contracts/AdminResponses.cs`, `WorkDemoServer/Services/AdminService.cs`, `WorkDemoServer/README.md`, `Docs/CHANGELOG_AI.md`, `Docs/AI_CONTEXT.md`, `Docs/FEATURE_MAP.md`.
- Behavior change: `GET /api/admin/accounts` returns safe account summaries as JSON, and `GET /admin/accounts` renders a browser-readable account table. The admin view shows account name, player ID, display name, online state, friend count, incoming friend request count, and creation time; it intentionally does not expose password hashes.
- Affected modules: WorkDemoServer development tooling and local account debugging.
- Verification: Normal build output was locked by the currently running `WorkDemoServer` process; `dotnet build WorkDemoServer/WorkDemoServer.csproj /p:UseAppHost=false -o Temp/AdminBuild` passed with 0 warnings and 0 errors.
- Follow-up note: Restart the running server before opening `/admin/accounts`, because the current process is still using the old code.

## 2026-05-23 - Reset Local Server Test Accounts
- Change/task: Removed the two local test accounts with random-derived player IDs so the sequential ID demo can restart cleanly.
- Changed files: `WorkDemoServer/Data/accounts.json`, `Docs/CHANGELOG_AI.md`.
- Behavior change: The local backend account store is now empty. The next registered account should receive `playerId` `100001`, followed by `100002`, as long as the running server uses the updated sequential ID code.
- Affected modules: Local WorkDemoServer test account data only.
- Verification: Confirmed `WorkDemoServer/Data/accounts.json` now contains `[]`.

## 2026-05-23 - LoginPanel Password Visibility
- Change/task: Added password visibility toggle support to `LoginPanel.PasswordField`.
- Changed files: `Assets/Scripts/GameCore/GameUI/Panel/LoginPanel.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: LoginPanel now looks for `PasswordField/VisibleImage` and `PasswordField/UnVisibleImage`, adds clickable button components if needed, defaults the password input to hidden, and toggles between password and plain text display just like RegisterPanel.
- Affected modules: Login UI and account login input flow.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings only. Unity Play Mode should verify the two image nodes are positioned and clickable in the LoginPanel prefab.

## 2026-05-23 - Red Dot Notification System
- Change/task: Added a reusable red-dot notification system and wired the first friend UI reminders.
- Changed files: `Assets/Scripts/GameCore/GameRedDot/RedDotType.cs`, `Assets/Scripts/GameCore/GameRedDot/RedDotMgr.cs`, `Assets/Scripts/GameCore/GameUI/Item/RedDotView.cs`, `Assets/Scripts/Manager/GameMgr.cs`, `Assets/Scripts/GameCore/GameUI/Panel/PlayerMainPanel.cs`, `Assets/Scripts/GameCore/GameUI/Panel/FriendPanel.cs`, `Assets/Scripts/GameCore/GameSocial/SocialMgr.cs`, `Assembly-CSharp.csproj`, `Docs/CHANGELOG_AI.md`, `Docs/AI_CONTEXT.md`, `Docs/FEATURE_MAP.md`.
- Behavior change: `GameMgr.RedDot` now stores notification counts. `PlayerMainPanel` adds a pure red dot to `FriendButton` for the aggregate friend entry, while `FriendPanel` adds a numeric red dot to `ApplyFriendButton` for incoming friend request count. `SocialMgr.RefreshIncomingFriendRequests()` updates `RedDotType.FriendRequest`, and the aggregate `RedDotType.Friend` follows friend requests plus chat count.
- Affected modules: Main HUD friend entry, FriendPanel apply-friend entry, social/friend request refresh flow, future chat unread reminders.
- Verification: `dotnet build WorkDemo.sln` passed with existing project warnings only. Unity Play Mode should verify final red-dot placement on the actual prefabs and adjust prefab `RedDot` children if custom art/positioning is desired.

## 2026-05-23 - WorkDemoServer Friend API
- Change/task: Added the first real backend friend system endpoints to `WorkDemoServer`.
- Changed files: `WorkDemoServer/Program.cs`, `WorkDemoServer/appsettings.json`, `WorkDemoServer/Contracts/SocialRequests.cs`, `WorkDemoServer/Contracts/SocialResponses.cs`, `WorkDemoServer/Models/FriendRequestRecord.cs`, `WorkDemoServer/Models/FriendshipRecord.cs`, `WorkDemoServer/Models/SocialStoreData.cs`, `WorkDemoServer/Services/ISocialRepository.cs`, `WorkDemoServer/Services/JsonSocialRepository.cs`, `WorkDemoServer/Services/SocialService.cs`, `WorkDemoServer/Services/SocialResult.cs`, `WorkDemoServer/Services/SocialStoreOptions.cs`, `WorkDemoServer/Services/SessionService.cs`, `WorkDemoServer/README.md`, `Docs/CHANGELOG_AI.md`, `Docs/AI_CONTEXT.md`, `Docs/FEATURE_MAP.md`, `Docs/MultiplayerFriendInvitePlan.md`.
- Behavior change: Authenticated clients can now search players by ID, send friend requests, list incoming friend requests, accept/refuse requests, list friends, and delete friends. Social data is persisted to `Data/social.json`; friend list responses include `isOnline` based on current in-memory sessions.
- Affected modules: Backend social/friend flow, future Unity `HttpSocialService`, FriendPanel/AddFriendPanel/ApplyListPanel integration.
- Verification: `dotnet build WorkDemoServer/WorkDemoServer.csproj /p:UseAppHost=false` passed.
- Follow-up note: Unity still uses the mock social service until `HttpSocialService` is added and wired into `GameMgr.Social`.

## 2026-05-23 - Sequential Account Player IDs
- Change/task: Changed backend account player ID generation from random 6-digit IDs to creation-order sequential IDs.
- Changed files: `WorkDemoServer/Services/AccountService.cs`, `WorkDemoServer/Services/IAccountRepository.cs`, `WorkDemoServer/Services/JsonAccountRepository.cs`, `WorkDemoServer/README.md`, `Docs/CHANGELOG_AI.md`, `Docs/AI_CONTEXT.md`, `Docs/FEATURE_MAP.md`, `Docs/MultiplayerFriendInvitePlan.md`.
- Behavior change: New accounts created by `WorkDemoServer` now receive numeric player IDs starting at `100001`; each new ID is generated from the current maximum existing numeric `playerId` plus one.
- Affected modules: Backend account registration, Unity LoginPanel/RegisterPanel returned player ID display, FriendPanel ID display, future friend search by ID.
- Verification: `dotnet build WorkDemo.sln` passed earlier; `dotnet build WorkDemoServer/WorkDemoServer.csproj /p:UseAppHost=false` passed. The running server must be restarted before the new generation rule is active.
- Follow-up note: Existing JSON account data is preserved. If old random IDs already exist in `WorkDemoServer/Data/accounts.json`, the next sequential ID will continue after the current maximum instead of restarting at `100001`.

## 2026-05-23 - PlayerMainPanel Friend Button
- Change/task: Wired `PlayerMainPanel/Button/FriendButton` to open `FriendPanel`.
- Changed files: `Assets/Scripts/GameCore/GameUI/Panel/PlayerMainPanel.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Clicking the in-game FriendButton now calls `GameMgr.UI.ShowPanel<FriendPanel>()` and uses the same hover/click sound setup as the other main HUD buttons.
- Affected modules: Player HUD, friend/social UI entry.
- Verification: `dotnet build WorkDemo.sln` passed. Unity Play Mode should verify the `FriendPanel` Addressables key and FriendButton prefab path.

## 2026-05-23 - GameStartPanel Logout Button
- Change/task: Wired the new `LogOutButton` on GameStartPanel to a confirmation TipPanel and account logout flow.
- Changed files: `Assets/Scripts/GameCore/GameUI/Panel/GameStartPanel.cs`, `Docs/CHANGELOG_AI.md`.
- Behavior change: Clicking `LogOutButton` shows `确定退出此账号吗`. Confirming logs out the current account, refreshes account-scoped save selection and social mock data, hides the TipPanel, and shows a message. Cancel only closes the TipPanel.
- Affected modules: Start menu UI, account session flow, account-scoped role save state.
- Verification: `dotnet build WorkDemo.sln` passed. Unity Play Mode should verify the button path `BKImage/LogOutButton`, the TipPanel copy, confirm logout, and cancel no-op behavior.

## 2026-05-23 - Session Token Startup Validation
- Change/task: Added formal startup login-state validation for the Unity HTTP account flow.
- Changed files: `Assets/Scripts/GameCore/GameAccount/HttpAccountService.cs`, `Assets/Scripts/GameCore/GameAccount/AccountMgr.cs`, `Docs/CHANGELOG_AI.md`, `Docs/AI_CONTEXT.md`, `Docs/MultiplayerFriendInvitePlan.md`.
- Behavior change: `HttpAccountService.LoadSavedProfile()` now requires both a saved profile and saved session token, then calls `GET /api/account/me` before treating the user as logged in. If the token is missing, expired, rejected, or the account server is unreachable, the saved local login state is cleared and LoginPanel will be shown again.
- Affected modules: Startup login flow, account persistence, server-backed account validation.
- Verification: `dotnet build WorkDemo.sln` passed. Unity Play Mode should verify three cases: valid token skips LoginPanel, server restart invalidates old in-memory tokens and shows LoginPanel, and server offline also shows LoginPanel.

## 2026-05-23 - Account-Scoped Role Saves
- Change/task: Isolated role save visibility by logged-in account so a newly registered account no longer sees old local saves from other accounts.
- Changed files: `Assets/Scripts/GameCore/GameFile/GameFile.cs`, `Assets/Scripts/Manager/FileMgr.cs`, `Assets/Scripts/GameCore/GameUI/Panel/ChooseRolePanel.cs`, `Assets/Scripts/Manager/GameMgr.cs`, `Docs/CHANGELOG_AI.md`, `Docs/AI_CONTEXT.md`, `Docs/FEATURE_MAP.md`.
- Behavior change: New saves store `ownerPlayerId` from the current account. ChooseRolePanel now uses `FileMgr.GetCurrentAccountGameFiles()` and only lists saves whose `ownerPlayerId` matches the logged-in `playerId`; when account changes, `FileMgr.EnsureCurrentGameFileForCurrentAccount()` updates the current save pointer to that account's latest role or clears it.
- Affected modules: Account login/register flow, role selection, save file ownership.
- Verification: `dotnet build WorkDemo.sln` passed. Unity Play Mode should be used to confirm that a newly registered account shows an empty role list and only sees roles created under that account.

## 2026-05-23 - Unity HttpAccountService Backend Login
- Change/task: Added Unity-side `HttpAccountService` and switched `GameMgr.Account` to use the local `WorkDemoServer` account backend by default.
- Changed files: `Assets/Scripts/GameCore/GameAccount/HttpAccountService.cs`, `Assets/Scripts/GameCore/GameAccount/HttpAccountService.cs.meta`, `Assets/Scripts/Manager/GameMgr.cs`, `Assembly-CSharp.csproj`, `Docs/CHANGELOG_AI.md`, `Docs/AI_CONTEXT.md`, `Docs/FEATURE_MAP.md`, `Docs/MultiplayerFriendInvitePlan.md`.
- Behavior change: LoginPanel/RegisterPanel now call `http://127.0.0.1:5188` through `HttpAccountService` for account login/register. Successful responses are saved locally with the returned 6-digit `playerId` and session token; `AccountMgr.SetDisplayName` now syncs display-name changes back to the backend when a session token exists. If the backend is not running, account actions show a connection failure through `AccountMgr.LastError`.
- Affected modules: Account login/register flow, role-name-to-account display-name sync, FriendPanel player ID/name display.
- Verification: `dotnet build WorkDemo.sln` passed. Unity Play Mode still needs manual validation with `dotnet run --project WorkDemoServer/WorkDemoServer.csproj` running before testing LoginPanel/RegisterPanel.

## 2026-05-22 - WorkDemoServer Account Backend Bootstrap
- Change/task: Added a lightweight ASP.NET Core backend project for the real account system path.
- Changed files: `WorkDemoServer/Program.cs`, `WorkDemoServer/Contracts/*`, `WorkDemoServer/Models/*`, `WorkDemoServer/Services/*`, `WorkDemoServer/Properties/launchSettings.json`, `WorkDemoServer/README.md`, `WorkDemoServer/.gitignore`, `Docs/CHANGELOG_AI.md`, `Docs/AI_CONTEXT.md`, `Docs/FEATURE_MAP.md`, `Docs/MultiplayerFriendInvitePlan.md`.
- Behavior change: `WorkDemoServer` can run at `http://127.0.0.1:5188` and provides register/login/current-account/display-name endpoints. Register/login return a 6-digit player ID and an in-memory session token. Account data is stored in local JSON through `IAccountRepository`, leaving a clean replacement point for SQLite later.
- Affected modules: Future server-backed account flow, friend/social/chat backend integration, Unity `HttpAccountService` work.
- Verification: `dotnet build WorkDemoServer/WorkDemoServer.csproj` passed; started the server locally and successfully called register/login endpoints, then removed generated test account data. Unity integration is not yet implemented.

后续每个 AI/Codex 窗口完成项目修改后，请在本文件顶部追加记录。记录要短，但要能让其他窗口知道你碰了什么、行为如何变化、是否影响其他模块。

## 2026-05-22 - Account Display Name Sync From Role
- 修改窗口/任务：把创建/选择角色的角色名同步到 `AccountMgr.CurrentProfile.displayName`。
- 改动文件：`Assets/Scripts/GameCore/GameAccount/IAccountService.cs`、`Assets/Scripts/GameCore/GameAccount/AccountMgr.cs`、`Assets/Scripts/GameCore/GameAccount/MockAccountService.cs`、`Assets/Scripts/GameCore/GameUI/Panel/CreateRoleNamePanel.cs`、`Assets/Scripts/GameCore/GameUI/Panel/ChooseRolePanel.cs`、`Docs/CHANGELOG_AI.md`、`Docs/AI_CONTEXT.md`。
- 行为变化：创建新角色时会把输入的角色名写入账号显示名；选择已有角色、点击开始进入游戏时也会同步当前角色名；FriendPanel、聊天、邀请等读取账号显示名的位置会跟随当前角色名变化。
- 影响模块：账号系统、角色选择/创建流程、好友/聊天显示名。
- 验证：`dotnet build WorkDemo.sln` 通过；Unity Play Mode 中仍需确认创建/切换角色后 FriendPanel 名称刷新。
## 2026-05-22 - FriendPanel Player Info Header
- 修改窗口/任务：接入 FriendPanel 玩家信息区域，显示当前玩家头像、名称和 ID。
- 改动文件：`Assets/Scripts/GameCore/GameUI/Panel/FriendPanel.cs`、`Docs/CHANGELOG_AI.md`。
- 行为变化：FriendPanel 打开或刷新时会同步 `PlayerNameText` 和 `IDText`；名称优先使用 `displayName`，没有角色名时回退到账号名，再回退到玩家 ID；`PlayerIcon` 保留 Prefab 上配置的默认头像。
- 影响模块：好友 UI、账号信息展示。
- 验证：`dotnet build WorkDemo.sln` 通过；Unity Play Mode 中仍需确认头像、名称和 ID 的显示布局。
## 2026-05-22 - FriendPanel Account ID Text
- 修改窗口/任务：接入 FriendPanel 上新增的 IDText，用于显示当前登录玩家 ID。
- 改动文件：`Assets/Scripts/GameCore/GameUI/Panel/FriendPanel.cs`、`Docs/CHANGELOG_AI.md`。
- 行为变化：FriendPanel 打开或刷新好友列表时，会把当前账号 `playerId` 显示为 `ID：xxxxxx`；未登录或没有 ID 时显示 `ID：------`。
- 影响模块：好友 UI、账号 ID 展示流程。
- 验证：`dotnet build WorkDemo.sln` 通过；Unity Play Mode 中仍需确认 IDText 位置和显示效果。
## 2026-05-22 - RegisterPanel Password Visibility
- 修改窗口/任务：接入用户制作的 RegisterPanel 新层级，并为注册密码/确认密码添加可见性切换。
- 改动文件：`Assets/Scripts/GameCore/GameUI/Panel/RegisterPanel.cs`、`Assembly-CSharp.csproj`、`Docs/CHANGELOG_AI.md`。
- 行为变化：`AccountField` 输入注册账号，`PasswordField` 输入注册密码，`RePasswordField` 重复确认密码；两个密码框默认 Password 类型，显示 `UnVisibleImage`、隐藏 `VisibleImage`；点击图标会在明文/密码输入之间切换，并同步切换两张图片。
- 影响模块：注册 UI、Mock 账号注册流程。
- 验证：`dotnet build WorkDemo.sln` 通过；Unity Play Mode 中仍需确认图片点击范围和密码输入框显示效果。
## 2026-05-22 - LoginPanel Error Feedback
- 修改窗口/任务：接入 LoginPanel 新增的 TipText，并为账号/密码错误增加对应输入框轻微晃动反馈。
- 改动文件：`Assets/Scripts/GameCore/GameAccount/IAccountService.cs`、`Assets/Scripts/GameCore/GameAccount/AccountMgr.cs`、`Assets/Scripts/GameCore/GameAccount/MockAccountService.cs`、`Assets/Scripts/GameCore/GameUI/Panel/LoginPanel.cs`、`Docs/CHANGELOG_AI.md`。
- 行为变化：登录失败时 `TipText` 会显示“账户不存在”或“密码输入错误”；账户不存在时晃动 AccountField，密码错误时晃动 PasswordField；账号/密码为空时提示输入账号和密码。
- 影响模块：账号登录 UI、MockAccountService 错误反馈接口。
- 验证：`dotnet build WorkDemo.sln` 通过；Unity Play Mode 中仍需确认 TipText 绑定和 Field 晃动效果。
## 2026-05-22 - LoginPanel Mock Account Login
- 修改窗口/任务：接入用户制作的 LoginPanel，并把 Mock 账号系统扩展为账号密码注册/登录。
- 改动文件：`Assets/Scripts/GameCore/GameAccount/AccountProfile.cs`、`Assets/Scripts/GameCore/GameAccount/IAccountService.cs`、`Assets/Scripts/GameCore/GameAccount/AccountMgr.cs`、`Assets/Scripts/GameCore/GameAccount/MockAccountService.cs`、`Assets/Scripts/GameCore/GameUI/Panel/LoginPanel.cs`、`Assets/Scripts/GameCore/GameUI/Panel/RegisterPanel.cs`、`Assets/Scripts/GameCore/GameUI/Panel/GameStartPanel.cs`、`Assembly-CSharp.csproj`、`Docs/CHANGELOG_AI.md`、`Docs/MultiplayerFriendInvitePlan.md`。
- 行为变化：开始游戏前若未登录会打开 LoginPanel；账号注册会生成 6 位数字玩家 ID；玩家名称仍保留给创建角色流程决定；登录/注册成功后刷新好友数据并进入角色选择。
- 影响模块：账号系统、开始菜单流程、好友/聊天使用的当前玩家 ID。
- 验证：`dotnet build WorkDemo.sln` 通过；Unity Play Mode、LoginPanel/RegisterPanel Prefab 绑定和 Addressables key 仍需在 Editor 中确认。
## 2026-05-22 - SocialMgr Online Request Flow
- 修改窗口/任务：把好友联机 UI 从直接启动 Host/Client 调整为先走 SocialMgr 联机请求链路。
- 改动文件：`Assets/Scripts/GameCore/GameSocial/SocialMgr.cs`、`Assets/Scripts/GameCore/GameUI/Panel/OnlinePanel.cs`、`Assets/Scripts/GameCore/GameUI/Panel/OnlineRequestPanel.cs`、`Assets/Scripts/GameCore/GameSocial/SocialDebugHud.cs`、`Docs/CHANGELOG_AI.md`、`Docs/MultiplayerFriendInvitePlan.md`。
- 行为变化：`OnlinePanel` 点击邀请/申请会调用 `SocialMgr.SendOnlineRequest`；Mock 阶段会本地回环到 `ReceiveOnlineRequest` 并弹出 `OnlineRequestPanel`；同意/拒绝请求统一走 `SocialMgr.AcceptOnlineRequestAsync` / `RefuseOnlineRequest`。
- 影响模块：好友 UI、联机请求流程、Mock 本地 Host/Client 测试流程。
- 验证：`dotnet build WorkDemo.sln` 通过；Unity Play Mode 和 Addressables Prefab 绑定仍需在 Editor 中确认。
## 2026-05-22 - OnlineRequestPanel Mock Accept Flow
- 修改窗口/任务：把用户制作的 OnlineRequestPanel Prefab 层级接入 Mock 联机请求弹窗。
- 改动文件：`Assets/Scripts/GameCore/GameUI/Panel/OnlineRequestPanel.cs`、`Assets/Scripts/GameCore/GameUI/Panel/OnlinePanel.cs`、`Assets/Scripts/GameCore/GameSocial/InviteData.cs`、`Assets/Scripts/GameCore/GameSocial/SocialDebugHud.cs`、`Assembly-CSharp.csproj`、`Docs/CHANGELOG_AI.md`、`Docs/MultiplayerFriendInvitePlan.md`。
- 行为变化：OnlineRequestPanel 可显示申请加入/邀请加入两类请求；同意申请加入会启动 Host，同意邀请加入会启动 Client；F7 调试面板可模拟两类收到的联机请求。
- 影响模块：UI、Mock Friend Invite、本地 Host/Client 联机测试流程。
- 验证：`dotnet build WorkDemo.sln` 通过；Unity Play Mode 和 Addressables Prefab 绑定仍需在 Editor 中确认。

## 2026-05-22 - OnlinePanel Mock Invite Actions
- 修改窗口/任务：把用户制作的 OnlinePanel Prefab 层级接入 Mock 联机方式选择。
- 改动文件：`Assets/Scripts/GameCore/GameUI/Panel/OnlinePanel.cs`、`Docs/CHANGELOG_AI.md`。
- 行为变化：OnlinePanel 可显示当前好友，CloseButton 关闭面板；InviteButton 在本地测试阶段启动 Host，ApplyButton 按 `127.0.0.1:7777` 尝试启动 Client。
- 影响模块：UI、Mock Friend Invite、本地 Host/Client 联机测试流程。
- 验证：`dotnet build WorkDemo.sln` 通过；Unity Play Mode 和 Addressables Prefab 绑定仍需在 Editor 中确认。

## 2026-05-22 - ApplyListPanel Mock Request Flow
- 修改窗口/任务：把用户制作的 ApplyListPanel Prefab 层级接入 Mock 好友申请列表、同意和拒绝流程。
- 改动文件：`Assets/Scripts/GameCore/GameSocial/ISocialService.cs`、`Assets/Scripts/GameCore/GameSocial/SocialMgr.cs`、`Assets/Scripts/GameCore/GameSocial/MockSocialService.cs`、`Assets/Scripts/GameCore/GameSocial/FriendRequestData.cs`、`Assets/Scripts/GameCore/GameUI/Panel/ApplyListPanel.cs`、`Assets/Scripts/GameCore/GameUI/Item/ApplyItemUI.cs`、`Assets/Scripts/GameCore/GameUI/Panel/AddFriendPanel.cs`、`Assembly-CSharp.csproj`、`Docs/CHANGELOG_AI.md`。
- 行为变化：AddFriendPanel 发送的 Mock 好友申请会进入申请列表；ApplyListPanel 可显示申请玩家，拒绝会移除申请，同意会添加为好友并刷新 FriendPanel。
- 影响模块：UI、Mock Account/Friend 调试流程。
- 验证：`dotnet build WorkDemo.sln` 通过；Unity Play Mode 和 Addressables Prefab 绑定仍需在 Editor 中确认。

## 2026-05-22 - AddFriendPanel Mock Search Flow
- 修改窗口/任务：把用户制作的 AddFriendPanel Prefab 层级接入 Mock 玩家 ID 搜索和添加好友流程。
- 改动文件：`Assets/Scripts/GameCore/GameUI/Panel/AddFriendPanel.cs`、`Assets/Scripts/GameCore/GameUI/Item/AddFriendItemUI.cs`、`Assembly-CSharp.csproj`、`Docs/CHANGELOG_AI.md`。
- 行为变化：AddFriendPanel 可从 FriendIDField 读取玩家 ID，点击 SearchButton 搜索玩家并显示 AddFriendItem，点击 AddFriendButton 在当前 Mock 阶段直接添加好友并刷新好友列表。
- 影响模块：UI、Mock Account/Friend 调试流程。
- 验证：`dotnet build WorkDemo.sln` 通过；Unity Play Mode 和 Addressables Prefab 绑定仍需在 Editor 中确认。

## 2026-05-21 - FriendPanel Mock Friend List
- 修改窗口/任务：把用户制作的 FriendPanel Prefab 层级接入 Mock 好友列表、聊天入口和删除确认流程。
- 改动文件：`Assets/Scripts/GameCore/GameUI/Panel/FriendPanel.cs`、`Assets/Scripts/GameCore/GameUI/Item/FriendItemUI.cs`、`Assets/Scripts/GameCore/GameUI/Panel/OnlinePanel.cs`、`Assets/Scripts/GameCore/GameUI/Panel/AddFriendPanel.cs`、`Assets/Scripts/GameCore/GameUI/Panel/ApplyListPanel.cs`、`Assembly-CSharp.csproj`、`Docs/CHANGELOG_AI.md`。
- 行为变化：FriendPanel 可渲染好友列表，在线显示绿色“在线”，离线显示红色“离线”；好友项支持打开聊天、打开联机选择面板、删除模式和 TipPanel 确认删除。
- 影响模块：UI、Mock Account/Friend/Chat 调试流程。
- 验证：`dotnet build WorkDemo.sln` 通过；Unity Play Mode 和 Addressables Prefab 绑定仍需在 Editor 中确认。

## 2026-05-21 - ChatPanel Mock Friend Chat
- 修改窗口/任务：把用户制作的 ChatPanel Prefab 层级接入 Mock 好友私聊流程。
- 改动文件：`Assets/Scripts/GameCore/GameUI/Panel/ChatPanel.cs`、`Assets/Scripts/GameCore/GameUI/Item/ChatMessageItem.cs`、`Assets/Scripts/GameCore/GameUI/Item/UIPanelDragHandle.cs`、`Assets/Scripts/GameCore/GameSocial/SocialDebugHud.cs`、`Assembly-CSharp.csproj`、`Docs/CHANGELOG_AI.md`。
- 行为变化：ChatPanel 可显示指定好友名称，点击 SendButton 发送私聊消息，CloseButton 关闭面板，DragContent 支持拖拽，消息按左右气泡显示并滚动到底部。
- 影响模块：UI、Mock Account/Friend/Chat 调试流程。
- 验证：`dotnet build WorkDemo.sln` 通过；Unity Play Mode 和 Addressables Prefab 绑定仍需在 Editor 中确认。

## Template

```markdown
## YYYY-MM-DD - 任务名

- 修改窗口/任务：一句话描述。
- 改动文件：列出关键文件路径。
- 行为变化：说明玩家或开发流程会看到什么变化。
- 影响模块：说明可能受影响的系统。
- 验证：列出运行过的命令、Unity Play Mode、手动步骤；未验证也写明。
- 后续注意：可选，写待确认或风险。
```

## 2026-05-20 - 创建项目共识包

- 修改窗口/任务：为后续多窗口 AI 协作创建项目共识文档。
- 改动文件：`.gitignore`、`AGENTS.md`、`README.md`、`Docs/AI_CONTEXT.md`、`Docs/FEATURE_MAP.md`、`Docs/CODING_RULES.md`、`Docs/CHANGELOG_AI.md`。
- 行为变化：无运行时行为变化，只新增文档。
- 影响模块：协作流程、项目理解、后续任务交接。
- 验证：已扫描项目结构、Unity 版本、包依赖、核心 Manager、场景 Controller、玩家、任务、建造、Buff、联机等关键脚本；未运行 Unity Play Mode。
- 后续注意：后续功能窗口完成改动后，应继续维护本文件；若架构变化，也同步更新 `Docs/AI_CONTEXT.md` 和 `Docs/FEATURE_MAP.md`。
