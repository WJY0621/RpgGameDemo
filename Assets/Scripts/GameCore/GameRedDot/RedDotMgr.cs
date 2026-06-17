using System;
using System.Collections.Generic;
using UnityEngine;

public class RedDotMgr
{
    private readonly Dictionary<RedDotType, int> counts = new Dictionary<RedDotType, int>();

    public event Action<RedDotType> OnRedDotChanged;

    public int GetCount(RedDotType type)
    {
        if (type == RedDotType.Friend)
        {
            return GetCount(RedDotType.FriendRequest) + GetCount(RedDotType.Chat);
        }

        return counts.TryGetValue(type, out int count) ? count : 0;
    }

    public bool HasRedDot(RedDotType type)
    {
        return GetCount(type) > 0;
    }

    public void SetCount(RedDotType type, int count)
    {
        if (type == RedDotType.Friend)
        {
            return;
        }

        int safeCount = Mathf.Max(0, count);
        if (counts.TryGetValue(type, out int oldCount) && oldCount == safeCount)
        {
            return;
        }

        counts[type] = safeCount;
        NotifyChanged(type);

        if (type == RedDotType.FriendRequest || type == RedDotType.Chat)
        {
            NotifyChanged(RedDotType.Friend);
        }
    }

    public void Clear(RedDotType type)
    {
        SetCount(type, 0);
    }

    public void ClearAll()
    {
        bool hadFriendRequest = GetCount(RedDotType.FriendRequest) > 0;
        bool hadChat = GetCount(RedDotType.Chat) > 0;
        counts.Clear();

        if (hadFriendRequest)
        {
            NotifyChanged(RedDotType.FriendRequest);
        }

        if (hadChat)
        {
            NotifyChanged(RedDotType.Chat);
        }

        if (hadFriendRequest || hadChat)
        {
            NotifyChanged(RedDotType.Friend);
        }
    }

    private void NotifyChanged(RedDotType type)
    {
        OnRedDotChanged?.Invoke(type);
    }
}
