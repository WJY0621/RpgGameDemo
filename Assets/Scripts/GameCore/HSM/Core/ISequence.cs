using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public interface ISequence
{
    bool IsDone { get; }
    //初始化序列
    void Start();
    bool Update();
}

//封装激活停用任务
public delegate Task PhaseStep(CancellationToken ct);
