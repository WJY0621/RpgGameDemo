using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoopPhase : ISequence
{
    public bool IsDone { get; private set; }
    public void Start() => IsDone = true; //立即完成
    public bool Update() => IsDone;
}
