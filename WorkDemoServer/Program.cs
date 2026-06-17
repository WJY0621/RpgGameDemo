using System.Net;
using System.Text;
using Microsoft.Extensions.FileProviders;
using WorkDemoServer.Contracts;
using WorkDemoServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AccountStoreOptions>(
    builder.Configuration.GetSection(AccountStoreOptions.SectionName));
builder.Services.Configure<SocialStoreOptions>(
    builder.Configuration.GetSection(SocialStoreOptions.SectionName));
builder.Services.AddSingleton<IAccountRepository, JsonAccountRepository>();
builder.Services.AddSingleton<ISocialRepository, JsonSocialRepository>();
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<AccountService>();
builder.Services.AddSingleton<SocialService>();
builder.Services.AddSingleton<ChatService>();
builder.Services.AddSingleton<OnlineInviteService>();
builder.Services.AddSingleton<RealtimeConnectionHub>();
builder.Services.AddSingleton<AdminService>();
builder.Services.AddSingleton<SessionService>();
builder.Services.AddSingleton<HotUpdateService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("UnityDev", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("UnityDev");
app.UseWebSockets();

// 托管 Addressables 远程资源（bundle + 远程 catalog），充当 CDN。
// 把 Unity 构建出的 ServerData/* 拷到 AddressablesContent/ 下，
// 客户端 Profile 的 RemoteLoadPath 指向 http://<host>/addressables/[BuildTarget]。
var addressablesRoot = Path.Combine(app.Environment.ContentRootPath, "AddressablesContent");
Directory.CreateDirectory(addressablesRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(addressablesRoot),
    RequestPath = "/addressables",
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream"
});

app.MapGet("/", () => Results.Ok(new
{
    service = "WorkDemoServer",
    status = "Running",
    version = "0.1.0"
}));

// 客户端拿【已发布】清单（不是实时扫盘）
app.MapGet("/api/hotupdate/manifest", (HotUpdateService hotUpdate) =>
{
    return Results.Ok(hotUpdate.GetPublishedManifest());
});

app.MapGet("/api/hotupdate/file", (string path, HotUpdateService hotUpdate) =>
{
    return hotUpdate.TryReadFile(path, out var content)
        ? Results.Text(content, "application/json")
        : Results.NotFound(ApiError.NotFound("Hot-update file not found."));
});

// 发布（程序化）：扫盘 + 对比 + 冻结新版本，返回差异
app.MapPost("/api/hotupdate/publish", (HotUpdateService hotUpdate) =>
{
    return Results.Ok(hotUpdate.Publish());
});

// 发布管理页（浏览器点按钮）
app.MapGet("/admin/hotupdate", (HotUpdateService hotUpdate) =>
{
    var manifest = hotUpdate.GetPublishedManifest();
    return Results.Content(BuildHotUpdateAdminHtml(manifest.Version, manifest.Files.Count, null), "text/html; charset=utf-8");
});

app.MapPost("/admin/hotupdate/publish", (HotUpdateService hotUpdate) =>
{
    var result = hotUpdate.Publish();
    return Results.Content(BuildHotUpdateAdminHtml(result.Version, -1, result, null), "text/html; charset=utf-8");
});

// 一键部署 + 发布：从 Unity 工程同步配置/资源 → 再发布
app.MapPost("/admin/hotupdate/deploy-publish", (HotUpdateService hotUpdate) =>
{
    var deployLog = hotUpdate.DeployFromProject();
    var result = hotUpdate.Publish();
    return Results.Content(BuildHotUpdateAdminHtml(result.Version, -1, result, deployLog), "text/html; charset=utf-8");
});

app.MapGet("/api/admin/accounts", async (
    AdminService admin,
    CancellationToken cancellationToken) =>
{
    var accounts = await admin.GetAccountsAsync(cancellationToken);
    return Results.Ok(accounts);
});

app.MapGet("/admin/accounts", async (
    AdminService admin,
    CancellationToken cancellationToken) =>
{
    var accounts = await admin.GetAccountsAsync(cancellationToken);
    return Results.Content(BuildAccountsAdminHtml(accounts), "text/html; charset=utf-8");
});

app.MapPost("/api/account/register", async (
    RegisterAccountRequest request,
    AccountService accounts,
    SessionService sessions,
    RealtimeConnectionHub realtimeHub,
    CancellationToken cancellationToken) =>
{
    var result = await accounts.RegisterAsync(request, cancellationToken);
    return await ToAccountResultAsync(result, sessions, realtimeHub, cancellationToken);
});

app.MapPost("/api/account/login", async (
    LoginAccountRequest request,
    AccountService accounts,
    SessionService sessions,
    RealtimeConnectionHub realtimeHub,
    CancellationToken cancellationToken) =>
{
    var result = await accounts.LoginAsync(request, cancellationToken);
    return await ToAccountResultAsync(result, sessions, realtimeHub, cancellationToken);
});

app.MapGet("/api/account/me", async (
    HttpRequest request,
    AccountService accounts,
    SessionService sessions,
    CancellationToken cancellationToken) =>
{
    if (!TryGetSessionToken(request, out var token) ||
        !sessions.TryGetPlayerId(token, out var playerId))
    {
        return Results.Unauthorized();
    }

    var profile = await accounts.GetProfileAsync(playerId, cancellationToken);
    return profile == null ? Results.NotFound(ApiError.NotFound("Account not found.")) : Results.Ok(profile);
});

app.Map("/ws", async (
    HttpContext context,
    SessionService sessions,
    RealtimeConnectionHub realtimeHub) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var token = context.Request.Query["token"].ToString();
    if (string.IsNullOrWhiteSpace(token) ||
        !sessions.TryGetPlayerId(token, out var playerId))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    await realtimeHub.HandleSocketAsync(playerId, socket, context.RequestAborted);
});

app.MapGet("/api/account/player/{playerId}", async (
    string playerId,
    AccountService accounts,
    CancellationToken cancellationToken) =>
{
    var profile = await accounts.GetProfileAsync(playerId, cancellationToken);
    return profile == null ? Results.NotFound(ApiError.NotFound("Account not found.")) : Results.Ok(profile);
});

app.MapPatch("/api/account/display-name", async (
    UpdateDisplayNameRequest request,
    HttpRequest httpRequest,
    AccountService accounts,
    SessionService sessions,
    CancellationToken cancellationToken) =>
{
    if (!TryGetSessionToken(httpRequest, out var token) ||
        !sessions.TryGetPlayerId(token, out var playerId))
    {
        return Results.Unauthorized();
    }

    var result = await accounts.UpdateDisplayNameAsync(playerId, request.DisplayName, cancellationToken);
    return result.IsSuccess
        ? Results.Ok(result.Profile)
        : Results.BadRequest(ApiError.Validation(result.ErrorMessage));
});

app.MapGet("/api/social/search/{playerId}", async (
    string playerId,
    HttpRequest request,
    SocialService social,
    SessionService sessions,
    CancellationToken cancellationToken) =>
{
    if (!TryGetAuthenticatedPlayerId(request, sessions, out _))
    {
        return Results.Unauthorized();
    }

    var profile = await social.SearchPlayerAsync(playerId, cancellationToken);
    return profile == null ? Results.NotFound(ApiError.NotFound("Account not found.")) : Results.Ok(profile);
});

app.MapPost("/api/social/friend-requests", async (
    CreateFriendRequestRequest request,
    HttpRequest httpRequest,
    SocialService social,
    RealtimeConnectionHub realtimeHub,
    SessionService sessions,
    CancellationToken cancellationToken) =>
{
    if (!TryGetAuthenticatedPlayerId(httpRequest, sessions, out var playerId))
    {
        return Results.Unauthorized();
    }

    var result = await social.SendFriendRequestAsync(playerId, request.TargetPlayerId, cancellationToken);
    if (result.IsSuccess)
    {
        await realtimeHub.BroadcastSocialRefreshAsync("friend-request-created", cancellationToken);
    }

    return ToSocialResult(result, "Friend request sent.");
});

app.MapGet("/api/social/friend-requests", async (
    HttpRequest request,
    SocialService social,
    SessionService sessions,
    CancellationToken cancellationToken) =>
{
    if (!TryGetAuthenticatedPlayerId(request, sessions, out var playerId))
    {
        return Results.Unauthorized();
    }

    var requests = await social.GetIncomingFriendRequestsAsync(playerId, cancellationToken);
    return Results.Ok(requests);
});

app.MapPost("/api/social/friend-requests/{requestId}/accept", async (
    string requestId,
    HttpRequest request,
    SocialService social,
    RealtimeConnectionHub realtimeHub,
    SessionService sessions,
    CancellationToken cancellationToken) =>
{
    if (!TryGetAuthenticatedPlayerId(request, sessions, out var playerId))
    {
        return Results.Unauthorized();
    }

    var result = await social.AcceptFriendRequestAsync(playerId, requestId, cancellationToken);
    if (result.IsSuccess)
    {
        await realtimeHub.BroadcastSocialRefreshAsync("friend-request-accepted", cancellationToken);
    }

    return ToSocialResult(result, "Friend request accepted.");
});

app.MapPost("/api/social/friend-requests/{requestId}/refuse", async (
    string requestId,
    HttpRequest request,
    SocialService social,
    RealtimeConnectionHub realtimeHub,
    SessionService sessions,
    CancellationToken cancellationToken) =>
{
    if (!TryGetAuthenticatedPlayerId(request, sessions, out var playerId))
    {
        return Results.Unauthorized();
    }

    var result = await social.RefuseFriendRequestAsync(playerId, requestId, cancellationToken);
    if (result.IsSuccess)
    {
        await realtimeHub.BroadcastSocialRefreshAsync("friend-request-refused", cancellationToken);
    }

    return ToSocialResult(result, "Friend request refused.");
});

app.MapGet("/api/social/friends", async (
    HttpRequest request,
    SocialService social,
    SessionService sessions,
    CancellationToken cancellationToken) =>
{
    if (!TryGetAuthenticatedPlayerId(request, sessions, out var playerId))
    {
        return Results.Unauthorized();
    }

    var friends = await social.GetFriendsAsync(playerId, cancellationToken);
    return Results.Ok(friends);
});

app.MapDelete("/api/social/friends/{friendPlayerId}", async (
    string friendPlayerId,
    HttpRequest request,
    SocialService social,
    RealtimeConnectionHub realtimeHub,
    SessionService sessions,
    CancellationToken cancellationToken) =>
{
    if (!TryGetAuthenticatedPlayerId(request, sessions, out var playerId))
    {
        return Results.Unauthorized();
    }

    var result = await social.DeleteFriendAsync(playerId, friendPlayerId, cancellationToken);
    if (result.IsSuccess)
    {
        await realtimeHub.BroadcastSocialRefreshAsync("friend-deleted", cancellationToken);
    }

    return ToSocialResult(result, "Friend deleted.");
});

app.MapGet("/api/chat/friends/{friendPlayerId}/messages", async (
    string friendPlayerId,
    bool? markRead,
    HttpRequest request,
    ChatService chat,
    SessionService sessions,
    CancellationToken cancellationToken) =>
{
    if (!TryGetAuthenticatedPlayerId(request, sessions, out var playerId))
    {
        return Results.Unauthorized();
    }

    var messages = await chat.GetFriendMessagesAsync(
        playerId,
        friendPlayerId,
        markRead.GetValueOrDefault(true),
        cancellationToken);
    return Results.Ok(messages);
});

app.MapPost("/api/chat/friends/{friendPlayerId}/messages", async (
    string friendPlayerId,
    SendChatMessageRequest request,
    HttpRequest httpRequest,
    ChatService chat,
    RealtimeConnectionHub realtimeHub,
    SessionService sessions,
    CancellationToken cancellationToken) =>
{
    if (!TryGetAuthenticatedPlayerId(httpRequest, sessions, out var playerId))
    {
        return Results.Unauthorized();
    }

    var (result, message) = await chat.SendFriendMessageAsync(
        playerId,
        friendPlayerId,
        request.MessageText,
        cancellationToken);

    if (result.IsSuccess && message != null)
    {
        await realtimeHub.SendChatMessageAsync(friendPlayerId, message, cancellationToken);
        return Results.Ok(message);
    }

    return result.IsSuccess && message != null
        ? Results.Ok(message)
        : ToChatResult(result);
});

app.MapPost("/api/chat/friends/{friendPlayerId}/read", async (
    string friendPlayerId,
    HttpRequest request,
    ChatService chat,
    SessionService sessions,
    CancellationToken cancellationToken) =>
{
    if (!TryGetAuthenticatedPlayerId(request, sessions, out var playerId))
    {
        return Results.Unauthorized();
    }

    var result = await chat.MarkFriendMessagesReadAsync(playerId, friendPlayerId, cancellationToken);
    return result.IsSuccess
        ? Results.Ok(new SocialActionResponse("Messages marked as read."))
        : ToChatResult(result);
});

app.MapGet("/api/chat/unread", async (
    HttpRequest request,
    ChatService chat,
    SessionService sessions,
    CancellationToken cancellationToken) =>
{
    if (!TryGetAuthenticatedPlayerId(request, sessions, out var playerId))
    {
        return Results.Unauthorized();
    }

    var summary = await chat.GetUnreadSummaryAsync(playerId, cancellationToken);
    return Results.Ok(summary);
});

app.MapPost("/api/online/invites", async (
    SendOnlineInviteRequest request,
    HttpRequest httpRequest,
    OnlineInviteService onlineInvites,
    RealtimeConnectionHub realtimeHub,
    SessionService sessions,
    CancellationToken cancellationToken) =>
{
    if (!TryGetAuthenticatedPlayerId(httpRequest, sessions, out var playerId))
    {
        return Results.Unauthorized();
    }

    var result = await onlineInvites.SendInviteAsync(playerId, request, cancellationToken);
    if (result.IsSuccess && result.Invite != null)
    {
        await realtimeHub.SendOnlineInviteAsync(result.Invite.TargetPlayerId, result.Invite, cancellationToken);
        return Results.Ok(result.Invite);
    }

    return ToOnlineInviteResult(result);
});

app.MapPost("/api/online/invites/{inviteId}/result", async (
    string inviteId,
    ReplyOnlineInviteRequest request,
    HttpRequest httpRequest,
    OnlineInviteService onlineInvites,
    RealtimeConnectionHub realtimeHub,
    SessionService sessions,
    CancellationToken cancellationToken) =>
{
    if (!TryGetAuthenticatedPlayerId(httpRequest, sessions, out var playerId))
    {
        return Results.Unauthorized();
    }

    var (result, response) = await onlineInvites.ReplyInviteAsync(
        playerId,
        inviteId,
        request,
        cancellationToken);
    if (result.IsSuccess && response != null)
    {
        await realtimeHub.SendOnlineInviteResultAsync(response.RequesterPlayerId, response, cancellationToken);
        return Results.Ok(response);
    }

    return ToOnlineInviteResult(result);
});

app.Run();

static async Task<IResult> ToAccountResultAsync(
    AccountResult result,
    SessionService sessions,
    RealtimeConnectionHub realtimeHub,
    CancellationToken cancellationToken)
{
    if (!result.IsSuccess || result.Profile == null)
    {
        return result.ErrorCode switch
        {
            AccountErrorCode.AccountAlreadyExists => Results.Conflict(ApiError.Conflict(result.ErrorMessage)),
            AccountErrorCode.AccountNotFound => Results.NotFound(ApiError.NotFound(result.ErrorMessage)),
            AccountErrorCode.WrongPassword => Results.BadRequest(ApiError.Validation(result.ErrorMessage)),
            _ => Results.BadRequest(ApiError.Validation(result.ErrorMessage))
        };
    }

    var token = sessions.CreateSession(result.Profile.PlayerId);
    await realtimeHub.DisconnectPlayerAsync(
        result.Profile.PlayerId,
        "This account signed in from another client.",
        cancellationToken);

    return Results.Ok(new AccountAuthResponse(result.Profile, token));
}

static bool TryGetSessionToken(HttpRequest request, out string token)
{
    token = string.Empty;

    if (request.Headers.TryGetValue("X-Session-Token", out var sessionHeader))
    {
        token = sessionHeader.ToString();
    }
    else if (request.Headers.TryGetValue("Authorization", out var authorization))
    {
        const string bearerPrefix = "Bearer ";
        var value = authorization.ToString();
        if (value.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            token = value[bearerPrefix.Length..].Trim();
        }
    }

    return !string.IsNullOrWhiteSpace(token);
}

static bool TryGetAuthenticatedPlayerId(
    HttpRequest request,
    SessionService sessions,
    out string playerId)
{
    playerId = string.Empty;
    return TryGetSessionToken(request, out var token) &&
           sessions.TryGetPlayerId(token, out playerId);
}

static IResult ToSocialResult(SocialResult result, string successMessage)
{
    if (result.IsSuccess)
    {
        return Results.Ok(new SocialActionResponse(successMessage));
    }

    return result.ErrorCode switch
    {
        SocialErrorCode.AccountNotFound => Results.NotFound(ApiError.NotFound(result.ErrorMessage)),
        SocialErrorCode.RequestNotFound => Results.NotFound(ApiError.NotFound(result.ErrorMessage)),
        SocialErrorCode.AlreadyFriends => Results.Conflict(ApiError.Conflict(result.ErrorMessage)),
        SocialErrorCode.RequestAlreadyExists => Results.Conflict(ApiError.Conflict(result.ErrorMessage)),
        _ => Results.BadRequest(ApiError.Validation(result.ErrorMessage))
    };
}

static IResult ToChatResult(ChatResult result)
{
    return result.ErrorCode switch
    {
        ChatErrorCode.AccountNotFound => Results.NotFound(ApiError.NotFound(result.ErrorMessage)),
        ChatErrorCode.NotFriends => Results.Conflict(ApiError.Conflict(result.ErrorMessage)),
        _ => Results.BadRequest(ApiError.Validation(result.ErrorMessage))
    };
}

static IResult ToOnlineInviteResult(OnlineInviteResult result)
{
    return result.ErrorCode switch
    {
        OnlineInviteErrorCode.AccountNotFound => Results.NotFound(ApiError.NotFound(result.ErrorMessage)),
        OnlineInviteErrorCode.NotFriends => Results.Conflict(ApiError.Conflict(result.ErrorMessage)),
        OnlineInviteErrorCode.TargetOffline => Results.Conflict(ApiError.Conflict(result.ErrorMessage)),
        _ => Results.BadRequest(ApiError.Validation(result.ErrorMessage))
    };
}

static string BuildAccountsAdminHtml(IReadOnlyList<AccountAdminResponse> accounts)
{
    var builder = new StringBuilder();
    builder.AppendLine("<!doctype html>");
    builder.AppendLine("<html lang=\"zh-CN\">");
    builder.AppendLine("<head>");
    builder.AppendLine("<meta charset=\"utf-8\">");
    builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
    builder.AppendLine("<meta http-equiv=\"refresh\" content=\"5\">");
    builder.AppendLine("<title>WorkDemo Accounts</title>");
    builder.AppendLine("<style>");
    builder.AppendLine("body{margin:0;background:#12151c;color:#e7ecf5;font-family:Segoe UI,Microsoft YaHei,Arial,sans-serif;}");
    builder.AppendLine("main{max-width:1120px;margin:0 auto;padding:28px;}");
    builder.AppendLine("h1{font-size:24px;margin:0 0 8px;}");
    builder.AppendLine(".sub{color:#9aa7ba;margin-bottom:22px;}");
    builder.AppendLine("table{width:100%;border-collapse:collapse;background:#1b202b;border:1px solid #2b3445;}");
    builder.AppendLine("th,td{padding:12px 14px;border-bottom:1px solid #2b3445;text-align:left;font-size:14px;}");
    builder.AppendLine("th{color:#9aa7ba;background:#202838;font-weight:600;}");
    builder.AppendLine("tr:hover td{background:#222b3b;}");
    builder.AppendLine(".pill{display:inline-block;min-width:42px;padding:3px 8px;border-radius:999px;font-size:12px;text-align:center;}");
    builder.AppendLine(".online{background:#1f6f43;color:#d8ffe8;}");
    builder.AppendLine(".offline{background:#5a2631;color:#ffdce2;}");
    builder.AppendLine(".empty{padding:28px;background:#1b202b;border:1px solid #2b3445;color:#9aa7ba;}");
    builder.AppendLine("</style>");
    builder.AppendLine("</head>");
    builder.AppendLine("<body>");
    builder.AppendLine("<main>");
    builder.AppendLine("<h1>WorkDemo 账号管理器</h1>");
    builder.AppendLine($"<div class=\"sub\">当前账号数量：{accounts.Count}。页面每 5 秒自动刷新。</div>");

    if (accounts.Count == 0)
    {
        builder.AppendLine("<div class=\"empty\">当前还没有注册账号。</div>");
    }
    else
    {
        builder.AppendLine("<table>");
        builder.AppendLine("<thead><tr><th>账号</th><th>玩家 ID</th><th>显示名</th><th>在线</th><th>好友数</th><th>收到申请</th><th>创建时间 UTC</th></tr></thead>");
        builder.AppendLine("<tbody>");

        for (var i = 0; i < accounts.Count; i++)
        {
            var account = accounts[i];
            var statusClass = account.IsOnline ? "online" : "offline";
            var statusText = account.IsOnline ? "在线" : "离线";

            builder.AppendLine("<tr>");
            builder.AppendLine($"<td>{Encode(account.AccountName)}</td>");
            builder.AppendLine($"<td>{Encode(account.PlayerId)}</td>");
            builder.AppendLine($"<td>{Encode(account.DisplayName)}</td>");
            builder.AppendLine($"<td><span class=\"pill {statusClass}\">{statusText}</span></td>");
            builder.AppendLine($"<td>{account.FriendCount}</td>");
            builder.AppendLine($"<td>{account.IncomingFriendRequestCount}</td>");
            builder.AppendLine($"<td>{account.CreatedAt:yyyy-MM-dd HH:mm:ss}</td>");
            builder.AppendLine("</tr>");
        }

        builder.AppendLine("</tbody>");
        builder.AppendLine("</table>");
    }

    builder.AppendLine("</main>");
    builder.AppendLine("</body>");
    builder.AppendLine("</html>");
    return builder.ToString();
}

static string BuildHotUpdateAdminHtml(int version, int fileCount, HotUpdatePublishResult? result, List<string>? deployLog = null)
{
    var sb = new StringBuilder();
    sb.AppendLine("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">");
    sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
    sb.AppendLine("<title>WorkDemo 热更发布</title><style>");
    sb.AppendLine("body{margin:0;background:#12151c;color:#e7ecf5;font-family:Segoe UI,Microsoft YaHei,Arial,sans-serif;}");
    sb.AppendLine("main{max-width:760px;margin:0 auto;padding:28px;}");
    sb.AppendLine("h1{font-size:24px;margin:0 0 6px;}.sub{color:#9aa7ba;margin-bottom:18px;}");
    sb.AppendLine(".card{background:#1b202b;border:1px solid #2b3445;border-radius:8px;padding:18px 20px;margin-bottom:18px;}");
    sb.AppendLine(".btn{background:#2f6fed;color:#fff;border:none;border-radius:6px;padding:11px 22px;font-size:15px;cursor:pointer;}");
    sb.AppendLine(".btn:hover{background:#3f7ffd;}.ver{font-size:20px;font-weight:600;}");
    sb.AppendLine("ul{margin:4px 0 10px;padding-left:20px;}li{margin:2px 0;font-family:Consolas,monospace;font-size:13px;}");
    sb.AppendLine(".added{color:#74e39b;}.modified{color:#ffd166;}.removed{color:#ff7a90;}.none{color:#6b7787;}");
    sb.AppendLine("</style></head><body><main>");
    sb.AppendLine("<h1>WorkDemo 热更发布</h1>");
    sb.AppendLine("<div class=\"sub\">编辑 HotUpdateContent 下的 lua/json 后点发布。客户端只会下载【已发布】的版本，编辑中的改动不会泄露。</div>");

    if (deployLog != null && deployLog.Count > 0)
    {
        sb.AppendLine("<div class=\"card\"><div class=\"ver\">📦 从 Unity 工程部署</div><ul>");
        foreach (var line in deployLog)
        {
            sb.AppendLine($"<li>{Encode(line)}</li>");
        }
        sb.AppendLine("</ul></div>");
    }

    if (result != null)
    {
        sb.AppendLine("<div class=\"card\">");
        sb.AppendLine($"<div class=\"ver\">✅ 发布成功！当前版本 v{result.Version}</div>");
        sb.AppendLine($"<div class=\"sub\">发布时间：{Encode(result.PublishedAt)}</div>");
        AppendHotUpdateDiff(sb, "新增", "added", result.Added);
        AppendHotUpdateDiff(sb, "修改", "modified", result.Modified);
        AppendHotUpdateDiff(sb, "删除", "removed", result.Removed);
        if (result.Added.Count == 0 && result.Modified.Count == 0 && result.Removed.Count == 0)
        {
            sb.AppendLine("<div class=\"none\">（和上一版相比没有变化）</div>");
        }
        sb.AppendLine("</div>");
    }
    else
    {
        sb.AppendLine("<div class=\"card\">");
        sb.AppendLine($"<div class=\"ver\">当前已发布版本：v{version}</div>");
        sb.AppendLine($"<div class=\"sub\">已发布文件数：{fileCount}</div>");
        sb.AppendLine("</div>");
    }

    sb.AppendLine("<form method=\"post\" action=\"/admin/hotupdate/deploy-publish\" style=\"margin-bottom:12px;\">");
    sb.AppendLine("<button class=\"btn\" type=\"submit\">📦🚀 一键部署 + 发布（从 Unity 工程同步配置/资源，再发布）</button>");
    sb.AppendLine("</form>");
    sb.AppendLine("<form method=\"post\" action=\"/admin/hotupdate/publish\">");
    sb.AppendLine("<button class=\"btn\" type=\"submit\" style=\"background:#3a4658;\">🚀 仅发布（不重新拷文件，直接冻结当前 HotUpdateContent）</button>");
    sb.AppendLine("</form>");
    sb.AppendLine("</main></body></html>");
    return sb.ToString();
}

static void AppendHotUpdateDiff(StringBuilder sb, string label, string cls, IReadOnlyList<string> items)
{
    if (items == null || items.Count == 0)
    {
        return;
    }

    sb.AppendLine($"<div class=\"{cls}\">{label}（{items.Count}）：</div><ul>");
    foreach (var p in items)
    {
        sb.AppendLine($"<li class=\"{cls}\">{Encode(p)}</li>");
    }
    sb.AppendLine("</ul>");
}

static string Encode(string? value)
{
    return WebUtility.HtmlEncode(value ?? string.Empty);
}
