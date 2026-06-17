# WorkDemoServer

Lightweight backend for the WorkDemo friend-based multiplayer flow.

Current scope:

- Account registration.
- Account login.
- Sequential 6-digit player ID generation, starting at `100001`.
- Password hashing with PBKDF2-SHA256.
- Session token returned after register/login.
- Local JSON account storage behind `IAccountRepository`.
- Friend search by player ID.
- Friend request send/list/accept/refuse flow.
- Friend list and delete-friend endpoints.
- Private friend chat send/history/read/unread endpoints.
- WebSocket realtime push for private chat and online world invites.
- Online world invite send endpoint.
- Local JSON social storage behind `ISocialRepository`.
- Read-only local account admin endpoints for development inspection.

The JSON repository is only the first development store. The service is shaped so it can later be replaced by SQLite without changing Unity UI code.

## Run

```powershell
dotnet run --project WorkDemoServer/WorkDemoServer.csproj
```

Default local address:

```text
http://127.0.0.1:5188
```

## Endpoints

### Register

```http
POST /api/account/register
Content-Type: application/json
```

```json
{
  "accountName": "player001",
  "password": "password123",
  "displayName": "Player"
}
```

### Login

```http
POST /api/account/login
Content-Type: application/json
```

```json
{
  "accountName": "player001",
  "password": "password123"
}
```

### Current Account

```http
GET /api/account/me
X-Session-Token: <session token>
```

### Update Display Name

```http
PATCH /api/account/display-name
X-Session-Token: <session token>
Content-Type: application/json
```

```json
{
  "displayName": "RoleName"
}
```

### Search Player

```http
GET /api/social/search/{playerId}
X-Session-Token: <session token>
```

### Send Friend Request

```http
POST /api/social/friend-requests
X-Session-Token: <session token>
Content-Type: application/json
```

```json
{
  "targetPlayerId": "100002"
}
```

### Incoming Friend Requests

```http
GET /api/social/friend-requests
X-Session-Token: <session token>
```

### Accept Friend Request

```http
POST /api/social/friend-requests/{requestId}/accept
X-Session-Token: <session token>
```

### Refuse Friend Request

```http
POST /api/social/friend-requests/{requestId}/refuse
X-Session-Token: <session token>
```

### Friend List

```http
GET /api/social/friends
X-Session-Token: <session token>
```

### Delete Friend

```http
DELETE /api/social/friends/{friendPlayerId}
X-Session-Token: <session token>
```

### Send Friend Chat Message

```http
POST /api/chat/friends/{friendPlayerId}/messages
X-Session-Token: <session token>
Content-Type: application/json
```

```json
{
  "messageText": "hello"
}
```

### Friend Chat History

Returns the latest friend conversation messages. `markRead` defaults to `true`.

```http
GET /api/chat/friends/{friendPlayerId}/messages?markRead=true
X-Session-Token: <session token>
```

### Mark Friend Chat Read

```http
POST /api/chat/friends/{friendPlayerId}/read
X-Session-Token: <session token>
```

### Chat Unread Summary

```http
GET /api/chat/unread
X-Session-Token: <session token>
```

### Realtime WebSocket

Connect after login/register with the returned session token:

```text
ws://127.0.0.1:5188/ws?token=<session token>
```

Server-pushed event types:

- `chat.message`
- `online.invite`

### Send Online Invite

Requires both players to be friends and the target player to have an active WebSocket connection.

```http
POST /api/online/invites
X-Session-Token: <session token>
Content-Type: application/json
```

```json
{
  "targetPlayerId": "100002",
  "inviteType": "InviteToMyWorld",
  "address": "127.0.0.1",
  "port": 7777
}
```

### Reply To Online Invite

The target player calls this after accepting or refusing the popup. The server pushes `online.invite-result` back to the original requester.

```http
POST /api/online/invites/{inviteId}/result
X-Session-Token: <session token>
Content-Type: application/json
```

```json
{
  "accepted": true,
  "address": "127.0.0.1",
  "port": 7777
}
```

### Account Admin JSON

Development-only read endpoint for checking current local accounts. It does not return password hashes.

```http
GET /api/admin/accounts
```

### Account Admin Page

Open this in a browser while the server is running:

```text
http://127.0.0.1:5188/admin/accounts
```

The page shows account name, player ID, display name, online state, friend count, incoming friend request count, and creation time.

## Next Steps

1. Add Relay/Lobby or another NAT traversal layer so world joining works beyond LAN/local testing.
2. Add invite timeout/expiry cleanup for abandoned pending invites.
3. Keep Netcode for GameObjects responsible for the actual host/client world connection.
