using UnityEngine;

public static class NormalCollectionRules
{
    public const int AutoMergeCount = 10;
    public const int AutoTicketCollectCount = 12;
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
        return Mathf.Min(
            MaximumChance,
            BaseChance + Mathf.Max(0, missCount) * ChanceIncreasePerMiss);
    }
}
