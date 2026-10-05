using System;
using Utility;

// 엔딩 크레딧에 필요한 플레이 통계를 모아 표시 문구로 바꾼다.
// 화면 연출과 무관하므로 MainEndingUI가 직접 집계하지 않는다.
public static class MainEndingCreditsTextBuilder
{
    public static string BuildGraduation(SlimeManager manager)
    {
        if (manager == null || !manager.GraduationSnapshot.HasValue)
            return "졸업 기록을 준비하고 있어요.";

        GraduationStatistics snapshot = manager.GraduationSnapshot.Value;
        return Build(manager, snapshot, "함께 만든 몬스터 유치원");
    }

    public static void BuildGraduationColumns(
        SlimeManager manager,
        out string left,
        out string right)
    {
        if (manager == null || !manager.GraduationSnapshot.HasValue)
        {
            left = "졸업 기록을\n준비하고 있어요.";
            right = string.Empty;
            return;
        }

        BuildColumns(manager, manager.GraduationSnapshot.Value, "졸업한 날", out left, out right);
    }

    // 설정의 놀이 기록. 졸업식과 같은 두 줄 구성이지만 끝점이 졸업일이 아니라 지금이라
    // 날짜 이름을 "오늘"로 바꾼다.
    public static void BuildCurrentColumns(
        SlimeManager manager,
        DateTime nowUtc,
        out string left,
        out string right)
    {
        if (manager == null) throw new ArgumentNullException(nameof(manager));
        BuildColumns(manager, manager.CreateCurrentStatistics(nowUtc), "오늘", out left, out right);
    }

    private static void BuildColumns(
        SlimeManager manager,
        GraduationStatistics stats,
        string endLabel,
        out string left,
        out string right)
    {
        int daysTogether = Math.Max(1,
            (stats.GraduatedAtUtc.ToLocalTime().Date -
             stats.StartedAtUtc.ToLocalTime().Date).Days + 1);
        string favorite = stats.MostTouchedCount > 0
            ? manager.GetName(stats.MostTouchedGrade)
            : "아직 기록 없음";

        left =
            $"처음 만난 날\n{stats.StartedAtUtc.ToLocalTime():yyyy.MM.dd}\n\n" +
            $"{endLabel}\n{stats.GraduatedAtUtc.ToLocalTime():yyyy.MM.dd}\n\n" +
            $"함께한 시간\n{daysTogether:N0}일\n\n" +
            $"함께 만든 포인트\n{stats.ProducedPointTotal.ToFormattedString()}\n\n" +
            $"누적 뽑기권 획득\n{stats.GachaTicketsObtainedTotal:N0}장";
        right =
            $"자연 출현한 슬라임\n{stats.NaturalSpawnCount:N0}마리\n\n" +
            $"합성으로 태어난 슬라임\n{stats.MergeCreatedCount:N0}마리\n\n" +
            $"슬라임을 터치한 횟수\n{stats.ManualTouchCount:N0}회\n\n" +
            $"가장 많이 터치한 친구\n{favorite}\n\n" +
            $"자동 합성 사용\n{stats.AutoMergeUseCount:N0}회";
    }

    private static string Build(
        SlimeManager manager,
        GraduationStatistics snapshot,
        string title)
    {
        DateTime startedAt = snapshot.StartedAtUtc;
        DateTime endingAt = snapshot.GraduatedAtUtc;

        long totalNaturalSpawns = 0;
        long totalMergeCreated = 0;
        long totalManualTouches = 0;
        double totalProducedPoints = 0d;
        long mostTouches = -1;
        ESlimeGrade mostTouchedGrade = ESlimeGrade.Grade1;

        totalNaturalSpawns = snapshot.NaturalSpawnCount;
        totalMergeCreated = snapshot.MergeCreatedCount;
        totalManualTouches = snapshot.ManualTouchCount;
        totalProducedPoints = snapshot.ProducedPointTotal;
        mostTouches = snapshot.MostTouchedCount;
        mostTouchedGrade = snapshot.MostTouchedGrade;

        int daysTogether = Math.Max(
            1,
            (endingAt.ToLocalTime().Date - startedAt.ToLocalTime().Date).Days + 1);
        string favorite = mostTouches > 0
            ? manager.GetName(mostTouchedGrade)
            : "아직 기록 없음";

        return title + "\n\n\n" +
               $"처음 만난 날\n{startedAt.ToLocalTime():yyyy.MM.dd}\n\n" +
               $"졸업한 날\n{endingAt.ToLocalTime():yyyy.MM.dd}\n\n" +
               $"함께한 시간\n{daysTogether:N0}일\n\n\n" +
               $"슬라임을 터치한 횟수\n{totalManualTouches:N0}회\n\n" +
               $"합성으로 태어난 슬라임\n{totalMergeCreated:N0}마리\n\n" +
               $"자연 출현한 슬라임\n{totalNaturalSpawns:N0}마리\n\n" +
               $"함께 만든 포인트\n{totalProducedPoints.ToFormattedString()}\n\n" +
               $"가장 많이 터치한 친구\n{favorite}";
    }

}
