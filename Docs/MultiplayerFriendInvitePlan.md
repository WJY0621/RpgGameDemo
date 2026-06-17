# Friend-Based Multiplayer Plan

## Target Experience

The multiplayer system should feel like a small host-world co-op game, closer to Stardew Valley or Terraria than a public room browser.

Player flow:

1. Player logs in or creates an account.
2. The account receives a unique player ID.
3. Player enters the game.
4. Player searches another player by ID.
5. Player sends a friend request.
6. The other player accepts the request.
7. Accepted friends appear in the friend list.
8. From the friend list, a player can:
   - Invite a friend into their own world.
   - Request to join a friend's world.
   - Open a private chat with that friend.
9. The invited/requested player can accept or reject.
10. If accepted, the joining player connects to the host player's world.

## Core Design

Use a host-world model:

- The host owns the world save.
- The invited player joins as a client.
- The host is authoritative for shared world state.
- The client controls only their own player character.
- Important shared data should be requested from the client and confirmed by the host.

This keeps the project achievable and makes the architecture easy to explain in interviews.

## System Layers

### Account Layer

Purpose:

- Login.
- Create account.
- Generate or retrieve the player's unique ID.
- Store display name and basic profile data.

Recommended implementation:

- First version: local/mock account service for UI and flow validation.
- Production version: Unity Authentication or a custom backend.

Data:

- `AccountName`
- `PlayerId`
- `DisplayName`
- `AccountState`

Current mock behavior:

- `LoginPanel` reads `AccountField` and `PasswordField`.
- `RegisterPanel` creates a local mock account and generates a unique 6-digit numeric `PlayerId`.
- Account display name is intentionally separate from account login. The final player/character name is decided by the role creation flow.
- `GameStartPanel` opens `LoginPanel` before entering role selection when no account is logged in.

### Social Layer

Purpose:

- Search player by ID.
- Send friend request.
- Accept or reject friend request.
- Maintain friend list.
- Track friend online/offline state.

Recommended implementation:

- First version: `MockSocialService`.
- Production version: Unity Friends or a custom backend.

Data:

- `FriendInfo`
- `FriendRequestData`
- `FriendOnlineState`

### Invite Layer

Purpose:

- Send an invitation to enter my world.
- Send a request to join a friend's world.
- Accept or reject invite/request.
- Pass connection data to the network layer after acceptance.

Important distinction:

- Invite friend into my world: I am or will become Host.
- Request to join friend world: friend is or will become Host.

Data:

- `InviteData`
- `InviteType`
- `RequesterPlayerId`
- `RequesterDisplayName`
- `HostPlayerId`
- `TargetPlayerId`
- `ConnectionPayload`

For local testing, `ConnectionPayload` can be:

- IP address.
- Port.

For real online play, `ConnectionPayload` should become:

- Lobby ID.
- Relay join code.
- Host player ID.

### Chat Layer

Purpose:

- Friend-to-friend private chat from the friend list.
- Optional world chat after players are in the same world.
- System messages for friend requests, invites, joins, leaves, and errors.

Important distinction:

- Private friend chat: belongs to the social/account system and can exist before joining a world.
- World chat: belongs to the current multiplayer session and is broadcast only to players in that world.

Recommended implementation:

- First version: `MockChatService` stores chat messages locally and validates chat UI.
- Current server-backed version: `HttpChatService` sends private friend messages to WorkDemoServer, loads recent history, marks conversations read, and updates unread red-dot counts.
- Current realtime version: `RealtimeMgr` connects to WorkDemoServer WebSocket and receives `chat.message` pushes so online friends can see messages without waiting for polling.
- Later live version: Unity Vivox text chat, SignalR, or another hosted realtime channel can replace the lightweight custom WebSocket if needed.

Data:

- `ChatMessageData`
- `ChatConversationData`
- `ChatChannelType`
- `SenderPlayerId`
- `ReceiverPlayerId`
- `MessageText`
- `SentTime`

Basic rules:

- Reject empty messages.
- Limit message length.
- Add send-rate limiting.
- Keep recent message history in the UI.
- Treat incoming messages as untrusted text and sanitize display where needed.

### Network Layer

Purpose:

- Start Host.
- Start Client.
- Spawn network players.
- Separate local player control from remote player display.
- Synchronize basic player position, rotation, and animation parameters.

Current implementation:

- `NetworkMgr`
- `NetworkPlayer`
- `NetworkDebugHud`

Later implementation:

- Remove or hide `NetworkDebugHud`.
- Let friend invite UI call `NetworkMgr` directly.
- Add Relay and Lobby when local flow is stable.

### UI Layer

Required panels:

- `LoginPanel`
- `CreateAccountPanel`
- `FriendPanel`
- `FriendRequestPanel`
- `InvitePopupPanel`
- `FriendChatPanel`

Friend panel should include:

- My player ID.
- Search ID input.
- Search result.
- Add friend button.
- Friend list.
- Invite to my world button.
- Request to join world button.
- Private chat button.

Invite popup should include:

- Inviter/requester name.
- Invite type.
- Accept button.
- Reject button.

Friend chat panel should include:

- Friend name and ID.
- Recent message list.
- Input field.
- Send button.
- Unread message indicator in the friend list.

## Development Stages

### Stage 1: Local Network Foundation

Goal:

- Host and Client can connect locally.
- Each side controls only its own player.
- Remote player is visible.

Status:

- In progress.

Notes:

- This stage does not need login, account, friends, Lobby, or Relay.

### Stage 2: Mock Account And Friend UI

Goal:

- Add login/create-account UI.
- Generate a temporary player ID.
- Search mock player IDs.
- Send and accept mock friend requests.
- Show friends in a friend list.

Status:

- Implemented as a development HUD opened with `F7`.
- Current implementation uses local `PlayerPrefs`, so it validates project flow but does not send data between real accounts yet.

Implemented scripts:

- `AccountMgr`
- `LoginPanel`
- `RegisterPanel`
- `SocialMgr`
- `ChatMgr`
- `MockAccountService`
- `MockSocialService`
- `MockChatService`
- `SocialDebugHud`
- `FriendPanel`
- `AddFriendPanel`
- `ApplyListPanel`
- `OnlinePanel`
- `OnlineRequestPanel`
- `ChatPanel`

Mock friend request behavior:

- `AddFriendPanel` searches a mock player ID and sends a local mock friend request.
- `ApplyListPanel` displays pending mock requests.
- Agreeing to a request adds that player to the local friend list.
- Refusing a request removes it from the pending request list.
- Because this is local mock data, a sent request is looped back into the current account's request list so the full accept/refuse UI can be tested on one client.

Why:

- This validates the complete user-facing flow before adding real online services.

### Stage 3: Mock Friend Invite Flow

Goal:

- Friend list can invite a friend into my world.
- Friend list can request to join a friend's world.
- Accepting an invite/request calls `NetworkMgr`.

Local test behavior:

- `OnlinePanel` is the sender-side choice panel.
- `OnlineRequestPanel` is the receiver-side popup.
- `OnlinePanel` does not start Host/Client directly. It calls `SocialMgr.SendOnlineRequest`.
- `SocialMgr.ReceiveOnlineRequest` is the central receive path and opens `OnlineRequestPanel`.
- `OnlineRequestPanel` does not talk to `NetworkMgr` directly. It calls `SocialMgr.AcceptOnlineRequestAsync` or `SocialMgr.RefuseOnlineRequest`.
- If the receiver accepts a request to join their world, the receiver starts Host.
- If the receiver accepts an invitation to join the requester's world, the receiver starts Client.
- Mock delivery currently loops a sent request back into `ReceiveOnlineRequest` so the complete UI flow can be tested on one client.
- F7 debug UI also uses `ReceiveOnlineRequest` to open mock `OnlineRequestPanel` states before real invite delivery exists.

### Stage 4: Mock Friend Chat Flow

Goal:

- Open a private chat from the friend list.
- Send and display private messages.
- Show unread state on friends.
- Keep recent local chat history.

Why:

- This validates the social/chat UI before integrating a real chat backend.

### Stage 5: Real Lobby And Relay

Goal:

- Replace local IP payload with Relay/Lobby connection data.
- Host creates a Lobby and Relay allocation.
- Client joins through Relay join code.

Result:

- Players can connect without sharing LAN or public IP.

### Stage 6: Real Account, Friends, And Chat

Goal:

- Replace mock account/social services with real services.
- Login returns stable player ID.
- Searching ID and friend requests work across machines/accounts.
- Friend online state is real.
- Private friend chat works across accounts.

Current backend start:

- `WorkDemoServer` has been added as a standalone ASP.NET Core service.
- It currently implements the account slice and first social slice: register, login, current account, profile lookup by player ID, display-name update, player search, friend request send/list/accept/refuse, friend list, and delete friend.
- Register/login return a sequential 6-digit player ID starting at `100001` and a session token.
- Storage is currently JSON-backed through `IAccountRepository` and `ISocialRepository`; SQLite is the intended later replacement once package/deployment setup is stable.
- Unity now has `HttpAccountService` and `GameMgr.Account` uses it by default, so LoginPanel/RegisterPanel call the backend account endpoints.
- Saved login state is validated on startup through `GET /api/account/me`; invalid or missing session tokens clear the local profile and return to LoginPanel.
- Unity now has `HttpSocialService` and `GameMgr.Social` uses it by default, so FriendPanel/AddFriendPanel/ApplyListPanel call the backend social endpoints.
- Unity now has `HttpChatService` and `GameMgr.Chat` uses it by default, so ChatPanel calls the backend chat endpoints and `RedDotType.Chat` follows the server unread count.
- Unity now has `RealtimeMgr`, which connects to WorkDemoServer `/ws` and dispatches `chat.message` and `online.invite` events to ChatMgr/SocialMgr.
- Unity now has `HttpOnlineInviteService`, and `SocialMgr.SendOnlineRequestAsync` sends online invite/request actions to `POST /api/online/invites` instead of local mock loopback.
- Invite accept/refuse results are server-backed through `POST /api/online/invites/{inviteId}/result` and pushed to the requester as `online.invite-result`.
- `InviteToMyWorld`: requester starts Host before sending; receiver accepts and starts Client.
- `RequestToJoinWorld`: receiver accepts and starts Host; requester receives the accept result and starts Client.
- `MockSocialService` and `MockChatService` remain available for local fallback tests, but the default friend/chat flow is now server-backed.

### Stage 7: Gameplay Synchronization

Goal:

- Synchronize one monster.
- Synchronize player attack and monster damage.
- Synchronize one drop item.
- Synchronize one build object.

Portfolio value:

- Shows host-authoritative gameplay synchronization, not just SDK connection.

## Immediate Next Step

Build the real request transport seam:

- Current flow is `OnlinePanel -> SocialMgr.SendOnlineRequestAsync -> HttpOnlineInviteService -> WorkDemoServer -> WebSocket online.invite -> SocialMgr.ReceiveOnlineRequest -> OnlineRequestPanel -> /result -> WebSocket online.invite-result`.
- The invite transport is now server-backed and realtime, but the actual world connection still uses the existing local host/client address and Netcode flow.
- Next step: test two local clients where one side starts Host and the other side accepts an invite/request and joins as Client, then replace local IP/port assumptions with Relay/Lobby or another NAT traversal layer.

Do not add gameplay synchronization yet. First make the invite/request handoff easy to explain and easy to replace with a real transport.
