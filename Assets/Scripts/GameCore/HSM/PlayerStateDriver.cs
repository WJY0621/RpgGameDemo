using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerStateDriver : MonoBehaviour
{
    public PlayerContext ctx = new PlayerContext();
    public Transform groundCheck;
    public float groundRadius = 0.2f;
    public LayerMask groundMask;
    public bool drawGizmos = true;
    string lastPath;
    float targetRot = 0.0f;
    public float rotSmoothTime = 0.05f;
    float rotVelocity;
    Vector3 moveDir;

    //相机参数
    private GameObject mainCamera;

    CharacterController cC;
    //Rigidbody rb;
    StateMachine machine;
    State root;
    public CinemachineVirtualCamera virtualCamera;

    void Start()
    {
        InitializePosition();
        cC = gameObject.GetComponent<CharacterController>();
        ctx.cc = cC;
        ctx.anim = GetComponentInChildren<Animator>();
        mainCamera = GameObject.FindGameObjectWithTag("MainCamera");

        root = new PlayerRoot(null, ctx);
        var builder = new StateMachineBuilder(root);
        machine = builder.Build();

        if (groundCheck == null)
        {
            var t = new GameObject("groundCheck").transform;
            t.SetParent(transform, false);
            var y = 0f;
            t.localPosition = new Vector3(0, y, 0);
            groundCheck = t;
        }
    }

    void Update()
    {
        ctx.jumpPressed = GameMgr.input.Data.JumpInput;
        ctx.move.x = GameMgr.input.Data.DirKeyAxis.x;
        ctx.move.z = GameMgr.input.Data.DirKeyAxis.y;
        ctx.look = GameMgr.input.Data.Look;
        ctx.isLeftPressed = GameMgr.input.Data.Fire;
        ctx.isRightPressed = GameMgr.input.Data.RightPress;

        ctx.grounded = Physics.CheckSphere(groundCheck.position, groundRadius, groundMask);

        //初始化状态机
        machine.Tick(Time.deltaTime);

        var path = StatePath(machine.Root.Leaf());

        if (path != lastPath)
        {
            Debug.Log("State:" + path);
            lastPath = path;
        }


        HandleMoveMent();
        if (ctx.move.x != 0 || ctx.move.z != 0 || ctx.isLeftPressed)
            HandleRotation();
        Aim();
    }

    private void InitializePosition()
    {
        GameMgr.Instance.Player = this;
        // 已经在 GameSceneController 中处理了位置初始化，这里只需要注册 Player 实例即可
        // 避免重复设置位置可能导致的冲突（特别是如果 CC 已经启用）
    }
    private void HandleMoveMent()
    {
        Vector3 cameraForward = mainCamera.transform.forward;
        Vector3 cameraRight = mainCamera.transform.right;

        // 忽略相机的Y轴影响，确保移动在水平面上
        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();
        moveDir = (cameraForward * ctx.move.z + cameraRight * ctx.move.x).normalized;
        Vector3 movement = moveDir * ctx.moveSpeed * Time.deltaTime;
        cC.Move(movement);
        if (ctx.ySpeed != 0)
        {
            Vector3 verticalMovement = new Vector3(0, ctx.ySpeed, 0) * Time.deltaTime;
            cC.Move(verticalMovement);
        }
    }

    private void HandleRotation()
    {
        //获取输入方向的方向向量
        Vector3 inputDir = new Vector3(ctx.move.x, 0.0f, ctx.move.z).normalized;
        //移动方向
        float rotation;
        if (ctx.isLeftPressed || ctx.isRightPressed)
        {
            targetRot = mainCamera.transform.eulerAngles.y;
            rotation = targetRot;
        }
        else
        {
            targetRot = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + mainCamera.transform.eulerAngles.y;
            //旋转角度
            rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRot, ref rotVelocity, rotSmoothTime);
        }
        //旋转自身
        transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
    }

    private void Aim()
    {
        if (ctx.isRightPressed)
        {
            virtualCamera.Priority = ctx.priority;
        }
        else
        {
            virtualCamera.Priority = ctx.priority + 2;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos || groundCheck == null) return;

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
    }

    //获取状态节点的路径字符串表示
    static string StatePath(State s)
    {
        return string.Join(">", s.PathToRoot().AsEnumerable().Reverse().Select(n => n.GetType().Name));
    }
}
