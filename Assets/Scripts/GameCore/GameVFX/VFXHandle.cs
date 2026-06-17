/// <summary>
/// 循环特效的控制句柄。由 VFXMgr.PlayLooping() 返回。
/// 持有此句柄的对象负责在合适时机调用 Stop() 停止并回收特效。
/// </summary>
public sealed class VFXHandle
{
    private PooledVFXController ctrl;
    private bool stopRequested;

    /// <summary>特效是否仍在播放中。</summary>
    public bool IsValid => ctrl != null && ctrl.IsPlaying;

    /// <summary>停止特效并释放句柄引用。</summary>
    public void Stop()
    {
        stopRequested = true;
        ctrl?.Stop();
        ctrl = null;
    }

    internal void Bind(PooledVFXController controller)
    {
        if (stopRequested)
        {
            controller?.Stop();
            return;
        }

        ctrl = controller;
    }
}
