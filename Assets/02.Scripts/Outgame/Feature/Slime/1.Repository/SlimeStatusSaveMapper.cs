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
            GetRegisteredCollection(saveData.NormalCollectionRegistered));
        var registeredBeforeRestore = new HashSet<ESlimeGrade>(
            registeredNormalCollection);
        var restoredStats = new NormalSlimeCollectionStats(saveData);
        // 특별 도감은 없어졌다. 예전 저장에서 특별한 슬라임으로만 등록한 등급은 일반 도감에
        // 등록한 것으로 옮긴다.
        var legacySpecialCollection = new List<ESlimeGrade>(
            GetRegisteredCollection(saveData.SpecialCollectionRegistered));

        var effectiveRegistered = new HashSet<ESlimeGrade>(
            registeredNormalCollection);
        effectiveRegistered.UnionWith(legacySpecialCollection);
        foreach (SlimeInstance instance in activeSlimes)
        {
            if (instance.Location == ESlimeLocation.DisplayRoom)
            {
                effectiveRegistered.Add(instance.Grade);
            }
        }

        if (!TryResolveJourneyDates(
                saveData,
                restoredAtUtc,
                effectiveRegistered.Count >= NormalCollectionRules.MainEndingCount,
                out DateTime gameStartedAtUtc,
                out DateTime? mainEndingReachedAtUtc,
                out bool restoredJourneyDates,
                out failureMessage))
        {
            return false;
        }

        if (!TryRestoreGraduationSnapshot(
                saveData,
                gameStartedAtUtc,
                out GraduationStatistics? graduationSnapshot,
                out failureMessage))
        {
            return false;
        }

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
                (EBackgroundTheme)saveData.SelectedBackgroundTheme,
                saveData.BackgroundUnlockCompleted,
                saveData.PendingTickets,
                !saveData.AutoSpawnDisabled,
                saveData.MainEndingSeen,
                gameStartedAtUtc,
                mainEndingReachedAtUtc,
                graduationSnapshot,
                saveData.SpecialGachaMissCount,
                saveData.CompletedTutorials,
                ToPlacedObjects(saveData.PlacedObjects),
                saveData.OwnedPlaygroundObjects,
                ToBackgroundThemes(saveData.OwnedBackgroundThemes),
                legacySpecialCollection,
                saveData.GachaTicketsObtainedTotal,
                saveData.AutoMergeUseCount);
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
                             restoredRegistrationStats ||
                             restoredJourneyDates ||
                             ExceedsPlaygroundObjectLimit(saveData);
        return true;
    }

    // 오브젝트 상한을 낮춘 버전에서 예전 저장을 한 번만 정리해 다시 기록한다.
    // 런타임 복원 자체는 SlimeStatus가 담당하고, 여기서는 그 정리가 저장에도
    // 확정되어야 하는지만 판단한다.
    private static bool ExceedsPlaygroundObjectLimit(SlimeStatusSaveData saveData)
    {
        if (saveData.OwnedPlaygroundObjects != null)
        {
            foreach (int owned in saveData.OwnedPlaygroundObjects)
            {
                if (owned > PlaygroundRules.MaxPerType) return true;
            }
        }

        if (saveData.PlacedObjects == null) return false;

        var placedCounts = new int[(int)EPlaygroundObjectType.Count];
        foreach (PlacedObjectSaveData placed in saveData.PlacedObjects)
        {
            if (placed == null) continue;

            var type = (EPlaygroundObjectType)placed.Type;
            if (!PlaygroundRules.IsValid(type)) continue;
            if (++placedCounts[(int)type] > PlaygroundRules.MaxPerType) return true;
        }

        return false;
    }

    // 저장의 정수를 도메인 값으로 옮긴다. 모르는 번호를 여기서 거르지 않는 것은
    // 판단을 도메인 한 곳에 두기 위해서다. 도메인이 흘려보낸다.
    private static List<PlacedPlaygroundObject> ToPlacedObjects(
        List<PlacedObjectSaveData> saved)
    {
        var result = new List<PlacedPlaygroundObject>();
        if (saved == null) return result;

        foreach (PlacedObjectSaveData entry in saved)
        {
            if (entry == null) continue;

            result.Add(new PlacedPlaygroundObject(
                (EPlaygroundObjectType)entry.Type,
                entry.X,
                entry.Y));
        }

        return result;
    }

    private static List<EBackgroundTheme> ToBackgroundThemes(List<int> saved)
    {
        var result = new List<EBackgroundTheme>();
        if (saved == null) return result;

        foreach (int value in saved)
        {
            result.Add((EBackgroundTheme)value);
        }

        return result;
    }

    private static List<PlacedObjectSaveData> BuildPlacedObjects(SlimeStatus status)
    {
        var result = new List<PlacedObjectSaveData>();
        foreach (PlacedPlaygroundObject placed in status.PlacedObjects)
        {
            result.Add(new PlacedObjectSaveData((int)placed.Type, placed.X, placed.Y));
        }

        return result;
    }

    private static List<int> BuildOwnedPlaygroundObjects(SlimeStatus status)
    {
        var result = new List<int>((int)EPlaygroundObjectType.Count);
        for (int i = 0; i < (int)EPlaygroundObjectType.Count; i++)
        {
            result.Add(status.GetOwnedPlaygroundObjectCount((EPlaygroundObjectType)i));
        }

        return result;
    }

    // 기본 테마는 담지 않는다. 도메인이 저장 없이도 가진 것으로 보므로,
    // 담으면 같은 사실을 두 곳에서 관리하게 된다.
    private static List<int> BuildOwnedBackgroundThemes(SlimeStatus status)
    {
        var result = new List<int>();
        foreach (EBackgroundTheme theme in status.OwnedBackgroundThemes)
        {
            result.Add((int)theme);
        }

        return result;
    }

    public static SlimeStatusSaveData Build(
        SlimeStatus status,
        NormalSlimeCollectionStats collectionStats)
    {
        var saveData = new SlimeStatusSaveData
        {
            SchemaVersion = GameDataDomains.SlimeStatus.CurrentSchemaVersion,
            HighestGrade = (int)status.HighestGrade,
            ActiveSlimes = new List<SlimeInstanceSaveData>(),
            SelectedBackgroundTheme = (int)status.SelectedBackgroundTheme,
            BackgroundUnlockCompleted = status.BackgroundUnlockCompleted,
            PendingTickets = status.PendingTickets,
            AutoSpawnDisabled = !status.IsAutoSpawnEnabled,
            // 자동 합성은 버튼 발동형이라 ON/OFF 상태가 없다. 필드는 기존 로컬 JSON과
            // Firestore 문서 호환을 위해 남기되 새 저장에는 항상 false를 쓴다.
            AutoMergeEnabled = false,
            MainEndingSeen = status.MainEndingSeen,
            GameStartedAtUtc = status.GameStartedAtUtc.ToString("o"),
            MainEndingReachedAtUtc = status.MainEndingReachedAtUtc?.ToString("o"),
            GraduationSnapshotAtUtc = status.GraduationSnapshot?.GraduatedAtUtc.ToString("o"),
            GraduationNaturalSpawnCount = status.GraduationSnapshot?.NaturalSpawnCount ?? 0,
            GraduationMergeCreatedCount = status.GraduationSnapshot?.MergeCreatedCount ?? 0,
            GraduationManualTouchCount = status.GraduationSnapshot?.ManualTouchCount ?? 0,
            GraduationProducedPointTotal = status.GraduationSnapshot?.ProducedPointTotal ?? 0d,
            GraduationMostTouchedGrade = (int)(status.GraduationSnapshot?.MostTouchedGrade ?? ESlimeGrade.Grade1),
            GraduationMostTouchedCount = status.GraduationSnapshot?.MostTouchedCount ?? 0,
            GraduationGachaTicketsObtainedTotal = status.GraduationSnapshot?.GachaTicketsObtainedTotal ?? 0,
            GraduationAutoMergeUseCount = status.GraduationSnapshot?.AutoMergeUseCount ?? 0,
            SpecialGachaMissCount = status.SpecialGachaMissCount,
            GachaTicketsObtainedTotal = status.GachaTicketsObtainedTotal,
            AutoMergeUseCount = status.AutoMergeUseCount,
            CompletedTutorials = new List<string>(status.CompletedTutorials),
            PlacedObjects = BuildPlacedObjects(status),
            OwnedPlaygroundObjects = BuildOwnedPlaygroundObjects(status),
            OwnedBackgroundThemes = BuildOwnedBackgroundThemes(status),
            NormalCollectionRegistered = BuildNormalCollectionSaveData(status),
            NormalFirstRegisteredAt = collectionStats.BuildFirstRegisteredAt(),
            NormalNaturalSpawnCounts = collectionStats.BuildNaturalSpawnCounts(),
            NormalMergeCreatedCounts = collectionStats.BuildMergeCreatedCounts(),
            NormalManualTouchCounts = collectionStats.BuildManualTouchCounts(),
            NormalProducedPointTotals = collectionStats.BuildProducedPointTotals(),
            NormalGachaObtainedCounts = collectionStats.BuildGachaObtainedCounts(),
        };

        foreach (SlimeInstance instance in status.ActiveSlimes)
        {
            saveData.ActiveSlimes.Add(
                SlimeInstanceSaveData.FromDomain(instance));
        }

        return saveData;
    }

    private static bool TryRestoreGraduationSnapshot(
        SlimeStatusSaveData saveData,
        DateTime startedAtUtc,
        out GraduationStatistics? snapshot,
        out string failureMessage)
    {
        snapshot = null;
        failureMessage = null;
        if (!TryParseOptionalUtc(saveData.GraduationSnapshotAtUtc, out DateTime? at))
        {
            failureMessage = "SlimeStatus : 졸업 통계 시각을 해석할 수 없습니다.";
            return false;
        }
        if (!at.HasValue) return true;
        if (at.Value < startedAtUtc ||
            saveData.GraduationNaturalSpawnCount < 0 ||
            saveData.GraduationMergeCreatedCount < 0 ||
            saveData.GraduationManualTouchCount < 0 ||
            saveData.GraduationProducedPointTotal < 0d ||
            double.IsNaN(saveData.GraduationProducedPointTotal) ||
            double.IsInfinity(saveData.GraduationProducedPointTotal) ||
            saveData.GraduationMostTouchedCount < 0 ||
            saveData.GraduationGachaTicketsObtainedTotal < 0 ||
            saveData.GraduationAutoMergeUseCount < 0)
        {
            failureMessage = "SlimeStatus : 졸업 통계 값이 올바르지 않습니다.";
            return false;
        }

        ESlimeGrade favorite = (ESlimeGrade)saveData.GraduationMostTouchedGrade;
        if (favorite < ESlimeGrade.Grade1 || favorite >= ESlimeGrade.Count)
        {
            failureMessage = "SlimeStatus : 졸업 통계의 슬라임 등급이 올바르지 않습니다.";
            return false;
        }

        snapshot = new GraduationStatistics(
            startedAtUtc,
            at.Value,
            saveData.GraduationNaturalSpawnCount,
            saveData.GraduationMergeCreatedCount,
            saveData.GraduationManualTouchCount,
            saveData.GraduationProducedPointTotal,
            favorite,
            saveData.GraduationMostTouchedCount,
            saveData.GraduationGachaTicketsObtainedTotal,
            saveData.GraduationAutoMergeUseCount);
        return true;
    }

    private static bool TryResolveJourneyDates(
        SlimeStatusSaveData saveData,
        DateTime restoredAtUtc,
        bool hasReachedEnding,
        out DateTime gameStartedAtUtc,
        out DateTime? mainEndingReachedAtUtc,
        out bool wasRestored,
        out string failureMessage)
    {
        gameStartedAtUtc = default;
        mainEndingReachedAtUtc = null;
        wasRestored = false;
        failureMessage = null;

        if (!TryParseOptionalUtc(
                saveData.GameStartedAtUtc,
                out DateTime? savedStartedAtUtc))
        {
            failureMessage = "SlimeStatus : 게임 시작 시각을 해석할 수 없습니다.";
            return false;
        }

        if (!TryParseOptionalUtc(
                saveData.MainEndingReachedAtUtc,
                out DateTime? savedEndingAtUtc))
        {
            failureMessage = "SlimeStatus : 메인 엔딩 도달 시각을 해석할 수 없습니다.";
            return false;
        }

        DateTime fallback = restoredAtUtc.Kind == DateTimeKind.Utc
            ? restoredAtUtc
            : restoredAtUtc.ToUniversalTime();
        DateTime? earliestRegistration = FindRegistrationBoundary(
            saveData.NormalFirstRegisteredAt,
            findEarliest: true);
        DateTime? latestRegistration = FindRegistrationBoundary(
            saveData.NormalFirstRegisteredAt,
            findEarliest: false);

        gameStartedAtUtc = savedStartedAtUtc ?? earliestRegistration ?? fallback;
        wasRestored |= !savedStartedAtUtc.HasValue;

        if (savedEndingAtUtc.HasValue)
        {
            mainEndingReachedAtUtc = savedEndingAtUtc;
        }
        else if (hasReachedEnding)
        {
            mainEndingReachedAtUtc = latestRegistration ?? fallback;
            wasRestored = true;
        }

        if (mainEndingReachedAtUtc.HasValue &&
            mainEndingReachedAtUtc.Value < gameStartedAtUtc)
        {
            failureMessage = "SlimeStatus : 엔딩 도달 시각이 게임 시작 시각보다 빠릅니다.";
            return false;
        }

        return true;
    }

    private static bool TryParseOptionalUtc(string value, out DateTime? parsedUtc)
    {
        parsedUtc = null;
        if (string.IsNullOrWhiteSpace(value)) return true;

        if (!DateTime.TryParse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out DateTime parsed))
        {
            return false;
        }

        parsedUtc = parsed.Kind == DateTimeKind.Utc
            ? parsed
            : parsed.ToUniversalTime();
        return true;
    }

    private static DateTime? FindRegistrationBoundary(
        IReadOnlyList<string> registrations,
        bool findEarliest)
    {
        if (registrations == null) return null;

        DateTime? result = null;
        foreach (string value in registrations)
        {
            if (!TryParseOptionalUtc(value, out DateTime? parsed) ||
                !parsed.HasValue)
            {
                continue;
            }

            if (!result.HasValue ||
                (findEarliest && parsed.Value < result.Value) ||
                (!findEarliest && parsed.Value > result.Value))
            {
                result = parsed.Value;
            }
        }

        return result;
    }

    private static IEnumerable<ESlimeGrade> GetRegisteredCollection(
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
