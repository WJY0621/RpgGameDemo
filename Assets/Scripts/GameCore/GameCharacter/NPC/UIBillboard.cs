using UnityEngine;

/// <summary>
/// 让World Space Canvas始终面向摄像机（只绕Y轴旋转，左右翻转）
/// </summary>
public class UIBillboard : MonoBehaviour
{
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (mainCamera == null) return;

        // 只让UI绕Y轴旋转，始终面向摄像机（左右转，不上下翻转）
        Vector3 lookDir = mainCamera.transform.position - transform.position;
        lookDir.y = 0; // 忽略Y方向

        if (lookDir.sqrMagnitude > 0.001f)
        {
            // 旋转到面向摄像机后，再加180度让文字镜像
            transform.rotation = Quaternion.LookRotation(lookDir) * Quaternion.Euler(0, 180, 0);
        }
    }
}
