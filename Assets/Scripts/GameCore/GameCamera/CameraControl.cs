using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraControl : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform cameraRoot;

    [Header("Camera Settings")]
    [SerializeField] private float rotationSpeed = 0.2f;
    [SerializeField] private float minVerticalAngle = -30f;
    [SerializeField] private float maxVerticalAngle = 60f;
    [SerializeField] private float cameraDistance = 5f;
    [SerializeField] private LayerMask obstacleLayers;

    [Header("Movement Detection")]
    [SerializeField] private float movementThreshold = 0.1f;
    [SerializeField] private float rotationSmoothTime = 0.1f;

    private Vector2 _lookInput;
    private Vector2 _moveInput;
    private float _currentVerticalAngle;
    private float _currentHorizontalAngle;
    private Vector3 _cameraOffset;
    private Vector3 _velocity = Vector3.zero;
    private bool _isMoving;

    private void Awake()
    {
        InitializeCamera();
    }

    private void InitializeCamera()
    {
        if (playerTransform == null)
            playerTransform = GameObject.FindGameObjectWithTag("Player").transform;

        if (virtualCamera == null)
            virtualCamera = GetComponentInChildren<CinemachineVirtualCamera>();

        if (cameraRoot == null)
            cameraRoot = transform;

        _cameraOffset = new Vector3(0, 1.5f, -cameraDistance);
        _currentHorizontalAngle = playerTransform ? playerTransform.eulerAngles.y : 0f;
        _currentVerticalAngle = 0f;

        UpdateCameraPosition();
    }

    public void OnLook()
    {
        _lookInput = GameMgr.input.Data.Look;
    }

    public void OnMove()
    {
        _moveInput = GameMgr.input.Data.dirKeyAxis;
        _isMoving = _moveInput.magnitude > movementThreshold;
    }

    private void Update()
    {
        OnLook();
        OnMove();
        HandleCameraRotation();
        UpdateCameraPosition();
    }

    private void HandleCameraRotation()
    {
        if (!_isMoving && _lookInput.magnitude > 0.01f)
        {
            // 角色静止时：相机围绕角色旋转
            _currentHorizontalAngle += _lookInput.x * rotationSpeed * 0.1f;
            _currentVerticalAngle = Mathf.Clamp(_currentVerticalAngle - _lookInput.y * rotationSpeed * 0.1f,
                                               minVerticalAngle, maxVerticalAngle);
        }
        else if (_isMoving && playerTransform != null)
        {
            // 角色移动时：相机跟随角色朝向，角色面朝屏幕前方
            float targetAngle = Mathf.Atan2(_moveInput.x, _moveInput.y) * Mathf.Rad2Deg;
            Vector3 currentRotation = playerTransform.rotation.eulerAngles;
            Vector3 targetRotation = new Vector3(0, targetAngle, 0);

            playerTransform.rotation = Quaternion.Slerp(playerTransform.rotation,
                                                       Quaternion.Euler(targetRotation),
                                                       rotationSmoothTime * Time.deltaTime);

            // 相机水平角度跟随角色
            _currentHorizontalAngle = playerTransform.eulerAngles.y;
        }
    }

    private void UpdateCameraPosition()
    {
        if (playerTransform == null) return;

        // 计算相机目标位置
        Quaternion rotation = Quaternion.Euler(_currentVerticalAngle, _currentHorizontalAngle, 0);
        Vector3 desiredPosition = playerTransform.position + rotation * _cameraOffset;

        // 检测障碍物
        if (obstacleLayers != 0)
        {
            Vector3 direction = (desiredPosition - playerTransform.position).normalized;
            float maxDistance = cameraDistance;

            if (Physics.Raycast(playerTransform.position, direction, out RaycastHit hit, maxDistance,
obstacleLayers))
            {
                float hitDistance = Vector3.Distance(playerTransform.position, hit.point);
                desiredPosition = playerTransform.position + direction * (hitDistance * 0.9f);
            }
        }

        // 平滑移动相机
        cameraRoot.position = Vector3.SmoothDamp(cameraRoot.position, desiredPosition, ref _velocity, 0.1f);

        // 相机始终朝向角色
        cameraRoot.LookAt(playerTransform.position + Vector3.up * 1.5f);
    }
}

