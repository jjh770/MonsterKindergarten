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
    // referenceRect의 크기를 기준으로 네 방향 여백을 환산한다.
    // 캔버스 루트를 넘기면 화면 전체 기준, 하위 RectTransform을 넘기면 그 크기 기준이 된다.
    public static SafeAreaInsets GetInsets(RectTransform referenceRect)
    {
        if (referenceRect == null) return default;

        Rect safeArea = Screen.safeArea;
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
