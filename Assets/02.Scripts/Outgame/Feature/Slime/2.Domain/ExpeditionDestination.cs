using System;
using System.Collections.Generic;

public enum EExpeditionDestinationType
{
    Short,
    Medium,
    Long,
}

// 탐험지 규칙이다. 구체적인 필요 등급과 보상은 진행 중 탐험의 저장 내용과 분리한다.
public sealed class ExpeditionDestination
{
    public const int MaxMembers = 3;

    public EExpeditionDestinationType Type { get; }
    public string DestinationId { get; }
    public TimeSpan Duration { get; }
    public bool IsFreeParty { get; }
    public int RequiredCollectionCount { get; }
    public IReadOnlyList<ESlimeGrade> RequiredGrades { get; }

    public ExpeditionDestination(EExpeditionDestinationType type, IEnumerable<ESlimeGrade> requiredGrades, bool freeParty = false)
    {
        switch (type)
        {
            case EExpeditionDestinationType.Short:
                DestinationId = "expedition-short";
                Duration = TimeSpan.FromHours(2);
                break;
            case EExpeditionDestinationType.Medium:
                DestinationId = "expedition-medium";
                Duration = TimeSpan.FromHours(4);
                RequiredCollectionCount = 10;
                break;
            case EExpeditionDestinationType.Long:
                DestinationId = "expedition-long";
                Duration = TimeSpan.FromHours(8);
                RequiredCollectionCount = 13;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }

        if (requiredGrades == null) throw new ArgumentNullException(nameof(requiredGrades));
        var grades = new List<ESlimeGrade>();
        var uniqueGrades = new HashSet<ESlimeGrade>();
        foreach (ESlimeGrade grade in requiredGrades)
        {
            if (grade < ESlimeGrade.Grade1 || grade >= ESlimeGrade.Count || !uniqueGrades.Add(grade))
                throw new ArgumentException("필요 슬라임 종류가 유효하지 않거나 중복돼요.", nameof(requiredGrades));
            grades.Add(grade);
        }
        if ((!freeParty && grades.Count == 0) || grades.Count > MaxMembers || (freeParty && grades.Count != 0))
            throw new ArgumentException("필요 슬라임 종류는 1~3종이어야 해요.", nameof(requiredGrades));

        IsFreeParty = freeParty;
        Type = type;
        RequiredGrades = grades.AsReadOnly();
    }

    // 일반·특별은 같은 종류로 인정한다. 개체 중복·장식장 소속은 출발 검증에서 확인한다.
    public bool HasRequiredGrades(IReadOnlyList<SlimeInstance> members)
    {
        if (members == null || members.Count == 0 || members.Count > MaxMembers) return false;
        foreach (SlimeInstance member in members)
            if (member == null) return false;

        if (IsFreeParty)
        {
            var types = new HashSet<ESlimeGrade>();
            foreach (SlimeInstance member in members) types.Add(member.Grade);
            return members.Count == MaxMembers && types.Count == MaxMembers;
        }

        foreach (ESlimeGrade requiredGrade in RequiredGrades)
        {
            bool found = false;
            foreach (SlimeInstance member in members)
            {
                if (member.Grade != requiredGrade) continue;
                found = true;
                break;
            }
            if (!found) return false;
        }
        return true;
    }
}
