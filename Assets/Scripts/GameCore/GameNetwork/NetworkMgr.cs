using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class NetworkMgr
{
    private const string PlayerPrefabAddress = "Player";
    private const string GameSceneName = "GameScene";
    private const ushort DefaultPort = 7777;
    private const float ClientConnectTimeoutSeconds = 15f;
    private const string RelayTransportFailureMessage = "Relay connection failed because the Relay allocation expired or became invalid. Please send a new invite.";

    private GameObject playerPrefab;
    private bool callbacksRegistered;
    private bool disconnectingToSinglePlayer;
    private bool restoringSinglePlayer;
    private bool handlingTransportFailure;
    private bool transportFailureDetected;
    private bool hasPendingRestorePose;
    private Vector3 pendingRestorePosition;
    private Quaternion pendingRestoreRotation = Quaternion.identity;
    private RelayLobbySessionInfo lastRelayHostSession;
    private readonly HashSet<ulong> spawningClientIds = new HashSet<ulong>();

    public NetworkConnectionMode Mode { get; private set; } = NetworkConnectionMode.Offline;
    public string LastError { get; private set; } = string.Empty;

    public bool IsSessionActive =>
        NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsListening;

    public bool IsServer =>
        NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsServer;

    public bool IsClient =>
        NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsClient;

    public event System.Action<bool> OnSessionActiveChanged;

    public RelayLobbySessionInfo LastRelayHostSession => lastRelayHostSession;

    public void Init()
    {
        EnsureNetworkManager();
        RegisterCallbacks();
    }

    public async UniTask<bool> StartHostAsync(string listenAddress = "0.0.0.0", ushort port = DefaultPort)
    {
        LastError = string.Empty;

        if (!await PrepareNetworkAsync(listenAddress, port))
        {
            return false;
        }

        if (SceneManager.GetActiveScene().name == GameSceneName)
        {
            DestroyUnspawnedLocalPlayers();
        }

        bool started = NetworkManager.Singleton.StartHost();
        if (!started)
        {
            LastError = "StartHost failed.";
            return false;
        }

        Mode = NetworkConnectionMode.Host;
        NetworkBoss.RegisterHandlersIfNeeded();
        OnSessionActiveChanged?.Invoke(true);
        GameMgr.Message?.RegisterMessage("Host started", priority: MessagePriority.Medium);

        if (SceneManager.GetActiveScene().name == GameSceneName)
        {
            await ReplaceLocalPlayersWithNetworkPlayersAsync();
        }

        return true;
    }

    public async UniTask<bool> StartClientAsync(string address = "127.0.0.1", ushort port = DefaultPort)
    {
        LastError = string.Empty;

        if (!await PrepareNetworkAsync(address, port))
        {
            return false;
        }

        CaptureLocalNetworkPlayerPose();
        DestroyUnspawnedLocalPlayers();

        bool started = NetworkManager.Singleton.StartClient();
        if (!started)
        {
            LastError = "StartClient failed.";
            await RestoreSinglePlayerAfterFailedClientStartAsync();
            return false;
        }

        Mode = NetworkConnectionMode.Client;
        NetworkBoss.RegisterHandlersIfNeeded();
        OnSessionActiveChanged?.Invoke(true);
        GameMgr.Message?.RegisterMessage("Client connecting", priority: MessagePriority.Medium);

        bool connected = await WaitForClientConnectedAsync();
        if (!connected)
        {
            LastError = transportFailureDetected ? RelayTransportFailureMessage : "Client connection timed out.";
            await RestoreSinglePlayerAfterFailedClientStartAsync();
            return false;
        }

        return true;
    }

    public async UniTask<bool> StartRelayHostAsync(int maxConnections = 4)
    {
        LastError = string.Empty;
        lastRelayHostSession = null;

        if (!WorkDemoRelayLobbyService.IsAvailable)
        {
            LastError = "Unity Relay/Lobby is not enabled. Install UGS packages and add scripting define WORKDEMO_USE_UGS_RELAY.";
            return false;
        }

        if (!await PrepareNetworkAsync("0.0.0.0", DefaultPort))
        {
            return false;
        }

        RelayLobbySessionInfo session = await WorkDemoRelayLobbyService.CreateHostSessionAsync(maxConnections);
        if (session == null || !session.success)
        {
            LastError = session != null ? session.error : "Relay host session failed.";
            return false;
        }

        if (SceneManager.GetActiveScene().name == GameSceneName)
        {
            DestroyUnspawnedLocalPlayers();
        }

        bool started = NetworkManager.Singleton.StartHost();
        if (!started)
        {
            LastError = "Start Relay Host failed.";
            await WorkDemoRelayLobbyService.CleanupSessionAsync();
            return false;
        }

        lastRelayHostSession = session;
        Mode = NetworkConnectionMode.Host;
        NetworkBoss.RegisterHandlersIfNeeded();
        OnSessionActiveChanged?.Invoke(true);
        GameMgr.Message?.RegisterMessage("Relay Host started", priority: MessagePriority.Medium);

        if (SceneManager.GetActiveScene().name == GameSceneName)
        {
            await ReplaceLocalPlayersWithNetworkPlayersAsync();
        }

        return true;
    }

    public async UniTask<bool> StartRelayClientAsync(string relayJoinCode, string lobbyId)
    {
        LastError = string.Empty;

        if (!WorkDemoRelayLobbyService.IsAvailable)
        {
            LastError = "Unity Relay/Lobby is not enabled. Install UGS packages and add scripting define WORKDEMO_USE_UGS_RELAY.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(relayJoinCode))
        {
            LastError = "Relay join code is empty.";
            return false;
        }

        if (!await PrepareNetworkAsync("127.0.0.1", DefaultPort))
        {
            return false;
        }

        RelayLobbySessionInfo session = await WorkDemoRelayLobbyService.JoinClientSessionAsync(relayJoinCode, lobbyId);
        if (session == null || !session.success)
        {
            LastError = session != null ? session.error : "Relay client session failed.";
            return false;
        }

        CaptureLocalNetworkPlayerPose();
        DestroyUnspawnedLocalPlayers();

        bool started = NetworkManager.Singleton.StartClient();
        if (!started)
        {
            LastError = "Start Relay Client failed.";
            await WorkDemoRelayLobbyService.CleanupSessionAsync();
            await RestoreSinglePlayerAfterFailedClientStartAsync();
            return false;
        }

        Mode = NetworkConnectionMode.Client;
        NetworkBoss.RegisterHandlersIfNeeded();
        OnSessionActiveChanged?.Invoke(true);
        GameMgr.Message?.RegisterMessage("Relay Client connecting", priority: MessagePriority.Medium);

        bool connected = await WaitForClientConnectedAsync();
        if (!connected)
        {
            LastError = transportFailureDetected ? RelayTransportFailureMessage : "Relay client connection timed out.";
            await WorkDemoRelayLobbyService.CleanupSessionAsync();
            await RestoreSinglePlayerAfterFailedClientStartAsync();
            return false;
        }

        return true;
    }

    public void Shutdown()
    {
        DisconnectToSinglePlayerAsync().Forget();
    }

    public async UniTask DisconnectToSinglePlayerAsync()
    {
        if (disconnectingToSinglePlayer)
        {
            return;
        }

        disconnectingToSinglePlayer = true;
        LastError = string.Empty;
        CaptureLocalNetworkPlayerPose();

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        await WorkDemoRelayLobbyService.CleanupSessionAsync();
        lastRelayHostSession = null;

        Mode = NetworkConnectionMode.Offline;
        OnSessionActiveChanged?.Invoke(false);

        await RestoreSinglePlayerAfterDisconnectAsync();

        GameMgr.Message?.RegisterMessage("Network stopped", priority: MessagePriority.Medium);
        disconnectingToSinglePlayer = false;
    }

    private void StopNetworkWithoutRestore()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        WorkDemoRelayLobbyService.CleanupSessionAsync().Forget();
        lastRelayHostSession = null;
        Mode = NetworkConnectionMode.Offline;
        LastError = string.Empty;
        OnSessionActiveChanged?.Invoke(false);
    }

    private async UniTask<bool> PrepareNetworkAsync(string address, ushort port)
    {
        NetworkManager networkManager = EnsureNetworkManager();
        RegisterCallbacks();

        playerPrefab = await GameMgr.AssetLoader.LoadPrefab(PlayerPrefabAddress);
        if (playerPrefab == null)
        {
            LastError = "Player prefab load failed.";
            Debug.LogError($"[NetworkMgr] Failed to load prefab: {PlayerPrefabAddress}");
            return false;
        }

        if (!ValidatePlayerPrefab(playerPrefab))
        {
            return false;
        }

        ConfigureNetworkPlayerPrefabDefaults(playerPrefab);
        ConfigureTransport(networkManager, address, port);
        ConfigureNetworkPrefabs(networkManager, playerPrefab);
        return true;
    }

    private NetworkManager EnsureNetworkManager()
    {
        if (NetworkManager.Singleton != null)
        {
            EnsureNetworkConfig(NetworkManager.Singleton);
            return NetworkManager.Singleton;
        }

        GameObject networkObject = new GameObject("NetworkManager");
        Object.DontDestroyOnLoad(networkObject);

        NetworkManager networkManager = networkObject.AddComponent<NetworkManager>();
        UnityTransport transport = networkObject.AddComponent<UnityTransport>();
        EnsureNetworkConfig(networkManager);
        networkManager.NetworkConfig.NetworkTransport = transport;
        networkManager.NetworkConfig.PlayerPrefab = null;
        networkManager.NetworkConfig.EnableSceneManagement = false;

        return networkManager;
    }

    private void EnsureNetworkConfig(NetworkManager networkManager)
    {
        if (networkManager.NetworkConfig == null)
        {
            networkManager.NetworkConfig = new NetworkConfig();
        }

        if (networkManager.NetworkConfig.Prefabs == null)
        {
            networkManager.NetworkConfig.Prefabs = new NetworkPrefabs();
        }
    }

    private void RegisterCallbacks()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || callbacksRegistered)
        {
            return;
        }

        networkManager.OnClientConnectedCallback += HandleClientConnected;
        networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
        networkManager.OnTransportFailure += HandleTransportFailure;
        callbacksRegistered = true;
    }

    private void ConfigureTransport(NetworkManager networkManager, string address, ushort port)
    {
        UnityTransport transport = networkManager.GetComponent<UnityTransport>();
        if (transport == null)
        {
            transport = networkManager.gameObject.AddComponent<UnityTransport>();
        }

        networkManager.NetworkConfig.NetworkTransport = transport;
        transport.SetConnectionData(address, port);
    }

    private void ConfigureNetworkPrefabs(NetworkManager networkManager, GameObject prefab)
    {
        networkManager.NetworkConfig.PlayerPrefab = null;
        networkManager.NetworkConfig.EnableSceneManagement = false;

        try
        {
            networkManager.RemoveNetworkPrefab(prefab);
        }
        catch
        {
            // RemoveNetworkPrefab throws when the prefab was not registered yet.
        }

        networkManager.AddNetworkPrefab(prefab);
    }

    private void ConfigureNetworkPlayerPrefabDefaults(GameObject prefab)
    {
        if (prefab == null)
        {
            return;
        }

        SetPlayerInputsEnabled(prefab, false);
    }

    private static void SetPlayerInputsEnabled(GameObject root, bool enabled)
    {
        if (root == null)
        {
            return;
        }

        PlayerInput[] playerInputs = root.GetComponentsInChildren<PlayerInput>(true);
        for (int i = 0; i < playerInputs.Length; i++)
        {
            if (playerInputs[i] != null)
            {
                playerInputs[i].enabled = enabled;
            }
        }
    }

    private bool ValidatePlayerPrefab(GameObject prefab)
    {
        bool valid = true;

        if (prefab.GetComponent<NetworkObject>() == null)
        {
            LastError = "Player prefab needs NetworkObject. Run Tools/WorkDemo Multiplayer/Setup Player Prefab.";
            Debug.LogError($"[NetworkMgr] {LastError}");
            valid = false;
        }

        if (prefab.GetComponent<NetworkPlayer>() == null)
        {
            LastError = "Player prefab needs NetworkPlayer. Run Tools/WorkDemo Multiplayer/Setup Player Prefab.";
            Debug.LogError($"[NetworkMgr] {LastError}");
            valid = false;
        }

        return valid;
    }

    private void HandleClientConnected(ulong clientId)
    {
        GameMgr.Message?.RegisterMessage($"Client connected: {clientId}", priority: MessagePriority.Medium);

        if (!IsServer || SceneManager.GetActiveScene().name != GameSceneName)
        {
            return;
        }

        SpawnPlayerForClientAsync(clientId).Forget();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        GameMgr.Message?.RegisterMessage($"Client disconnected: {clientId}", priority: MessagePriority.Medium);

        if (disconnectingToSinglePlayer)
        {
            return;
        }

        if (handlingTransportFailure)
        {
            return;
        }

        if (Mode == NetworkConnectionMode.Client && !IsServer)
        {
            CaptureLocalNetworkPlayerPose();
            StopNetworkWithoutRestore();
            RestoreSinglePlayerAfterDisconnectAsync().Forget();
        }
    }

    private void HandleTransportFailure()
    {
        if (handlingTransportFailure || disconnectingToSinglePlayer)
        {
            return;
        }

        transportFailureDetected = true;
        LastError = RelayTransportFailureMessage;
        GameMgr.Message?.RegisterMessage(LastError, priority: MessagePriority.High);
        HandleTransportFailureAsync().Forget();
    }

    private async UniTaskVoid HandleTransportFailureAsync()
    {
        handlingTransportFailure = true;
        CaptureLocalNetworkPlayerPose();

        await WorkDemoRelayLobbyService.CleanupSessionAsync();
        lastRelayHostSession = null;
        Mode = NetworkConnectionMode.Offline;
        OnSessionActiveChanged?.Invoke(false);

        await RestoreSinglePlayerAfterDisconnectAsync();
        handlingTransportFailure = false;
    }

    private async UniTask ReplaceLocalPlayersWithNetworkPlayersAsync()
    {
        DestroyUnspawnedLocalPlayers();

        if (!IsServer)
        {
            return;
        }

        List<ulong> clientIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        for (int i = 0; i < clientIds.Count; i++)
        {
            await SpawnPlayerForClientAsync(clientIds[i]);
        }
    }

    private async UniTask SpawnPlayerForClientAsync(ulong clientId)
    {
        if (!IsServer)
        {
            return;
        }

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client) &&
            client.PlayerObject != null)
        {
            return;
        }

        if (spawningClientIds.Contains(clientId))
        {
            return;
        }

        spawningClientIds.Add(clientId);

        try
        {
            if (playerPrefab == null)
            {
                playerPrefab = await GameMgr.AssetLoader.LoadPrefab(PlayerPrefabAddress);
            }

            if (playerPrefab == null || !ValidatePlayerPrefab(playerPrefab))
            {
                return;
            }

            Vector3 spawnPosition = ResolveSpawnPosition(clientId);
            GameObject playerObject = Object.Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
            playerObject.name = $"NetworkPlayer_{clientId}";

            NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();
            networkObject.SpawnAsPlayerObject(clientId, true);
        }
        finally
        {
            spawningClientIds.Remove(clientId);
        }
    }

    private Vector3 ResolveSpawnPosition(ulong clientId)
    {
        GameFile currentGameFile = GameMgr.File != null ? GameMgr.File.CurrentGameFile : null;
        if (currentGameFile != null)
        {
            Vector3 savedPosition = currentGameFile.GetPositionOnSceneLoaded(GameSceneName);
            return savedPosition + new Vector3((int)clientId * 1.5f, 0f, 0f);
        }

        if (GameMgr.Instance != null && GameMgr.Instance.playerInitialData != null)
        {
            return GameMgr.Instance.playerInitialData.initialPosition + new Vector3((int)clientId * 1.5f, 0f, 0f);
        }

        return new Vector3((int)clientId * 1.5f, 0f, 0f);
    }

    private void DestroyUnspawnedLocalPlayers()
    {
        PlayerStateDriver[] players = Object.FindObjectsOfType<PlayerStateDriver>();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerStateDriver player = players[i];
            if (player == null)
            {
                continue;
            }

            NetworkObject networkObject = player.GetComponentInParent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                continue;
            }

            Object.Destroy(GetPlayerRigRoot(player).gameObject);
        }

        if (GameMgr.Instance != null)
        {
            GameMgr.Instance.Player = null;
        }
    }

    private Transform GetPlayerRigRoot(PlayerStateDriver player)
    {
        return player.transform.parent != null ? player.transform.parent : player.transform;
    }

    private void CaptureLocalNetworkPlayerPose()
    {
        hasPendingRestorePose = false;

        NetworkManager networkManager = NetworkManager.Singleton;
        NetworkObject playerObject = networkManager != null && networkManager.LocalClient != null
            ? networkManager.LocalClient.PlayerObject
            : null;
        if (playerObject == null)
        {
            PlayerStateDriver currentPlayer = GameMgr.Instance != null ? GameMgr.Instance.Player : null;
            if (currentPlayer == null)
            {
                return;
            }

            pendingRestorePosition = currentPlayer.transform.position;
            pendingRestoreRotation = currentPlayer.transform.rotation;
            hasPendingRestorePose = true;
            return;
        }

        PlayerStateDriver driver = playerObject.GetComponentInChildren<PlayerStateDriver>(true);
        Transform poseTransform = driver != null ? driver.transform : playerObject.transform;
        pendingRestorePosition = poseTransform.position;
        pendingRestoreRotation = poseTransform.rotation;
        hasPendingRestorePose = true;
    }

    private async UniTask RestoreSinglePlayerAfterDisconnectAsync()
    {
        if (restoringSinglePlayer)
        {
            return;
        }

        restoringSinglePlayer = true;

        await UniTask.Yield();
        await UniTask.DelayFrame(2);

        if (SceneManager.GetActiveScene().name != GameSceneName)
        {
            restoringSinglePlayer = false;
            return;
        }

        DestroyAllScenePlayers();
        PlayerStateDriver player = await SpawnLocalPlayerAsync();
        if (player != null)
        {
            GameMgr.Instance.Player = player;
            ApplyRestorePose(player);
            GameMgr.input?.EnablePlayerActionMap();
            RebindSceneCamerasToLocalPlayer();
            GameMgr.cameraMgr?.UpdateCamera();
        }

        hasPendingRestorePose = false;
        restoringSinglePlayer = false;
    }

    private async UniTask<PlayerStateDriver> SpawnLocalPlayerAsync()
    {
        GameFile currentGameFile = GameMgr.File != null ? GameMgr.File.CurrentGameFile : null;
        if (currentGameFile != null)
        {
            GameMgr.File.ApplyCurrentGameFileToRuntime();
            PlayerModelManager.CurrentRoleModelName = currentGameFile.roleModelName;
        }

        GameObject prefab = playerPrefab != null
            ? playerPrefab
            : await GameMgr.AssetLoader.LoadPrefab(PlayerPrefabAddress);
        if (prefab == null)
        {
            Debug.LogError("[NetworkMgr] Failed to restore local Player prefab.");
            return null;
        }

        GameObject playerObject = Object.Instantiate(prefab);
        playerObject.name = "Player";
        PlayerStateDriver player = playerObject.GetComponentInChildren<PlayerStateDriver>(true);
        if (player == null)
        {
            Debug.LogError("[NetworkMgr] Restored Player prefab has no PlayerStateDriver.");
            Object.Destroy(playerObject);
            return null;
        }

        player.SetLocalControlEnabled(true);
        player.enabled = true;
        SetPlayerInputsEnabled(playerObject, true);
        return player;
    }

    private async UniTask<bool> WaitForClientConnectedAsync()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            return false;
        }

        transportFailureDetected = false;
        float startTime = Time.unscaledTime;
        while (networkManager != null &&
               networkManager.IsListening &&
               !networkManager.IsConnectedClient &&
               !transportFailureDetected &&
               Time.unscaledTime - startTime < ClientConnectTimeoutSeconds)
        {
            await UniTask.Yield();
            networkManager = NetworkManager.Singleton;
        }

        return networkManager != null && networkManager.IsConnectedClient;
    }

    private async UniTask RestoreSinglePlayerAfterFailedClientStartAsync()
    {
        string error = LastError;
        StopNetworkWithoutRestore();
        LastError = error;
        await RestoreSinglePlayerAfterDisconnectAsync();
    }

    private void ApplyRestorePose(PlayerStateDriver player)
    {
        if (player == null)
        {
            return;
        }

        Vector3 position;
        Quaternion rotation;
        if (!TryResolveRestorePose(out position, out rotation))
        {
            return;
        }

        bool playerDriverEnabled = player.enabled;
        CharacterController characterController = player.GetComponent<CharacterController>();
        Transform rigRoot = GetPlayerRigRoot(player);

        player.enabled = false;
        if (characterController != null && characterController.enabled)
        {
            characterController.enabled = false;
        }

        ApplyRigTransform(rigRoot, player.transform, position, rotation);

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        player.enabled = playerDriverEnabled;
    }

    private bool TryResolveRestorePose(out Vector3 position, out Quaternion rotation)
    {
        if (hasPendingRestorePose)
        {
            position = pendingRestorePosition;
            rotation = pendingRestoreRotation;
            return true;
        }

        GameFile currentGameFile = GameMgr.File != null ? GameMgr.File.CurrentGameFile : null;
        if (currentGameFile != null)
        {
            string activeSceneName = SceneManager.GetActiveScene().name;
            if (currentGameFile.TryGetSceneLocation(activeSceneName, out position, out rotation))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(currentGameFile.lastScene) &&
                currentGameFile.TryGetSceneLocation(currentGameFile.lastScene, out position, out rotation))
            {
                return true;
            }
        }

        if (GameMgr.Instance != null && GameMgr.Instance.playerInitialData != null)
        {
            position = GameMgr.Instance.playerInitialData.initialPosition;
            rotation = GameMgr.Instance.playerInitialData.initialRotation;
            return true;
        }

        position = Vector3.zero;
        rotation = Quaternion.identity;
        return false;
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

    private void DestroyAllScenePlayers()
    {
        PlayerStateDriver[] players = Object.FindObjectsOfType<PlayerStateDriver>();
        for (int i = 0; i < players.Length; i++)
        {
            PlayerStateDriver player = players[i];
            if (player == null)
            {
                continue;
            }

            Object.Destroy(GetPlayerRigRoot(player).gameObject);
        }

        if (GameMgr.Instance != null)
        {
            GameMgr.Instance.Player = null;
        }
    }

    private static void RebindSceneCamerasToLocalPlayer()
    {
        ThirdPersonCameraComtrol[] thirdPersonCameras = Object.FindObjectsOfType<ThirdPersonCameraComtrol>(true);
        for (int i = 0; i < thirdPersonCameras.Length; i++)
        {
            thirdPersonCameras[i]?.RebindToCurrentPlayer();
        }

        CameraControl[] cameraControls = Object.FindObjectsOfType<CameraControl>(true);
        for (int i = 0; i < cameraControls.Length; i++)
        {
            cameraControls[i]?.RebindToCurrentPlayer();
        }
    }
}
