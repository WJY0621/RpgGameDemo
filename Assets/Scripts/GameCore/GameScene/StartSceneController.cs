using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
[SceneController(sceneName = "GameStartScene", isGameScene = false)]
public class StartSceneController : SceneControllerBase
{
    public override async void OnSceneEnter()
    {
        await GameMgr.UI.ShowPanel<GameStartPanel>();
    }
}
