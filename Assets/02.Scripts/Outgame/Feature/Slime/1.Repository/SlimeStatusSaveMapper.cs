using System;
using System.Collections.Generic;

// 슬라임 저장 문서 전체와 도메인 상태 사이의 변환을 맡는다.
//
// 개체 하나의 변환은 SlimeInstanceSaveData가 맡고, 여기서는 문서 단위의 규칙을
// 다룬다. 항목 검증, 도감 배열 변환, 승격 저장이 필요한지의 판단이다.
// 매니저는 저장소 선택, 실패 신고, 이벤트, 저장 시점만 정한다.
public static class SlimeStatusSaveMapper
{
    // 실패하면 failureMessage에 SaveDataLoadGuard로 보낼 사유를 담고 false를 돌려준다.
    // needsMigrationSave는 읽은 문서가 현재 형식과 달라 바로 다시 저장해야 하는지다.
    public static bool TryRestore(
        SlimeStatusSaveData saveData,
        DateTime restoredAtUtc,
        out SlimeStatus status,
        out NormalSlimeCollectionStats collectionStats,
        out bool needsMigrationSave,
        out string failureMessage)
    {
        status = null;
        collectionStats = null;
        needsMigrationSave = false;
        failureMessage = null;

        // 개체 ID는 Guid로만 만들어지고, 중복 등록은 도메인이 막고,
        // 레거시 승격도 등급별 순번으로 고유한 ID를 만든다. 그러므로 복원할 수 없는
        // 항목이 있다는 것은 저장 데이터가 변질됐다는 뜻이다.
        // 일부만 버리고 진행하면 다음 저장이 그 손실을 확정하므로 세션을 차단한다.
        var activeSlimes = new List<SlimeInstance>();
        var restoredIds = new HashSet<string>();
        foreach (SlimeInstanceSaveData instanceData in saveData.ActiveSlimes)
        {
            if (instanceData == null)
            {
                failureMessage = "SlimeStatus : 비어 있는 슬라임 저장 항목이 있습니다.";
                return false;
            }

            SlimeInstance instance;
            try
            {
                instance = instanceData.ToDomain();
            }
            catch (ArgumentException e)
            {
                failureMessage =
                    $"SlimeStatus : 복원할 수 없는 슬라임 개체가 있습니다. : {e.Message}";
                return false;
            }

            if (!restoredIds.Add(instance.InstanceId))
            {
                failureMessage =
                    $"SlimeStatus : 중복된 슬라임 개체 ID가 있습니다. : {instance.InstanceId}";
                return false;
            }

            activeSlimes.Add(instance);
        }

        var registeredNormalCollection = new List<ESlimeGrade>(
            GetRegisteredNormalCollection(saveData.NormalCollectionRegistered));
        var registeredBeforeRestore = new HashSet<ESlimeGrade>(
            registeredNormalCollection);
        var restoredStats = new NormalSlimeCollectionStats(saveData);

        // HighestGrade가 범위를 벗어나면 도메인이 예외를 던진다. 그대로 두면
        // 초기화가 중단돼 안내 없이 화면이 멈추므로, 다른 손상과 같은 경로로 보낸다.
        // 필드가 없는 문서는 0(None)으로 변환되므로 변질뿐 아니라 결손으로도 닿는다.
        SlimeStatus restoredStatus;
        try
        {
            restoredStatus = new SlimeStatus(
                saveData.GetHighestGrade(),
                activeSlimes,
                registeredNormalCollection,
                (EGameStage)saveData.CurrentStage,
                saveData.SkyIntroCompleted,
                saveData.PendingGroundTickets,
                saveData.PendingSkyTickets,
                !saveData.AutoSpawnDisabled,
                saveData.AutoMergeEnabled,
                saveData.MainEndingSeen,
                saveData.SpecialGachaMissCount,
                saveData.CompletedTutorials);
        }
        catch (ArgumentException e)
        {
            failureMessage =
                $"SlimeStatus : 슬라임 진행 상태를 복원할 수 없습니다. : {e.Message}";
            return false;
        }

        bool restoredRegistrationStats = false;
        for (int gradeValue = (int)ESlimeGrade.Grade1;
             gradeValue < (int)ESlimeGrade.Count;
             gradeValue++)
        {
            ESlimeGrade grade = (ESlimeGrade)gradeValue;
            if (restoredStatus.IsNormalCollectionRegistered(grade) &&
                !registeredBeforeRestore.Contains(grade))
            {
                restoredRegistrationStats |=
                    restoredStats.RecordRegistration(grade, restoredAtUtc);
            }
        }

        status = restoredStatus;
        collectionStats = restoredStats;
        needsMigrationSave = saveData.WasMigrated ||
                             restoredStatus.NormalCollectionCount >
                             registeredNormalCollection.Count ||
                             restoredRegistrationStats;
        return true;
    }

    public static SlimeStatusSaveData Build(
        SlimeStatus status,
        NormalSlimeCollectionStats collectionStats)
    {
        var saveData = new SlimeStatusSaveData
        {
            SchemaVersion = SaveSchema.SlimeCurrentVersion,
            HighestGrade = (int)status.HighestGrade,
            ActiveSlimes = new List<SlimeInstanceSaveData>(),
            CurrentStage = (int)status.CurrentStage,
            SkyIntroCompleted = status.SkyIntroCompleted,
            PendingGroundTickets = status.PendingGroundTickets,
            PendingSkyTickets = status.PendingSkyTickets,
            AutoSpawnDisabled = !status.IsAutoSpawnEnabled,
            AutoMergeEnabled = status.IsAutoMergeEnabled,
            MainEndingSeen = status.MainEndingSeen,
            SpecialGachaMissCount = status.SpecialGachaMissCount,
            CompletedTutorials = new List<string>(status.CompletedTutorials),
            NormalCollectionRegistered = BuildNormalCollectionSaveData(status),
            NormalFirstRegisteredAt = collectionStats.BuildFirstRegisteredAt(),
            NormalNaturalSpawnCounts = collectionStats.BuildNaturalSpawnCounts(),
            NormalMergeCreatedCounts = collectionStats.BuildMergeCreatedCounts(),
            NormalManualTouchCounts = collectionStats.BuildManualTouchCounts(),
            NormalProducedPointTotals = collectionStats.BuildProducedPointTotals(),
        };

        foreach (SlimeInstance instance in status.ActiveSlimes)
        {
            saveData.ActiveSlimes.Add(
                SlimeInstanceSaveData.FromDomain(instance));
        }

        return saveData;
    }

    private static IEnumerable<ESlimeGrade> GetRegisteredNormalCollection(
        IReadOnlyList<bool> registered)
    {
        if (registered == null)
        {
            yield break;
        }

        int count = Math.Min(
            registered.Count,
            SlimeStatusSaveData.NormalCollectionSize);
        for (int i = 0; i < count; i++)
        {
            if (registered[i])
            {
                yield return (ESlimeGrade)(
                    (int)ESlimeGrade.Grade1 + i);
            }
        }
    }

    private static List<bool> BuildNormalCollectionSaveData(SlimeStatus status)
    {
        List<bool> registered =
            SlimeStatusSaveData.CreateEmptyNormalCollection();
        for (int i = 0; i < registered.Count; i++)
        {
            ESlimeGrade grade = (ESlimeGrade)(
                (int)ESlimeGrade.Grade1 + i);
            registered[i] = status.IsNormalCollectionRegistered(grade);
        }

        return registered;
    }
}
