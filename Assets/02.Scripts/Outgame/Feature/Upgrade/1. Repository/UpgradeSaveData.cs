using Firebase.Firestore;
using System;
using System.Collections.Generic;

[Serializable]
[FirestoreData]
public class UpgradeEntry
{
    [FirestoreProperty]
    public int Type { get; set; }

    [FirestoreProperty]
    public int Grade { get; set; }

    [FirestoreProperty]
    public int Level { get; set; }

    // Firebase 역직렬화용 기본 생성자
    public UpgradeEntry() { }

    public UpgradeEntry(EUpgradeType type, ESlimeGrade grade, int level)
    {
        Type = (int)type;
        Grade = (int)grade;
        Level = level;
    }

    public EUpgradeType GetUpgradeType() => (EUpgradeType)Type;
    public ESlimeGrade GetSlimeGrade() => (ESlimeGrade)Grade;
}

[Serializable]
[FirestoreData]
public class UpgradeSaveData : ISaveData
{
    // 레벨 배열 (EUpgradeType 순서대로 저장)
    [FirestoreProperty]
    public List<UpgradeEntry> Entries { get; set; } = new();

    /// <summary>기본값 (새 게임)</summary>
    public static UpgradeSaveData Default => new UpgradeSaveData
    {
        Entries = new List<UpgradeEntry>(),
    };

    [FirestoreProperty]
    public string LastSaveTime { get; set; }
}
