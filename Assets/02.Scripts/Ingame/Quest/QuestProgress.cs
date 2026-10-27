using System.Collections.Generic;

// 퀘스트 판정에 쓰는 지금 상태. 모두 SlimeManager가 가진 저장 값에서 온다.
public readonly struct QuestState
{
    public readonly int HighestGrade;
    public readonly int NormalCollection;
    public readonly int DisplayRoomSlimes;
    public readonly bool HasSentExpedition;
    public readonly bool HasSeenMainEnding;

    public QuestState(
        int highestGrade,
        int normalCollection,
        int displayRoomSlimes,
        bool hasSentExpedition,
        bool hasSeenMainEnding = false)
    {
        HighestGrade = highestGrade;
        NormalCollection = normalCollection;
        DisplayRoomSlimes = displayRoomSlimes;
        HasSentExpedition = hasSentExpedition;
        HasSeenMainEnding = hasSeenMainEnding;
    }

    public static QuestState From(SlimeManager slimes) => new(
        (int)slimes.HighestGrade,
        slimes.NormalCollectionCount,
        slimes.DisplayRoomSlimeCount,
        slimes.ActiveExpeditions.Count > 0 || slimes.ExpeditionSettlement.Sequence > 0,
        slimes.IsMainEndingSeen);
}

// 지금 보여 줄 퀘스트를 고른다. 표의 순서대로 아직 끝나지 않은 첫 퀘스트이고, 뒤 퀘스트를 먼저
// 끝냈으면 그것은 건너뛴다. 모두 끝났으면 -1이다.
public static class QuestProgress
{
    public static int FindCurrent(IReadOnlyList<QuestEntry> quests, QuestState state)
    {
        if (quests == null) return -1;

        for (int i = 0; i < quests.Count; i++)
        {
            if (quests[i] != null && !IsComplete(quests[i], state)) return i;
        }

        return -1;
    }

    public static bool IsComplete(QuestEntry quest, QuestState state) =>
        GetCurrentValue(quest.Goal, state) >= quest.Target;

    // 도감과 장식장처럼 하나씩 채우는 퀘스트는 진행도를 함께 보여 준다.
    public static bool ShowsProgress(EQuestGoal goal) =>
        goal == EQuestGoal.NormalCollection || goal == EQuestGoal.DisplayRoomSlimes;

    public static int GetCurrentValue(EQuestGoal goal, QuestState state) => goal switch
    {
        EQuestGoal.HighestGrade => state.HighestGrade,
        EQuestGoal.NormalCollection => state.NormalCollection,
        EQuestGoal.DisplayRoomSlimes => state.DisplayRoomSlimes,
        EQuestGoal.ExpeditionSent => state.HasSentExpedition ? 1 : 0,
        EQuestGoal.MainEndingSeen => state.HasSeenMainEnding ? 1 : 0,
        _ => 0,
    };
}
