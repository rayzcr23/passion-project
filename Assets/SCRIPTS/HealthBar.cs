using UnityEngine;

/// A world-space health bar that builds its own sprites at runtime, so it needs
/// no Canvas, no art and no prefab setup - just add it to a character.
///
/// It survives the sprite flip because Movement flips SpriteRenderer.flipX rather
/// than the transform scale; a mirrored transform would flip the bar too.
[DisallowMultipleComponent]
public class HealthBar : MonoBehaviour
{
    public Health health;

    [Header("Placement")]
    public Vector3 offset = new Vector3(0f, 0.95f, 0f);
    public float width = 0.9f;
    public float height = 0.12f;

    [Header("Colours")]
    public Color fillColor = new Color(0.30f, 0.85f, 0.35f);
    public Color lowColor = new Color(0.90f, 0.25f, 0.25f);
    public Color backColor = new Color(0.08f, 0.08f, 0.10f, 0.85f);

    [Tooltip("Enemies usually hide theirs until hurt; keep a player's always on.")]
    public bool hideWhenFull = false;

    [Tooltip("Below this fraction the bar turns red.")]
    public float lowThreshold = 0.3f;

    Transform root;
    SpriteRenderer back;
    SpriteRenderer fill;
    SpriteRenderer ownerSprite;

    static Sprite whitePixel;

    void Awake()
    {
        if (health == null) health = GetComponentInParent<Health>();
        ownerSprite = GetComponent<SpriteRenderer>();

        Build();
    }

    void OnEnable()
    {
        if (health != null)
        {
            health.OnChanged += Redraw;
            Redraw(health.Current, health.Max);
        }
    }

    void OnDisable()
    {
        if (health != null) health.OnChanged -= Redraw;
    }

    void LateUpdate()
    {
        if (root == null) return;

        // Cancel any parent rotation so the bar stays level.
        root.rotation = Quaternion.identity;

        // The elevation triggers move characters between sorting orders, so follow.
        if (ownerSprite != null)
        {
            back.sortingOrder = ownerSprite.sortingOrder + 1;
            fill.sortingOrder = ownerSprite.sortingOrder + 2;
        }
    }

    void Build()
    {
        root = new GameObject("HealthBar").transform;
        root.SetParent(transform, false);
        root.localPosition = offset;

        back = MakeBar("Back", backColor, width);
        fill = MakeBar("Fill", fillColor, width);
    }

    SpriteRenderer MakeBar(string name, Color color, float w)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);

        // Pivot sits on the left edge, so scaling x drains the bar rightward.
        go.transform.localPosition = new Vector3(-width * 0.5f, 0f, 0f);
        go.transform.localScale = new Vector3(w, height, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = WhitePixel();
        sr.color = color;
        return sr;
    }

    void Redraw(int current, int max)
    {
        if (fill == null || root == null) return;

        float fraction = max <= 0 ? 0f : Mathf.Clamp01(current / (float)max);

        Vector3 scale = fill.transform.localScale;
        scale.x = width * fraction;
        fill.transform.localScale = scale;

        fill.color = fraction <= lowThreshold ? lowColor : fillColor;

        bool visible = !(hideWhenFull && fraction >= 1f) && fraction > 0f;
        root.gameObject.SetActive(visible);
    }

    /// One shared 1x1 sprite for every bar in the game.
    static Sprite WhitePixel()
    {
        if (whitePixel != null) return whitePixel;

        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.filterMode = FilterMode.Point;
        tex.Apply();

        whitePixel = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0f, 0.5f), 1f);
        whitePixel.name = "HealthBarPixel";
        return whitePixel;
    }
}
