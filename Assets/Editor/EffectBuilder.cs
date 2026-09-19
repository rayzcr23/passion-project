using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Builds the one-shot death effect from the Tiny Swords explosion sheet.
///
/// No Animator or controller: the prefab is a SpriteRenderer plus SpriteFlipbook,
/// which steps the frames and deletes itself. A cosmetic effect needs no
/// NetworkObject either - every client spawns its own.
public static class EffectBuilder
{
    const string Sheet = "Assets/Tiny Swords/Tiny Swords (Update 010)/Effects/Explosion/Explosions.png";
    const string PrefabPath = "Assets/PREFABS/DeathPoof.prefab";

    [MenuItem("Tools/Wanderers/Build Death Effect")]
    public static void Build()
    {
        if (!CharacterAnimationBuilder.SliceSheet(Sheet))
        {
            Debug.LogError("[Wanderers] Could not slice " + Sheet);
            return;
        }
        AssetDatabase.Refresh();

        List<Sprite> frames = SortedSprites(Sheet);
        if (frames.Count == 0) { Debug.LogError("[Wanderers] No sprites after slicing"); return; }

        bool isNew = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
        GameObject go = isNew ? new GameObject("DeathPoof")
                              : PrefabUtility.LoadPrefabContents(PrefabPath);

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sortingOrder = 20;

        var book = go.GetComponent<SpriteFlipbook>();
        if (book == null) book = go.AddComponent<SpriteFlipbook>();
        book.frames = frames.ToArray();
        book.framesPerSecond = 16f;
        book.loop = false;
        book.destroyWhenDone = true;
        book.fadeOutFraction = 0.3f;

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);

        if (isNew) Object.DestroyImmediate(go);
        else PrefabUtility.UnloadPrefabContents(go);

        AssetDatabase.SaveAssets();
        Debug.Log("[Wanderers] Built DeathPoof with " + frames.Count + " frames");
    }

    static List<Sprite> SortedSprites(string path)
    {
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
        var sprites = new List<Sprite>();
        foreach (Object o in all) { var s = o as Sprite; if (s != null) sprites.Add(s); }

        sprites.Sort((a, b) => Index(a.name).CompareTo(Index(b.name)));
        return sprites;
    }

    static int Index(string name)
    {
        int u = name.LastIndexOf('_');
        int n;
        return (u >= 0 && int.TryParse(name.Substring(u + 1), out n)) ? n : 0;
    }
}
