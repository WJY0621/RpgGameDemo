using System.Collections.Generic;

public class BossStateMachine
{
    private readonly Dictionary<BossStateType, BossState> states = new Dictionary<BossStateType, BossState>();

    public BossState CurrentState { get; private set; }
    public BossStateType CurrentStateType => CurrentState != null ? CurrentState.StateType : BossStateType.Inactive;

    public void RegisterState(BossState state)
    {
        if (state == null)
        {
            return;
        }

        states[state.StateType] = state;
    }

    public void ChangeState(BossStateType stateType)
    {
        if (!states.TryGetValue(stateType, out BossState nextState))
        {
            return;
        }

        if (CurrentState == nextState)
        {
            return;
        }

        CurrentState?.Exit();
        CurrentState = nextState;
        CurrentState.Enter();
    }

    public void Tick(float deltaTime)
    {
        CurrentState?.Tick(deltaTime);
    }
}
