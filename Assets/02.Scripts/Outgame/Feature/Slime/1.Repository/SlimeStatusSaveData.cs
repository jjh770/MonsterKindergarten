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
    public List<SlimeInstance> ActiveSlimes { get; set; } = new();

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
        ActiveSlimes = new List<SlimeInstance>(),
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

        var activeSlimes = new List<SlimeInstance>();
        if (legacyData.ActiveSlimes != null)
        {
            foreach (LegacySlimeEntry entry in legacyData.ActiveSlimes)
            {
                if (entry == null || entry.Count <= 0) continue;

                ESlimeGrade grade = (ESlimeGrade)entry.Grade;
                if (grade == ESlimeGrade.None || grade == ESlimeGrade.Count)
                {
                    continue;
                }

                for (int i = 0; i < entry.Count; i++)
                {
                    activeSlimes.Add(SlimeInstance.Create(grade));
                }
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
