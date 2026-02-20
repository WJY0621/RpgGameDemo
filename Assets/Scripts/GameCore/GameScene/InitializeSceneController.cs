using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[SceneController(sceneName = "GameInitializeScene", isGameScene = false)]
public class InitializeSceneController : SceneControllerBase
{
    protected override async void Update()
    {
        base.Update();
        if (GameMgr.Instance.completeGameInitialze && Input.anyKeyDown)
        {
            var panel = await GameMgr.UI.GetPanel<LogoPanel>();
            panel.HideLogo();
            GameMgr.UI.HidePanel<LogoPanel>(() =>
            {
                GameMgr.Instance.readyToActiveLoadedScene = true;
            });
        }
    }
}
