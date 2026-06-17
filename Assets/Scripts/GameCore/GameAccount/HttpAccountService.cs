using System;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;

public class HttpAccountService : IAccountService
{
    private const string DefaultBaseUrl = "http://127.0.0.1:5188";
    private const string BaseUrlKey = "WorkDemo.HttpAccount.BaseUrl";
    private const string ProfileKey = "WorkDemo.HttpAccount.Profile";
    private const int RequestTimeoutMilliseconds = 5000;

    private readonly string baseUrl;

    public HttpAccountService(string baseUrl = null)
    {
        this.baseUrl = ResolveBaseUrl(baseUrl);
    }

    public string LastError { get; private set; }

    public bool HasSavedProfile()
    {
        return PlayerPrefs.HasKey(ProfileKey);
    }

    public AccountProfile LoadSavedProfile()
    {
        LastError = string.Empty;
        string json = PlayerPrefs.GetString(ProfileKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        AccountProfile profile = JsonUtility.FromJson<AccountProfile>(json);
        if (profile == null || !profile.IsValid())
        {
            ClearSavedProfile();
            return null;
        }

        string sessionToken = HttpSessionContext.LoadSavedSessionToken();
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            LastError = "登录状态已失效";
            ClearSavedProfile();
            return null;
        }

        try
        {
            string responseJson = SendRequest(
                "/api/account/me",
                "GET",
                null,
                sessionToken,
                out _);

            ServerProfileResponse response = JsonUtility.FromJson<ServerProfileResponse>(responseJson);
            AccountProfile serverProfile = ToProfile(response, profile.accountName);
            if (serverProfile == null || !serverProfile.IsValid())
            {
                LastError = "\u670d\u52a1\u5668\u8fd4\u56de\u7684\u8d26\u53f7\u6570\u636e\u65e0\u6548";
                ClearSavedProfile();
                return null;
            }

            SaveLocalProfile(serverProfile);
            HttpSessionContext.SetSessionToken(sessionToken);
            return serverProfile.Clone();
        }
        catch (WebException webException)
        {
            string errorJson = ReadErrorResponse(webException.Response);
            LastError = ResolveRequestError(webException, errorJson);
            ClearSavedProfile();
            return null;
        }
        catch (Exception exception)
        {
            LastError = ResolveRequestError(exception, null);
            ClearSavedProfile();
            return null;
        }
    }

    public AccountProfile Login(string accountName, string password)
    {
        LastError = string.Empty;
        if (string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(password))
        {
            LastError = "\u8bf7\u8f93\u5165\u8d26\u53f7\u548c\u5bc6\u7801";
            return null;
        }

        LoginRequest request = new LoginRequest
        {
            accountName = accountName.Trim(),
            password = password
        };

        return SendAuthRequest("/api/account/login", request);
    }

    public AccountProfile Register(string accountName, string password)
    {
        LastError = string.Empty;
        if (string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(password))
        {
            LastError = "\u8bf7\u8f93\u5165\u8d26\u53f7\u548c\u5bc6\u7801";
            return null;
        }

        RegisterRequest request = new RegisterRequest
        {
            accountName = accountName.Trim(),
            password = password,
            displayName = string.Empty
        };

        return SendAuthRequest("/api/account/register", request);
    }

    public AccountProfile CreateAccount(string displayName)
    {
        LastError = "\u670d\u52a1\u5668\u8d26\u53f7\u9700\u8981\u5148\u6ce8\u518c";
        return null;
    }

    public AccountProfile LoginOrCreate(string displayName)
    {
        AccountProfile savedProfile = LoadSavedProfile();
        if (savedProfile != null)
        {
            LastError = string.Empty;
            return savedProfile.Clone();
        }

        LastError = "\u8bf7\u5148\u767b\u5f55\u6216\u6ce8\u518c\u8d26\u53f7";
        return null;
    }

    public void SaveProfile(AccountProfile profile)
    {
        if (profile == null || !profile.IsValid())
        {
            return;
        }

        SaveLocalProfile(profile);
        string sessionToken = HttpSessionContext.GetSessionToken();
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return;
        }

        UpdateDisplayNameRequest request = new UpdateDisplayNameRequest
        {
            displayName = profile.displayName ?? string.Empty
        };

        try
        {
            string responseJson = SendRequest(
                "/api/account/display-name",
                "PATCH",
                JsonUtility.ToJson(request),
                sessionToken,
                out _);

            ServerProfileResponse response = JsonUtility.FromJson<ServerProfileResponse>(responseJson);
            AccountProfile serverProfile = ToProfile(response, profile.accountName);
            if (serverProfile != null && serverProfile.IsValid())
            {
                SaveLocalProfile(serverProfile);
            }

            LastError = string.Empty;
        }
        catch (Exception exception)
        {
            LastError = ResolveRequestError(exception, null);
            Debug.LogWarning($"[HttpAccountService] Failed to sync display name: {LastError}");
        }
    }

    public void Logout()
    {
        HttpSessionContext.ClearSessionToken();
    }

    public void ClearSavedProfile()
    {
        PlayerPrefs.DeleteKey(ProfileKey);
        HttpSessionContext.ClearSessionToken();
        PlayerPrefs.Save();
    }

    private AccountProfile SendAuthRequest(string path, object requestBody)
    {
        try
        {
            string responseJson = SendRequest(
                path,
                "POST",
                JsonUtility.ToJson(requestBody),
                null,
                out _);

            AuthResponse response = JsonUtility.FromJson<AuthResponse>(responseJson);
            AccountProfile profile = ToProfile(response?.profile, null);
            if (profile == null || !profile.IsValid())
            {
                LastError = "\u670d\u52a1\u5668\u8fd4\u56de\u7684\u8d26\u53f7\u6570\u636e\u65e0\u6548";
                return null;
            }

            SaveLocalProfile(profile);
            if (!string.IsNullOrWhiteSpace(response.sessionToken))
            {
                HttpSessionContext.SaveSessionToken(response.sessionToken);
            }

            LastError = string.Empty;
            return profile.Clone();
        }
        catch (WebException webException)
        {
            string errorJson = ReadErrorResponse(webException.Response);
            LastError = ResolveRequestError(webException, errorJson);
            return null;
        }
        catch (Exception exception)
        {
            LastError = ResolveRequestError(exception, null);
            return null;
        }
    }

    private string SendRequest(
        string path,
        string method,
        string bodyJson,
        string sessionToken,
        out HttpStatusCode statusCode)
    {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(BuildUrl(path));
        request.Method = method;
        request.Accept = "application/json";
        request.Timeout = RequestTimeoutMilliseconds;
        request.ReadWriteTimeout = RequestTimeoutMilliseconds;

        if (!string.IsNullOrWhiteSpace(sessionToken))
        {
            request.Headers["X-Session-Token"] = sessionToken;
        }

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

    private void SaveLocalProfile(AccountProfile profile)
    {
        PlayerPrefs.SetString(ProfileKey, JsonUtility.ToJson(profile));
        PlayerPrefs.Save();
    }

    private static AccountProfile ToProfile(ServerProfileResponse response, string fallbackAccountName)
    {
        if (response == null || string.IsNullOrWhiteSpace(response.playerId))
        {
            return null;
        }

        return new AccountProfile
        {
            accountName = string.IsNullOrWhiteSpace(response.accountName)
                ? fallbackAccountName
                : response.accountName,
            playerId = response.playerId,
            displayName = response.displayName ?? string.Empty,
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

    private static string ResolveRequestError(Exception exception, string errorJson)
    {
        string serverMessage = ExtractServerMessage(errorJson);
        WebException webException = exception as WebException;
        HttpWebResponse httpResponse = webException?.Response as HttpWebResponse;

        if (httpResponse != null)
        {
            if (httpResponse.StatusCode == HttpStatusCode.NotFound)
            {
                return "\u8d26\u6237\u4e0d\u5b58\u5728";
            }

            if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                return "\u767b\u5f55\u72b6\u6001\u5df2\u5931\u6548";
            }

            if (httpResponse.StatusCode == HttpStatusCode.Conflict)
            {
                return "\u8d26\u6237\u5df2\u5b58\u5728";
            }

            if (httpResponse.StatusCode == HttpStatusCode.BadRequest &&
                serverMessage.IndexOf("wrong password", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "\u5bc6\u7801\u8f93\u5165\u9519\u8bef";
            }

            if (serverMessage.IndexOf("at least 3", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "\u8d26\u53f7\u957f\u5ea6\u81f3\u5c113\u4f4d";
            }

            if (serverMessage.IndexOf("at least 6", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "\u5bc6\u7801\u957f\u5ea6\u81f3\u5c116\u4f4d";
            }
        }

        if (webException != null)
        {
            return "\u65e0\u6cd5\u8fde\u63a5\u8d26\u53f7\u670d\u52a1\u5668";
        }

        return string.IsNullOrWhiteSpace(serverMessage)
            ? "\u8d26\u53f7\u8bf7\u6c42\u5931\u8d25"
            : serverMessage;
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
    private class LoginRequest
    {
        public string accountName;
        public string password;
    }

    [Serializable]
    private class RegisterRequest
    {
        public string accountName;
        public string password;
        public string displayName;
    }

    [Serializable]
    private class UpdateDisplayNameRequest
    {
        public string displayName;
    }

    [Serializable]
    private class AuthResponse
    {
        public ServerProfileResponse profile;
        public string sessionToken;
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
    private class ErrorResponse
    {
        public string code;
        public string message;
    }
#pragma warning restore 0649
}
