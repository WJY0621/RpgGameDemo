using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;

public class HttpChatService : IChatService
{
    private const string DefaultBaseUrl = "http://127.0.0.1:5188";
    private const string BaseUrlKey = "WorkDemo.HttpAccount.BaseUrl";
    private const int RequestTimeoutMilliseconds = 5000;

    private readonly string baseUrl;

    public HttpChatService(string baseUrl = null)
    {
        this.baseUrl = ResolveBaseUrl(baseUrl);
    }

    public string LastError { get; private set; }

    public List<ChatMessageData> LoadFriendMessages(string ownerPlayerId, string friendPlayerId)
    {
        LastError = string.Empty;
        if (string.IsNullOrWhiteSpace(friendPlayerId))
        {
            return new List<ChatMessageData>();
        }

        try
        {
            string path = "/api/chat/friends/" + Uri.EscapeDataString(friendPlayerId.Trim()) + "/messages?markRead=true";
            string responseJson = SendRequest(path, "GET", null);
            ChatMessageResponseList response = ParseList<ChatMessageResponseList>(responseJson);
            List<ChatMessageData> result = new List<ChatMessageData>();
            if (response?.items == null)
            {
                return result;
            }

            for (int i = 0; i < response.items.Count; i++)
            {
                ChatMessageData message = ToChatMessageData(response.items[i]);
                if (message != null)
                {
                    result.Add(message);
                }
            }

            return result;
        }
        catch (Exception exception)
        {
            LastError = ResolveRequestError(exception, false);
            Debug.LogWarning($"[HttpChatService] LoadFriendMessages failed: {LastError}");
            return new List<ChatMessageData>();
        }
    }

    public ChatMessageData SendFriendMessage(string ownerPlayerId, string friendPlayerId, string messageText)
    {
        LastError = string.Empty;
        if (string.IsNullOrWhiteSpace(friendPlayerId))
        {
            LastError = "\u597d\u53cbID\u65e0\u6548";
            return null;
        }

        string safeText = SanitizeMessage(messageText);
        if (string.IsNullOrWhiteSpace(safeText))
        {
            LastError = "\u8bf7\u8f93\u5165\u804a\u5929\u5185\u5bb9";
            return null;
        }

        try
        {
            SendChatMessageRequest request = new SendChatMessageRequest
            {
                messageText = safeText
            };
            string path = "/api/chat/friends/" + Uri.EscapeDataString(friendPlayerId.Trim()) + "/messages";
            string responseJson = SendRequest(path, "POST", JsonUtility.ToJson(request));
            ChatMessageResponse response = JsonUtility.FromJson<ChatMessageResponse>(responseJson);
            return ToChatMessageData(response);
        }
        catch (Exception exception)
        {
            LastError = ResolveRequestError(exception, true);
            Debug.LogWarning($"[HttpChatService] SendFriendMessage failed: {LastError}");
            return null;
        }
    }

    public int LoadUnreadMessageCount(string ownerPlayerId)
    {
        return LoadUnreadSummary(ownerPlayerId).totalUnreadCount;
    }

    public ChatUnreadSummaryData LoadUnreadSummary(string ownerPlayerId)
    {
        LastError = string.Empty;
        try
        {
            string responseJson = SendRequest("/api/chat/unread", "GET", null);
            ChatUnreadSummaryResponse response = JsonUtility.FromJson<ChatUnreadSummaryResponse>(responseJson);
            return ToUnreadSummaryData(response);
        }
        catch (Exception exception)
        {
            LastError = ResolveRequestError(exception, false);
            return new ChatUnreadSummaryData();
        }
    }

    public void MarkFriendMessagesRead(string ownerPlayerId, string friendPlayerId)
    {
        LastError = string.Empty;
        if (string.IsNullOrWhiteSpace(friendPlayerId))
        {
            return;
        }

        try
        {
            string path = "/api/chat/friends/" + Uri.EscapeDataString(friendPlayerId.Trim()) + "/read";
            SendRequest(path, "POST", string.Empty);
        }
        catch (Exception exception)
        {
            LastError = ResolveRequestError(exception, false);
        }
    }

    public void ClearFriendMessages(string ownerPlayerId, string friendPlayerId)
    {
        LastError = "\u670d\u52a1\u5668\u7248\u804a\u5929\u4e0d\u652f\u6301\u5ba2\u6237\u7aef\u4e00\u952e\u6e05\u7a7a\u5386\u53f2\u8bb0\u5f55";
        Debug.LogWarning($"[HttpChatService] {LastError}");
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

    private static T ParseList<T>(string json)
    {
        return JsonUtility.FromJson<T>("{\"items\":" + (string.IsNullOrWhiteSpace(json) ? "[]" : json) + "}");
    }

    private static ChatMessageData ToChatMessageData(ChatMessageResponse response)
    {
        if (response == null || string.IsNullOrWhiteSpace(response.messageId))
        {
            return null;
        }

        return new ChatMessageData
        {
            messageId = response.messageId,
            channelType = ChatChannelType.FriendPrivate,
            senderPlayerId = response.senderPlayerId,
            receiverPlayerId = response.receiverPlayerId,
            messageText = response.messageText,
            sentAtUnixSeconds = ParseUnixSeconds(response.sentAt)
        };
    }

    private static ChatUnreadSummaryData ToUnreadSummaryData(ChatUnreadSummaryResponse response)
    {
        ChatUnreadSummaryData summary = new ChatUnreadSummaryData();
        if (response == null)
        {
            return summary;
        }

        summary.totalUnreadCount = Mathf.Max(0, response.totalUnreadCount);
        if (response.friends == null)
        {
            return summary;
        }

        for (int i = 0; i < response.friends.Count; i++)
        {
            FriendUnreadResponse item = response.friends[i];
            if (item == null || string.IsNullOrWhiteSpace(item.friendPlayerId))
            {
                continue;
            }

            summary.friends.Add(new FriendChatUnreadData
            {
                friendPlayerId = item.friendPlayerId,
                unreadCount = Mathf.Max(0, item.unreadCount)
            });
        }

        return summary;
    }

    private static long ParseUnixSeconds(string sentAt)
    {
        if (DateTimeOffset.TryParse(sentAt, out DateTimeOffset parsed))
        {
            return parsed.ToUnixTimeSeconds();
        }

        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private static string SanitizeMessage(string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText))
        {
            return string.Empty;
        }

        string trimmed = messageText.Trim();
        return trimmed.Length > 100 ? trimmed.Substring(0, 100) : trimmed;
    }

    private static string ResolveRequestError(Exception exception, bool forSend)
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
                    return string.IsNullOrWhiteSpace(serverMessage) ? "\u4f60\u4eec\u8fd8\u4e0d\u662f\u597d\u53cb" : serverMessage;
                case HttpStatusCode.BadRequest:
                    return string.IsNullOrWhiteSpace(serverMessage) ? "\u804a\u5929\u8bf7\u6c42\u65e0\u6548" : serverMessage;
            }
        }

        if (webException != null)
        {
            return "\u65e0\u6cd5\u8fde\u63a5\u804a\u5929\u670d\u52a1\u5668";
        }

        if (!string.IsNullOrWhiteSpace(serverMessage))
        {
            return serverMessage;
        }

        return forSend ? "\u6d88\u606f\u53d1\u9001\u5931\u8d25" : "\u804a\u5929\u8bf7\u6c42\u5931\u8d25";
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
    private class SendChatMessageRequest
    {
        public string messageText;
    }

    [Serializable]
    private class ChatMessageResponse
    {
        public string messageId;
        public string senderPlayerId;
        public string receiverPlayerId;
        public string messageText;
        public string sentAt;
    }

    [Serializable]
    private class ChatMessageResponseList
    {
        public List<ChatMessageResponse> items;
    }

    [Serializable]
    private class ChatUnreadSummaryResponse
    {
        public int totalUnreadCount;
        public List<FriendUnreadResponse> friends;
    }

    [Serializable]
    private class FriendUnreadResponse
    {
        public string friendPlayerId;
        public int unreadCount;
    }

    [Serializable]
    private class ErrorResponse
    {
        public string code;
        public string message;
    }
#pragma warning restore 0649
}
