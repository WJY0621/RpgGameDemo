using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "RoleList",menuName = "Data/GameCharacter/Role/Role List")]
public class RoleListData : ScriptableObject
{
    public List<RoleData> roleList;

    /// <summary>
    /// 鏍规嵁 roleID 鍜?roleSex 鑾峰彇瑙掕壊鐩稿叧鐨勮祫婧愬悕绉?
    /// </summary>
    /// <param name="roleID">瑙掕壊ID</param>
    /// <param name="roleSex">瑙掕壊鎬у埆锛坱rue=鐢凤紝false=濂筹級</param>
    /// <param name="roleIconName">鍥炬爣鍚嶇О</param>
    /// <param name="createRolePanelBKName">鍒涘缓瑙掕壊闈㈡澘鑳屾櫙鍚嶇О</param>
    /// <param name="roleBKName">瑙掕壊鑳屾櫙鍚嶇О</param>
    /// <param name="roleModelName">妯″瀷鍚嶇О</param>
    public void GetRoleResourceNames(int roleID, bool roleSex, out string roleIconName, out string createRolePanelBKName, out string roleBKName, out string roleModelName)
    {
        // 鎬у埆鍓嶇紑
        string sexPrefix = roleSex ? "Man" : "Women";
        string sexPrefixForBK = roleSex ? "Man" : "Woman"; // createRolePanelBKName 浣跨敤 Woman 鑰屼笉鏄?Women

        // roleIconName: Role_Women_Icon_03
        roleIconName = $"Role_{sexPrefix}_Icon_{roleID:D2}";

        // createRolePanelBKName: Role_Woman_BK_02
        createRolePanelBKName = $"Role_{sexPrefixForBK}_BK_{roleID:D2}";

        // roleBKName: RoleBK_Man_01
        roleBKName = $"RoleBK_{sexPrefix}_{roleID:D2}";

        // roleModelName: RoleModel_Women_01
        roleModelName = $"RoleModel_{sexPrefix}_{roleID:D2}";
    }
}

