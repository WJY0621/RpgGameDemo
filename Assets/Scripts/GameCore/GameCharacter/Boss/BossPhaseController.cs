using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BossPhaseController : MonoBehaviour
{
    [SerializeField] private int currentPhaseIndex;
    [SerializeField] private BossPhaseConfig currentPhase;

    private BossController controller;
    private List<BossPhaseConfig> phases = new List<BossPhaseConfig>();

    public int CurrentPhaseIndex => currentPhaseIndex;
    public BossPhaseConfig CurrentPhase => currentPhase;
    public event Action<BossPhaseConfig, int> OnPhaseChanged;

    public void Bind(BossController bossController)
    {
        controller = bossController;
    }

    public void Initialize(BossConfigSO config)
    {
        phases = config != null && config.phases != null
            ? config.phases
            : new List<BossPhaseConfig>();

        currentPhaseIndex = 0;
        currentPhase = phases.Count > 0 ? phases[0] : null;
    }

    public void Tick()
    {
        if (controller == null || controller.Health == null || controller.Health.IsDead)
        {
            return;
        }

        if (phases == null || phases.Count <= 1)
        {
            return;
        }

        int nextIndex = currentPhaseIndex + 1;
        if (nextIndex >= phases.Count)
        {
            return;
        }

        BossPhaseConfig nextPhase = phases[nextIndex];
        if (nextPhase == null)
        {
            return;
        }

        if (controller.Health.NormalizedHP <= nextPhase.enterAtHpPercent)
        {
            EnterPhase(nextIndex);
        }
    }

    public void EnterPhase(int index)
    {
        if (phases == null || index < 0 || index >= phases.Count)
        {
            return;
        }

        currentPhaseIndex = index;
        currentPhase = phases[currentPhaseIndex];
        OnPhaseChanged?.Invoke(currentPhase, currentPhaseIndex);
        controller?.RequestPhaseChange(currentPhase);
    }
}
