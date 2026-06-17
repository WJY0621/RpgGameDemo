# Build System First Version

`BuildManager` is created by `GameMgr` as `GameMgr.Build`. Press `T` in play mode.

Default controls:

- `T`: toggle build mode
- `1-5`: select runtime default recipes
- `R`: rotate preview
- Left mouse: place or demolish
- `Y`: switch to demolish mode
- `Esc`: cancel
- `F`: interact with crafting tables and beds

For production use, create one `BuildRecipeListSO` asset from `Assets/Create/Data/GameBuild/Build Recipe List`, add recipe entries in that list, assign real prefabs, then register the asset in Addressables with the address `BuildRecipeList`.

The runtime fallback recipes are only for quick testing:

- Equipment Crafting Table
- Consumable Crafting Table
- Wood Floor
- Wood Wall
- Bed

Build prefabs can be scaled at the root. Runtime snap placement uses the preview root scale when aligning socket/anchor pairs, so generated snap points should stay in prefab local space.

Structure snapping uses socket/anchor pairs:

- Placed objects expose socket points.
- Preview objects expose anchor points.
- Foundation, wall, and board prefabs should be generated with `Tools/Game Build/Snap Points/...`.

Interactive build pieces use `BuildInteractionBehaviour` as the shared base. `BuildableObject.EnsureRuntimeComponents()` adds the runtime interaction component from the recipe interaction type:

- `Bed` -> `BedInteraction`
- `EquipmentCrafting` / `ConsumableCrafting` -> `CraftingStationInteraction`
- `Door` -> `DoorInteraction`

Beds set the respawn point automatically after they are placed. If multiple beds exist, the newest placed bed is the active respawn point; demolishing it falls back to the previous bed. Placed beds live under the persistent `PlacedBuildObjects` root, so respawn registration records the active gameplay scene instead of the internal `DontDestroyOnLoad` scene. Interacting with a bed refreshes the saved respawn point, teleports the player to the bed, applies the configured sleep pose offset, plays the `Sleep` animation, heals the player if enabled, and accelerates `TimeMgr` by 10x. While sleeping, bed interaction prompts are hidden and movement input exits sleep, restores player control, and restores the original `TimeMgr` speed. Bed interaction stays suppressed until the player leaves the sleep spot.

For a wall with an interactable door, use `BuildType.DoorWall` if the whole placed wall should be treated as a wall for snapping and as a door for interaction. If only a child door mesh should rotate, place `DoorInteraction` on that child or assign its `doorPivot`.
