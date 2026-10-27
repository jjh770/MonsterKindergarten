using System;
using System.Collections.Generic;
using UnityEngine;

// 졸업식까지 차례로 보여 주는 퀘스트의 순서와 문구. 문구와 순서는 에셋에서 고치고, 무엇을 세는지는
// EQuestGoal이 정한다. 퀘스트는 따로 보상을 주지 않는다. 보상 칸은 그 조건에서 원래 열리거나
// 지급되는 것을 알려 줄 뿐이라, 실제 해금 규칙(UnlockGrades, NormalCollectionRules, GradeReachTicketRewards)을
// 바꾸면 여기 문구도 함께 고쳐야 한다.
[CreateAssetMenu(fileName = "QuestTable", menuName = "Monster Kindergarten/Quest Table")]
public sealed class QuestTableSO : ScriptableObject
{
    [Tooltip("HUD와 팝업 위쪽의 번호 표시입니다. {0}에 1부터 세는 번호가 들어갑니다.")]
    [SerializeField] private string _headerFormat = "Quest.{0}";
    [SerializeField] private QuestEntry[] _quests = Array.Empty<QuestEntry>();

    public IReadOnlyList<QuestEntry> Quests => _quests;

    public string FormatHeader(int index) => string.Format(_headerFormat, index + 1);
}

// 무엇을 세어 완료를 판정하는가. 모두 저장된 값에서 바로 계산되므로 퀘스트는 따로 저장하지 않는다.
public enum EQuestGoal
{
    HighestGrade,
    NormalCollection,
    ExpeditionSent,
    DisplayRoomSlimes,
    // 졸업식을 봤는지. 장식장 20종을 채운 뒤 장식장으로 들어가야 졸업식이 열린다.
    MainEndingSeen,
}

public enum EQuestRewardKind
{
    NewFeature,
    PointBonus,
    GachaTickets,
    ExpeditionGift,
    Graduation,
}

[Serializable]
public sealed class QuestReward
{
    [SerializeField] private EQuestRewardKind _kind;
    [Tooltip("보상 종류 아래에 작게 붙는 설명입니다. 예: 뽑기, 자동 등장 ON/OFF")]
    [SerializeField] private string _detail;

    public EQuestRewardKind Kind => _kind;
    public string Detail => _detail;
}

[Serializable]
public sealed class QuestEntry
{
    [SerializeField] private EQuestGoal _goal;
    [Tooltip("이 값에 닿으면 완료입니다. 경험한 탐험 수는 1이면 됩니다.")]
    [SerializeField, Min(1)] private int _target = 1;
    [Tooltip("HUD에 보이는 한 줄입니다. 진행도가 있는 퀘스트는 뒤에 (현재/목표)가 붙습니다.")]
    [SerializeField] private string _title;
    [TextArea(2, 4)]
    [SerializeField] private string _description;
    [SerializeField] private QuestReward[] _rewards = Array.Empty<QuestReward>();

    public EQuestGoal Goal => _goal;
    public int Target => _target;
    public string Title => _title;
    public string Description => _description;
    public IReadOnlyList<QuestReward> Rewards => _rewards;
}
