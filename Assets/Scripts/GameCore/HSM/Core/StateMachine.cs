using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StateMachine
{
    //根状态
    public readonly State Root;
    public readonly TransitionSequencer Sequencer;
    bool started;

    public StateMachine(State root)
    {
        Root = root;
        //创建带有状态机引用的序列转换器
        Sequencer = new TransitionSequencer(this);
    }

    public void Start()
    {
        if (started) return;

        started = true;
        Root.Enter();
    }

    public void Tick(float deltaTime)
    {
        //先检查状态机是否已经启动
        if (!started) Start();
        //InternalTick(deltaTime);
        Sequencer.Tick(deltaTime);
    }

    internal void InternalTick(float deltaTime) => Root.Update(deltaTime);

    //真正的状态转
    public void ChangeState(State from, State to)
    {
        if (from == to || from == null || to == null) return;

        //先找到两状态的最近公共祖先状态
        State lca = TransitionSequencer.Lca(from, to);
        //再把需要转换的状态回退至 最近公共祖先状态
        for (State s = from; s != lca; s = s.Parent) s.Exit();

        //再从 祖先状态 到目标状态
        var stack = new Stack<State>();
        for (State s = to; s != lca; s = s.Parent) stack.Push(s);
        while (stack.Count > 0) stack.Pop().Enter();
    }
}
