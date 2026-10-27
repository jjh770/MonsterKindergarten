using System;
using System.Collections.Generic;

// 다음 목표를 끝낸 순간을 분석 이벤트로 남긴다. 플레이어가 어느 목표에서 오래 머무는지 보려는 것이다.
// 예를 들어 도감 목표를 끝냈을 때의 최고 등급이 목표보다 한참 높으면, 그동안 등록을 미뤘다는 뜻이다.
//
// 싣는 값은 목표 번호, 목표 종류, 그때의 최고 등급, 계정을 만든 뒤 지난 분, 같은 순간에 함께 끝난 목표 수다.
// 목표는 순서대로만 끝나므로, 앞 목표에 막혀 있던 동안 조건을 채운 뒤 목표는 앞 목표가 풀리는 순간에 한꺼번에
// 기록된다. batch_size가 1보다 크면 그 묶음의 뒤 목표들은 실제로 더 일찍 달성한 것이다. 계정 식별자와 재화 값은
// 싣지 않는다(IAnalyticsService.LogProgressEvent). 분석이 실패해도 게임은 막지 않으므로 결과를 돌려주지 않는다.
public static class QuestAnalytics
{
    public const string GoalCompleteEvent = "goal_complete";

    public static void LogGoalComplete(
        int index,
        QuestEntry quest,
        int highestGrade,
        DateTime gameStartedAtUtc,
        int batchSize)
    {
        if (quest == null) return;

        long accountMinutes = gameStartedAtUtc == DateTime.MinValue
            ? -1
            : (long)Math.Max(0d, (DateTime.UtcNow - gameStartedAtUtc).TotalMinutes);
        Analytics.Service.LogProgressEvent(
            GoalCompleteEvent,
            new Dictionary<string, string> { ["goal_type"] = GetGoalTypeName(quest.Goal) },
            new Dictionary<string, long>
            {
                ["goal_index"] = index + 1,
                ["goal_target"] = quest.Target,
                ["highest_grade"] = highestGrade,
                ["account_minutes"] = accountMinutes,
                ["batch_size"] = batchSize,
            });
    }

    private static string GetGoalTypeName(EQuestGoal goal) => goal switch
    {
        EQuestGoal.HighestGrade => "grade",
        EQuestGoal.NormalCollection => "collection",
        EQuestGoal.ExpeditionSent => "expedition",
        EQuestGoal.DisplayRoomSlimes => "display_room",
        EQuestGoal.MainEndingSeen => "main_ending",
        _ => "unknown",
    };
}
