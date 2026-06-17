using Cysharp.Threading.Tasks;
using UnityEngine;

public class NetworkDebugHud : MonoBehaviour
{
    private string address = "127.0.0.1";
    private string port = "7777";
    private bool visible = true;
    private bool busy;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9))
        {
            visible = !visible;
        }
    }

    private void OnGUI()
    {
        if (!visible || GameMgr.Network == null)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(16f, 16f, 260f, 190f), "Network Test", GUI.skin.window);
        GUILayout.Label($"Mode: {GameMgr.Network.Mode}");
        GUILayout.Label($"Active: {GameMgr.Network.IsSessionActive}");

        GUILayout.BeginHorizontal();
        GUILayout.Label("IP", GUILayout.Width(42f));
        address = GUILayout.TextField(address);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Port", GUILayout.Width(42f));
        port = GUILayout.TextField(port);
        GUILayout.EndHorizontal();

        GUI.enabled = !busy && !GameMgr.Network.IsSessionActive;
        if (GUILayout.Button("Start Host"))
        {
            RunStartHostAsync().Forget();
        }

        if (GUILayout.Button("Start Client"))
        {
            RunStartClientAsync().Forget();
        }

        GUI.enabled = !busy && GameMgr.Network.IsSessionActive;
        if (GUILayout.Button("Stop Network"))
        {
            GameMgr.Network.Shutdown();
        }

        GUI.enabled = true;
        if (!string.IsNullOrWhiteSpace(GameMgr.Network.LastError))
        {
            GUILayout.Label(GameMgr.Network.LastError);
        }

        GUILayout.Label("F9 hide/show");
        GUILayout.EndArea();
    }

    private async UniTaskVoid RunStartHostAsync()
    {
        busy = true;
        ushort parsedPort = ParsePort();
        await GameMgr.Network.StartHostAsync("0.0.0.0", parsedPort);
        busy = false;
    }

    private async UniTaskVoid RunStartClientAsync()
    {
        busy = true;
        ushort parsedPort = ParsePort();
        await GameMgr.Network.StartClientAsync(address, parsedPort);
        busy = false;
    }

    private ushort ParsePort()
    {
        return ushort.TryParse(port, out ushort parsedPort) ? parsedPort : (ushort)7777;
    }
}
