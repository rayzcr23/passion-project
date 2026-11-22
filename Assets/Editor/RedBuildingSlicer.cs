using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Creates real Unity 6 sub-sprite assets for every Red building sheet.
/// </summary>
[InitializeOnLoad]
public static class RedBuildingSlicer
{
    const string Folder =
        "Assets/Tiny Swords (Free Pack)/Tiny Swords (Free Pack)/Buildings/Red Buildings";
    const int CellSize = 64;
    const string SessionKey = "Wanderers.RedBuildingsSliced.V1";

    static readonly string[] BuildingNames =
    {
        "Archery",
        "Barracks",
        "Castle",
        "House1",
        "House2",
        "House3",
        "Monastery",
        "Tower"
    };

    static RedBuildingSlicer()
    {
        EditorApplication.delayCall += RunOnceAfterCompilation;
    }

    static void RunOnceAfterCompilation()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        SliceAll(false);
    }

    [MenuItem("Tools/Slice Red Buildings (64x64)")]
    public static void SliceFromMenu()
    {
        SliceAll(true);
    }

    public static void SliceFromBatch()
    {
        SliceAll(false);
    }

    static void SliceAll(bool showDialog)
    {
        int totalSprites = 0;
        var results = new List<string>();

        foreach (string buildingName in BuildingNames)
        {
            string path = $"{Folder}/{buildingName}.png";
            int count = SliceTexture(path, buildingName);
            totalSprites += count;
            results.Add($"{buildingName}: {count}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary = string.Join(", ", results);
        Debug.Log($"[RedBuildingSlicer] Created {totalSprites} real 64x64 sprites. {summary}");

        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                "Red Buildings Sliced",
                $"Created {totalSprites} sprites at 64x64.\n\n{string.Join("\n", results)}",
                "OK");
        }
    }

    static int SliceTexture(string path, string baseName)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[RedBuildingSlicer] Texture not found: {path}");
            return 0;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = CellSize;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null || texture.width % CellSize != 0 || texture.height % CellSize != 0)
        {
            Debug.LogError($"[RedBuildingSlicer] {path} is not divisible into {CellSize}x{CellSize} cells.");
            return 0;
        }

        var factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider =
            factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        var existingIds = provider.GetSpriteRects()
            .Where(sprite => !string.IsNullOrEmpty(sprite.name))
            .GroupBy(sprite => sprite.name)
            .ToDictionary(group => group.Key, group => group.First().spriteID);

        int columns = texture.width / CellSize;
        int rows = texture.height / CellSize;
        var spriteRects = new List<SpriteRect>(columns * rows);

        // Number left-to-right, top-to-bottom to match the other building sets.
        int index = 0;
        for (int row = 0; row < rows; row++)
        {
            int y = texture.height - ((row + 1) * CellSize);
            for (int column = 0; column < columns; column++)
            {
                string spriteName = $"{baseName}_{index++}";
                GUID spriteId = existingIds.TryGetValue(spriteName, out GUID existingId)
                    ? existingId
                    : GUID.Generate();

                spriteRects.Add(new SpriteRect
                {
                    name = spriteName,
                    rect = new Rect(column * CellSize, y, CellSize, CellSize),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    spriteID = spriteId
                });
            }
        }

        provider.SetSpriteRects(spriteRects.ToArray());

        ISpriteNameFileIdDataProvider nameProvider =
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        nameProvider.SetNameFileIdPairs(
            spriteRects.Select(sprite => new SpriteNameFileIdPair(sprite.name, sprite.spriteID)));

        provider.Apply();
        importer.SaveAndReimport();

        int importedCount = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count();
        if (importedCount != spriteRects.Count)
        {
            Debug.LogError(
                $"[RedBuildingSlicer] {baseName} expected {spriteRects.Count} sprites but imported {importedCount}.");
        }

        return importedCount;
    }
}
