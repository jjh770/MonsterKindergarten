using UnityEngine;

// 가로로 넓은 화면(태블릿, 폴더블)에서 게임을 9:19.5 세로 영역 하나로 고정하기 위한 계산.
//
// 이 게임의 레이아웃은 전부 세로 화면을 전제로 한다. 넓은 화면에서 레이아웃을 늘리면 HUD가
// 양 끝으로 벌어지고 슬라임이 가운데 좁은 열에만 몰리므로, 화면 가운데에 세로 영역만 쓰고
// 양옆은 비운다.
//
// 적용 기준은 9:16보다 넓은 화면이다. 그보다 좁은 화면(16:9 폰과 18:9~21:9 폰)은 지금처럼
// 화면을 꽉 채운다. 기준과 영역 비율을 한곳에 두어 카메라, 캔버스, 세이프에어리어가 같은
// 영역을 보게 한다.
public static class PortraitColumn
{
    // 세로 영역의 가로 대 세로 비율. CanvasScaler의 기준 해상도(1080x2340)와 같은 비율이다.
    public const float ColumnAspect = 9f / 19.5f;

    // 이 비율(가로/세로)보다 넓은 화면에만 영역을 적용한다.
    public const float ActivateAspect = 9f / 16f;

    public static bool IsActive => IsActiveFor(Screen.width, Screen.height);

    public static bool IsActiveFor(int screenWidth, int screenHeight)
    {
        if (screenWidth <= 0 || screenHeight <= 0) return false;

        return (float)screenWidth / screenHeight > ActivateAspect;
    }

    // 화면 픽셀 좌표로 본 영역. 적용하지 않으면 화면 전체다.
    public static Rect GetPixelRect()
    {
        return GetPixelRect(Screen.width, Screen.height);
    }

    public static Rect GetPixelRect(int screenWidth, int screenHeight)
    {
        if (!IsActiveFor(screenWidth, screenHeight))
        {
            return new Rect(0f, 0f, screenWidth, screenHeight);
        }

        float width = screenHeight * ColumnAspect;
        return new Rect((screenWidth - width) * 0.5f, 0f, width, screenHeight);
    }

    // Camera.rect에 쓰는 0~1 비율의 영역.
    public static Rect GetViewportRect()
    {
        return GetViewportRect(Screen.width, Screen.height);
    }

    public static Rect GetViewportRect(int screenWidth, int screenHeight)
    {
        if (!IsActiveFor(screenWidth, screenHeight))
        {
            return new Rect(0f, 0f, 1f, 1f);
        }

        float fraction = screenHeight * ColumnAspect / screenWidth;
        return new Rect((1f - fraction) * 0.5f, 0f, fraction, 1f);
    }
}
