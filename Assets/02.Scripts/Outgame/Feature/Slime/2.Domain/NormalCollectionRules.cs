using System;

public static class NormalCollectionRules
{
    public const int AutoMergeCount = 10;
    public const int TicketBulkCollectCount = 12;
    public const int OfflineTicketRewardCount = 15;
    public const int MainEndingCount = 20;
    public const int HiddenFeverCount = MainEndingCount;

    // 도감 3종을 채울 때마다 포인트 획득량이 10%씩 늘어난다. 시스템 업그레이드의 배율과 같은
    // 풀에 더해지고, 터치·자동 생산·오프라인 보상·도감 능력 표시가 PointCalculator를 거쳐 함께 쓴다.
    public const int PointBonusStepCount = 3;
    public const double PointBonusPercentPerStep = 10d;
    // 5단계(15종)에서 멈춘다. 후반 업그레이드 배율을 줄인 만큼(최대 50%)만 이 보너스로 옮겼다.
    public const int PointBonusMaxStepCount = 5;

    // 등록한 일반 슬라임 수에 대한 포인트 보너스(%). 3종 단위로만 오르고 남는 1~2종은 쓰이지 않으며, 15종 뒤로는 오르지 않는다.
    public static double GetPointBonusPercent(int registeredCount)
    {
        int steps = Math.Min(PointBonusMaxStepCount, Math.Max(0, registeredCount) / PointBonusStepCount);
        return steps * PointBonusPercentPerStep;
    }
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
