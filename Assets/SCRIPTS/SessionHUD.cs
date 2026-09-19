using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

/// Host/join over the internet via Unity Relay.
///
/// NetworkHUD's buttons call StartHost/StartClient directly, which only ever
/// talks to 127.0.0.1 - fine for two windows on one machine, useless for a
/// friend on another device. The Sessions API allocates a Relay server, hands
/// back a short join code, and starts Netcode against it.
///
/// Do NOT also call NetworkManager.StartHost(): WithRelayNetwork() does that.
public class SessionHUD : MonoBehaviour
{
    public int maxPlayers = 3;

    [Tooltip("Hide the local-only NetworkHUD while this is in use.")]
    public NetworkHUD localHud;

    string joinCodeInput = "";
    string statusText = "";
    bool busy;
    ISession session;

    void Awake()
    {
        if (localHud == null) localHud = GetComponent<NetworkHUD>();
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(12f, 12f, 260f, 240f));

        if (session != null)
        {
            GUILayout.Label(session.IsHost ? "HOSTING" : "JOINED");

            if (!string.IsNullOrEmpty(session.Code))
            {
                GUILayout.Label("Join code:");
                GUILayout.Label(session.Code, GUI.skin.textField);

                if (GUILayout.Button("Copy code"))
                    GUIUtility.systemCopyBuffer = session.Code;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                GUILayout.Label("Players: " + NetworkManager.Singleton.ConnectedClientsIds.Count);

            if (GUILayout.Button("Leave")) Leave();
        }
        else if (busy)
        {
            GUILayout.Label(statusText);
        }
        else
        {
            if (GUILayout.Button("Host online")) Host();

            GUILayout.Space(8f);
            GUILayout.Label("Join code:");
            joinCodeInput = GUILayout.TextField(joinCodeInput ?? "", 8);

            if (GUILayout.Button("Join")) Join(joinCodeInput);

            if (!string.IsNullOrEmpty(statusText)) GUILayout.Label(statusText);
        }

        GUILayout.EndArea();
    }

    async void Host()
    {
        if (busy) return;
        busy = true;
        statusText = "Creating session...";

        try
        {
            await EnsureSignedIn();

            var options = new SessionOptions { MaxPlayers = maxPlayers }.WithRelayNetwork();
            session = await MultiplayerService.Instance.CreateSessionAsync(options);

            statusText = "";
            Debug.Log("[Wanderers] Hosting. Join code: " + session.Code);
        }
        catch (Exception e)
        {
            statusText = "Host failed: " + e.Message;
            Debug.LogException(e);
        }
        finally { busy = false; }
    }

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

            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);

            statusText = "";
            Debug.Log("[Wanderers] Joined session " + session.Id);
        }
        catch (Exception e)
        {
            statusText = "Join failed: " + e.Message;
            Debug.LogException(e);
        }
        finally { busy = false; }
    }

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
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        }
    }

    /// Both calls are safe to reach twice, but not to run twice - guard rather
    /// than letting a second Host click throw.
    static async Task EnsureSignedIn()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    void OnEnable()
    {
        // Two HUDs drawing in the same corner is unreadable, and the local one
        // would start a conflicting localhost host.
        if (localHud != null) localHud.enabled = false;
    }
}
