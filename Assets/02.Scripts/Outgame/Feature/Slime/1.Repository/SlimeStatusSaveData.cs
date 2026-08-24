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
    };
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
            LastSaveTime = legacyData.LastSaveTime,
            WasMigrated = true,
        };
    }
}
