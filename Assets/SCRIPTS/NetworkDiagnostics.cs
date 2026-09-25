using System.Text;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// On-screen network state, so a desync can be compared side by side on both
/// machines instead of guessed at. Toggle with F3.
///
/// The question this answers: does each machine agree on who exists, who owns
/// what, and where everyone is? If the host lists a client player at one
/// position and the client lists it somewhere else, it is replication. If a
/// machine is missing a player object entirely, it is spawning.
public class NetworkDiagnostics : MonoBehaviour
{
    public KeyCode toggleKey = KeyCode.F3;
    public bool visible = true;

    readonly StringBuilder sb = new StringBuilder();
    GUIStyle style;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) visible = !visible;
    }

    void OnGUI()
    {
        if (!visible) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = 12;
            style.normal.textColor = Color.white;
            style.richText = false;
        }

        NetworkManager nm = NetworkManager.Singleton;

        sb.Length = 0;
        sb.AppendLine("=== NETWORK (F3) ===");

        if (nm == null)
        {
            sb.AppendLine("No NetworkManager");
        }
        else if (!nm.IsListening)
        {
            sb.AppendLine("Not connected");
        }
        else
        {
            string role = nm.IsHost ? "HOST" : (nm.IsServer ? "SERVER" : "CLIENT");
            sb.AppendLine("Role: " + role + "   LocalClientId: " + nm.LocalClientId);

            NetworkObject localPlayer = nm.SpawnManager != null
                ? nm.SpawnManager.GetLocalPlayerObject() : null;

            sb.AppendLine("My player: " + Describe(localPlayer));

            if (localPlayer != null)
            {
                var aim = localPlayer.GetComponent<AimInput>();
                var loadout = localPlayer.GetComponent<AbilityLoadout>();
                var move = localPlayer.GetComponent<Movement>();

                if (aim != null)
                    sb.AppendLine(string.Format("Aim: ({0:F2},{1:F2}) hasAim={2}",
                        aim.AimDirection.x, aim.AimDirection.y, aim.HasAim ? "Y" : "n"));

                if (move != null)
                    sb.AppendLine(string.Format("Move dir: ({0:F2},{1:F2})  facing={2}",
                        move.LastMoveDirection.x, move.LastMoveDirection.y,
                        move.facingDirection > 0 ? "R" : "L"));

                if (loadout != null)
                    sb.AppendLine(string.Format("ATTACK dir: ({0:F2},{1:F2})  mode={2}",
                        loadout.LastAttackDirection.x, loadout.LastAttackDirection.y,
                        loadout.aimMode));
            }

            sb.AppendLine();

            // Clients list is server-only; a client can still walk its own spawns.
            if (nm.IsServer)
            {
                sb.AppendLine("Connected: " + nm.ConnectedClientsIds.Count);
                foreach (NetworkClient c in nm.ConnectedClientsList)
                    sb.AppendLine("  client " + c.ClientId + ": " + Describe(c.PlayerObject));
            }
            else
            {
                sb.AppendLine("(client view - listing spawned players)");
            }

            sb.AppendLine();
            sb.AppendLine("QUESTS (0=unstarted 1=active 2=complete)");

            QuestManager qm = QuestManager.Instance;
            if (qm == null || qm.Entries == null)
            {
                sb.AppendLine("  no QuestManager");
            }
            else if (qm.Entries.Count == 0)
            {
                sb.AppendLine("  none started");
            }
            else
            {
                for (int i = 0; i < qm.Entries.Count; i++)
                {
                    QuestManager.Progress p = qm.Entries[i];
                    QuestDefinition def = qm.Find(p.id.ToString());

                    sb.AppendLine(string.Format("  {0}: state={1} count={2}{3}",
                        p.id, p.state, p.counter,
                        def == null ? "  NOT REGISTERED" : "/" + def.requiredCount));
                }
            }

            sb.AppendLine();
            sb.AppendLine("Spawned player objects visible here:");

            int seen = 0;
            if (nm.SpawnManager != null)
            {
                foreach (var kv in nm.SpawnManager.SpawnedObjects)
                {
                    NetworkObject no = kv.Value;
                    if (no == null || !no.IsPlayerObject) continue;

                    seen++;
                    sb.AppendLine("  " + Describe(no));
                }
            }
            if (seen == 0) sb.AppendLine("  NONE - nothing to see or shoot at");
        }

        GUI.Label(new Rect(Screen.width - 430f, 10f, 420f, 460f), sb.ToString(), style);
    }

    static string Describe(NetworkObject no)
    {
        if (no == null) return "MISSING";

        Vector3 p = no.transform.position;
        var nt = no.GetComponent<NetworkTransform>();

        return string.Format(
            "id={0} owner={1} mine={2} spawned={3} pos=({4:F1},{5:F1}) nt={6}",
            no.NetworkObjectId,
            no.OwnerClientId,
            no.IsOwner ? "Y" : "n",
            no.IsSpawned ? "Y" : "n",
            p.x, p.y,
            nt == null ? "none" : (nt.IsServerAuthoritative() ? "server" : "owner"));
    }
}
