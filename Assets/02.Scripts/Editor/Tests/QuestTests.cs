using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

// 졸업식까지의 퀘스트표와 지금 퀘스트를 고르는 규칙을 확인한다. 표의 문구를 고쳐도 여기는 그대로이고,
// 순서나 목표를 바꾸면 여기 기대값도 함께 고쳐야 한다.
public class QuestTests
{
    private const string TablePath = "Assets/10.ScriptableObjects/QuestTable.asset";

    [Test]
    public void 퀘스트는_순서대로_아직_안_끝난_첫_퀘스트를_고른다()
    {
        IReadOnlyList<QuestEntry> quests = LoadTable().Quests;

        Assert.AreEqual(0, QuestProgress.FindCurrent(quests, new QuestState(1, 0, 0, false)));
        // Lv.4와 Lv.6을 만들었지만 장식장에 아무것도 넣지 않았다: 도감 3종 퀘스트.
        Assert.AreEqual(2, QuestProgress.FindCurrent(quests, new QuestState(6, 0, 0, false)));
        // 뒤 퀘스트(도감 6종)를 먼저 끝냈으면 건너뛴다.
        Assert.AreEqual(5, QuestProgress.FindCurrent(quests, new QuestState(9, 6, 6, false)));
        // 탐험을 보내기 전에는 Lv.13을 넘겨도 탐험 퀘스트에 머문다.
        Assert.AreEqual(10, QuestProgress.FindCurrent(quests, new QuestState(14, 12, 12, false)));
        // 모든 레벨과 도감을 채웠어도 장식장에 20종이 함께 있어야 한다.
        Assert.AreEqual(quests.Count - 2, QuestProgress.FindCurrent(quests, new QuestState(20, 20, 19, true)));
        // 20종을 모아도 장식장에 들어가 졸업식을 봐야 끝난다.
        Assert.AreEqual(quests.Count - 1, QuestProgress.FindCurrent(quests, new QuestState(20, 20, 20, true)));
        Assert.AreEqual(-1, QuestProgress.FindCurrent(quests, new QuestState(20, 20, 20, true, true)));
    }

    [Test]
    public void 퀘스트표는_졸업식으로_끝나고_목표가_게임_범위_안에_있다()
    {
        IReadOnlyList<QuestEntry> quests = LoadTable().Quests;
        Assert.Greater(quests.Count, 0);

        int lastGrade = 1;
        int lastCollection = 0;
        foreach (QuestEntry quest in quests)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(quest.Title), "제목이 비어 있다");
            Assert.IsFalse(string.IsNullOrWhiteSpace(quest.Description), $"{quest.Title}: 설명이 비어 있다");
            Assert.Greater(quest.Rewards.Count, 0, $"{quest.Title}: 보상이 비어 있다");

            switch (quest.Goal)
            {
                case EQuestGoal.HighestGrade:
                    Assert.Greater(quest.Target, lastGrade, $"{quest.Title}: 등급 목표는 앞보다 높아야 한다");
                    Assert.LessOrEqual(quest.Target, (int)ESlimeGrade.Grade20);
                    lastGrade = quest.Target;
                    break;
                case EQuestGoal.NormalCollection:
                    Assert.Greater(quest.Target, lastCollection, $"{quest.Title}: 도감 목표는 앞보다 커야 한다");
                    Assert.LessOrEqual(quest.Target, NormalCollectionRules.MainEndingCount);
                    lastCollection = quest.Target;
                    break;
            }
        }

        QuestEntry gather = quests[quests.Count - 2];
        Assert.AreEqual(EQuestGoal.DisplayRoomSlimes, gather.Goal);
        Assert.AreEqual(NormalCollectionRules.MainEndingCount, gather.Target);

        QuestEntry last = quests[quests.Count - 1];
        Assert.AreEqual(EQuestGoal.MainEndingSeen, last.Goal);
        Assert.AreEqual(1, last.Target);
        Assert.AreEqual(EQuestRewardKind.Graduation, last.Rewards[0].Kind);
    }

    [Test]
    public void 진행도가_있는_퀘스트는_제목_뒤에_현재와_목표를_붙인다()
    {
        IReadOnlyList<QuestEntry> quests = LoadTable().Quests;
        QuestEntry collection = quests[QuestProgress.FindCurrent(quests, new QuestState(6, 1, 1, false))];

        Assert.AreEqual(collection.Title + " (1/3)", QuestPopupUI.FormatTitle(collection, new QuestState(6, 1, 1, false)));
        Assert.AreEqual(quests[0].Title, QuestPopupUI.FormatTitle(quests[0], new QuestState(1, 0, 0, false)));
    }

    private static QuestTableSO LoadTable()
    {
        var table = AssetDatabase.LoadAssetAtPath<QuestTableSO>(TablePath);
        Assert.IsNotNull(table, TablePath);
        return table;
    }
}
