using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

/// Host/join over the internet via Unity Relay + Sessions API.
///
/// Uses WithNetworkOptions(RelayProtocol.WSS) to force WebSocket (wss://)
/// connections on every platform. This is required for WebGL/browser builds
/// (iPad Safari, school Chromebooks, etc.) because browsers block raw UDP.
/// WSS also works fine on PC, so we just always use it.
public class SessionHUD : MonoBehaviour
{
    public int maxPlayers = 3;

    [Tooltip("Hide the local-only NetworkHUD while this is in use.")]
    public NetworkHUD localHud;

    string joinCodeInput = "";
    string statusText    = "";
    bool   busy;
    ISession session;

    // ──────────────────────────────────────────────
    //  Unity lifecycle
    // ──────────────────────────────────────────────

    void Awake()
    {
        Application.targetFrameRate = 60;
        Application.runInBackground = true;

        var nm = GetComponent<NetworkManager>();
        if (nm != null && nm.NetworkConfig != null)
            nm.NetworkConfig.TickRate = 60;

        if (localHud == null) localHud = GetComponent<NetworkHUD>();
    }

    void OnEnable()
    {
        // Two HUDs drawing in the same corner is unreadable, and the local one
        // would start a conflicting localhost host.
        if (localHud != null) localHud.enabled = false;
    }

    // ──────────────────────────────────────────────
    //  GUI
    // ──────────────────────────────────────────────

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(12f, 12f, 280f, 280f));

        if (session != null || (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient)))
        {
            bool isHost = session != null ? session.IsHost : (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost);
            GUILayout.Label(isHost ? "HOSTING" : "JOINED");

            if (session != null && !string.IsNullOrEmpty(session.Code))
            {
                GUILayout.Label("Join code:");
                GUILayout.Label(session.Code, GUI.skin.textField);

                if (GUILayout.Button("Copy code"))
                    GUIUtility.systemCopyBuffer = session.Code;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                GUILayout.Label("Players: " + NetworkManager.Singleton.ConnectedClientsIds.Count);

            if (GUILayout.Button("Leave / Disconnect")) Leave();
        }
        else if (busy)
        {
            GUILayout.Label(statusText);
        }
        else
        {
            if (GUILayout.Button("Host Online (Relay)")) Host();
            if (GUILayout.Button("Host Local (Direct)")) HostLocal();

            GUILayout.Space(8f);
            GUILayout.Label("Join code:");
            joinCodeInput = GUILayout.TextField(joinCodeInput ?? "", 8);

            if (GUILayout.Button("Join")) Join(joinCodeInput);

            if (!string.IsNullOrEmpty(statusText))
                GUILayout.Label(statusText);
        }

        GUILayout.EndArea();
    }

    void HostLocal()
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.StartHost();
            statusText = "";
            Debug.Log("[Wanderers] Hosting locally (Direct Host).");
        }
    }

    // ──────────────────────────────────────────────
    //  Host
    // ──────────────────────────────────────────────

    async void Host()
    {
        if (busy) return;
        busy = true;
        statusText = "Setting up session...";

        try
        {
            await EnsureSignedIn();

            // WithNetworkOptions forces WSS (WebSocket) relay on every platform.
            // This is the only protocol that works inside a browser (iPad Safari).
            // It also works on PC, so we always use it.
            var netOpts = new NetworkOptions { RelayProtocol = RelayProtocol.WSS };
            var options = new SessionOptions { MaxPlayers = maxPlayers }
                .WithRelayNetwork()
                .WithNetworkOptions(netOpts);

            session = await MultiplayerService.Instance.CreateSessionAsync(options);

            statusText = "";
            Debug.Log("[Wanderers] Hosting. Join code: " + session.Code);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Wanderers] UGS Online Host failed (" + e.Message + "). Falling back to Direct Host...");
            session = null;
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
            {
                bool started = NetworkManager.Singleton.StartHost();
                if (started)
                {
                    statusText = "";
                    Debug.Log("[Wanderers] Hosting locally (Direct Host).");
                    return;
                }
            }
            statusText = "Couldn't host online session — fallback to Direct Host.";
            Debug.LogError("[Wanderers] Host failed: " + e);
        }
        finally { busy = false; }
    }

    // ──────────────────────────────────────────────
    //  Join
    // ──────────────────────────────────────────────

    async void Join(string code)
    {
        if (busy) return;

        code = (code ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code)) { statusText = "Enter a join code."; return; }

        busy = true;
        statusText = "Joining " + code + "...";

        try
        {
            await EnsureSignedIn();

            // Force WSS so the browser can reach the relay server.
            var netOpts = new NetworkOptions { RelayProtocol = RelayProtocol.WSS };
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(
                code,
                new JoinSessionOptions().WithNetworkOptions(netOpts));

            statusText = "";
            Debug.Log("[Wanderers] Joined session " + session.Id);
        }
        catch (Exception e)
        {
            statusText = "Couldn't join — check the code and try again.";
            Debug.LogError("[Wanderers] Join failed: " + e);
            session = null;
        }
        finally { busy = false; }
    }

    // ──────────────────────────────────────────────
    //  Leave
    // ──────────────────────────────────────────────

    async void Leave()
    {
        try
        {
            if (session != null) await session.LeaveAsync();
        }
        catch (Exception e) { Debug.LogException(e); }
        finally
        {
            session = null;
            statusText = "";
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        }
    }

    // ──────────────────────────────────────────────
    //  Auth helper
    // ──────────────────────────────────────────────

    static async Task EnsureSignedIn()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            // Use a unique profile name per session so that multiple browser
            // tabs (or multiple instances on the same device) each get their
            // own anonymous player identity.  Without this every tab shares
            // the same cached token → UGS returns SessionConflict when the
            // second player tries to join.
            var options = new InitializationOptions()
                .SetProfile("player_" + Guid.NewGuid().ToString("N"));
            await UnityServices.InitializeAsync(options);
        }

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }
}
