using System;
using System.Collections.Generic;

[Serializable]
public struct UpgradeEntry
{
    public EUpgradeType Type;
    public ESlimeGrade Grade;
    public int Level;
}

[Serializable]
public class UpgradeSaveData
{
    // 레벨 배열 (EUpgradeType 순서대로 저장)
    public List<UpgradeEntry> Entries = new();
    public string LastSaveTime;

    /// <summary>기본값 (새 게임)</summary>
    public static UpgradeSaveData Default => new UpgradeSaveData
    {
        Entries = new List<UpgradeEntry>(),
        LastSaveTime = DateTime.Now.ToString("o")
    };
}
