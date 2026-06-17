using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 挂载在特效预制体根节点上（或由 VFXMgr 自动添加）。
/// 负责播放、跟随目标、播完后自动归还对象池。
/// 不需要手动调用，由 VFXMgr 统一管理。
/// </summary>
[DisallowMultipleComponent]
public sealed class PooledVFXController : MonoBehaviour
{
    /// <summary>超过此时间强制回收，防止特效泄漏。</summary>
    private const float AutoReturnTimeout = 30f;

    private ParticleSystem[] particles;
    private Transform followTarget;
    private Vector3 followOffset;
    private Quaternion followRotation = Quaternion.identity;
    private Action<PooledVFXController> onReturn;
    private int playVersion;

    public bool IsPlaying { get; private set; }

    /// <summary>该实例对应的对象池 Key。</summary>
    public string PoolKey { get; private set; }

    private void Awake()
    {
        particles = GetComponentsInChildren<ParticleSystem>(true);
    }

    // ── 内部接口（仅供 VFXMgr 调用）──────────────────────────────────

    internal void Setup(string key, Action<PooledVFXController> returnCallback)
    {
        PoolKey  = key;
        onReturn = returnCallback;
    }

    internal void Play(Vector3 position, Quaternion rotation, Transform follow, bool loop)
    {
        Play(position, rotation, follow, Vector3.zero, Quaternion.identity, loop);
    }

    internal void Play(Vector3 position, Quaternion rotation, Transform follow, Vector3 offset, Quaternion localRotation, bool loop)
    {
        int version = ++playVersion;

        IsPlaying    = true;
        followTarget = follow;
        followOffset = offset;
        followRotation = localRotation == default ? Quaternion.identity : localRotation;

        transform.position = position;
        transform.rotation = rotation;
        gameObject.SetActive(true);

        for (int i = 0; i < particles.Length; i++)
        {
            var main = particles[i].main;
            main.loop = loop;
            particles[i].Clear();
            particles[i].Play();
        }

        if (!loop)
        {
            WaitAndReturn(version).Forget();
        }
    }

    /// <summary>停止循环特效，等待当前粒子消散后回池。</summary>
    internal void Stop()
    {
        if (!IsPlaying)
        {
            return;
        }

        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        WaitAndReturn(playVersion).Forget();
    }

    // ── 私有 ──────────────────────────────────────────────────────────

    private void Update()
    {
        if (!IsPlaying || followTarget == null)
        {
            return;
        }

        transform.position = followTarget.TransformPoint(followOffset);
        transform.rotation = followTarget.rotation * followRotation;
    }

    private async UniTaskVoid WaitAndReturn(int version)
    {
        float elapsed = 0f;

        while (true)
        {
            await UniTask.Yield();

            if (this == null)
            {
                return;
            }

            if (version != playVersion)
            {
                return;
            }

            elapsed += Time.unscaledDeltaTime;

            if (elapsed >= AutoReturnTimeout)
            {
                Debug.LogWarning($"[VFXMgr] Effect '{PoolKey}' exceeded {AutoReturnTimeout}s, force returning to pool.");
                ReturnToPool();
                return;
            }

            if (!IsAnyParticleAlive())
            {
                ReturnToPool();
                return;
            }
        }
    }

    private bool IsAnyParticleAlive()
    {
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] != null && particles[i].IsAlive(true))
            {
                return true;
            }
        }

        return false;
    }

    private void ReturnToPool()
    {
        IsPlaying    = false;
        followTarget = null;
        followOffset = Vector3.zero;
        followRotation = Quaternion.identity;
        gameObject.SetActive(false);
        onReturn?.Invoke(this);
    }
}
