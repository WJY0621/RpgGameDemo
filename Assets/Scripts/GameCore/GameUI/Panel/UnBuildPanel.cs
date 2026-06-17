public class UnBuildPanel : BasePanel
{
    private bool initialized;

    protected override void Awake()
    {
        base.Awake();
        Init();
    }

    public override void Init()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
    }
}
