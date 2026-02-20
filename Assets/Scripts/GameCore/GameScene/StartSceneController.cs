using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
[SceneController(sceneName = "GameStartScene", isGameScene = false)]
public class StartSceneController : SceneControllerBase
{
    public override async void OnSceneEnter()
    {
        // 确保在开始菜单场景中鼠标是可见的
        GameMgr.Cursor.SetCursorState(true);
        GameMgr.input.EnableUIActionMap();

        await GameMgr.UI.ShowPanel<GameStartPanel>();
    }
}
