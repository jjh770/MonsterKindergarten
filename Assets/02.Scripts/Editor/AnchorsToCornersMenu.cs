using UnityEditor;
using UnityEngine;

// Scene 뷰에서 끌어 맞춘 UI 요소의 자리를 앵커로 옮긴다. 끌기는 오프셋만 바꾸므로 화면 비율이 바뀌면
// 자리가 어긋나는데, 이 메뉴는 지금 보이는 사각형 그대로 앵커를 네 모서리에 맞추고 오프셋을 0으로 만든다.
// 로그인 화면 슬라임처럼 배경 비율로만 자리를 잡아야 하는 요소에 쓴다. 여러 개를 골라도 된다.
public static class AnchorsToCornersMenu
{
    private const string MenuPath = "Tools/UI/Anchors to Corners %#&a";

    [MenuItem(MenuPath)]
    private static void AnchorsToCorners()
    {
        int changed = 0;
        foreach (GameObject selected in Selection.gameObjects)
        {
            if (!TryGetRects(selected, out RectTransform rect, out RectTransform parent)) continue;

            Vector2 parentSize = parent.rect.size;
            if (parentSize.x <= 0f || parentSize.y <= 0f)
            {
                Debug.LogWarning($"부모의 크기가 0이라 앵커를 계산할 수 없어요. : {selected.name}", selected);
                continue;
            }

            Undo.RecordObject(rect, "Anchors to Corners");
            Vector2 anchorMin = rect.anchorMin + Divide(rect.offsetMin, parentSize);
            Vector2 anchorMax = rect.anchorMax + Divide(rect.offsetMax, parentSize);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            EditorUtility.SetDirty(rect);
            changed++;
        }

        Debug.Log($"앵커를 모서리에 맞췄어요. : {changed}개");
    }

    [MenuItem(MenuPath, true)]
    private static bool CanAnchorsToCorners()
    {
        foreach (GameObject selected in Selection.gameObjects)
        {
            if (TryGetRects(selected, out _, out _)) return true;
        }

        return false;
    }

    private static bool TryGetRects(GameObject selected, out RectTransform rect, out RectTransform parent)
    {
        rect = selected != null ? selected.transform as RectTransform : null;
        parent = rect != null ? rect.parent as RectTransform : null;
        return rect != null && parent != null;
    }

    private static Vector2 Divide(Vector2 value, Vector2 size) => new Vector2(value.x / size.x, value.y / size.y);
}
