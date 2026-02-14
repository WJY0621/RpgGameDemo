using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class StateMachineBuilder
{
    readonly State root;

    public StateMachineBuilder(State root)
    {
        this.root = root;
    }

    //通过根节点构建新状态机 递归遍历所有节点并建立关联
    public StateMachine Build()
    {
        var m = new StateMachine(root);
        Wire(root, m, new HashSet<State>());
        return m; //返回完整状态机的引用
    }

    //把状态机注入每个状态 并遍历整个层级
    void Wire(State s, StateMachine m, HashSet<State> visited)
    {
        if (s == null) return;
        if (!visited.Add(s)) return; //状态已经访问过了

        var flags = BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
        //获取基类状态中的状态机字段引用
        var machineField = typeof(State).GetField("Machine", flags);
        if (machineField != null) machineField.SetValue(s, m);

        foreach(var fld in s.GetType().GetFields(flags))
        {
            if (!typeof(State).IsAssignableFrom(fld.FieldType)) continue;//只处理State类型及其子类的字段
            if (fld.Name == "Parent") continue; //跳过Parent字段以避免无限递归

            var child = (State)fld.GetValue(s);
            if (child == null) continue; //跳过空子状态
            if (!ReferenceEquals(child.Parent, s)) continue; //跳过非直接子状态

            Wire(child, m, visited); //递归处理子状态 并建立关联
        }
    }
}
