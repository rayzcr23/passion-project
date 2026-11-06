using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
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
[DefaultExecutionOrder(-10000)]
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
        DetachNestedNetworkObjects();

        Application.targetFrameRate = 60;
        Application.runInBackground = true;

        var nm = GetComponent<NetworkManager>();
        if (nm != null && nm.NetworkConfig != null)
        {
            nm.NetworkConfig.TickRate = 60;
            nm.NetworkConfig.ClientConnectionBufferTimeout = 30;
        }

        var transport = GetComponent<UnityTransport>();
        if (transport != null)
            transport.UseWebSockets = true;

        if (localHud == null) localHud = GetComponent<NetworkHUD>();
    }

    // NGO does not support one NetworkObject nested beneath another. A number
    // of scene enemies were accidentally arranged this way, which made WebGL
    // freeze while preparing a late-join snapshot. Repair every scene object
    // before NetworkObject.Awake runs and preserve each world position.
    static void DetachNestedNetworkObjects()
    {
        var networkObjects = FindObjectsByType<NetworkObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int detached = 0;
        foreach (var networkObject in networkObjects)
        {
            Transform parent = networkObject.transform.parent;
            if (parent == null || parent.GetComponentInParent<NetworkObject>() == null)
                continue;

            networkObject.transform.SetParent(null, true);
            detached++;
        }

        if (detached > 0)
            Debug.Log("[Wanderers] Corrected " + detached + " nested scene NetworkObjects.");
    }

    void OnEnable()
    {
        // Two HUDs drawing in the same corner is unreadable, and the local one
        // would start a conflicting localhost host.
        if (localHud != null) localHud.enabled = false;
    }

    void Start()
    {
#if UNITY_EDITOR
        // Pressing Play in Unity immediately starts a local demo. Start runs
        // after every component's Awake, so NetworkManager is ready here.
        StartDemo();
#endif
    }

    // ──────────────────────────────────────────────
    //  GUI
    // ──────────────────────────────────────────────

    void OnGUI()
    {
        // Keep the launcher above dialogue, banners, and other immediate-mode
        // overlays so the demo button is always reachable in the Game view.
        GUI.depth = -1000;

        NetworkManager nm = NetworkManager.Singleton;
        bool connected = session != null || (nm != null && (nm.IsServer || nm.IsClient));

        if (!connected && !busy)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            DrawOnlineLauncher();
#else
            // Centre the actual button in the rendered game. This keeps it in
            // view even when Unity's Game window is zoomed/cropped to 5x.
            const float buttonWidth = 360f;
            const float buttonHeight = 72f;
            var buttonRect = new Rect(
                (Screen.width - buttonWidth) * 0.5f,
                (Screen.height - buttonHeight) * 0.5f,
                buttonWidth,
                buttonHeight);

            var demoButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };

            Color previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.35f, 0.9f, 0.45f);
            if (GUI.Button(buttonRect, "PLAY DEMO", demoButton)) StartDemo();
            GUI.backgroundColor = previousBackground;

            if (!string.IsNullOrEmpty(statusText))
            {
                var statusStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16
                };
                GUI.Label(new Rect(buttonRect.x, buttonRect.yMax + 8f, buttonWidth, 28f), statusText, statusStyle);
            }
#endif

            return;
        }

        GUILayout.BeginArea(new Rect(12f, 12f, 320f, 180f), GUI.skin.box);

        if (connected)
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

        GUILayout.EndArea();
    }

    void DrawOnlineLauncher()
    {
        const float panelWidth = 420f;
        const float panelHeight = 260f;
        var panelRect = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);

        GUILayout.BeginArea(panelRect, GUI.skin.box);
        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24,
            fontStyle = FontStyle.Bold
        };

        GUILayout.Label("WANDERERS OF TIME", titleStyle);
        GUILayout.Space(12f);

        if (GUILayout.Button("HOST ONLINE GAME", GUILayout.Height(48f))) Host();

        GUILayout.Space(12f);
        GUILayout.Label("Join code:");
        joinCodeInput = GUILayout.TextField(joinCodeInput ?? "", 8, GUILayout.Height(32f));
        if (GUILayout.Button("JOIN ONLINE GAME", GUILayout.Height(48f))) Join(joinCodeInput);

        if (!string.IsNullOrEmpty(statusText))
            GUILayout.Label(statusText);

        GUILayout.EndArea();
    }

    void StartDemo()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        statusText = "Demo mode is available inside the Unity Editor.";
#else
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null)
        {
            statusText = "Demo could not start: NetworkManager is missing.";
            Debug.LogError("[Wanderers] Demo could not start because no NetworkManager exists.");
            return;
        }

        if (nm.IsListening) return;

        // Relay uses WebSockets, but an Editor demo should use a normal local
        // UTP connection and never contact Unity Services.
        var transport = nm.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.UseWebSockets = false;
            transport.SetConnectionData("127.0.0.1", 7777, "127.0.0.1");
        }

        bool started = nm.StartHost();
        statusText = started ? "" : "Demo could not start. Check the Console.";
        Debug.Log(started
            ? "[Wanderers] Demo mode started locally."
            : "[Wanderers] Demo mode failed to start.");
#endif
    }

    void HostLocal()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        statusText = "Direct hosting is not available in a browser. Use Host Online.";
        return;
#else
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.StartHost();
            statusText = "";
            Debug.Log("[Wanderers] Hosting locally (Direct Host).");
        }
#endif
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
            session = null;
            statusText = "Couldn't host online. Check your connection and try again.";
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
            // Unity profile names have a 30-character maximum. "player_" plus
            // 23 GUID characters is exactly 30.
            string profileId = Guid.NewGuid().ToString("N").Substring(0, 23);
            var options = new InitializationOptions()
                .SetProfile("player_" + profileId);
            await UnityServices.InitializeAsync(options);
        }

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }
}
