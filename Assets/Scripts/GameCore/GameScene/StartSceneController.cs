using Cysharp.Threading.Tasks;
using UnityEngine;
[SceneController(sceneName = "GameStartScene", isGameScene = false)]
public class StartSceneController : SceneControllerBase
{
    private const string StartBGMName = "BGM_Start";

    public override async UniTask OnSceneEnterAsync()
    {
        PlayStartBGM();

        // 确保在开始菜单场景中鼠标是可见的
        GameMgr.Cursor.SetCursorState(true);
        GameMgr.input.EnableUIActionMap();

        GameStartPanel panel = await GameMgr.UI.ShowPanel<GameStartPanel>();
        if (panel != null)
        {
            await panel.WaitUntilFullyShownAsync();
        }
    }

    private void PlayStartBGM()
    {
        GameMgr.Audio?.PlayBGM(StartBGMName);
    }
}
