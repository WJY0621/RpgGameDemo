using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class TransitionSequencer
{
    public readonly StateMachine Machine;

    ISequence sequencer; //当前活动的状态转换序列
    Action nextPhase;  //序列转换时执行
    (State from, State to)? pending; //状态转换请求
    State lastFrom, lastTo; //上一次转换请求
    CancellationTokenSource cts;
    //控制执行模式
    bool UseSequential = true;

    public TransitionSequencer(StateMachine machine)
    {
        Machine = machine;
    }

    //请求状态转换
    public void RequestTransition(State from, State to)
    {
        //Machine.ChangeState(form, to);
        if (to == null || from == to) return;
        if (sequencer != null)
        {
            pending = (from, to);
            return;
        }
        BeginTransition(from, to);
    }

    //得到构建序列器 在该阶段要执行的具体操作列表
    static List<PhaseStep> GatherPhaseSteps(List<State> chain, bool deactivate)
    {
        var steps = new List<PhaseStep>();
        for (int i = 0; i < chain.Count; i++)//遍历状态迁移链中的每个状态
        {
            var st = chain[i];
            //获取每个状态的关联活动项
            var acts = chain[i].Activities;
            for (int j = 0; j < acts.Count; j++) //遍历所有活动项
            {
                var a = acts[j];
                bool include = deactivate ? (a.Mode == ActivityMode.Active)
                        : (a.Mode == ActivityMode.Inactive);
                if (!include) continue;

                Debug.Log($"[Phase {(deactivate ? "Exit" : "Enter")}] state={st.GetType().Name}, activity={a.GetType().Name}, mode={a.Mode}");

                steps.Add(ct => deactivate ? a.DeactivateAsync(ct) : a.ActivateAsync(ct));
            }
        }
        return steps;
    }

    //从当前状态回退到最低共同祖先需要退出的状态路径
    static List<State> StatesToExit(State from, State lca)
    {
        var list = new List<State>();
        for (var s = from; s != null && s != lca; s = s.Parent) list.Add(s);
        return list;
    }
    //从目标状态回退到最低共同祖先需要激活的状态路径
    static List<State> StatesToEnter(State to, State lca)
    {
        var stack = new Stack<State>();
        for (var s = to; s != lca; s = s.Parent) stack.Push(s);
        return new List<State>(stack);
    }

    

    void BeginTransition(State from, State to)//1.停用久分支 2.切换状态 3.激活新分支
    {
        cts?.Cancel();
        cts = new CancellationTokenSource();
        
        //获取最近共同祖先
        var lca = Lca(from, to);
        //获取所有待退出 待进入状态
        var exitChain = StatesToExit(from, lca);
        var enterChain = StatesToEnter(to, lca);

        //1.状态停用 先用空操作占位实现
        var exitSteps = GatherPhaseSteps(exitChain, deactivate: true);
        //sequencer = new NoopPhase();
        sequencer = UseSequential ?
            new SequentialPhase(exitSteps, cts.Token) :
            new ParallelPhase(exitSteps, cts.Token);
        sequencer.Start();

        //调用序列转换的回调函数
        nextPhase = () =>
        {
            //转换状态
            //2.通知通知状态机进行状态切换
            Machine.ChangeState(from, to);
            //3.激活目标状态分支
            var enterSteps = GatherPhaseSteps(enterChain, deactivate: false);
            //sequencer = new NoopPhase();
            sequencer = UseSequential ?
                new SequentialPhase(exitSteps, cts.Token) :
                new ParallelPhase(exitSteps, cts.Token);
            sequencer.Start();
        };
    }

    void EndTransition()
    {
        //清除当前序列控制器
        sequencer = null;

        //检查执行期间是否有新的过渡请求达到
        if (pending.HasValue)
        {
            //如果有 就立即捕获
            (State from, State to) p = pending.Value;
            //清除请求
            pending = null;
            BeginTransition(p.from, p.to);
        }
    }
    
    //管理常规更新和过渡流程
    public void Tick(float deltaTime)
    {
        //先判断当前是否存在活跃的过渡进程
        if (sequencer != null)
        {
            if (sequencer.Update()) //判断过渡进程是否完成
            {
                if (nextPhase != null)
                {
                    var n = nextPhase;
                    nextPhase = null;
                    n();
                }
                else
                {
                    EndTransition();
                }
            }
            return; //存在活跃过渡进程 跳过过渡期的常规状态更新
        }
        //没有活跃的过渡进程 则进行常规状态更新
        Machine.InternalTick(deltaTime);
    }

    //得到两个状态的最近公共祖先节点
    public static State Lca(State a, State b)
    {
        //两个状态的所有祖先节点
        var ap = new HashSet<State>();
        //先得到a的所有祖先节点 将它们加入哈希集合中
        for (var s = a; s != null; s = s.Parent) ap.Add(s);
        //再遍历b的所有祖先节点 并判断哈希表中是否存在 若存在 则返回
        for (var s = b; s != null; s = s.Parent)
            if (ap.Contains(s)) return s;

        //如果不存在公共祖先节点
        return null;
    }
}
