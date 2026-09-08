using Firebase.Firestore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

[Serializable]
[FirestoreData]
public sealed class LegacySlimeEntry
{
    [FirestoreProperty]
    public int Grade { get; set; }

    [FirestoreProperty]
    public int Count { get; set; }

    public LegacySlimeEntry() { }
}

[Serializable]
[FirestoreData]
public sealed class LegacySlimeStatusSaveData : ISaveData
{
    [FirestoreProperty]
    public int SchemaVersion { get; set; }

    [FirestoreProperty]
    public int HighestGrade { get; set; }

    [FirestoreProperty]
    public List<LegacySlimeEntry> ActiveSlimes { get; set; } = new();

    [FirestoreProperty]
    public int CurrentStage { get; set; }

    [FirestoreProperty]
    public bool SkyIntroCompleted { get; set; }

    [FirestoreProperty]
    public string LastSaveTime { get; set; }
}

[Serializable]
[FirestoreData]
public sealed class SlimeStatusSaveData : ISaveData
{
    public const int NormalCollectionSize =
        (int)ESlimeGrade.Count - (int)ESlimeGrade.Grade1;

    [FirestoreProperty]
    public int SchemaVersion { get; set; }

    [FirestoreProperty]
    public int HighestGrade { get; set; }

    [FirestoreProperty]
    public List<SlimeInstanceSaveData> ActiveSlimes { get; set; } = new();

    [FirestoreProperty]
    public int CurrentStage { get; set; }

    [FirestoreProperty]
    public bool SkyIntroCompleted { get; set; }

    // 아직 줍지 않은 가챠권 수. 스테이지별로 따로 센다. 티켓이 속한 스테이지는
    // 드랍시킨 슬라임의 등급으로 드랍 시점에 정해져 그대로 고정되기 때문이다.
    //
    // 개수만 저장한다. 슬라임도 좌표를 저장하지 않고 복원할 때 다시 흩뿌리므로,
    // 티켓만 좌표를 남길 이유가 없다. 여러 장을 낱개 오브젝트로 보여주는 것은
    // 화면의 규칙이라 복원할 때 개수만큼 만들면 된다.
    //
    // 초기화를 두지 않는다. v4 이하 문서에는 이 필드가 없고 Firestore는 없는 필드를
    // C# 기본값으로 남기는데, 여기서는 그 0이 정확히 맞는 값이라 결손과 구분할
    // 필요가 없다. 같은 이유로 Default와 레거시 승격에도 적지 않는다.
    [FirestoreProperty]
    public int PendingGroundTickets { get; set; }

    [FirestoreProperty]
    public int PendingSkyTickets { get; set; }

    // 플레이어가 자연 스폰을 껐는지. 켜짐이 기본값이라 일부러 뒤집어 담는다.
    //
    // Firestore도 JSON도 없는 필드를 C# 기본값으로 남긴다. AutoSpawnEnabled로
    // 두면 v5 이하 문서가 전부 "꺼짐"으로 읽혀 기존 플레이어의 스폰이 멎는다.
    // 승격 함수에서 true로 채우는 방법도 있지만, 그 함수는 어느 버전에서 왔는지
    // 모르는 채 실행되므로 나중에 v7이 생기면 플레이어가 꺼 둔 설정을 도로
    // 켜 버린다. 뒤집어 담으면 그런 자리가 아예 없다.
    [FirestoreProperty]
    public bool AutoSpawnDisabled { get; set; }

    [FirestoreProperty]
    public List<bool> NormalCollectionRegistered { get; set; } =
        CreateEmptyNormalCollection();

    [FirestoreProperty]
    public List<string> NormalFirstRegisteredAt { get; set; } =
        CreateEmptyStringStats();

    [FirestoreProperty]
    public List<long> NormalNaturalSpawnCounts { get; set; } =
        CreateEmptyLongStats();

    [FirestoreProperty]
    public List<long> NormalMergeCreatedCounts { get; set; } =
        CreateEmptyLongStats();

    [FirestoreProperty]
    public List<long> NormalManualTouchCounts { get; set; } =
        CreateEmptyLongStats();

    [FirestoreProperty]
    public List<double> NormalProducedPointTotals { get; set; } =
        CreateEmptyDoubleStats();

    [FirestoreProperty]
    public string LastSaveTime { get; set; }

    [JsonIgnore]
    public bool WasMigrated { get; set; }

    public ESlimeGrade GetHighestGrade() => (ESlimeGrade)HighestGrade;

    public static SlimeStatusSaveData Default => new SlimeStatusSaveData
    {
        SchemaVersion = SaveSchema.SlimeCurrentVersion,
        HighestGrade = (int)ESlimeGrade.Grade1,
        ActiveSlimes = new List<SlimeInstanceSaveData>(),
        CurrentStage = (int)EGameStage.Ground,
        SkyIntroCompleted = false,
        NormalCollectionRegistered = CreateEmptyNormalCollection(),
        NormalFirstRegisteredAt = CreateEmptyStringStats(),
        NormalNaturalSpawnCounts = CreateEmptyLongStats(),
        NormalMergeCreatedCounts = CreateEmptyLongStats(),
        NormalManualTouchCounts = CreateEmptyLongStats(),
        NormalProducedPointTotals = CreateEmptyDoubleStats(),
    };

    public static List<bool> CreateEmptyNormalCollection()
    {
        return new List<bool>(new bool[NormalCollectionSize]);
    }

    public static List<bool> NormalizeNormalCollection(
        IReadOnlyList<bool> registered)
    {
        List<bool> normalized = CreateEmptyNormalCollection();
        if (registered == null)
        {
            return normalized;
        }

        int copyCount = Math.Min(registered.Count, normalized.Count);
        for (int i = 0; i < copyCount; i++)
        {
            normalized[i] = registered[i];
        }

        return normalized;
    }

    public static List<string> CreateEmptyStringStats() =>
        new(new string[NormalCollectionSize]);

    public static List<long> CreateEmptyLongStats() =>
        new(new long[NormalCollectionSize]);

    public static List<double> CreateEmptyDoubleStats() =>
        new(new double[NormalCollectionSize]);

    public static List<string> NormalizeStringStats(IReadOnlyList<string> values)
    {
        List<string> normalized = CreateEmptyStringStats();
        if (values == null) return normalized;

        int copyCount = Math.Min(values.Count, normalized.Count);
        for (int i = 0; i < copyCount; i++)
        {
            normalized[i] = values[i] ?? string.Empty;
        }

        return normalized;
    }

    public static List<long> NormalizeLongStats(IReadOnlyList<long> values)
    {
        List<long> normalized = CreateEmptyLongStats();
        if (values == null) return normalized;

        int copyCount = Math.Min(values.Count, normalized.Count);
        for (int i = 0; i < copyCount; i++)
        {
            normalized[i] = Math.Max(0L, values[i]);
        }

        return normalized;
    }

    public static List<double> NormalizeDoubleStats(IReadOnlyList<double> values)
    {
        List<double> normalized = CreateEmptyDoubleStats();
        if (values == null) return normalized;

        int copyCount = Math.Min(values.Count, normalized.Count);
        for (int i = 0; i < copyCount; i++)
        {
            double value = values[i];
            if (value < 0d || double.IsNaN(value))
            {
                continue;
            }

            normalized[i] = double.IsPositiveInfinity(value)
                ? double.MaxValue
                : value;
        }

        return normalized;
    }

    public static void NormalizeCollectionStats(SlimeStatusSaveData saveData)
    {
        if (saveData == null) return;

        saveData.NormalFirstRegisteredAt = NormalizeStringStats(
            saveData.NormalFirstRegisteredAt);
        saveData.NormalNaturalSpawnCounts = NormalizeLongStats(
            saveData.NormalNaturalSpawnCounts);
        saveData.NormalMergeCreatedCounts = NormalizeLongStats(
            saveData.NormalMergeCreatedCounts);
        saveData.NormalManualTouchCounts = NormalizeLongStats(
            saveData.NormalManualTouchCounts);
        saveData.NormalProducedPointTotals = NormalizeDoubleStats(
            saveData.NormalProducedPointTotals);
    }
}

public static class SlimeStatusSaveMigration
{
    // v0/v1의 { Grade, Count }를 Count 수만큼의 일반 MainStage 개체로 승격한다.
    public static SlimeStatusSaveData Upgrade(
        LegacySlimeStatusSaveData legacyData)
    {
        if (legacyData == null)
        {
            return SlimeStatusSaveData.Default;
        }

        var countsByGrade = new SortedDictionary<int, int>();
        if (legacyData.ActiveSlimes != null)
        {
            foreach (LegacySlimeEntry entry in legacyData.ActiveSlimes)
            {
                if (entry == null || entry.Count <= 0) continue;

                if (entry.Grade < (int)ESlimeGrade.Grade1 ||
                    entry.Grade >= (int)ESlimeGrade.Count)
                {
                    continue;
                }

                countsByGrade.TryGetValue(entry.Grade, out int currentCount);
                countsByGrade[entry.Grade] = currentCount + entry.Count;
            }
        }

        // 같은 레거시 내용은 항목 순서와 중복 여부에 관계없이 같은 ID를 만든다.
        var activeSlimes = new List<SlimeInstanceSaveData>();
        foreach (KeyValuePair<int, int> pair in countsByGrade)
        {
            for (int i = 0; i < pair.Value; i++)
            {
                activeSlimes.Add(new SlimeInstanceSaveData(
                    $"legacy-{pair.Key}-{i}",
                    (ESlimeGrade)pair.Key,
                    false,
                    ESlimeLocation.MainStage));
            }
        }

        return new SlimeStatusSaveData
        {
            SchemaVersion = SaveSchema.SlimeCurrentVersion,
            HighestGrade = legacyData.HighestGrade,
            ActiveSlimes = activeSlimes,
            CurrentStage = legacyData.CurrentStage,
            SkyIntroCompleted = legacyData.SkyIntroCompleted,
            NormalCollectionRegistered =
                SlimeStatusSaveData.CreateEmptyNormalCollection(),
            NormalFirstRegisteredAt = SlimeStatusSaveData.CreateEmptyStringStats(),
            NormalNaturalSpawnCounts = SlimeStatusSaveData.CreateEmptyLongStats(),
            NormalMergeCreatedCounts = SlimeStatusSaveData.CreateEmptyLongStats(),
            NormalManualTouchCounts = SlimeStatusSaveData.CreateEmptyLongStats(),
            NormalProducedPointTotals = SlimeStatusSaveData.CreateEmptyDoubleStats(),
            LastSaveTime = legacyData.LastSaveTime,
            WasMigrated = true,
        };
    }

    public static SlimeStatusSaveData UpgradeInstanceData(
        SlimeStatusSaveData saveData)
    {
        if (saveData == null)
        {
            return SlimeStatusSaveData.Default;
        }

        saveData.SchemaVersion = SaveSchema.SlimeCurrentVersion;
        saveData.ActiveSlimes ??= new List<SlimeInstanceSaveData>();
        saveData.NormalCollectionRegistered =
            SlimeStatusSaveData.NormalizeNormalCollection(
                saveData.NormalCollectionRegistered);
        SlimeStatusSaveData.NormalizeCollectionStats(saveData);
        saveData.WasMigrated = true;
        return saveData;
    }
}
