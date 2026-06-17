using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerRootMotionRelay : MonoBehaviour
{
    private Animator animator;
    private PlayerStateDriver driver;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void Bind(PlayerStateDriver targetDriver)
    {
        driver = targetDriver;
    }

    private void OnAnimatorMove()
    {
        if (driver == null || animator == null)
        {
            return;
        }

        driver.ReceiveRootMotion(animator.deltaPosition);
    }
}
