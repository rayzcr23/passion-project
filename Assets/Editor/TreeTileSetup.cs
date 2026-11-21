using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Run via: Tools > Setup Tree Tiles
/// Creates sliced Base + Top sprites from Tree.png and generates
/// Tile assets ready to paint on your tilemaps.
/// </summary>
public static class TreeTileSetup
{
    // Path to the tree sprite sheet (relative to Assets/)
    const string TREE_PNG_PATH = "Assets/Tiny Swords/Tiny Swords (Update 010)/Resources/Trees/Tree.png";

    // Where the generated tile assets will be saved
    const string OUTPUT_PATH = "Assets/Tiny Swords/Tiny Swords (Update 010)/Resources/Trees/Tiles";

    // Sprite sheet layout
    const int SHEET_COLS = 4;
    const int SHEET_ROWS = 2;
    const int CELL_W = 192;
    const int CELL_H = 192;
    const int SPLIT_Y = 96; // pixels from bottom where base/top divides (half cell height)

    // Total number of full trees (skip stump at bottom-left of row 3)
    const int TREE_COUNT = 6;

    [MenuItem("Tools/Setup Tree Tiles")]
    public static void Run()
    {
        // ── 1. Make texture readable ──────────────────────────────────────
        TextureImporter importer = AssetImporter.GetAtPath(TREE_PNG_PATH) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[TreeTileSetup] Could not find texture at: {TREE_PNG_PATH}");
            return;
        }

        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.isReadable = true;
        importer.filterMode = FilterMode.Point;

        // ── 2. Define sprite rects ────────────────────────────────────────
        var spriteMetaDatas = new List<SpriteMetaData>();

        // Sheet origin is bottom-left in Unity, top-left in image coordinates.
        // Image height = SHEET_ROWS * CELL_H = 384 px (visible tree area)
        // Full PNG may be taller — we anchor from the bottom.
        // We iterate row 0 = top image row, row 1 = bottom image row.
        int pngHeight = SHEET_ROWS * CELL_H; // 384 — actual PNG is 576 but trees only fill top 384

        for (int treeIndex = 0; treeIndex < TREE_COUNT; treeIndex++)
        {
            int col = treeIndex % SHEET_COLS;
            int row = treeIndex / SHEET_COLS;

            // Convert image-space top-left of cell to Unity's bottom-left rect coords.
            // PNG height we care about = 576 (full sheet).
            // Image row 0 is at y_image = 0 (top). Unity rect y is from bottom.
            // Unity Y = fullPNGHeight - (imageRow * CELL_H) - CELL_H
            int fullPNG = 576;
            int unityY = fullPNG - (row * CELL_H) - CELL_H; // bottom of this cell in Unity coords

            // ── Base tile (bottom SPLIT_Y pixels of the cell) ────────────
            var baseMeta = new SpriteMetaData
            {
                name      = $"Tree_{treeIndex + 1}_Base",
                rect      = new Rect(col * CELL_W, unityY, CELL_W, SPLIT_Y),
                pivot     = new Vector2(0.5f, 0f),   // bottom-centre
                alignment = (int)SpriteAlignment.BottomCenter
            };
            spriteMetaDatas.Add(baseMeta);

            // ── Top tile (upper SPLIT_Y pixels of the cell) ───────────────
            var topMeta = new SpriteMetaData
            {
                name      = $"Tree_{treeIndex + 1}_Top",
                rect      = new Rect(col * CELL_W, unityY + SPLIT_Y, CELL_W, SPLIT_Y),
                pivot     = new Vector2(0.5f, 0f),   // bottom-centre pivot so it sits on top of base
                alignment = (int)SpriteAlignment.BottomCenter
            };
            spriteMetaDatas.Add(topMeta);
        }

        importer.spritesheet = spriteMetaDatas.ToArray();
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        AssetDatabase.Refresh();

        // ── 3. Create output folder ───────────────────────────────────────
        if (!AssetDatabase.IsValidFolder(OUTPUT_PATH))
        {
            string parent = Path.GetDirectoryName(OUTPUT_PATH).Replace('\\', '/');
            string folderName = Path.GetFileName(OUTPUT_PATH);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        // ── 4. Load all sprites and create Tile assets ────────────────────
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(TREE_PNG_PATH);

        int tilesCreated = 0;
        foreach (Object asset in allAssets)
        {
            Sprite sprite = asset as Sprite;
            if (sprite == null) continue;

            bool isBase = sprite.name.EndsWith("_Base");
            bool isTop  = sprite.name.EndsWith("_Top");
            if (!isBase && !isTop) continue;

            string tilePath = $"{OUTPUT_PATH}/{sprite.name}_Tile.asset";

            // Reuse existing tile if already created
            Tile existingTile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            Tile tile = existingTile != null ? existingTile : ScriptableObject.CreateInstance<Tile>();

            tile.sprite       = sprite;
            tile.color        = Color.white;
            // Base tiles get a collider; Top tiles do not
            tile.colliderType = isBase ? Tile.ColliderType.Sprite : Tile.ColliderType.None;

            if (existingTile == null)
            {
                AssetDatabase.CreateAsset(tile, tilePath);
            }
            else
            {
                EditorUtility.SetDirty(tile);
            }

            tilesCreated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[TreeTileSetup] Done! Created {tilesCreated} tile assets in: {OUTPUT_PATH}\n" +
                  "Now open Window > 2D > Tile Palette, drag the new tiles in, and:\n" +
                  "  • Paint *_Base tiles on your 'collision low' tilemap\n" +
                  "  • Paint *_Top tiles on your 'collision high' tilemap (one tile above the base)");

        // Show a dialog so it's obvious it worked
        EditorUtility.DisplayDialog(
            "Tree Tiles Ready!",
            $"Created {tilesCreated} tile assets in:\n{OUTPUT_PATH}\n\n" +
            "Next steps:\n" +
            "1. Open Window > 2D > Tile Palette\n" +
            "2. Drag the new *_Base and *_Top tiles into your palette\n" +
            "3. Paint *_Base tiles on 'collision low'\n" +
            "4. Paint *_Top tiles one tile above on 'collision high'",
            "Got it!");
    }
}
