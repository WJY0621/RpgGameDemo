public static class TaskDisplayNameResolver
{
    public static string ResolveNpcName(int npcID)
    {
        if (npcID < 0)
        {
            return "未知NPC";
        }

        string npcIDText = npcID.ToString();
        NPCDataComponent npc = GameMgr.NPC != null ? GameMgr.NPC.GetNPCByID(npcIDText) : null;
        if (npc != null && !string.IsNullOrWhiteSpace(npc.NPCName))
        {
            return npc.NPCName;
        }

        return $"NPC {npcIDText}";
    }

    public static string ResolveMonsterName(int monsterID)
    {
        if (monsterID < 0)
        {
            return "未知怪物";
        }

        MonsterData monsterData = MonsterJsonDatabase.GetMonsterData(monsterID);
        if (monsterData != null && !string.IsNullOrWhiteSpace(monsterData.monsterName))
        {
            return monsterData.monsterName;
        }

        return $"怪物 {monsterID}";
    }

    public static string ResolveItemName(int itemID)
    {
        if (itemID < 0)
        {
            return "未知物品";
        }

        Item item = GameMgr.Package != null
            ? GameMgr.Package.GetItemConfig(itemID)
            : ItemJsonDatabase.GetItem(itemID);

        if (item != null && !string.IsNullOrWhiteSpace(item.name))
        {
            return item.name;
        }

        return $"物品 {itemID}";
    }
}
