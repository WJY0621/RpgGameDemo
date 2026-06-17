public abstract class BossState
{
    protected readonly BossController controller;

    protected BossState(BossController controller)
    {
        this.controller = controller;
    }

    public abstract BossStateType StateType { get; }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Tick(float deltaTime) { }
}
