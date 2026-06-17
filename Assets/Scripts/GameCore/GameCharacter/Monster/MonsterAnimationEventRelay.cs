using UnityEngine;

public class MonsterAnimationEventRelay : MonoBehaviour
{
    private MonsterAttackController attackController;

    public void Bind(MonsterAttackController controller)
    {
        attackController = controller;
    }

    public void BeginAttackWindow()
    {
        attackController?.BeginAttackWindow();
    }

    public void ApplyAttackDamage()
    {
        attackController?.ApplyAttackDamage();
    }

    public void EndAttackWindow()
    {
        attackController?.EndAttackWindow();
    }
}
