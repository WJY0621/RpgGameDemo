using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class State
{
    public readonly StateMachine Machine;
    public readonly State Parent;
    public State ActiveChild;
    readonly List<IActivity> activities = new List<IActivity>(); //状态活动列表
    public IReadOnlyList<IActivity> Activities => activities;

    public State(StateMachine machine, State parent = null)
    {
        Machine = machine;
        Parent = parent;
    }

    public void Add(IActivity a)
    {
        if(a != null)
            activities.Add(a);
    }

    protected virtual State GetInitialState() => null; //如果返回null 则为最终的状态节点
    protected virtual State GetTransition() => null; //切换目标状态

    protected virtual void OnEnter() { }
    protected virtual void OnExit() { }
    protected virtual void OnUpdate(float deltaTime) { }

    internal void Enter()
    {
        if (Parent != null) Parent.ActiveChild = this;
        OnEnter();
        State init = GetInitialState();
        if (init != null) init.Enter();
    }

    internal void Exit()
    {
        if (ActiveChild != null) ActiveChild.Exit();
        ActiveChild = null;
        OnExit();
    }

    internal void Update(float deltaTime)
    {
        State t = GetTransition();
        if (t != null)
        {
            Machine.Sequencer.RequestTransition(this, t);
            return;
        }

        if (ActiveChild != null) ActiveChild.Update(deltaTime);
        OnUpdate(deltaTime);
    }

    //不论在状态的什么层级 都能获得当前活跃的最末端子状态
    public State Leaf()
    {
        State s = this;
        while (s.ActiveChild != null) s = s.ActiveChild;
        return s;
    }

    //获取根状态
    public IEnumerable<State> PathToRoot()
    {
        for (State s = this; s != null; s = s.Parent) yield return s;
    }
}
