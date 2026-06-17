using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerAudioController : MonoBehaviour
{
    [Header("Groups")]
    [SerializeField] private string locomotionGroup = "Player";

    [Header("Footsteps")]
    [SerializeField] private string groundFootstepSound = "Walk_Ground";
    [SerializeField] private float footstepVolume = 1f;
    [SerializeField] private float minFootstepInterval = 0.05f;

    [Header("Landing")]
    [SerializeField] private string landingSound = "Land";
    [SerializeField] private float landingVolume = 1f;

    private PlayerStateDriver driver;
    private CharacterController characterController;
    private float lastFootstepTime = -999f;

    private void Awake()
    {
        driver = GetComponent<PlayerStateDriver>();
        characterController = GetComponent<CharacterController>();
    }

    // Animation Event
    public void PlayFootstep()
    {
        if (!CanPlayFootstep())
        {
            return;
        }

        lastFootstepTime = Time.time;
        GameMgr.Audio.PlayPooledEffectAt(locomotionGroup, groundFootstepSound, transform.position, footstepVolume);
    }

    public void PlayLanding()
    {
        if (GameMgr.Audio == null)
        {
            return;
        }

        GameMgr.Audio.PlayPooledEffectAt(locomotionGroup, landingSound, transform.position, landingVolume);
    }

    private bool CanPlayFootstep()
    {
        if (driver == null || GameMgr.Audio == null)
        {
            return false;
        }

        if (Time.time - lastFootstepTime < minFootstepInterval)
        {
            return false;
        }

        PlayerContext ctx = driver.ctx;
        bool grounded = ctx.grounded || (characterController != null && characterController.isGrounded);
        if (!grounded)
        {
            return false;
        }

        return ctx.isWalkingState || ctx.isRunningState;
    }
}
