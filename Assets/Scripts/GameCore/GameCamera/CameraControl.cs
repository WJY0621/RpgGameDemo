using Cinemachine;
using UnityEngine;

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
    private PlayerStateDriver boundPlayer;

    private void Awake()
    {
        InitializeCamera();
    }

    private void InitializeCamera()
    {
        ResolvePlayerTransform();

        if (virtualCamera == null)
        {
            virtualCamera = GetComponentInChildren<CinemachineVirtualCamera>();
        }

        if (cameraRoot == null)
        {
            cameraRoot = transform;
        }

        _cameraOffset = new Vector3(0, 1.5f, -cameraDistance);
        _currentHorizontalAngle = playerTransform != null ? playerTransform.eulerAngles.y : 0f;
        _currentVerticalAngle = 0f;

        UpdateCameraPosition();
    }

    public void OnLook()
    {
        if (GameMgr.input == null || GameMgr.input.Data == null)
        {
            return;
        }

        _lookInput = GameMgr.input.Data.Look;
    }

    public void OnMove()
    {
        if (GameMgr.input == null || GameMgr.input.Data == null)
        {
            return;
        }

        _moveInput = GameMgr.input.Data.DirKeyAxis;
        _isMoving = _moveInput.magnitude > movementThreshold;
    }

    private void Update()
    {
        ResolvePlayerTransform();
        OnLook();
        OnMove();
        HandleCameraRotation();
        UpdateCameraPosition();
    }

    private void HandleCameraRotation()
    {
        if (!_isMoving && _lookInput.magnitude > 0.01f)
        {
            _currentHorizontalAngle += _lookInput.x * rotationSpeed * 0.1f;
            _currentVerticalAngle = Mathf.Clamp(
                _currentVerticalAngle - _lookInput.y * rotationSpeed * 0.1f,
                minVerticalAngle,
                maxVerticalAngle);
        }
        else if (_isMoving && playerTransform != null)
        {
            float targetAngle = Mathf.Atan2(_moveInput.x, _moveInput.y) * Mathf.Rad2Deg;

            playerTransform.rotation = Quaternion.Slerp(
                playerTransform.rotation,
                Quaternion.Euler(0f, targetAngle, 0f),
                rotationSmoothTime * Time.deltaTime);

            _currentHorizontalAngle = playerTransform.eulerAngles.y;
        }
    }

    private void UpdateCameraPosition()
    {
        if (playerTransform == null || cameraRoot == null)
        {
            return;
        }

        Quaternion rotation = Quaternion.Euler(_currentVerticalAngle, _currentHorizontalAngle, 0f);
        Vector3 desiredPosition = playerTransform.position + rotation * _cameraOffset;

        if (obstacleLayers != 0)
        {
            Vector3 direction = (desiredPosition - playerTransform.position).normalized;
            if (Physics.Raycast(playerTransform.position, direction, out RaycastHit hit, cameraDistance, obstacleLayers))
            {
                float hitDistance = Vector3.Distance(playerTransform.position, hit.point);
                desiredPosition = playerTransform.position + direction * (hitDistance * 0.9f);
            }
        }

        cameraRoot.position = Vector3.SmoothDamp(cameraRoot.position, desiredPosition, ref _velocity, 0.1f);
        cameraRoot.LookAt(playerTransform.position + Vector3.up * 1.5f);
    }

    private void ResolvePlayerTransform()
    {
        PlayerStateDriver currentPlayer = GameMgr.Instance != null ? GameMgr.Instance.Player : null;
        if (currentPlayer == null)
        {
            return;
        }

        if (playerTransform == null || boundPlayer != currentPlayer)
        {
            boundPlayer = currentPlayer;
            playerTransform = currentPlayer.transform;
        }
    }

    public void RebindToCurrentPlayer()
    {
        boundPlayer = null;
        playerTransform = null;
        ResolvePlayerTransform();
    }
}
