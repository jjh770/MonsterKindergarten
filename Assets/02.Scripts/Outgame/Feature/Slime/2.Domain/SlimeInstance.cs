using System;

public sealed class SlimeInstance
{
    public string InstanceId { get; }
    public ESlimeGrade Grade { get; private set; }
    public bool IsSpecial { get; }
    public ESlimeLocation Location { get; private set; }

    public SlimeInstance(
        string instanceId,
        ESlimeGrade grade,
        bool isSpecial,
        ESlimeLocation location)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new ArgumentException("슬라임 개체 ID가 비어 있습니다.", nameof(instanceId));
        }

        ValidateGrade(grade);
        ValidateLocation(location);

        InstanceId = instanceId;
        Grade = grade;
        IsSpecial = isSpecial;
        Location = location;
    }

    public SlimeInstance(SlimeInstance source)
        : this(
            source.InstanceId,
            source.Grade,
            source.IsSpecial,
            source.Location)
    {
    }

    internal void PromoteTo(ESlimeGrade grade)
    {
        ValidateGrade(grade);
        if (grade != Grade + 1)
        {
            throw new InvalidOperationException(
                $"슬라임은 다음 등급으로만 승격할 수 있습니다. : {Grade} -> {grade}");
        }

        Grade = grade;
    }

    internal void MoveTo(ESlimeLocation location)
    {
        ValidateLocation(location);
        Location = location;
    }

    public static SlimeInstance Create(
        ESlimeGrade grade,
        bool isSpecial = false,
        ESlimeLocation location = ESlimeLocation.MainStage)
    {
        return new SlimeInstance(
            Guid.NewGuid().ToString("N"),
            grade,
            isSpecial,
            location);
    }

    private static void ValidateGrade(ESlimeGrade grade)
    {
        if (grade < ESlimeGrade.Grade1 || grade >= ESlimeGrade.Count)
        {
            throw new ArgumentException($"올바른 등급 설정이 아닙니다. : {grade}");
        }
    }

    private static void ValidateLocation(ESlimeLocation location)
    {
        if (!SlimeLocationRules.IsValid(location))
        {
            throw new ArgumentException($"유효하지 않은 슬라임 위치입니다. : {location}");
        }
    }
}
