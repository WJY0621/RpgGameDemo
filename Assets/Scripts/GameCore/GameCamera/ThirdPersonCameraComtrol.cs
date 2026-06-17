using Cinemachine;
using UnityEngine;

public class ThirdPersonCameraComtrol : MonoBehaviour
{
    private Vector2 look;

    [Header("Cinemachine")]
    public GameObject CameraTarget;
    public float topClamp = 90.0f;
    public float bottomClamp = -30.0f;

    private const float threshold = 0.01f;
    private float cinemachineTargetYaw;
    private float cinemachineTargetPitch;
    public float cameraDistance;
    private CinemachineVirtualCamera virtualCamera;
    private PlayerStateDriver boundPlayer;

    private void Start()
    {
        virtualCamera = GetComponent<CinemachineVirtualCamera>();
        TryResolveCameraTarget();

        if (CameraTarget != null)
        {
            cinemachineTargetYaw = CameraTarget.transform.rotation.eulerAngles.y;
        }
    }

    private void LateUpdate()
    {
        if (GameMgr.input == null || GameMgr.input.Data == null)
        {
            return;
        }

        if (!TryResolveCameraTarget())
        {
            return;
        }

        look = GameMgr.input.Data.Look;
        if (look.sqrMagnitude >= threshold)
        {
            cinemachineTargetYaw += look.x * 0.1f;
            cinemachineTargetPitch += -look.y * 0.1f;
        }

        cinemachineTargetYaw = ClampAngle(cinemachineTargetYaw, float.MinValue, float.MaxValue);
        cinemachineTargetPitch = ClampAngle(cinemachineTargetPitch, bottomClamp, topClamp);

        CameraTarget.transform.rotation = Quaternion.Euler(cinemachineTargetPitch, cinemachineTargetYaw, 0.0f);
    }

    private bool TryResolveCameraTarget()
    {
        PlayerStateDriver currentPlayer = GameMgr.Instance != null ? GameMgr.Instance.Player : null;
        if (CameraTarget != null && boundPlayer == currentPlayer)
        {
            return true;
        }

        if (currentPlayer == null)
        {
            return false;
        }

        boundPlayer = currentPlayer;
        Transform playerTransform = currentPlayer.transform;
        Transform lookAt = playerTransform.Find("LookAt");
        if (lookAt != null)
        {
            CameraTarget = lookAt.gameObject;
            return true;
        }

        CameraTarget = playerTransform.gameObject;
        return true;
    }

    public void RebindToCurrentPlayer()
    {
        CameraTarget = null;
        boundPlayer = null;
        TryResolveCameraTarget();
    }

    private static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360f) angle += 360f;
        if (angle > 360f) angle -= 360f;
        return Mathf.Clamp(angle, min, max);
    }
}
