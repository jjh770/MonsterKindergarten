using System;
using System.Collections.Generic;

public class SlimeStatus
{
    // 최고 해금 등급 (한번 올라가면 내려가지 않음)
    public ESlimeGrade HighestGrade { get; private set; }

    private readonly List<SlimeInstance> _activeSlimes = new();
    public IReadOnlyList<SlimeInstance> ActiveSlimes => _activeSlimes;
    public EGameStage CurrentStage { get; private set; }
    public bool SkyIntroCompleted { get; private set; }

    public SlimeStatus(
        ESlimeGrade highestGrade,
        IEnumerable<SlimeInstance> activeSlimes,
        EGameStage currentStage,
        bool skyIntroCompleted)
    {
        ValidateGrade(highestGrade);
        HighestGrade = highestGrade;

        bool isSkyUnlocked = GameStageRules.IsSkyUnlocked(highestGrade);
        CurrentStage = isSkyUnlocked && GameStageRules.IsValid(currentStage)
            ? currentStage
            : EGameStage.Ground;
        SkyIntroCompleted = isSkyUnlocked && skyIntroCompleted;

        if (activeSlimes == null)
        {
            throw new ArgumentNullException(nameof(activeSlimes));
        }

        var instanceIds = new HashSet<string>();
        foreach (SlimeInstance instance in activeSlimes)
        {
            ValidateInstance(instance);
            if (!instanceIds.Add(instance.InstanceId))
            {
                throw new ArgumentException(
                    $"중복된 슬라임 개체 ID입니다. : {instance.InstanceId}");
            }

            _activeSlimes.Add(instance);
        }
    }

    public void UpdateStageProgress(
        EGameStage currentStage,
        bool skyIntroCompleted)
    {
        if (!GameStageRules.IsValid(currentStage))
        {
            throw new ArgumentException($"올바른 스테이지가 아닙니다. : {currentStage}");
        }

        if (currentStage == EGameStage.Sky &&
            !GameStageRules.IsSkyUnlocked(HighestGrade))
        {
            throw new InvalidOperationException("하늘 스테이지가 아직 해금되지 않았습니다.");
        }

        CurrentStage = currentStage;
        SkyIntroCompleted = skyIntroCompleted &&
                            GameStageRules.IsSkyUnlocked(HighestGrade);
    }

    public void UpdateHighestGrade(ESlimeGrade newGrade)
    {
        ValidateGrade(newGrade);
        if (newGrade <= HighestGrade)
        {
            throw new ArgumentException(
                $"새 등급은 현재 최고 등급보다 높아야 합니다. : {newGrade} <= {HighestGrade}");
        }

        HighestGrade = newGrade;
    }

    public void AddSlime(SlimeInstance instance)
    {
        ValidateInstance(instance);
        if (_activeSlimes.Exists(
                active => active.InstanceId == instance.InstanceId))
        {
            throw new InvalidOperationException(
                $"이미 등록된 슬라임 개체입니다. : {instance.InstanceId}");
        }

        _activeSlimes.Add(instance);
    }

    public void MoveSlime(string instanceId, ESlimeLocation location)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new ArgumentException("이동할 슬라임 개체 ID가 비어 있습니다.", nameof(instanceId));
        }

        if (!SlimeLocationRules.IsValid(location))
        {
            throw new ArgumentException(
                $"유효하지 않은 슬라임 위치입니다. : {location}",
                nameof(location));
        }

        SlimeInstance instance = _activeSlimes.Find(
            active => active.InstanceId == instanceId);
        if (instance == null)
        {
            throw new InvalidOperationException(
                $"저장 상태에 없는 슬라임은 이동할 수 없습니다. : {instanceId}");
        }

        if (instance.Location == location)
        {
            throw new InvalidOperationException(
                $"이미 해당 위치에 있는 슬라임입니다. : {instanceId}, {location}");
        }

        if (location == ESlimeLocation.DisplayRoom &&
            HasDisplayRoomSlime(instance.Grade, instance.IsSpecial))
        {
            throw new InvalidOperationException(
                "장식장에는 같은 종류와 타입의 슬라임을 한 마리만 보관할 수 있습니다.");
        }

        instance.MoveTo(location);
    }

    public bool HasDisplayRoomSlime(ESlimeGrade grade, bool isSpecial)
    {
        return _activeSlimes.Exists(instance =>
            instance.Location == ESlimeLocation.DisplayRoom &&
            instance.Grade == grade &&
            instance.IsSpecial == isSpecial);
    }

    public void MergeSlimes(
        string keeperId,
        string removedId,
        ESlimeGrade toGrade)
    {
        if (string.IsNullOrWhiteSpace(keeperId) ||
            string.IsNullOrWhiteSpace(removedId) ||
            keeperId == removedId)
        {
            throw new ArgumentException("합성할 슬라임 개체가 올바르지 않습니다.");
        }

        SlimeInstance keeper = _activeSlimes.Find(
            instance => instance.InstanceId == keeperId);
        SlimeInstance removed = _activeSlimes.Find(
            instance => instance.InstanceId == removedId);
        if (keeper == null || removed == null)
        {
            throw new InvalidOperationException("저장 상태에 없는 슬라임은 합성할 수 없습니다.");
        }

        ESlimeGrade fromGrade = keeper.Grade;
        if (fromGrade != removed.Grade || toGrade != fromGrade + 1)
        {
            throw new InvalidOperationException("동일 등급의 다음 단계로만 합성할 수 있습니다.");
        }

        ValidateGrade(toGrade);
        keeper.PromoteTo(toGrade);
        // 활성 개체 제거는 현재 합성으로 소모되는 경우에만 허용한다.
        _activeSlimes.Remove(removed);
    }

    private static void ValidateInstance(SlimeInstance instance)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }

        if (string.IsNullOrWhiteSpace(instance.InstanceId))
        {
            throw new ArgumentException("슬라임 개체 ID가 비어 있습니다.");
        }

        ValidateGrade(instance.Grade);
        if (!SlimeLocationRules.IsValid(instance.Location))
        {
            throw new ArgumentException(
                $"유효하지 않은 슬라임 위치입니다. : {instance.Location}");
        }
    }

    private static void ValidateGrade(ESlimeGrade grade)
    {
        if (grade < ESlimeGrade.Grade1 || grade >= ESlimeGrade.Count)
        {
            throw new ArgumentException($"올바른 등급 설정이 아닙니다. : {grade}");
        }
    }
}
