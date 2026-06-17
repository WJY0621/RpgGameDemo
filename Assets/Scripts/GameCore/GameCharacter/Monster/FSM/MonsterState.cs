public abstract class MonsterState
{
    protected readonly MonsterController controller;

    protected MonsterState(MonsterController controller)
    {
        this.controller = controller;
    }

    public abstract MonsterStateType StateType { get; }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Tick(float deltaTime) { }
}
