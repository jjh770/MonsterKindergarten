using Firebase.Firestore;
using System;
using System.Collections.Generic;

[Serializable]
[FirestoreData]
public class SlimeEntry
{
    [FirestoreProperty]
    public int Grade { get; set; }

    [FirestoreProperty]
    public int Count { get; set; }

    public SlimeEntry() { }

    public SlimeEntry(ESlimeGrade grade, int count)
    {
        Grade = (int)grade;
        Count = count;
    }

    public ESlimeGrade GetGrade() => (ESlimeGrade)Grade;
}

[Serializable]
[FirestoreData]
public class SlimeStatusSaveData : ISaveData
{
    [FirestoreProperty]
    public int SchemaVersion { get; set; }

    [FirestoreProperty]
    public int HighestGrade { get; set; }

    [FirestoreProperty]
    public List<SlimeEntry> ActiveSlimes { get; set; } = new();

    [FirestoreProperty]
    public int CurrentStage { get; set; }

    [FirestoreProperty]
    public bool SkyIntroCompleted { get; set; }

    public ESlimeGrade GetHighestGrade() => (ESlimeGrade)HighestGrade;

    public Dictionary<ESlimeGrade, int> GetActiveSlimesDict()
    {
        var dict = new Dictionary<ESlimeGrade, int>();
        foreach (var entry in ActiveSlimes)
        {
            dict[entry.GetGrade()] = entry.Count;
        }
        return dict;
    }

    public static SlimeStatusSaveData Default => new SlimeStatusSaveData
    {
        SchemaVersion = SaveSchema.SlimeCurrentVersion,
        HighestGrade = (int)ESlimeGrade.Grade1,
        ActiveSlimes = new List<SlimeEntry>(),
        CurrentStage = (int)EGameStage.Ground,
        SkyIntroCompleted = false,
    };

    [FirestoreProperty]
    public string LastSaveTime { get; set; }
}
