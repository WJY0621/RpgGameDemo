using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCameraComtrol : MonoBehaviour
{
    private Vector2 look;
    private GameObject mainCamera;
    [Header("Cinemachine")]
    [Tooltip("跟随的目标")]
    public GameObject CameraTarget;
    [Tooltip("上移的最大角度")]
    public float topClamp = 90.0f;
    [Tooltip("下移的最大角度")]
    public float bottomClamp = -30.0f;

    private const float threshold = 0.01f;
    private float cinemachineTargetYaw;
    private float cinemachineTargetPitch;
    //相机与目标的距离
    public float cameraDistance;
    private CinemachineVirtualCamera virtualCamera;

    void Start()
    {
        
        if (mainCamera == null)
        {
            mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        }
        cinemachineTargetYaw = CameraTarget.transform.rotation.eulerAngles.y;

        virtualCamera = this.GetComponent<CinemachineVirtualCamera>();
    }

    void LateUpdate()
    {
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

    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }
}
