using System;

public static class NormalCollectionRules
{
    public const int AutoMergeCount = 10;
    public const int TicketBulkCollectCount = 12;
    public const int OfflineTicketRewardCount = 15;
    public const int MainEndingCount = 20;
    public const int HiddenFeverCount = MainEndingCount;
}

public static class SpecialGachaFever
{
    public const float BaseChance = 0.03f;
    public const float ChanceIncreasePerMiss = 0.005f;
    public const float MaximumChance = 0.10f;
    public const int MaximumMissCount = 14;

    public static float GetChance(int missCount)
    {
        return Math.Min(
            MaximumChance,
            BaseChance + Math.Max(0, missCount) * ChanceIncreasePerMiss);
    }
}

// 특별한 슬라임의 능력. 일반과 같은 등급이라도 특별한 쪽이 더 많이 번다.
//
// 터치와 자동 생산, 오프라인 보상, 도감의 능력 표시가 모두 이 값을 함께 쓴다. 한 곳이라도
// 빠지면 화면에 적힌 숫자와 실제로 버는 포인트가 어긋난다.
public static class SpecialSlimeRules
{
    public const double PointMultiplier = 3d;
}
