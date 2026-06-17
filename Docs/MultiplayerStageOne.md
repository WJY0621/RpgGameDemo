# Multiplayer Stage One

## Goal

Build the first playable host-based multiplayer slice:

- Two local clients enter `GameScene`.
- One client starts as Host.
- The other client joins as Client through IP and port.
- Each player controls only their own character.
- Remote players are displayed through network-synchronized transform data.

This stage now includes player sync, social entry points, and a first Host-authoritative Boss bridge. It still does not include Relay/Lobby, inventory, tasks, building synchronization, normal monster authority, drop authority, or full Boss animation/skill VFX replication.

## Current Test Flow

1. Open the project in Unity and wait for scripts to compile.
2. Run `Tools/WorkDemo Multiplayer/Setup Player Prefab` once. It will configure `Assets/Resources_moved/Prefabs/Player.prefab` first, then fall back to the old `Assets/Resources/Prefabs/Player.prefab` path if needed.
3. Start Play Mode and enter `GameScene` with any save.
4. Press `F9` if the `Network Test` panel is hidden.
5. In the Host instance, click `Start Host`.
6. In a second instance or build, enter `GameScene`, keep IP as `127.0.0.1`, and click `Start Client`.
7. During an active session, `PlayerMainPanel/Button/BrokenLineButton` is visible. Clicking it should disconnect that instance and restore a local single-player player in `GameScene`.
8. If the Host disconnects, Clients should receive the network disconnect callback and return to their own single-player mode. If a Client disconnects, its network player should disappear from the Host world while the Client returns to local single-player control.

## Architecture

- `NetworkMgr` creates/configures the runtime `NetworkManager`, `UnityTransport`, and network prefab registration.
- `NetworkDebugHud` is a temporary development UI for Host/Client testing.
- `NetworkPlayer` owns player replication and separates local owner control from remote display.
- `PlayerStateDriver` now has `SetLocalControlEnabled`, so remote players do not read local input.
- Only the owning `NetworkPlayer` may register itself as `GameMgr.Instance.Player`, enable local player input, or keep player-attached cameras/listeners active. Remote player instances must stay display-only so they cannot steal the local camera target or global player reference.
- Remote player instances keep `PlayerStateDriver` enabled so model/Animator/component initialization still runs, but `SetLocalControlEnabled(false)` stops them from reading local input. `NetworkPlayer` syncs a lightweight animation state for remote Idle/Walk/Run/Jump/Fall/Fly/Attack display.
- Remote player instances must not initialize or write local gameplay data such as `GameMgr.Instance.playerData`, `BuffComponent`, `PlayerHealth`, or equipment-derived stat refreshes. These systems are owned only by the local gameplay player; remote players are visual/network replicas until dedicated network stat sync is added.
- Remote player instances disable local-only components such as `PlayerInput`, weapon mode switching, player attack controllers, attack effect controllers, and Buff runtime. This prevents remote replicas from reacting to local keyboard input or changing local gameplay state while still allowing model and Animator display.
- The current animation sync is intentionally lightweight: it replicates a coarse locomotion/action state and movement blend values. It does not yet synchronize full Animator parameters, weapon mode, attack combo timing, hit windows, root motion events, or networked health/stats.
- Remote player HUD state is also lightweight. `NetworkPlayer` currently syncs owner display name plus current/max HP for display, and `PlayerMainPanel/FriendLifebar` shows the first remote player with the default `PlayerIcon1` avatar, remote name, and HP percentage. Owner HP sync is event-driven: local `PlayerHealth.OnHealthChanged` updates server-owned vitals immediately, with Host owners writing directly and Client owners submitting a dedicated vitals `ServerRpc`. This is still a display/state replication layer; final combat authority and real networked damage validation are future work.
- `NetworkMgr.OnSessionActiveChanged` is the current UI signal for active multiplayer state. `PlayerMainPanel` uses it to show or hide `BrokenLineButton`.
- `NetworkMgr.DisconnectToSinglePlayerAsync` is the shared disconnect path for Host, Client, and the temporary debug shutdown button. It shuts down Netcode, marks the mode offline, destroys scene network player objects after shutdown, respawns the local `Player` prefab, restores the best available player pose, re-enables input, and rebinds scene cameras.
- Client-side unexpected server/Host disconnects are handled through the Netcode disconnect callback. The Client stops its local network session and runs the same single-player restore path so it does not remain inside the Host world after the Host leaves.
- Boss multiplayer uses a Host-authoritative bridge rather than spawning Bosses as Netcode prefabs. `BossHealth` auto-attaches `NetworkBoss`; Clients redirect local Boss hit attempts through their owning `NetworkPlayer` to the Host, and the Host applies damage to the real `BossHealth`.
- Boss summon consumables are now routed through the same bridge. Only the Host may consume a Boss summon item during multiplayer. When the Host summons through `BossSummonArena`, it sends a `WorkDemo.BossSummon` custom message containing the Arena id, Boss id, pose, HP, and battle-active state. Clients find the matching local `BossSummonArena`, instantiate/prepare the same assigned Boss prefab as a display replica, and then receive normal `WorkDemo.BossState` snapshots from the Host.
- Summoned Boss lifecycle is reset after death. The Arena listens for the active Boss death, clears `battleStarted/prepared`, and removes the dead instance before the next summon. Client display Bosses hide shortly after a Host dead-state snapshot so defeated Bosses disappear from invited players' view.
- The Host sends lightweight Boss snapshots through Netcode custom messages: Boss hierarchy id, position, rotation, current/max HP, dead state, battle-active flag, and the Host Animator base-layer state. Clients use these snapshots to activate/update the matching scene Boss, keep local HP UI in sync, and display the Host's current Boss animation state.
- Client Boss instances switch into display mode during multiplayer by disabling local Boss AI, attack, phase, skill runtime, and NavMesh authority. The Host remains the only side that runs Boss decision-making and authoritative HP.
- Boss-to-player damage is routed from the Host. If a Host Boss attack, authored skill hitbox, contact damage capsule, or Boss projectile overlaps a remote `NetworkPlayer`, `NetworkPlayer` sends a targeted ClientRpc to that owner so the owner-side `PlayerHealth` applies damage locally, then the existing player HP sync broadcasts the new HP. Remote players keep their Host-side `CharacterController` enabled for Boss hit detection, but their input and local gameplay authority remain disabled.
- Boss barrage/projectile visuals are mirrored separately from damage. When the Host spawns a Boss projectile from an authored barrage event, it sends `WorkDemo.BossProjectileVisual`; Clients create a visual-only projectile runner and attach the matching VFX key while leaving collision and damage authority on the Host.
- Client-to-Boss damage is submitted from the attacking Client to the Host. The Host applies the real Boss damage, updates Boss HP, and sends the actual damage number back to the attacking Client so their local screen can show a damage popup.
- Current Boss bridge limitation: it assumes clients have a matching `BossSummonArena` configuration with the same assigned Boss prefab and hierarchy id. It does not yet solve fully Netcode-spawned Boss prefabs, full Animator parameter sync, every non-projectile skill VFX clip on clients, or server-side validation of every client attack hit.
- Relay/Lobby is integrated behind the `WORKDEMO_USE_UGS_RELAY` scripting define so the project still compiles before UGS packages are installed. When the define is enabled and Unity Services packages are present, online invites prefer Relay: Host creates Relay allocation + private Lobby, WorkDemoServer forwards `relayJoinCode`/`lobbyId`, and the invite receiver joins Relay instead of using LAN/IP. Without the define, the same invite flow falls back to `127.0.0.1:7777`/LAN direct connect.

## Relay/Lobby Setup

To enable the Relay path in Unity:

1. Install Unity packages: `com.unity.services.core`, `com.unity.services.authentication`, `com.unity.services.relay`, and `com.unity.services.lobby`.
2. Link the Unity project to a Unity Dashboard project.
3. Enable Authentication, Relay, and Lobby in Unity Gaming Services.
4. Add `WORKDEMO_USE_UGS_RELAY` to `Project Settings/Player/Scripting Define Symbols`.
5. Restart Play Mode and test the normal好友邀请流程. The UI does not change; the connection payload changes from IP/port to Relay join code.

## Next Stage

After local Host/Client is stable:

1. Move network startup into real UI instead of `NetworkDebugHud`.
2. Add chat by RPC.
3. Add Lobby + Relay for room-code joining.
4. Add Authentication + Friends for player ID search and invites.
5. Synchronize one monster, one drop item, and one build object as portfolio proof points.
