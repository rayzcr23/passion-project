using Unity.Netcode;
using UnityEngine;

/// Manages the blacksmith NPC in the north-western village plaza.
///
/// Quest chain:
///   [0] Blacksmith_IronRequest  — first talk: asks for 20 iron, starts quest_iron_collection
///   [1] Blacksmith_SwordUpgraded — unlocks after quest_iron_collection is complete;
///                                   upgrades sword damage and points to upcoming boss
///
/// The NPC auto-spawns on scene load if no GameObject named "Blacksmith" is found
/// among inactive or active objects.
public class BlacksmithForgeNPC : MonoBehaviour
{
    [Header("NPC Settings")]
    public string npcId = "blacksmith_forge";

    [Tooltip("World-space position for the auto-spawned blacksmith. " +
             "Matches the house-front spot BlacksmithVillageBuilder uses.")]
    public Vector3 villagePosition = new Vector3(47f, 7.5f, 0f);

    [Header("Sword Upgrade")]
    [Tooltip("Bonus damage added to the local player's MeleeArcAbility on quest completion.")]
    public int swordDamageBonus = 15;

    bool postBossDialogueApplied;
    DialogueNPC forgeNpc;
    DialogueData postBossDialogue;

    // ------------------------------------------------------------------ static

    static BlacksmithForgeNPC _instance;
    static bool chipmunkBossDefeated;
    static bool swordUpgradeGranted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetStoryState()
    {
        _instance = null;
        chipmunkBossDefeated = false;
        swordUpgradeGranted = false;
    }

    void Awake()
    {
        _instance = this;
        EnsureNpc();
        DialogueNPC.ConversationFinishedByNpc += OnConversationFinished;
    }

    void Update()
    {
        if (chipmunkBossDefeated && !postBossDialogueApplied)
            ApplyPostBossDialogue();
    }

    void OnDestroy()
    {
        DialogueNPC.ConversationFinishedByNpc -= OnConversationFinished;
        if (_instance == this) _instance = null;
    }

    // Called by BlacksmithVillageEvent after the village is ready.
    public static BlacksmithForgeNPC EnsureInstance()
    {
        if (_instance != null) return _instance;

        // Look for an existing one (including inactive)
        _instance = Object.FindFirstObjectByType<BlacksmithForgeNPC>(
            FindObjectsInactive.Include);

        if (_instance == null)
        {
            var go = new GameObject("BlacksmithForgeNPC");
            _instance = go.AddComponent<BlacksmithForgeNPC>();
            // Awake runs automatically and calls EnsureNpc()
        }

        return _instance;
    }

    /// Called on every client when the Chipmunk Boss defeat RPC arrives.
    /// From this point onward, speaking to the forge blacksmith advances the story.
    public static void NotifyChipmunkBossDefeated()
    {
        chipmunkBossDefeated = true;

        // The scene contains several blacksmith NPCs. Update each one so the
        // player hears the new directions whichever copy they interact with.
        var blacksmiths = Object.FindObjectsByType<BlacksmithForgeNPC>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (blacksmiths.Length == 0)
        {
            BlacksmithForgeNPC blacksmith = EnsureInstance();
            if (blacksmith != null) blacksmith.ApplyPostBossDialogue();
            return;
        }

        foreach (var blacksmith in blacksmiths)
            blacksmith.ApplyPostBossDialogue();
    }

    // ------------------------------------------------------------------ setup

    void EnsureNpc()
    {
        // A controller attached to a blacksmith must drive that same NPC.
        // Searching the whole scene first can bind several controllers to
        // one copy while another overlapping copy keeps its old dialogue.
        DialogueNPC npc = GetComponent<DialogueNPC>();
        var all = Object.FindObjectsByType<DialogueNPC>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (npc == null)
        {
            float bestDistance = float.PositiveInfinity;
            foreach (var n in all)
            {
                string nm = n.gameObject.name.ToLower();
                if (!nm.Contains("blacksmith") || nm.Contains("village") || nm.Contains("event")) continue;

                float distance = Vector2.Distance(n.transform.position, villagePosition);
                if (distance > 8f || distance >= bestDistance) continue;

                npc = n;
                bestDistance = distance;
            }
        }

        if (npc == null)
        {
            var go = new GameObject("Blacksmith");
            go.transform.position = villagePosition;
            go.transform.localScale = Vector3.one * 1.2f;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;
            sr.sprite = ResolveSprite();

            npc = go.AddComponent<DialogueNPC>();
            Debug.Log($"[Wanderers] Blacksmith NPC spawned at {villagePosition}.");
        }

        npc.gameObject.SetActive(true);
        forgeNpc = npc;

        // Assign conversation chain
        var request  = Resources.Load<DialogueData>("STORY/Blacksmith_IronRequest");
        var upgraded = Resources.Load<DialogueData>("STORY/Blacksmith_SwordUpgraded");

        npc.npcId = npcId;

        if (request != null && upgraded != null)
        {
            npc.conversations = new[] { request, upgraded };
            npc.dialogue = null;
        }
        else if (request != null)
        {
            npc.dialogue = request;
            Debug.LogWarning("[Wanderers] Blacksmith_SwordUpgraded not found in Resources/STORY.");
        }
        else
        {
            Debug.LogWarning("[Wanderers] Blacksmith_IronRequest not found in Resources/STORY.");
        }


        if (chipmunkBossDefeated)
            ApplyPostBossDialogue();
    }

    void ApplyPostBossDialogue()
    {
        if (forgeNpc == null)
            EnsureNpc();
        if (forgeNpc == null) return;

        if (postBossDialogue == null)
        {
            postBossDialogue = ScriptableObject.CreateInstance<DialogueData>();
            postBossDialogue.name = "Blacksmith_WitchcraftVillageGuide";
            postBossDialogue.speakerName = "Blacksmith";
            postBossDialogue.lines = new[]
            {
                "You beat the Chipmunk Boss! I knew that reforged blade would serve you well.",
                "Your next destination is Witchcraft Village.",
                "Head east from here and keep following the road. Witchcraft Village lies that way."
            };
            postBossDialogue.inProgressLines = System.Array.Empty<string>();
            postBossDialogue.completedLines = System.Array.Empty<string>();
        }

        forgeNpc.conversations = null;
        forgeNpc.dialogue = postBossDialogue;
        postBossDialogueApplied = true;
    }

    // ------------------------------------------------------------------ events

    void OnConversationFinished(DialogueNPC speaker, DialogueData data)
    {
        if (speaker != forgeNpc) return;
        if (data == null) return;
        if (!data.name.Contains("Blacksmith_SwordUpgraded")) return;
        if (swordUpgradeGranted) return;

        QuestManager qm = QuestManager.Instance;
        if (qm == null || !qm.IsComplete("quest_iron_collection")) return;

        swordUpgradeGranted = true;
        UpgradeSword();
    }

    void UpgradeSword()
    {
        // Find the local player's loadout and boost melee damage.
        var loadouts = Object.FindObjectsByType<AbilityLoadout>(FindObjectsSortMode.None);
        foreach (var loadout in loadouts)
        {
            var netObj = loadout.GetComponent<NetworkObject>();
            if (netObj != null && !netObj.IsOwner) continue;

            var melee = loadout.m1 as MeleeArcAbility;
            if (melee == null) continue;

            melee.damage += swordDamageBonus;

            // Flash screen gold to celebrate
            ImpactFrameEffect.TriggerCustom(new Color(1f, 0.75f, 0.1f), 0.4f, 0.9f, true);

            BlacksmithVillageEvent eventForBoss = BlacksmithVillageEvent.EnsureVillageInstance();
            if (eventForBoss != null)
            {
                Vector3 spawnFrom = forgeNpc != null ? forgeNpc.transform.position : transform.position;
                eventForBoss.TriggerChipmunkBossAfterSwordUpgrade(spawnFrom);
            }

            // Use BlacksmithVillageEvent's banner system if available
            var village = BlacksmithVillageEvent.Instance;
            if (village != null)
                village.ShowBannerPublic(
                    $"⚔️ SWORD UPGRADED! +{swordDamageBonus} ATK! " +
                    "The blacksmith says: 'A powerful boss lurks to the north — go find it!'");

            Debug.Log($"[Wanderers] Sword upgraded! Melee damage → {melee.damage}.");
            if (eventForBoss != null)
                eventForBoss.ShowBannerPublic("A CHIPMUNK BOSS BURSTS OUT! Watch for chipmunk_1, then dodge the slash!");

            break;
        }
    }

    // ------------------------------------------------------------------ util

    static Sprite ResolveSprite()
    {
        var all = Resources.FindObjectsOfTypeAll<Sprite>();
        // Prefer a red pawn (blacksmith colour)
        foreach (var s in all)
            if (s != null && s.name.ToLower().Contains("pawn") &&
                s.name.ToLower().Contains("red")) return s;
        // Fall back to any pawn
        foreach (var s in all)
            if (s != null && s.name.ToLower().Contains("pawn")) return s;
        return null;
    }
}
