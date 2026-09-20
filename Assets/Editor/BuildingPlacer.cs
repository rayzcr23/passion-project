using UnityEditor;
using UnityEngine;

/// Places a building as a sprite GameObject rather than painting it into a tilemap.
///
/// Tiny Swords buildings are 1.3 x 1.9 up to 3.2 x 2.6 world units, and the Grid
/// is 1 x 1, so none of them divide evenly into cells. Slicing them into tiles
/// chops them at arbitrary boundaries, which is why painted buildings look wrong.
/// A building is one object with one collider - it is not terrain.
public static class BuildingPlacer
{
    [MenuItem("Tools/Wanderers/Place Selected Building Here")]
    public static void Place()
    {
        Texture2D texture = Selection.activeObject as Texture2D;
        if (texture == null)
        {
            Debug.LogError("[Wanderers] Select a building PNG in the Project window first.");
            return;
        }

        string path = AssetDatabase.GetAssetPath(texture);
        Sprite sprite = PrepareSprite(path);
        if (sprite == null) { Debug.LogError("[Wanderers] Could not load a sprite from " + path); return; }

        Vector3 position = SceneView.lastActiveSceneView != null
            ? SceneView.lastActiveSceneView.pivot
            : Vector3.zero;
        position.z = 0f;

        var go = new GameObject(texture.name);
        go.transform.position = position;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;

        // Same order as characters on purpose: with Y-axis transparency sorting,
        // position decides who is in front, not a hand-tuned number.
        sr.sortingOrder = 1;

        // Collide with the base only, so players can walk behind the roof.
        float width = sprite.bounds.size.x;
        float height = sprite.bounds.size.y;
        float footprint = Mathf.Min(0.45f * height, 0.9f);

        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(width * 0.8f, footprint);
        col.offset = new Vector2(0f, footprint * 0.5f);

        Undo.RegisterCreatedObjectUndo(go, "Place Building");
        Selection.activeGameObject = go;

        Debug.Log(string.Format("[Wanderers] Placed {0} ({1:F1} x {2:F1} units) at {3}",
            texture.name, width, height, position));
    }

    [MenuItem("Tools/Wanderers/Place Selected Building Here", true)]
    static bool ValidatePlace()
    {
        return Selection.activeObject is Texture2D;
    }

    /// Buildings must pivot at their base, not their centre: Y-sorting compares
    /// transform positions, so a centre pivot sorts a building by its middle and
    /// players walk through the lower half.
    static Sprite PrepareSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;

        bool changed = false;

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }
        if (importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;
            changed = true;
        }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }
        if (importer.spritePixelsPerUnit != 100f)
        {
            importer.spritePixelsPerUnit = 100f;
            changed = true;
        }

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        if (settings.spriteAlignment != (int)SpriteAlignment.BottomCenter)
        {
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            importer.SetTextureSettings(settings);
            changed = true;
        }

        if (changed) importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
