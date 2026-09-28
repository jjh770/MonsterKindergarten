using System;
using Utility;

// 엔딩 크레딧에 필요한 플레이 통계를 모아 표시 문구로 바꾼다.
// 화면 연출과 무관하므로 MainEndingUI가 직접 집계하지 않는다.
public static class MainEndingCreditsTextBuilder
{
    public static string Build(SlimeManager manager, DateTime fallbackUtc)
    {
        if (manager == null)
        {
            throw new ArgumentNullException(nameof(manager));
        }

        DateTime startedAt = manager.GameStartedAtUtc == DateTime.MinValue
            ? fallbackUtc
            : manager.GameStartedAtUtc;
        DateTime endingAt = manager.MainEndingReachedAtUtc ?? fallbackUtc;

        long totalNaturalSpawns = 0;
        long totalMergeCreated = 0;
        long totalManualTouches = 0;
        double totalProducedPoints = 0d;
        long mostTouches = -1;
        ESlimeGrade mostTouchedGrade = ESlimeGrade.Grade1;

        for (int value = (int)ESlimeGrade.Grade1;
             value < (int)ESlimeGrade.Count;
             value++)
        {
            ESlimeGrade grade = (ESlimeGrade)value;
            NormalSlimeCollectionStatsSnapshot stats =
                manager.GetNormalCollectionStats(grade);
            totalNaturalSpawns = SaturatingAdd(
                totalNaturalSpawns,
                stats.NaturalSpawnCount);
            totalMergeCreated = SaturatingAdd(
                totalMergeCreated,
                stats.MergeCreatedCount);
            totalManualTouches = SaturatingAdd(
                totalManualTouches,
                stats.ManualTouchCount);
            totalProducedPoints = SaturatingAdd(
                totalProducedPoints,
                stats.ProducedPointTotal);

            if (stats.ManualTouchCount > mostTouches)
            {
                mostTouches = stats.ManualTouchCount;
                mostTouchedGrade = grade;
            }
        }

        int daysTogether = Math.Max(
            1,
            (endingAt.ToLocalTime().Date - startedAt.ToLocalTime().Date).Days + 1);
        string favorite = mostTouches > 0
            ? manager.GetName(mostTouchedGrade)
            : "아직 기록 없음";

        return "함께 만든 몬스터 유치원\n\n\n" +
               $"처음 만난 날\n{startedAt.ToLocalTime():yyyy.MM.dd}\n\n" +
               $"졸업한 날\n{endingAt.ToLocalTime():yyyy.MM.dd}\n\n" +
               $"함께한 시간\n{daysTogether:N0}일\n\n\n" +
               $"슬라임을 터치한 횟수\n{totalManualTouches:N0}회\n\n" +
               $"합성으로 태어난 슬라임\n{totalMergeCreated:N0}마리\n\n" +
               $"자연 출현한 슬라임\n{totalNaturalSpawns:N0}마리\n\n" +
               $"함께 만든 포인트\n{totalProducedPoints.ToFormattedString()}\n\n" +
               $"가장 많이 터치한 친구\n{favorite}\n\n\n" +
               "모든 순간을 함께해 주셔서\n고맙습니다.";
    }

    private static long SaturatingAdd(long left, long right)
    {
        if (right <= 0) return left;
        return left > long.MaxValue - right ? long.MaxValue : left + right;
    }

    private static double SaturatingAdd(double left, double right)
    {
        if (right <= 0d || double.IsNaN(right)) return left;
        double result = left + right;
        return double.IsInfinity(result) ? double.MaxValue : result;
    }
}
