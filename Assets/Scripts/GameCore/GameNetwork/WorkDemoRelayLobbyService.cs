using Cysharp.Threading.Tasks;
using UnityEngine;

#if WORKDEMO_USE_UGS_RELAY
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
#endif

public static class WorkDemoRelayLobbyService
{
    public const string EnableDefine = "WORKDEMO_USE_UGS_RELAY";

#if WORKDEMO_USE_UGS_RELAY
    private const string RelayProtocol = "dtls";
    private const string RelayJoinCodeKey = "relayJoinCode";
    private const int LobbyHeartbeatSeconds = 15;
    private const string DefaultAuthProfile = "wd_default";

    private static string currentLobbyId;
    private static bool currentLobbyCreatedByHost;
    private static CancellationTokenSource lobbyHeartbeatCts;
#endif

    public static bool IsAvailable
    {
        get
        {
#if WORKDEMO_USE_UGS_RELAY
            return true;
#else
            return false;
#endif
        }
    }

    public static async UniTask<RelayLobbySessionInfo> CreateHostSessionAsync(int maxConnections)
    {
#if WORKDEMO_USE_UGS_RELAY
        try
        {
            await CleanupSessionAsync();
            await EnsureUnityServicesSignedInAsync();

            int safeMaxConnections = Mathf.Clamp(maxConnections, 1, 32);
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(safeMaxConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            UnityTransport transport = ResolveTransport();
            transport.SetRelayServerData(new RelayServerData(allocation, RelayProtocol));

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(
                "WorkDemo-" + joinCode,
                safeMaxConnections + 1,
                new CreateLobbyOptions
                {
                    IsPrivate = true,
                    Data = new Dictionary<string, DataObject>
                    {
                        { RelayJoinCodeKey, new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
                    }
                });

            currentLobbyId = lobby.Id;
            currentLobbyCreatedByHost = true;
            StartLobbyHeartbeat(lobby.Id);
            return RelayLobbySessionInfo.Success(joinCode, lobby.Id);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[WorkDemoRelayLobbyService] Create host session failed: " + exception.Message);
            return RelayLobbySessionInfo.Fail(exception.Message);
        }
#else
        await UniTask.CompletedTask;
        return RelayLobbySessionInfo.Fail("Unity Relay/Lobby packages are not enabled. Install UGS packages and add scripting define WORKDEMO_USE_UGS_RELAY.");
#endif
    }

    public static async UniTask<RelayLobbySessionInfo> JoinClientSessionAsync(string relayJoinCode, string lobbyId)
    {
#if WORKDEMO_USE_UGS_RELAY
        try
        {
            if (string.IsNullOrWhiteSpace(relayJoinCode) && string.IsNullOrWhiteSpace(lobbyId))
            {
                return RelayLobbySessionInfo.Fail("Relay join code is empty.");
            }

            await CleanupSessionAsync();
            await EnsureUnityServicesSignedInAsync();

            if (!string.IsNullOrWhiteSpace(lobbyId))
            {
                Lobby lobby = await JoinOrGetLobbyForClientAsync(lobbyId);
                currentLobbyId = lobby != null && !string.IsNullOrWhiteSpace(lobby.Id)
                    ? lobby.Id
                    : lobbyId;
                currentLobbyCreatedByHost = false;

                if (string.IsNullOrWhiteSpace(relayJoinCode) &&
                    lobby.Data != null &&
                    lobby.Data.TryGetValue(RelayJoinCodeKey, out DataObject relayData))
                {
                    relayJoinCode = relayData.Value;
                }
            }

            if (string.IsNullOrWhiteSpace(relayJoinCode))
            {
                return RelayLobbySessionInfo.Fail("Relay join code is empty.");
            }

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode.Trim());
            UnityTransport transport = ResolveTransport();
            transport.SetRelayServerData(new RelayServerData(joinAllocation, RelayProtocol));

            return RelayLobbySessionInfo.Success(relayJoinCode.Trim(), currentLobbyId);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("[WorkDemoRelayLobbyService] Join client session failed: " + exception.Message);
            return RelayLobbySessionInfo.Fail(exception.Message);
        }
#else
        await UniTask.CompletedTask;
        return RelayLobbySessionInfo.Fail("Unity Relay/Lobby packages are not enabled. Install UGS packages and add scripting define WORKDEMO_USE_UGS_RELAY.");
#endif
    }

    public static async UniTask CleanupSessionAsync()
    {
#if WORKDEMO_USE_UGS_RELAY
        if (string.IsNullOrWhiteSpace(currentLobbyId))
        {
            return;
        }

        try
        {
            StopLobbyHeartbeat();

            if (currentLobbyCreatedByHost)
            {
                await LobbyService.Instance.DeleteLobbyAsync(currentLobbyId);
            }
            else
            {
                await LobbyService.Instance.RemovePlayerAsync(currentLobbyId, AuthenticationService.Instance.PlayerId);
            }
        }
        catch
        {
            try
            {
                await LobbyService.Instance.RemovePlayerAsync(currentLobbyId, AuthenticationService.Instance.PlayerId);
            }
            catch
            {
                // Cleanup is best-effort because the lobby may already be gone.
            }
        }
        finally
        {
            currentLobbyId = string.Empty;
            currentLobbyCreatedByHost = false;
        }
#else
        await UniTask.CompletedTask;
#endif
    }

#if WORKDEMO_USE_UGS_RELAY
    private static async UniTask EnsureUnityServicesSignedInAsync()
    {
        string profile = ResolveAuthenticationProfile();

        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            InitializationOptions options = new InitializationOptions().SetProfile(profile);
            await UnityServices.InitializeAsync(options);
        }

        if (AuthenticationService.Instance.IsSignedIn &&
            !string.Equals(AuthenticationService.Instance.Profile, profile, StringComparison.Ordinal))
        {
            AuthenticationService.Instance.SignOut();
        }

        if (!AuthenticationService.Instance.IsSignedIn &&
            !string.Equals(AuthenticationService.Instance.Profile, profile, StringComparison.Ordinal))
        {
            AuthenticationService.Instance.SwitchProfile(profile);
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    private static string ResolveAuthenticationProfile()
    {
        AccountProfile accountProfile = GameMgr.Account != null ? GameMgr.Account.CurrentProfile : null;
        string playerId = accountProfile != null ? accountProfile.playerId : string.Empty;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return DefaultAuthProfile;
        }

        string profile = "wd_" + playerId.Trim();
        return profile.Length <= 30 ? profile : profile.Substring(0, 30);
    }

    private static async UniTask<Lobby> JoinOrGetLobbyForClientAsync(string lobbyId)
    {
        try
        {
            return await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);
        }
        catch (LobbyServiceException exception) when (IsAlreadyLobbyMemberException(exception))
        {
            Debug.Log("[WorkDemoRelayLobbyService] Already a member of this Lobby; continuing with Relay join.");

            try
            {
                return await LobbyService.Instance.GetLobbyAsync(lobbyId);
            }
            catch (Exception getException)
            {
                Debug.LogWarning("[WorkDemoRelayLobbyService] Get existing Lobby failed: " + getException.Message);
                return null;
            }
        }
    }

    private static bool IsAlreadyLobbyMemberException(LobbyServiceException exception)
    {
        if (exception == null || string.IsNullOrWhiteSpace(exception.Message))
        {
            return false;
        }

        return exception.Message.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0 &&
               exception.Message.IndexOf("member", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static UnityTransport ResolveTransport()
    {
        if (NetworkManager.Singleton == null)
        {
            throw new System.InvalidOperationException("NetworkManager is not initialized.");
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            transport = NetworkManager.Singleton.gameObject.AddComponent<UnityTransport>();
        }

        return transport;
    }

    private static void StartLobbyHeartbeat(string lobbyId)
    {
        StopLobbyHeartbeat();
        lobbyHeartbeatCts = new CancellationTokenSource();
        SendLobbyHeartbeatLoop(lobbyId, lobbyHeartbeatCts.Token).Forget();
    }

    private static void StopLobbyHeartbeat()
    {
        if (lobbyHeartbeatCts == null)
        {
            return;
        }

        lobbyHeartbeatCts.Cancel();
        lobbyHeartbeatCts.Dispose();
        lobbyHeartbeatCts = null;
    }

    private static async UniTaskVoid SendLobbyHeartbeatLoop(string lobbyId, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(LobbyHeartbeatSeconds), cancellationToken: token);
                if (!token.IsCancellationRequested && currentLobbyId == lobbyId)
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
                }
            }
            catch (System.OperationCanceledException)
            {
                break;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[WorkDemoRelayLobbyService] Lobby heartbeat failed: " + exception.Message);
            }
        }
    }
#endif
}
