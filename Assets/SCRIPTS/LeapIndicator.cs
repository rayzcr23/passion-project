using UnityEngine;

/// Ground indicator rendered while charging Executioner's Leap.
///
/// Draws a targeting trajectory line from the player to the cursor, an outer ring
/// showing the final slam damage radius (which expands as charge builds), an inner
/// charging ring that fills outward, and a center impact crosshair.
public class LeapIndicator : MonoBehaviour
{
    LineRenderer outerRing;
    LineRenderer innerRing;
    LineRenderer trajectoryLine;
    LineRenderer[] crosshairs = new LineRenderer[2];

    static Material indicatorMat;

    public static LeapIndicator Create(int sortingOrder = 35)
    {
        var go = new GameObject("LeapIndicator");
        var indicator = go.AddComponent<LeapIndicator>();
        indicator.Init(sortingOrder);
        go.SetActive(false);
        return indicator;
    }

    static Material GetMaterial()
    {
        if (indicatorMat == null)
        {
            Shader s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Hidden/Internal-Colored");
            indicatorMat = new Material(s);
        }
        return indicatorMat;
    }

    void Init(int sortingOrder)
    {
        outerRing = CreateLine("OuterRing", sortingOrder, 0.07f, true);
        innerRing = CreateLine("InnerRing", sortingOrder + 1, 0.05f, true);
        trajectoryLine = CreateLine("Trajectory", sortingOrder - 1, 0.045f, false);

        crosshairs[0] = CreateLine("CrossX", sortingOrder + 2, 0.04f, false);
        crosshairs[1] = CreateLine("CrossY", sortingOrder + 2, 0.04f, false);
    }

    LineRenderer CreateLine(string lineName, int sortingOrder, float width, bool loop)
    {
        var child = new GameObject(lineName);
        child.transform.SetParent(transform, false);

        var lr = child.AddComponent<LineRenderer>();
        lr.material = GetMaterial();
        lr.useWorldSpace = true;
        lr.loop = loop;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.sortingOrder = sortingOrder;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;
        return lr;
    }

    public void UpdateVisual(Vector3 origin, Vector3 target, float radius, float chargeRatio, bool isFull, Color baseColor, Color readyColor)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        Color activeColor = isFull ? readyColor : Color.Lerp(baseColor, readyColor, chargeRatio);
        if (isFull)
        {
            // Rapid high-energy pulse when fully charged
            float pulse = (Mathf.Sin(Time.time * 20f) + 1f) * 0.5f;
            activeColor = Color.Lerp(readyColor, Color.white, pulse * 0.4f);
        }

        // Trajectory line from player to landing spot
        Color startCol = activeColor;
        startCol.a *= 0.3f;
        Color endCol = activeColor;
        endCol.a *= 0.85f;

        trajectoryLine.positionCount = 2;
        trajectoryLine.SetPosition(0, origin);
        trajectoryLine.SetPosition(1, target);
        trajectoryLine.startColor = startCol;
        trajectoryLine.endColor = endCol;

        // Outer AoE slam radius
        int segments = 36;
        outerRing.positionCount = segments;
        float step = (Mathf.PI * 2f) / segments;
        float aspectY = 0.85f; // Flattens slightly for top-down perspective

        for (int i = 0; i < segments; i++)
        {
            float angle = i * step;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius * aspectY;
            outerRing.SetPosition(i, target + new Vector3(x, y, 0f));
        }
        outerRing.startColor = activeColor;
        outerRing.endColor = activeColor;

        // Inner charge ring expanding from 0 to radius
        float innerRadius = radius * Mathf.Max(0.04f, chargeRatio);
        innerRing.positionCount = segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * step;
            float x = Mathf.Cos(angle) * innerRadius;
            float y = Mathf.Sin(angle) * innerRadius * aspectY;
            innerRing.SetPosition(i, target + new Vector3(x, y, 0f));
        }

        Color innerCol = Color.Lerp(baseColor, Color.white, chargeRatio * 0.7f);
        innerCol.a = Mathf.Lerp(0.35f, 0.95f, chargeRatio);
        innerRing.startColor = innerCol;
        innerRing.endColor = innerCol;

        // Center crosshair marker
        float crossSize = 0.22f;
        crosshairs[0].positionCount = 2;
        crosshairs[0].SetPosition(0, target + new Vector3(-crossSize, 0f, 0f));
        crosshairs[0].SetPosition(1, target + new Vector3(crossSize, 0f, 0f));
        crosshairs[0].startColor = activeColor;
        crosshairs[0].endColor = activeColor;

        crosshairs[1].positionCount = 2;
        crosshairs[1].SetPosition(0, target + new Vector3(0f, -crossSize * aspectY, 0f));
        crosshairs[1].SetPosition(1, target + new Vector3(0f, crossSize * aspectY, 0f));
        crosshairs[1].startColor = activeColor;
        crosshairs[1].endColor = activeColor;
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Dismiss()
    {
        if (gameObject != null) Destroy(gameObject);
    }
}
