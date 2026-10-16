using System;

// 출발 순간의 개체를 기록한다. 이후 장식장 이동이나 밸런스 변경이 탐험 내용을 바꾸지 않는다.
public sealed class ExpeditionMember
{
    public string InstanceId { get; }
    public ESlimeGrade Grade { get; }
    public bool IsSpecial { get; }

    public ExpeditionMember(string instanceId, ESlimeGrade grade, bool isSpecial)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            throw new ArgumentException("탐험 슬라임 ID가 비어 있습니다.", nameof(instanceId));
        if (grade < ESlimeGrade.Grade1 || grade >= ESlimeGrade.Count)
            throw new ArgumentOutOfRangeException(nameof(grade));

        InstanceId = instanceId;
        Grade = grade;
        IsSpecial = isSpecial;
    }
}
