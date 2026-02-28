using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "RoleList",menuName = "Data/GameCharacter/RoleList")]
public class RoleListData : ScriptableObject
{
    public List<RoleData> roleList;

    /// <summary>
    /// 根据 roleID 和 roleSex 获取角色相关的资源名称
    /// </summary>
    /// <param name="roleID">角色ID</param>
    /// <param name="roleSex">角色性别（true=男，false=女）</param>
    /// <param name="roleIconName">图标名称</param>
    /// <param name="createRolePanelBKName">创建角色面板背景名称</param>
    /// <param name="roleBKName">角色背景名称</param>
    /// <param name="roleModelName">模型名称</param>
    public void GetRoleResourceNames(int roleID, bool roleSex, out string roleIconName, out string createRolePanelBKName, out string roleBKName, out string roleModelName)
    {
        // 性别前缀
        string sexPrefix = roleSex ? "Man" : "Women";
        string sexPrefixForBK = roleSex ? "Man" : "Woman"; // createRolePanelBKName 使用 Woman 而不是 Women

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
