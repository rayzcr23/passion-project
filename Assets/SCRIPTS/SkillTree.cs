using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// An endless, organically branching skill tree.
///
/// Nothing is authored or stored. Every node's stat, cost, branch count and
/// angle is derived from its id by a hash, so the tree grows outward forever
/// and the save state is just the list of ids you have taken. Same seed, same
/// tree for everyone.
///
/// Ids are a ternary tree: the children of n are 3n+1, 3n+2, 3n+3, so the parent
/// of n is (n-1)/3. That gives a unique id per node with no lookup table, and
/// lets a node's position be rebuilt by walking the path down from the root.
///
/// Unlocks are server-authoritative - these bonuses change damage and health.
public class SkillTree : NetworkBehaviour
{
    public enum Stat
    {
        Damage,
        MaxHealth,
        MoveSpeed,
        PowerGain,
        Cooldown,
        Range,        // how far your swing reaches
        Knockback,
        Lifesteal,    // heal a fraction of the damage you deal
        ArcWidth      // how wide the swing is
    }

    const int StatCount = 9;

    public struct Node : INetworkSerializable, IEquatable<Node>
    {
        public long id;

        public Node(long id) { this.id = id; }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref id);
        }

        public bool Equals(Node other) => id == other.id;
        public override int GetHashCode() => id.GetHashCode();
    }

    [Header("Generation")]
    public int seed = 1337;
    public float branchLength = 44f;
    public float branchSpread = 46f;

    [Tooltip("Skill points per kill, accumulated fractionally.")]
    public float pointsPerKill = 0.34f;

    public NetworkList<Node> Unlocked { get; private set; }

    readonly NetworkVariable<int> points = new NetworkVariable<int>();
    readonly NetworkVariable<float> pointFraction = new NetworkVariable<float>();

    public int Points => points.Value;
    public event Action OnChanged;

    readonly HashSet<long> unlockedLookup = new HashSet<long>();

    float damageBonus, healthBonus, speedBonus, powerBonus, cooldownBonus;
    float rangeBonus, knockbackBonus, lifestealBonus, arcBonus;

    public float DamageMultiplier => 1f + damageBonus;
    public float HealthBonus => healthBonus;
    public float SpeedMultiplier => 1f + speedBonus;
    public float PowerMultiplier => 1f + powerBonus;
    public float CooldownMultiplier => Mathf.Max(0.25f, 1f - cooldownBonus);
    public float RangeMultiplier => 1f + rangeBonus;
    public float KnockbackMultiplier => 1f + knockbackBonus;

    /// Fraction of damage dealt returned as health. Capped so it cannot make you
    /// unkillable no matter how deep the tree goes.
    public float Lifesteal => Mathf.Min(0.5f, lifestealBonus);

    /// Added degrees on the swing arc, capped short of a full circle.
    public float ArcBonusDegrees => Mathf.Min(120f, arcBonus * 260f);

    void Awake()
    {
        Unlocked = new NetworkList<Node>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && Unlocked.Count == 0) Unlocked.Add(new Node(0));

        Unlocked.OnListChanged += _ => Recalculate();
        points.OnValueChanged += (_, __) => OnChanged?.Invoke();

        Recalculate();
    }

    // ------------------------------------------------------------- generation

    public static long ParentOf(long id) => id <= 0 ? -1 : (id - 1) / 3;

    public static long ChildOf(long id, int index) => id * 3 + 1 + index;

    public static int DepthOf(long id)
    {
        int depth = 0;
        while (id > 0) { id = (id - 1) / 3; depth++; }
        return depth;
    }

    // Larger than long.MaxValue, so it types as ulong unless cast - and
    // "long *= ulong" is ambiguous rather than a silent conversion.
    const long Mix = unchecked((long)0xff51afd7ed558ccdUL);

    int Hash(long id, int salt)
    {
        unchecked
        {
            long h = id * 6364136223846793005L + seed + salt * 1442695040888963407L;
            h ^= h >> 33;
            h *= Mix;
            h ^= h >> 33;
            return (int)h;
        }
    }

    float Roll(long id, int salt)
    {
        int h = Hash(id, salt);
        return ((h % 10000) + 10000) % 10000 / 10000f;
    }

    /// How many ways this node branches. Irregular on purpose - a uniform fan
    /// looks like a diagram, and the point is that it looks grown.
    public int BranchCount(long id)
    {
        if (id == 0) return 3;                       // the root opens three ways

        float roll = Roll(id, 11);
        if (roll < 0.34f) return 1;
        if (roll < 0.80f) return 2;
        return 3;
    }

    /// Position is rebuilt by walking the path from the root, so nothing is cached
    /// and any node in the infinite tree can be placed on demand.
    public Vector2 PositionOf(long id)
    {
        if (id <= 0) return Vector2.zero;

        // Collect the child indices from this node up to the root.
        var path = new List<int>();
        long walk = id;

        while (walk > 0)
        {
            path.Add((int)((walk - 1) % 3));
            walk = (walk - 1) / 3;
        }
        path.Reverse();

        Vector2 position = Vector2.zero;
        float angle = 0f;
        long current = 0;

        for (int step = 0; step < path.Count; step++)
        {
            int index = path[step];
            int branches = BranchCount(current);

            if (step == 0)
            {
                // Spread the first ring right around, so it grows outward not sideways.
                angle = 360f / Mathf.Max(1, branches) * index + Roll(current, 23) * 40f;
            }
            else
            {
                float offset = branches == 1
                    ? (Roll(current, 31) - 0.5f) * branchSpread * 0.6f
                    : Mathf.Lerp(-branchSpread, branchSpread, index / (float)(branches - 1));

                angle += offset + (Roll(current * 7 + index, 47) - 0.5f) * 14f;
            }

            long next = ChildOf(current, index);

            // Longer steps further out, so the middle stays legible when crowded.
            float length = branchLength * (0.85f + Roll(next, 59) * 0.4f)
                         * (1f + step * 0.045f);

            float radians = angle * Mathf.Deg2Rad;
            position += new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * length;

            current = next;
        }

        return position;
    }

    public Stat StatOf(long id)
    {
        int h = Hash(id, 3);
        return (Stat)(((h % StatCount) + StatCount) % StatCount);
    }

    public float MagnitudeOf(long id)
    {
        int depth = Mathf.Max(1, DepthOf(id));
        float value = (0.035f + 0.010f * depth) * (0.7f + Roll(id, 71) * 0.8f);

        if (StatOf(id) == Stat.MaxHealth) return Mathf.Round(value * 180f);
        return value;
    }

    public int CostOf(long id) => 1 + DepthOf(id) / 3;

    public bool IsUnlocked(long id) => unlockedLookup.Contains(id);

    /// Reachable means its parent is taken - the tree only grows from itself.
    public bool IsReachable(long id)
    {
        if (id < 0 || IsUnlocked(id)) return false;

        long parent = ParentOf(id);
        if (parent < 0) return false;

        // Nodes beyond the parent's branch count do not exist.
        int index = (int)((id - 1) % 3);
        if (index >= BranchCount(parent)) return false;

        return IsUnlocked(parent);
    }

    public bool CanAfford(long id) => points.Value >= CostOf(id);

    // ----------------------------------------------------------------- unlock

    [Rpc(SendTo.Server)]
    public void RequestUnlockRpc(long id)
    {
        if (!IsReachable(id)) return;

        int cost = CostOf(id);
        if (points.Value < cost) return;

        points.Value -= cost;
        Unlocked.Add(new Node(id));
    }

    public void ServerAwardKill()
    {
        if (!IsServer) return;

        pointFraction.Value += pointsPerKill;

        while (pointFraction.Value >= 1f)
        {
            pointFraction.Value -= 1f;
            points.Value += 1;
        }
    }

    public void ServerAwardPoints(int amount)
    {
        if (!IsServer || amount <= 0) return;
        points.Value += amount;
    }

    // ------------------------------------------------------------------ stats

    void Recalculate()
    {
        unlockedLookup.Clear();
        damageBonus = healthBonus = speedBonus = powerBonus = cooldownBonus = 0f;
        rangeBonus = knockbackBonus = lifestealBonus = arcBonus = 0f;

        for (int i = 0; i < Unlocked.Count; i++)
        {
            long id = Unlocked[i].id;
            unlockedLookup.Add(id);

            if (id == 0) continue;                   // the root grants nothing

            float m = MagnitudeOf(id);

            switch (StatOf(id))
            {
                case Stat.Damage: damageBonus += m; break;
                case Stat.MaxHealth: healthBonus += m; break;
                case Stat.MoveSpeed: speedBonus += m; break;
                case Stat.PowerGain: powerBonus += m; break;
                case Stat.Cooldown: cooldownBonus += m; break;
                case Stat.Range: rangeBonus += m; break;
                case Stat.Knockback: knockbackBonus += m * 2f; break;
                case Stat.Lifesteal: lifestealBonus += m * 0.35f; break;
                case Stat.ArcWidth: arcBonus += m; break;
            }
        }

        OnChanged?.Invoke();
    }

    /// Every unlocked node plus the nodes that touch them - the visible frontier.
    public void CollectVisible(List<long> into)
    {
        into.Clear();

        for (int i = 0; i < Unlocked.Count; i++)
        {
            long id = Unlocked[i].id;
            into.Add(id);

            int branches = BranchCount(id);
            for (int k = 0; k < branches; k++)
            {
                long child = ChildOf(id, k);
                if (!IsUnlocked(child)) into.Add(child);
            }
        }
    }

    public static Color ColourOf(Stat stat)
    {
        switch (stat)
        {
            case Stat.Damage: return new Color(0.95f, 0.42f, 0.40f);
            case Stat.MaxHealth: return new Color(0.45f, 0.88f, 0.52f);
            case Stat.MoveSpeed: return new Color(0.42f, 0.74f, 1f);
            case Stat.PowerGain: return new Color(0.76f, 0.50f, 1f);
            case Stat.Cooldown: return new Color(1f, 0.82f, 0.40f);
            case Stat.Range: return new Color(0.40f, 0.92f, 0.88f);
            case Stat.Knockback: return new Color(1f, 0.60f, 0.30f);
            case Stat.Lifesteal: return new Color(0.95f, 0.35f, 0.62f);
            default: return new Color(0.80f, 0.80f, 0.86f);
        }
    }

    public string DescribeNode(long id)
    {
        Stat stat = StatOf(id);
        float m = MagnitudeOf(id);

        if (stat == Stat.MaxHealth) return "+" + Mathf.RoundToInt(m) + " Max Health";

        if (stat == Stat.ArcWidth)
            return "+" + Mathf.RoundToInt(m * 260f) + " degree Swing Arc";

        string name;
        switch (stat)
        {
            case Stat.Damage: name = "Damage"; break;
            case Stat.MoveSpeed: name = "Move Speed"; break;
            case Stat.PowerGain: name = "POW Gain"; break;
            case Stat.Cooldown: name = "Cooldown Reduction"; break;
            case Stat.Range: name = "Attack Range"; break;
            case Stat.Knockback: return "+" + Mathf.RoundToInt(m * 200f) + "% Knockback";
            default: return "+" + Mathf.RoundToInt(m * 35f) + "% Lifesteal";
        }
        return "+" + Mathf.RoundToInt(m * 100f) + "% " + name;
    }
}
