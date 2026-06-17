using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;

public class HttpSocialService : ISocialService
{
    private const string DefaultBaseUrl = "http://127.0.0.1:5188";
    private const string BaseUrlKey = "WorkDemo.HttpAccount.BaseUrl";
    private const int RequestTimeoutMilliseconds = 5000;

    private readonly string baseUrl;

    public HttpSocialService(string baseUrl = null)
    {
        this.baseUrl = ResolveBaseUrl(baseUrl);
    }

    public string LastError { get; private set; }

    public List<FriendInfo> LoadFriends(string ownerPlayerId)
    {
        LastError = string.Empty;
        try
        {
            string responseJson = SendRequest("/api/social/friends", "GET", null, out _);
            FriendResponseList response = ParseList<FriendResponseList>(responseJson);
            List<FriendInfo> result = new List<FriendInfo>();
            if (response?.items == null)
            {
                return result;
            }

            for (int i = 0; i < response.items.Count; i++)
            {
                FriendInfo friend = ToFriendInfo(response.items[i]);
                if (friend != null)
                {
                    result.Add(friend);
                }
            }

            return result;
        }
        catch (Exception exception)
        {
            LastError = ResolveRequestError(exception);
            Debug.LogWarning($"[HttpSocialService] LoadFriends failed: {LastError}");
            return new List<FriendInfo>();
        }
    }

    public FriendInfo SearchPlayerById(string requesterPlayerId, string targetPlayerId)
    {
        LastError = string.Empty;
        if (string.IsNullOrWhiteSpace(targetPlayerId))
        {
            LastError = "\u8bf7\u8f93\u5165\u73a9\u5bb6ID";
            return null;
        }

        if (string.Equals(NormalizePlayerId(requesterPlayerId), NormalizePlayerId(targetPlayerId), StringComparison.OrdinalIgnoreCase))
        {
            LastError = "\u4e0d\u80fd\u6dfb\u52a0\u81ea\u5df1\u4e3a\u597d\u53cb";
            return null;
        }

        try
        {
            string path = "/api/social/search/" + Uri.EscapeDataString(targetPlayerId.Trim());
            string responseJson = SendRequest(path, "GET", null, out _);
            ServerProfileResponse response = JsonUtility.FromJson<ServerProfileResponse>(responseJson);
            return ToFriendInfo(response, true);
        }
        catch (Exception exception)
        {
            LastError = ResolveRequestError(exception);
            Debug.LogWarning($"[HttpSocialService] SearchPlayerById failed: {LastError}");
            return null;
        }
    }

    public List<FriendRequestData> LoadIncomingFriendRequests(string ownerPlayerId)
    {
        LastError = string.Empty;
        try
        {
            string responseJson = SendRequest("/api/social/friend-requests", "GET", null, out _);
            FriendRequestResponseList response = ParseList<FriendRequestResponseList>(responseJson);
            List<FriendRequestData> result = new List<FriendRequestData>();
            if (response?.items == null)
            {
                return result;
            }

            for (int i = 0; i < response.items.Count; i++)
            {
                FriendRequestData request = ToFriendRequestData(response.items[i], ownerPlayerId);
                if (request != null)
                {
                    result.Add(request);
                }
            }

            return result;
        }
        catch (Exception exception)
        {
            LastError = ResolveRequestError(exception);
            Debug.LogWarning($"[HttpSocialService] LoadIncomingFriendRequests failed: {LastError}");
            return new List<FriendRequestData>();
        }
    }

    public bool SendFriendRequest(string requesterPlayerId, string requesterDisplayName, FriendInfo targetFriend)
    {
        LastError = string.Empty;
        if (targetFriend == null || string.IsNullOrWhiteSpace(targetFriend.playerId))
        {
            LastError = "\u8bf7\u5148\u9009\u62e9\u8981\u6dfb\u52a0\u7684\u73a9\u5bb6";
            return false;
        }

        if (string.Equals(NormalizePlayerId(requesterPlayerId), NormalizePlayerId(targetFriend.playerId), StringComparison.OrdinalIgnoreCase))
        {
            LastError = "\u4e0d\u80fd\u6dfb\u52a0\u81ea\u5df1\u4e3a\u597d\u53cb";
            return false;
        }

        CreateFriendRequestRequest request = new CreateFriendRequestRequest
        {
            targetPlayerId = targetFriend.playerId.Trim()
        };

        return SendSocialAction("/api/social/friend-requests", "POST", JsonUtility.ToJson(request), "SendFriendRequest");
    }

    public bool AcceptFriendRequest(string ownerPlayerId, string requestId)
    {
        LastError = string.Empty;
        if (string.IsNullOrWhiteSpace(requestId))
        {
            LastError = "\u597d\u53cb\u7533\u8bf7\u65e0\u6548";
            return false;
        }

        string path = "/api/social/friend-requests/" + Uri.EscapeDataString(requestId.Trim()) + "/accept";
        return SendSocialAction(path, "POST", string.Empty, "AcceptFriendRequest");
    }

    public bool RefuseFriendRequest(string ownerPlayerId, string requestId)
    {
        LastError = string.Empty;
        if (string.IsNullOrWhiteSpace(requestId))
        {
            LastError = "\u597d\u53cb\u7533\u8bf7\u65e0\u6548";
            return false;
        }

        string path = "/api/social/friend-requests/" + Uri.EscapeDataString(requestId.Trim()) + "/refuse";
        return SendSocialAction(path, "POST", string.Empty, "RefuseFriendRequest");
    }

    public bool AddFriend(string ownerPlayerId, FriendInfo friendInfo)
    {
        return SendFriendRequest(ownerPlayerId, string.Empty, friendInfo);
    }

    public bool RemoveFriend(string ownerPlayerId, string friendPlayerId)
    {
        LastError = string.Empty;
        if (string.IsNullOrWhiteSpace(friendPlayerId))
        {
            LastError = "\u597d\u53cbID\u65e0\u6548";
            return false;
        }

        string path = "/api/social/friends/" + Uri.EscapeDataString(friendPlayerId.Trim());
        return SendSocialAction(path, "DELETE", string.Empty, "RemoveFriend");
    }

    public void ClearFriends(string ownerPlayerId)
    {
        LastError = "\u670d\u52a1\u5668\u7248\u597d\u53cb\u7cfb\u7edf\u4e0d\u652f\u6301\u4e00\u952e\u6e05\u7a7a\u597d\u53cb";
        Debug.LogWarning($"[HttpSocialService] {LastError}");
    }

    private bool SendSocialAction(string path, string method, string bodyJson, string actionName)
    {
        try
        {
            SendRequest(path, method, bodyJson, out _);
            LastError = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            LastError = ResolveRequestError(exception);
            Debug.LogWarning($"[HttpSocialService] {actionName} failed: {LastError}");
            return false;
        }
    }

    private string SendRequest(string path, string method, string bodyJson, out HttpStatusCode statusCode)
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
            statusCode = response.StatusCode;
            return reader.ReadToEnd();
        }
    }

    private string BuildUrl(string path)
    {
        string trimmedBaseUrl = baseUrl.TrimEnd('/');
        string trimmedPath = path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
        return trimmedBaseUrl + trimmedPath;
    }

    private static T ParseList<T>(string json)
    {
        return JsonUtility.FromJson<T>("{\"items\":" + (string.IsNullOrWhiteSpace(json) ? "[]" : json) + "}");
    }

    private static FriendInfo ToFriendInfo(FriendResponse response)
    {
        if (response == null || response.profile == null)
        {
            return null;
        }

        FriendInfo info = ToFriendInfo(response.profile, response.isOnline);
        return info;
    }

    private static FriendInfo ToFriendInfo(ServerProfileResponse profile, bool isOnline)
    {
        if (profile == null || string.IsNullOrWhiteSpace(profile.playerId))
        {
            return null;
        }

        return new FriendInfo
        {
            playerId = profile.playerId,
            displayName = string.IsNullOrWhiteSpace(profile.displayName) ? profile.playerId : profile.displayName,
            onlineState = isOnline ? FriendOnlineState.Online : FriendOnlineState.Offline,
            hasUnreadMessage = false
        };
    }

    private static FriendRequestData ToFriendRequestData(FriendRequestResponse response, string ownerPlayerId)
    {
        if (response == null || response.requester == null || string.IsNullOrWhiteSpace(response.requestId))
        {
            return null;
        }

        return new FriendRequestData
        {
            requestId = response.requestId,
            fromPlayerId = response.requester.playerId,
            fromDisplayName = string.IsNullOrWhiteSpace(response.requester.displayName)
                ? response.requester.playerId
                : response.requester.displayName,
            toPlayerId = ownerPlayerId ?? string.Empty,
            createdAtUnixSeconds = ParseUnixSeconds(response.createdAt)
        };
    }

    private static long ParseUnixSeconds(string createdAt)
    {
        if (DateTimeOffset.TryParse(createdAt, out DateTimeOffset created))
        {
            return created.ToUnixTimeSeconds();
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
                case HttpStatusCode.NotFound:
                    return "\u73a9\u5bb6\u4e0d\u5b58\u5728";
                case HttpStatusCode.Unauthorized:
                    return "\u767b\u5f55\u72b6\u6001\u5df2\u5931\u6548";
                case HttpStatusCode.Conflict:
                    return string.IsNullOrWhiteSpace(serverMessage) ? "\u5df2\u7ecf\u662f\u597d\u53cb\u6216\u5df2\u53d1\u9001\u7533\u8bf7" : serverMessage;
                case HttpStatusCode.BadRequest:
                    return string.IsNullOrWhiteSpace(serverMessage) ? "\u597d\u53cb\u8bf7\u6c42\u65e0\u6548" : serverMessage;
            }
        }

        if (webException != null)
        {
            return "\u65e0\u6cd5\u8fde\u63a5\u597d\u53cb\u670d\u52a1\u5668";
        }

        return string.IsNullOrWhiteSpace(serverMessage)
            ? "\u597d\u53cb\u8bf7\u6c42\u5931\u8d25"
            : serverMessage;
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

    private static string NormalizePlayerId(string playerId)
    {
        return string.IsNullOrWhiteSpace(playerId) ? string.Empty : playerId.Trim().ToUpperInvariant();
    }

#pragma warning disable 0649
    [Serializable]
    private class CreateFriendRequestRequest
    {
        public string targetPlayerId;
    }

    [Serializable]
    private class ServerProfileResponse
    {
        public string accountName;
        public string playerId;
        public string displayName;
        public string createdAt;
    }

    [Serializable]
    private class FriendResponse
    {
        public ServerProfileResponse profile;
        public bool isOnline;
    }

    [Serializable]
    private class FriendRequestResponse
    {
        public string requestId;
        public ServerProfileResponse requester;
        public string createdAt;
    }

    [Serializable]
    private class FriendResponseList
    {
        public List<FriendResponse> items;
    }

    [Serializable]
    private class FriendRequestResponseList
    {
        public List<FriendRequestResponse> items;
    }

    [Serializable]
    private class ErrorResponse
    {
        public string code;
        public string message;
    }
#pragma warning restore 0649
}
