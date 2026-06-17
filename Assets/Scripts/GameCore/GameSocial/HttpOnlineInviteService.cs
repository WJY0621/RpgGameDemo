using System;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;

public class HttpOnlineInviteService
{
    private const string DefaultBaseUrl = "http://127.0.0.1:5188";
    private const string BaseUrlKey = "WorkDemo.HttpAccount.BaseUrl";
    private const int RequestTimeoutMilliseconds = 5000;

    private readonly string baseUrl;

    public HttpOnlineInviteService(string baseUrl = null)
    {
        this.baseUrl = ResolveBaseUrl(baseUrl);
    }

    public string LastError { get; private set; }

    public InviteData SendOnlineInvite(InviteData inviteData)
    {
        LastError = string.Empty;
        if (inviteData == null || string.IsNullOrWhiteSpace(inviteData.targetPlayerId))
        {
            LastError = "\u8054\u673a\u9080\u8bf7\u76ee\u6807\u65e0\u6548";
            return null;
        }

        try
        {
            SendOnlineInviteRequest request = new SendOnlineInviteRequest
            {
                targetPlayerId = inviteData.targetPlayerId,
                inviteType = inviteData.inviteType.ToString(),
                address = inviteData.address,
                port = inviteData.port,
                relayJoinCode = inviteData.relayJoinCode,
                lobbyId = inviteData.lobbyId
            };
            string responseJson = SendRequest("/api/online/invites", "POST", JsonUtility.ToJson(request));
            OnlineInviteResponse response = JsonUtility.FromJson<OnlineInviteResponse>(responseJson);
            return ToInviteData(response);
        }
        catch (Exception exception)
        {
            LastError = ResolveRequestError(exception);
            Debug.LogWarning($"[HttpOnlineInviteService] SendOnlineInvite failed: {LastError}");
            return null;
        }
    }

    public InviteResultData SendOnlineInviteResult(
        InviteData inviteData,
        bool accepted,
        string address,
        ushort port,
        string relayJoinCode = null,
        string lobbyId = null)
    {
        LastError = string.Empty;
        if (inviteData == null || string.IsNullOrWhiteSpace(inviteData.inviteId))
        {
            LastError = "\u8054\u673a\u9080\u8bf7\u5df2\u5931\u6548";
            return null;
        }

        try
        {
            ReplyOnlineInviteRequest request = new ReplyOnlineInviteRequest
            {
                accepted = accepted,
                address = address,
                port = port,
                relayJoinCode = relayJoinCode,
                lobbyId = lobbyId
            };
            string path = "/api/online/invites/" + Uri.EscapeDataString(inviteData.inviteId) + "/result";
            string responseJson = SendRequest(path, "POST", JsonUtility.ToJson(request));
            OnlineInviteResultResponse response = JsonUtility.FromJson<OnlineInviteResultResponse>(responseJson);
            return ToInviteResultData(response);
        }
        catch (Exception exception)
        {
            LastError = ResolveRequestError(exception);
            Debug.LogWarning($"[HttpOnlineInviteService] SendOnlineInviteResult failed: {LastError}");
            return null;
        }
    }

    private string SendRequest(string path, string method, string bodyJson)
    {
        string sessionToken = HttpSessionContext.GetSessionToken();
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            throw new UnauthorizedAccessException("\u8bf7\u5148\u767b\u5f55\u8d26\u53f7");
        }

        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(BuildUrl(path));
        request.Method = method;
        request.Accept = "application/json";
        request.Timeout = RequestTimeoutMilliseconds;
        request.ReadWriteTimeout = RequestTimeoutMilliseconds;
        request.Headers["X-Session-Token"] = sessionToken;

        if (!string.IsNullOrWhiteSpace(bodyJson))
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(bodyJson);
            request.ContentType = "application/json";
            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(bodyBytes, 0, bodyBytes.Length);
            }
        }

        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
        using (Stream responseStream = response.GetResponseStream())
        using (StreamReader reader = new StreamReader(responseStream, Encoding.UTF8))
        {
            return reader.ReadToEnd();
        }
    }

    private string BuildUrl(string path)
    {
        string trimmedBaseUrl = baseUrl.TrimEnd('/');
        string trimmedPath = path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
        return trimmedBaseUrl + trimmedPath;
    }

    private static InviteData ToInviteData(OnlineInviteResponse response)
    {
        if (response == null || string.IsNullOrWhiteSpace(response.inviteId))
        {
            return null;
        }

        return new InviteData
        {
            inviteId = response.inviteId,
            inviteType = ParseInviteType(response.inviteType),
            requesterPlayerId = response.requesterPlayerId,
            requesterDisplayName = response.requesterDisplayName,
            hostPlayerId = response.hostPlayerId,
            targetPlayerId = response.targetPlayerId,
            address = response.address,
            port = (ushort)Mathf.Clamp(response.port, 1, ushort.MaxValue),
            relayJoinCode = response.relayJoinCode,
            lobbyId = response.lobbyId,
            createdAtUnixSeconds = ParseUnixSeconds(response.createdAt)
        };
    }

    private static InviteResultData ToInviteResultData(OnlineInviteResultResponse response)
    {
        if (response == null || string.IsNullOrWhiteSpace(response.inviteId))
        {
            return null;
        }

        return new InviteResultData
        {
            inviteId = response.inviteId,
            inviteType = ParseInviteType(response.inviteType),
            requesterPlayerId = response.requesterPlayerId,
            requesterDisplayName = response.requesterDisplayName,
            hostPlayerId = response.hostPlayerId,
            targetPlayerId = response.targetPlayerId,
            address = response.address,
            port = (ushort)Mathf.Clamp(response.port, 1, ushort.MaxValue),
            relayJoinCode = response.relayJoinCode,
            lobbyId = response.lobbyId,
            accepted = response.accepted,
            respondedAtUnixSeconds = ParseUnixSeconds(response.respondedAt)
        };
    }

    private static InviteType ParseInviteType(string inviteType)
    {
        return string.Equals(inviteType, "RequestToJoinWorld", StringComparison.OrdinalIgnoreCase)
            ? InviteType.RequestToJoinWorld
            : InviteType.InviteToMyWorld;
    }

    private static long ParseUnixSeconds(string createdAt)
    {
        if (DateTimeOffset.TryParse(createdAt, out DateTimeOffset parsed))
        {
            return parsed.ToUnixTimeSeconds();
        }

        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private static string ResolveRequestError(Exception exception)
    {
        if (exception is UnauthorizedAccessException unauthorizedAccessException)
        {
            return unauthorizedAccessException.Message;
        }

        WebException webException = exception as WebException;
        string errorJson = ReadErrorResponse(webException?.Response);
        string serverMessage = ExtractServerMessage(errorJson);
        HttpWebResponse httpResponse = webException?.Response as HttpWebResponse;

        if (httpResponse != null)
        {
            switch (httpResponse.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    return "\u767b\u5f55\u72b6\u6001\u5df2\u5931\u6548";
                case HttpStatusCode.NotFound:
                    return "\u597d\u53cb\u8d26\u53f7\u4e0d\u5b58\u5728";
                case HttpStatusCode.Conflict:
                    return string.IsNullOrWhiteSpace(serverMessage) ? "\u5bf9\u65b9\u4e0d\u5728\u7ebf\u6216\u4e0d\u662f\u597d\u53cb" : serverMessage;
                case HttpStatusCode.BadRequest:
                    return string.IsNullOrWhiteSpace(serverMessage) ? "\u8054\u673a\u9080\u8bf7\u65e0\u6548" : serverMessage;
            }
        }

        if (webException != null)
        {
            return "\u65e0\u6cd5\u8fde\u63a5\u8054\u673a\u9080\u8bf7\u670d\u52a1\u5668";
        }

        return string.IsNullOrWhiteSpace(serverMessage) ? "\u8054\u673a\u9080\u8bf7\u53d1\u9001\u5931\u8d25" : serverMessage;
    }

    private static string ReadErrorResponse(WebResponse response)
    {
        if (response == null)
        {
            return string.Empty;
        }

        using (response)
        using (Stream responseStream = response.GetResponseStream())
        using (StreamReader reader = new StreamReader(responseStream, Encoding.UTF8))
        {
            return reader.ReadToEnd();
        }
    }

    private static string ExtractServerMessage(string errorJson)
    {
        if (string.IsNullOrWhiteSpace(errorJson))
        {
            return string.Empty;
        }

        try
        {
            ErrorResponse response = JsonUtility.FromJson<ErrorResponse>(errorJson);
            return response?.message ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ResolveBaseUrl(string configuredBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return configuredBaseUrl.Trim();
        }

        string savedBaseUrl = PlayerPrefs.GetString(BaseUrlKey, string.Empty);
        return string.IsNullOrWhiteSpace(savedBaseUrl) ? DefaultBaseUrl : savedBaseUrl.Trim();
    }

#pragma warning disable 0649
    [Serializable]
    private class SendOnlineInviteRequest
    {
        public string targetPlayerId;
        public string inviteType;
        public string address;
        public int port;
        public string relayJoinCode;
        public string lobbyId;
    }

    [Serializable]
    private class ReplyOnlineInviteRequest
    {
        public bool accepted;
        public string address;
        public int port;
        public string relayJoinCode;
        public string lobbyId;
    }

    [Serializable]
    private class OnlineInviteResponse
    {
        public string inviteId;
        public string inviteType;
        public string requesterPlayerId;
        public string requesterDisplayName;
        public string hostPlayerId;
        public string targetPlayerId;
        public string address;
        public int port;
        public string relayJoinCode;
        public string lobbyId;
        public string createdAt;
    }

    [Serializable]
    private class OnlineInviteResultResponse
    {
        public string inviteId;
        public string inviteType;
        public string requesterPlayerId;
        public string requesterDisplayName;
        public string hostPlayerId;
        public string targetPlayerId;
        public string address;
        public int port;
        public string relayJoinCode;
        public string lobbyId;
        public bool accepted;
        public string respondedAt;
    }

    [Serializable]
    private class ErrorResponse
    {
        public string code;
        public string message;
    }
#pragma warning restore 0649
}
