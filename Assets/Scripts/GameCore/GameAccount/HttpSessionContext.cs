using UnityEngine;

public static class HttpSessionContext
{
    public const string SessionTokenKey = "WorkDemo.HttpAccount.SessionToken";

    private static string runtimeSessionToken = string.Empty;

    public static string SessionToken => runtimeSessionToken;

    public static bool HasSessionToken => !string.IsNullOrWhiteSpace(runtimeSessionToken);

    public static void SetSessionToken(string sessionToken)
    {
        runtimeSessionToken = string.IsNullOrWhiteSpace(sessionToken) ? string.Empty : sessionToken.Trim();
    }

    public static string GetSessionToken()
    {
        return runtimeSessionToken;
    }

    public static string LoadSavedSessionToken()
    {
        string sessionToken = PlayerPrefs.GetString(SessionTokenKey, string.Empty);
        SetSessionToken(sessionToken);
        return runtimeSessionToken;
    }

    public static void SaveSessionToken(string sessionToken)
    {
        SetSessionToken(sessionToken);
        if (string.IsNullOrWhiteSpace(runtimeSessionToken))
        {
            PlayerPrefs.DeleteKey(SessionTokenKey);
        }
        else
        {
            PlayerPrefs.SetString(SessionTokenKey, runtimeSessionToken);
        }

        PlayerPrefs.Save();
    }

    public static void ClearSessionToken()
    {
        runtimeSessionToken = string.Empty;
        PlayerPrefs.DeleteKey(SessionTokenKey);
        PlayerPrefs.Save();
    }
}
