using Cysharp.Threading.Tasks;
using UnityEngine;

[SceneController(sceneName = "GameScene", isGameScene = true)]
public class GameSceneController : SceneControllerBase
{
    private const string GameBGMName = "BGM_Game";
    private const string GameAmbientBGMName = "BGM_Fenwei";

    public override async UniTask OnSceneEnterAsync()
    {
        PlayGameBGM();

        GameMgr.Cursor.SetCursorState(false);
        GameMgr.input.EnablePlayerActionMap();

        await GameMgr.UI.ShowPanel<PlayerMainPanel>();

        PlayerStateDriver player = await EnsureSinglePlayerAsync();
        await ApplySavedTransformAsync(player);

        await UniTask.Yield();
        GameMgr.cameraMgr.UpdateCamera();
    }

    private void PlayGameBGM()
    {
        GameMgr.Audio?.PlayBGM(GameBGMName);
        GameMgr.Audio?.PlayAmbientBGM(GameAmbientBGMName);
    }

    protected override void Update()
    {
        base.Update();

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

    private async UniTask<PlayerStateDriver> EnsureSinglePlayerAsync()
    {
        PlayerStateDriver[] players = FindObjectsOfType<PlayerStateDriver>();
        PlayerStateDriver player = null;

        if (players.Length > 0)
        {
            player = players[0];
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i] != player)
                {
                    Destroy(GetPlayerRigRoot(players[i]).gameObject);
                }
            }
        }

        if (player == null)
        {
            GameObject playerPrefab = await GameMgr.AssetLoader.LoadPrefab("Player");
            if (playerPrefab == null)
            {
                Debug.LogError("[GameSceneController] Failed to load Player prefab.");
                return null;
            }

            GameObject playerObject = Instantiate(playerPrefab);
            playerObject.name = "Player";
            player = playerObject.GetComponentInChildren<PlayerStateDriver>(true);
            if (player == null)
            {
                Debug.LogError("[GameSceneController] PlayerStateDriver was not found in instantiated Player prefab.");
                Destroy(playerObject);
                return null;
            }
        }

        GameMgr.Instance.Player = player;
        return player;
    }

    private async UniTask ApplySavedTransformAsync(PlayerStateDriver player)
    {
        if (player == null)
        {
            return;
        }

        GameFile currentGameFile = GameMgr.File.CurrentGameFile;
        if (currentGameFile == null)
        {
            return;
        }

        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Vector3 pos;
        Quaternion rot;

        bool hasSavedLocation = currentGameFile.TryGetSceneLocation(currentSceneName, out pos, out rot);
        if (!hasSavedLocation && !string.IsNullOrEmpty(currentGameFile.lastScene))
        {
            hasSavedLocation = currentGameFile.TryGetSceneLocation(currentGameFile.lastScene, out pos, out rot);
        }

        if (!hasSavedLocation && GameMgr.Instance.playerInitialData != null)
        {
            pos = GameMgr.Instance.playerInitialData.initialPosition;
            rot = GameMgr.Instance.playerInitialData.initialRotation;
            hasSavedLocation = true;
        }

        if (!hasSavedLocation)
        {
            return;
        }

        bool playerDriverEnabled = player.enabled;
        CharacterController cc = player.GetComponent<CharacterController>();
        Transform rigRoot = GetPlayerRigRoot(player);

        player.enabled = false;
        if (cc != null && cc.enabled)
        {
            cc.enabled = false;
        }

        ApplyRigTransform(rigRoot, player.transform, pos, rot);
        await UniTask.Yield();
        ApplyRigTransform(rigRoot, player.transform, pos, rot);
        await UniTask.DelayFrame(1);
        ApplyRigTransform(rigRoot, player.transform, pos, rot);

        if (cc != null)
        {
            cc.enabled = true;
        }

        await UniTask.DelayFrame(1);
        ApplyRigTransform(rigRoot, player.transform, pos, rot);

        player.enabled = playerDriverEnabled;
    }

    private void ApplyRigTransform(Transform rigRoot, Transform playerTransform, Vector3 targetPosition, Quaternion targetRotation)
    {
        if (rigRoot != playerTransform)
        {
            Vector3 delta = targetPosition - playerTransform.position;
            rigRoot.position += delta;
            playerTransform.rotation = targetRotation;
            return;
        }

        playerTransform.SetPositionAndRotation(targetPosition, targetRotation);
    }

    private Transform GetPlayerRigRoot(PlayerStateDriver player)
    {
        if (player == null)
        {
            return null;
        }

        return player.transform.parent != null ? player.transform.parent : player.transform;
    }
}
