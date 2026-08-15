using System;

public readonly struct PointCountUpRequest
{
    public Currency StartPoint { get; }
    public Currency TargetPoint { get; }
    public float Duration { get; }

    public PointCountUpRequest(Currency startPoint, Currency targetPoint, float duration)
    {
        StartPoint = startPoint;
        TargetPoint = targetPoint;
        Duration = duration;
    }
}

public static class PointCountUpEvents
{
    public static event Action<PointCountUpRequest> OnRequested;

    public static void Request(PointCountUpRequest request)
    {
        OnRequested?.Invoke(request);
    }
}
