using UnityEngine;

// 결과 슬라임이 필드로 날아가는 모양이다. 1회 결과(GachaResultDirector)와 5회 결과(GachaResultSlots)가
// 함께 쓴다. 두 경로가 각자 계산하던 때는 한쪽만 회전 수를 고쳐 1회와 5회가 다르게 돌았다.
public readonly struct GachaResultFlight
{
    // 필드에 닿은 결과 그림이 한 번 부풀었다 돌아오는 크기다.
    public const float ArrivalPulse = 0.28f;

    public readonly float Duration;
    public readonly float ArcRadius;
    public readonly float SpinTurns;

    public GachaResultFlight(float duration, float arcRadius, float spinTurns)
    {
        Duration = duration;
        ArcRadius = arcRadius;
        SpinTurns = spinTurns;
    }

    // 호의 반지름만 바꾼 비행이다. 여러 마리가 함께 날 때 서로 엉키지 않게 좁힌다.
    public GachaResultFlight WithArcScale(float scale)
    {
        return new GachaResultFlight(Duration, ArcRadius * scale, SpinTurns);
    }

    // ratio(0~1) 지점의 자세다. 곧은 길 옆으로 한 바퀴 원을 그리며 부풀었다 오므라들고, 그동안
    // SpinTurns만큼 돌며 startScale에서 endScale로 줄어든다.
    public void Apply(
        RectTransform rect,
        Vector2 start,
        Vector2 destination,
        float startScale,
        float endScale,
        float ratio)
    {
        Vector2 path = destination - start;
        Vector2 forward = path.sqrMagnitude > 0.01f ? path.normalized : Vector2.up;
        Vector2 perpendicular = new(-forward.y, forward.x);
        float angle = ratio * Mathf.PI * 2f;
        float envelope = Mathf.Sin(ratio * Mathf.PI);
        Vector2 orbit =
            (perpendicular * Mathf.Sin(angle) +
             forward * (1f - Mathf.Cos(angle)) * 0.45f) *
            (ArcRadius * envelope);
        rect.anchoredPosition = Vector2.Lerp(start, destination, ratio) + orbit;
        rect.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, ratio);
        rect.localRotation = Quaternion.Euler(0f, 0f, -360f * SpinTurns * ratio);
    }
}
