using System.Collections.Generic;

public class MonsterStateMachine
{
    private readonly Dictionary<MonsterStateType, MonsterState> states = new Dictionary<MonsterStateType, MonsterState>();

    public MonsterState CurrentState { get; private set; }
    public MonsterStateType CurrentStateType => CurrentState != null ? CurrentState.StateType : MonsterStateType.Idle;

    public void RegisterState(MonsterState state)
    {
        if (state == null)
        {
            return;
        }

        states[state.StateType] = state;
    }

    public void ChangeState(MonsterStateType stateType)
    {
        if (!states.TryGetValue(stateType, out MonsterState nextState))
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
