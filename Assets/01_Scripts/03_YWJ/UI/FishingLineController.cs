using UnityEngine;

public class FishingLineController : MonoBehaviour
{
    [SerializeField] private RectTransform line;
    [SerializeField] private RectTransform rodPoint;
    [SerializeField] private RectTransform mouthPoint;

    [SerializeField] private float thickness = 6f;

    private void Reset()
    {
        line = GetComponent<RectTransform>();
    }

    private void LateUpdate()
    {
        if (line == null || rodPoint == null || mouthPoint == null)
            return;

        RectTransform parent = line.parent as RectTransform;

        Vector2 start = WorldToLocal(parent, rodPoint.position);
        Vector2 end = WorldToLocal(parent, mouthPoint.position);

        Vector2 dir = end - start;

        line.anchoredPosition = (start + end) * 0.5f;
        line.sizeDelta = new Vector2(thickness, dir.magnitude);

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        line.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private Vector2 WorldToLocal(RectTransform parent, Vector3 worldPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent,
            RectTransformUtility.WorldToScreenPoint(null, worldPos),
            null,
            out Vector2 localPos
        );

        return localPos;
    }
}