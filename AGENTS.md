# WorkDemo AI Collaboration Guide

这个文件是给后续 Codex/AI 窗口看的项目协作入口。每个新窗口开始开发前，先读本文件，再读 `Docs/AI_CONTEXT.md`、`Docs/FEATURE_MAP.md`、`Docs/CODING_RULES.md`。

## Project Snapshot

- 项目类型：Unity 3D ARPG/生存建造向 Demo，包含角色控制、背包装备、任务、对话、怪物/Boss、采集、建造、商店、Buff、VFX、音频和第一阶段联机。
- Unity 版本：`2022.3.53f1c1`，见 `ProjectSettings/ProjectVersion.txt`。
- 主要代码目录：`Assets/Scripts/Manager` 和 `Assets/Scripts/GameCore`。
- 主要数据目录：`Assets/GameData`、`Assets/Resources`、`Assets/Resources_moved`、`Assets/Save`。
- 入口场景：`Assets/Scenes/GameInitializeScene.unity`，之后预加载并进入 `GameStartScene`，游戏主场景为 `GameScene`。

## Required Reading For New AI Windows

1. `AGENTS.md`
2. `Docs/AI_CONTEXT.md`
3. `Docs/FEATURE_MAP.md`
4. `Docs/CODING_RULES.md`
5. 如果任务涉及联机，读 `Docs/MultiplayerStageOne.md`
6. 如果任务涉及建造，读 `Assets/Scripts/GameCore/GameBuild/BUILD_SYSTEM_README.md`

## Work Rules

- 不要先重构。先读相关模块，再做最小必要修改。
- 不要删除或覆盖用户已有改动。当前项目经常处于大量未提交改动状态。
- 不要随意移动 Unity 资源、`.meta` 文件、场景、Prefab、ScriptableObject。移动资源必须考虑 GUID 引用。
- 新增运行时代码优先放到对应的 `Assets/Scripts/GameCore/<Feature>` 目录；全局服务类优先接入 `GameMgr` 已有模式。
- UI 面板脚本继承 `BasePanel`，面板 Prefab 通常通过 Addressables 按类名加载。
- 异步流程优先使用 UniTask，跟随现有 `GameMgr.AssetLoader`、`SceneMgr`、`LoadingMgr` 模式。
- 数据配置优先使用现有 JSON、ScriptableObject、Resources 或 Addressables 模式，不要额外引入新的配置系统。
- 功能完成后更新 `Docs/CHANGELOG_AI.md`。如果改变架构、模块边界、启动流程或约定，也同步更新 `Docs/AI_CONTEXT.md`、`Docs/FEATURE_MAP.md` 或 `Docs/CODING_RULES.md`。

## Common Commands

- 构建解决方案：`dotnet build WorkDemo.sln`
- 快速搜文件：`rg --files`
- 快速搜代码：`rg "关键词" Assets/Scripts`

`dotnet build WorkDemo.sln` 只能验证 C# 编译的一部分。Unity 场景、Prefab、Addressables、资源引用和运行时生命周期仍需要 Unity Editor/Play Mode 验证。

## Do Not Freely Edit

- `Assets/Scenes/**`：场景文件体积大，人工/Unity Editor 改动更安全。
- `Assets/GameData/**`：数据资源会影响多个系统，改前先确认用途。
- `Assets/Resources_moved/**`：包含 UI、角色、音频、VFX 等资源，注意 Addressables/资源引用。
- `ProjectSettings/**`、`Packages/**`：除非任务明确需要，不要改项目配置和包依赖。
- `Assets/Save/**`：包含当前开发存档，改动会影响本地测试状态。

## Current Known Notes

- 源码中部分中文注释在终端输出中会显示为乱码，疑似编码/控制台显示问题。不要为了“修注释”批量重编码源文件。
- 项目存在大量未提交的功能改动，后续窗口要先看 `git status --short`，只处理自己任务相关文件。
- 联机目前是第一阶段 Host/Client 本地测试切片，不应假设库存、任务、建造、怪物已经完成网络同步。
