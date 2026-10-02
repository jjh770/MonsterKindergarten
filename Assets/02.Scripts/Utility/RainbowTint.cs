using UnityEngine;

// 특별한 슬라임을 보여 주는 UI가 함께 쓰는 무지개 색이다. 색상환을 시간에 따라 돌린다.
// 같은 색을 어디서든 같은 박자로 내야 가챠 결과와 도감이 서로 다른 반짝임으로 보이지 않는다.
public static class RainbowTint
{
    private const float CyclesPerSecond = 0.9f;
    private const float Saturation = 0.6f;

    // 순수한 무지개 색. 포털이나 빛처럼 색 자체를 칠하는 곳에 쓴다.
    public static Color Pure()
    {
        return Color.HSVToRGB(
            Mathf.Repeat(Time.unscaledTime * CyclesPerSecond, 1f),
            Saturation,
            1f);
    }

    // 그림 위에 곱해 원래 색을 살리면서 반짝이게 하는 색이다.
    public static Color Shimmer()
    {
        return Color.Lerp(Color.white, Pure(), 0.4f);
    }
}
