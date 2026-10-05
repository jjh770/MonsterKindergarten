using UnityEngine;

// 기기 세이프에어리어(노치, 펀치홀, 제스처 바) 여백을 캔버스 좌표로 환산한 값.
public readonly struct SafeAreaInsets
{
    public float Left { get; }
    public float Right { get; }
    public float Top { get; }
    public float Bottom { get; }

    public SafeAreaInsets(float left, float right, float top, float bottom)
    {
        Left = left;
        Right = right;
        Top = top;
        Bottom = bottom;
    }
}

// 화면 가장자리에 붙는 UI가 노치나 제스처 바에 가리지 않도록 여백을 계산한다.
// 슬라임의 스폰·드래그 경계는 월드 좌표계라 이 계산과 무관하다.
public static class SafeAreaUtility
{
    // 화면 전체를 덮는 기준 RectTransform(캔버스 루트 또는 전체 스트레치 Rect)을 넘긴다.
    // 화면 픽셀 비율에 rect 크기만 곱하므로 하위 영역의 위치와 앵커는 반영하지 않는다.
    public static SafeAreaInsets GetInsets(RectTransform referenceRect)
    {
        if (referenceRect == null) return default;

        // 넓은 화면에서는 세로 영역(PortraitColumn) 밖도 쓸 수 없는 여백으로 본다. 노치 여백과 영역의
        // 바깥쪽 중 더 안쪽을 쓰므로, 이 계산을 쓰는 HUD와 서랍이 영역 안으로 들어온다.
        Rect safeArea = Screen.safeArea;
        Rect column = PortraitColumn.GetPixelRect();
        safeArea = Rect.MinMaxRect(
            Mathf.Max(safeArea.xMin, column.xMin),
            Mathf.Max(safeArea.yMin, column.yMin),
            Mathf.Min(safeArea.xMax, column.xMax),
            Mathf.Min(safeArea.yMax, column.yMax));
        float width = referenceRect.rect.width;
        float height = referenceRect.rect.height;

        return new SafeAreaInsets(
            ToCanvasInset(safeArea.xMin, Screen.width, width),
            ToCanvasInset(Screen.width - safeArea.xMax, Screen.width, width),
            ToCanvasInset(Screen.height - safeArea.yMax, Screen.height, height),
            ToCanvasInset(safeArea.yMin, Screen.height, height));
    }

    // 픽셀 단위 여백을 캔버스 좌표 비율로 환산한다.
    private static float ToCanvasInset(
        float pixelInset,
        int screenSize,
        float canvasSize)
    {
        if (screenSize <= 0 || canvasSize <= 0f) return 0f;

        return Mathf.Max(0f, pixelInset / screenSize * canvasSize);
    }
}
