using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("移动参数")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float sprintSpeed = 8f;
    public float groundRotationSpeed = 20f;  // 地面转向速度
    public float airRotationSpeed = 8f;      // 空中转向速度
    public float acceleration = 10f;
    public float deceleration = 15f;

    [Header("跳跃参数")]
    public float jumpHeight = 1.5f;
    public float gravity = -15f;
    public float airControl = 0.5f;
    public int maxJumps = 2;

    [Header("鼠标控制")]
    public float rightClickZoomAmount = 0.5f;
    public float zoomTransitionSpeed = 2f;
    //public float faceScreenSmoothness = 5f; // 面向屏幕的平滑度

    [Header("组件引用")]
    public CharacterController controller;
    //public Animator animator;
    public Camera playerCamera;
    public ThirdPersonCameraComtrol cinemachineController; // 引用之前的相机控制器

    // 输入状态
    private Vector2 moveInput;
    private bool isRunning;
    private bool isSprinting;
    private bool jumpPressed;

    // 运动状态
    private Vector3 velocity;
    private Vector3 currentMovement;
    private float currentSpeed;
    private int jumpsRemaining;
    private bool isGrounded;

    // 状态机
    private CharacterState currentState;

    // 空中转向相关
    private Vector3 airMovementDirection;    // 空中移动方向
    private float currentRotationSpeed;      // 当前旋转速度

    // 鼠标控制相关
    private bool isMouseControllingRotation = false;
    private float originalCameraDistance;
    private float targetCameraDistance;
    private float currentCameraDistance;

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction runAction;
    private InputAction sprintAction;
    private InputAction jumpAction;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        //animator = GetComponent<Animator>();
        playerInput = GetComponent<PlayerInput>();

        // 获取输入Action
        moveAction = playerInput.actions["Move"];
        runAction = playerInput.actions["Run"];
        sprintAction = playerInput.actions["Sprint"];
        jumpAction = playerInput.actions["Jump"];

        // 获取相机控制器
        if (cinemachineController == null)
        {
            cinemachineController = FindObjectOfType<ThirdPersonCameraComtrol>();
        }

        // 保存原始相机距离
        if (cinemachineController != null)
        {
            originalCameraDistance = cinemachineController.cameraDistance;
            currentCameraDistance = originalCameraDistance;
            targetCameraDistance = originalCameraDistance;
        }

        jumpsRemaining = maxJumps;
        currentState = CharacterState.Grounded;
        currentRotationSpeed = groundRotationSpeed;
    }

    void Update()
    {
        HandleInput();
        UpdateStateMachine();
        HandleMouseControls();
        //HandleAnimation();
    }

    void HandleInput()
    {
        moveInput = moveAction.ReadValue<Vector2>();
        isRunning = runAction.ReadValue<float>() > 0.1f;
        //isSprinting = sprintAction.ReadValue<float>() > 0.1f;
        jumpPressed = jumpAction.triggered;
    }

    void UpdateStateMachine()
    {
        isGrounded = controller.isGrounded;

        // 更新旋转速度
        currentRotationSpeed = isGrounded ? groundRotationSpeed : airRotationSpeed;

        switch (currentState)
        {
            case CharacterState.Grounded:
                HandleGroundedState();
                break;
            case CharacterState.Jumping:
                HandleJumpingState();
                break;
            case CharacterState.Falling:
                HandleFallingState();
                break;
        }

        ApplyGravity();
        ApplyMovement();

        // 如果不是鼠标控制旋转，则处理角色旋转
        if (!isMouseControllingRotation)
        {
            HandleRotation();
        }
    }

    void HandleGroundedState()
    {
        // 重置跳跃次数
        if (isGrounded)
        {
            jumpsRemaining = maxJumps;
            velocity.y = -2f; // 轻微向下的力确保接地
        }

        // 计算目标速度
        float targetSpeed = walkSpeed;
        if (isSprinting) targetSpeed = sprintSpeed;
        else if (isRunning) targetSpeed = runSpeed;

        // 平滑速度变化
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed,
            (moveInput.magnitude > 0.1f ? acceleration : deceleration) * Time.deltaTime);

        // 计算移动方向（相对于相机）
        Vector3 moveDirection = CalculateCameraRelativeMovement();
        currentMovement = moveDirection * currentSpeed;

        // 处理跳跃
        if (jumpPressed && jumpsRemaining > 0)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpsRemaining--;
            currentState = CharacterState.Jumping;

            // 跳跃时保存当前移动方向
            if (moveInput.magnitude > 0.1f)
            {
                airMovementDirection = moveDirection;
            }
        }

        // 检查是否开始下落
        if (!isGrounded)
        {
            currentState = CharacterState.Falling;
        }
    }
    
    //控制空中跳跃的状态
    void HandleJumpingState()
    {
        // 空中控制
        if (moveInput.magnitude > 0.1f)
        {
            Vector3 moveDirection = CalculateCameraRelativeMovement();
            Vector3 airMovement = moveDirection * currentSpeed * airControl;
            currentMovement = Vector3.Lerp(currentMovement, airMovement, 5f * Time.deltaTime);

            // 保存空中移动方向用于旋转
            airMovementDirection = moveDirection;
        }

        // 检查是否开始下落
        if (velocity.y < 0)
        {
            currentState = CharacterState.Falling;
        }
    }
    
    //控制下落的状态
    void HandleFallingState()
    {
        // 空中控制
        if (moveInput.magnitude > 0.1f)
        {
            Vector3 moveDirection = CalculateCameraRelativeMovement();
            Vector3 airMovement = moveDirection * currentSpeed * airControl;
            currentMovement = Vector3.Lerp(currentMovement, airMovement, 5f * Time.deltaTime);

            // 保存空中移动方向用于旋转
            airMovementDirection = moveDirection;
        }

        // 检查是否落地
        if (isGrounded)
        {
            currentState = CharacterState.Grounded;
        }
    }

    void HandleRotation()
    {
        // 如果有移动输入，处理角色旋转
        if (moveInput.magnitude > 0.1f)
        {
            Vector3 targetDirection = CalculateCameraRelativeMovement();

            // 确保目标方向有效
            if (targetDirection.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);

                // 应用旋转
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    currentRotationSpeed * Time.deltaTime * 3
                );
            }
        }
        // 空中时保持移动方向
        else if (!isGrounded && airMovementDirection.magnitude > 0.1f)
        {
            // 可以添加轻微的自适应旋转，使角色更自然地面对移动方向
            Quaternion targetRotation = Quaternion.LookRotation(airMovementDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                airRotationSpeed * 0.3f * Time.deltaTime
            );
        }
    }

    void HandleMouseControls()
    {
        // 检查鼠标左键和右键状态
        bool leftMousePressed = Mouse.current.leftButton.isPressed;
        bool rightMousePressed = Mouse.current.rightButton.isPressed;
        
        // 处理鼠标左键点击
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            //Debug.Log("Left mouse clicked - 开始面向屏幕");
            isMouseControllingRotation = true;
        }
        
        // 处理鼠标右键点击
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            //Debug.Log("Right mouse clicked - 开始面向屏幕并拉近镜头");
            isMouseControllingRotation = true;
            targetCameraDistance = originalCameraDistance * rightClickZoomAmount;
        }
        
        // 处理鼠标左键释放
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            //Debug.Log("Left mouse released - 停止面向屏幕");
            isMouseControllingRotation = false;
        }
        
        // 处理鼠标右键释放
        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            //Debug.Log("Right mouse released - 停止面向屏幕并恢复镜头");
            isMouseControllingRotation = false;
            targetCameraDistance = originalCameraDistance;
        }
        
        // 如果按住鼠标按键，持续面向屏幕前方
        if (isMouseControllingRotation && (leftMousePressed || rightMousePressed))
        {
            FaceScreenDirectionContinuous();
        }
        
        // 平滑过渡相机距离
        if (cinemachineController != null)
        {
            currentCameraDistance = Mathf.Lerp(
                currentCameraDistance, 
                targetCameraDistance, 
                zoomTransitionSpeed * Time.deltaTime
            );
            cinemachineController.cameraDistance = currentCameraDistance;
        }
    }
    
    void FaceScreenDirectionContinuous()
    {
        // 使角色持续面朝屏幕前方（相机前方）
        if (playerCamera != null)
        {
            Vector3 screenForward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
            
            if (screenForward.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(screenForward);

                // 平滑旋转到屏幕方向
                transform.rotation = targetRotation;
                
            }
        }
    }

    Vector3 CalculateCameraRelativeMovement()
    {
        Vector3 cameraForward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
        Vector3 cameraRight = Vector3.ProjectOnPlane(playerCamera.transform.right, Vector3.up).normalized;
        return (cameraForward * moveInput.y + cameraRight * moveInput.x).normalized;
    }

    void ApplyGravity()
    {
        if (!isGrounded)
        {
            velocity.y += gravity * Time.deltaTime;
        }
    }

    void ApplyMovement()
    {
        Vector3 finalMovement = currentMovement + Vector3.up * velocity.y;
        controller.Move(finalMovement * Time.deltaTime);
    }

    // void HandleAnimation()
    // {
    //     if (animator == null) return;

    //     // 计算相对于角色朝向的移动速度
    //     Vector3 localMovement = transform.InverseTransformDirection(currentMovement);
    //     float forwardSpeed = localMovement.z;
    //     float strafeSpeed = localMovement.x;

    //     animator.SetFloat("ForwardSpeed", forwardSpeed, 0.1f, Time.deltaTime);
    //     animator.SetFloat("StrafeSpeed", strafeSpeed, 0.1f, Time.deltaTime);
    //     animator.SetFloat("MoveSpeed", currentMovement.magnitude, 0.1f, Time.deltaTime);
    //     animator.SetBool("IsGrounded", isGrounded);
    //     animator.SetFloat("VerticalVelocity", velocity.y);

    //     // 设置移动状态
    //     int moveState = 0; // 站立
    //     if (currentSpeed > walkSpeed * 1.1f) moveState = 1; // 奔跑
    //     if (currentSpeed > runSpeed * 1.1f) moveState = 2; // 冲刺
    //     animator.SetInteger("MoveState", moveState);

    //     // 添加空中状态
    //     animator.SetBool("IsInAir", !isGrounded);
    // }

    // 输入回调方法
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnRun(InputAction.CallbackContext context)
    {
        isRunning = context.ReadValue<float>() > 0.1f;
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        isSprinting = context.ReadValue<float>() > 0.1f;
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            jumpPressed = true;
        }
        else if (context.canceled)
        {
            jumpPressed = false;
        }
    }
}

public enum CharacterState
{
    Grounded,
    Jumping,
    Falling,
    Knockdown,
    Move,
    DiveRoll,
    DoubleJump,
    Knockback,
    Jump,
    Fall,
    Idle
}
