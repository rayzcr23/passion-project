using Unity.Netcode;
using UnityEngine;

/// King of Fighters style POW gauge. You build it by fighting - landing hits
/// mostly, taking them a little - and spend it on abilities.
///
/// Server-authoritative, because it gates damage. A client that owned its own
/// meter could simply declare itself full.
public class PowerMeter : NetworkBehaviour
{
    public const ulong NoAttacker = ulong.MaxValue;

    public float max = 100f;

    [Header("Gain")]
    [Tooltip("POW per point of damage dealt.")]
    public float gainPerDamageDealt = 1.6f;

    [Tooltip("POW per point of damage taken. Losing a fight should still build " +
         "toward a comeback, but slower than winning one.")]
    public float gainPerDamageTaken = 0.8f;

    readonly NetworkVariable<float> power = new NetworkVariable<float>();

    public float Current => power.Value;
    public float Fraction => max <= 0f ? 0f : Mathf.Clamp01(power.Value / max);
    public bool IsFull => power.Value >= max;

    public bool Has(float cost) => power.Value >= cost;

    // ------------------------------------------------------------ server side

    public void ServerAdd(float amount)
    {
        if (!IsServer || amount <= 0f) return;
        power.Value = Mathf.Clamp(power.Value + amount, 0f, max);
    }

    public bool ServerTrySpend(float cost)
    {
        if (!IsServer) return false;
        if (cost <= 0f) return true;
        if (power.Value < cost) return false;

        power.Value -= cost;
        return true;
    }

    /// Awards the attacker for landing a hit and the victim a little for eating
    /// one. Called from Health, which is the one place that knows both.
    public static void ServerAwardForDamage(ulong attackerClientId, Health victim, int damage)
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer || damage <= 0) return;

        if (attackerClientId != NoAttacker)
        {
            NetworkClient attacker;
            if (nm.ConnectedClients.TryGetValue(attackerClientId, out attacker) &&
                attacker.PlayerObject != null)
            {
                var meter = attacker.PlayerObject.GetComponent<PowerMeter>();
                var tree = attacker.PlayerObject.GetComponent<SkillTree>();
                float scale = tree != null ? tree.PowerMultiplier : 1f;

                if (meter != null) meter.ServerAdd(damage * meter.gainPerDamageDealt * scale);
            }
        }

        if (victim != null)
        {
            var victimMeter = victim.GetComponent<PowerMeter>();
            if (victimMeter != null)
                victimMeter.ServerAdd(damage * victimMeter.gainPerDamageTaken);
        }
    }

    // ---------------------------------------------------------------- display

    static Texture2D pixel;

    void OnGUI()
    {
        if (!IsOwner || !IsSpawned) return;

        if (pixel == null)
        {
            pixel = new Texture2D(1, 1);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
        }

        float width = 320f;
        float height = 16f;
        var bar = new Rect((Screen.width - width) * 0.5f, Screen.height - 46f, width, height);

        Color previous = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(bar.x - 2f, bar.y - 2f, bar.width + 4f, bar.height + 4f), pixel);

        float f = Fraction;

        // Flares white at full, so "you can use it now" is unmissable mid-fight.
        GUI.color = IsFull
            ? Color.Lerp(new Color(0.75f, 0.35f, 1f), Color.white,
                         Mathf.PingPong(Time.time * 3f, 1f))
            : new Color(0.55f, 0.25f, 0.85f);

        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * f, bar.height), pixel);

        GUI.color = previous;

        var label = new GUIStyle(GUI.skin.label);
        label.alignment = TextAnchor.MiddleCenter;
        label.fontStyle = FontStyle.Bold;
        label.normal.textColor = Color.white;

        GUI.Label(bar, IsFull ? "POW  MAX" : "POW  " + Mathf.FloorToInt(power.Value), label);
    }
}
