using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[SceneController(sceneName = "GameScene", isGameScene = true)]
public class GameSceneController : SceneControllerBase
{
    public override async void OnSceneEnter()
    {
        // 进入游戏场景时，默认隐藏鼠标并锁定，切换到 Player 输入模式
        GameMgr.Cursor.SetCursorState(false);
        GameMgr.input.EnablePlayerActionMap();

        await GameMgr.UI.ShowPanel<PlayerMainPanel>();

        // 设置角色位置
        var player = GameMgr.Instance.Player;
        if (player == null)
        {
             player = FindObjectOfType<PlayerStateDriver>();
        }
        
        if (player != null && GameMgr.File.CurrentGameFile != null)
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            
            Vector3 pos = GameMgr.File.CurrentGameFile.GetPositionOnSceneLoaded(sceneName);
            Quaternion rot = GameMgr.File.CurrentGameFile.GetRotationOnSceneLoaded(sceneName);

            // 如果位置有效（非零），则应用位置
            if (pos != Vector3.zero)
            {
                var cc = player.GetComponent<CharacterController>();
                // CharacterController 会覆盖 transform.position，所以修改位置前必须先禁用它
                if (cc != null) 
                {
                    cc.enabled = false;
                }

                player.transform.position = pos;
                player.transform.rotation = rot;

                if (cc != null) 
                {
                    cc.enabled = true;
                }
            }
        }
    }

    protected override void Update()
    {
        base.Update();
        
        // 监听 Alt 键 (LeftAlt 或 RightAlt)
        if (UnityEngine.Input.GetKeyDown(KeyCode.LeftAlt) || UnityEngine.Input.GetKeyDown(KeyCode.RightAlt))
        {
            GameMgr.Cursor.ToggleCursorState();
            if (GameMgr.Cursor.IsCursorVisible())
            {
                GameMgr.input.EnableUIActionMap();
            }
            else
            {
                GameMgr.input.EnablePlayerActionMap();
            }
        }
    }
}
