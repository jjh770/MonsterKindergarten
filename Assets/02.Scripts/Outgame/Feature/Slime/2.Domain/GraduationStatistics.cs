using System;

// 최초 졸업식이 시작된 순간의 통계다. 이후의 플레이 기록과 분리해 다시보기에서도
// 같은 숫자를 보여 준다.
public readonly struct GraduationStatistics
{
    public DateTime StartedAtUtc { get; }
    public DateTime GraduatedAtUtc { get; }
    public long NaturalSpawnCount { get; }
    public long MergeCreatedCount { get; }
    public long ManualTouchCount { get; }
    public double ProducedPointTotal { get; }
    public ESlimeGrade MostTouchedGrade { get; }
    public long MostTouchedCount { get; }
    public long GachaTicketsObtainedTotal { get; }
    public long AutoMergeUseCount { get; }

    public GraduationStatistics(
        DateTime startedAtUtc,
        DateTime graduatedAtUtc,
        long naturalSpawnCount,
        long mergeCreatedCount,
        long manualTouchCount,
        double producedPointTotal,
        ESlimeGrade mostTouchedGrade,
        long mostTouchedCount,
        long gachaTicketsObtainedTotal = 0,
        long autoMergeUseCount = 0)
    {
        StartedAtUtc = startedAtUtc;
        GraduatedAtUtc = graduatedAtUtc;
        NaturalSpawnCount = naturalSpawnCount;
        MergeCreatedCount = mergeCreatedCount;
        ManualTouchCount = manualTouchCount;
        ProducedPointTotal = producedPointTotal;
        MostTouchedGrade = mostTouchedGrade;
        MostTouchedCount = mostTouchedCount;
        GachaTicketsObtainedTotal = gachaTicketsObtainedTotal;
        AutoMergeUseCount = autoMergeUseCount;
    }
}
